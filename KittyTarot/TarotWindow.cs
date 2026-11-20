using System;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = UnityEngine.EventType;

namespace KittyTarot
{
    // The Kitty Tarot window: a title row, the lace table with the deck and the spread on it, and a row
    // of buttons under it. It sits in the stack with the other mods' windows until you drag its title
    // row. Mouse only: the keyboard stays the game's, and it never touches your character.
    //
    // Everything on the table is placed in art pixels (TableLayout) and drawn at a whole number of screen
    // pixels per art pixel, so the pixel art stays sharp; the rest of the window scales with the screen
    // like other mods' windows.
    internal sealed class TarotWindow : MonoBehaviour
    {
        private const string PanelId = "KittyTarot";
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;
        private const int CardW = CardFaces.Width;
        private const int CardH = CardFaces.Height;

        // ---- The window around the table, in 1080p units ----
        private const float PadUnits = 8f;
        private const float HeaderUnits = 28f;
        private const float GapUnits = 6f;
        private const float StatusUnits = 28f;
        private const float ButtonGapUnits = 4f;

        // ---- Timing, in seconds ----
        private const float SlideTime = 0.24f;
        private const float DealGap = 0.14f;
        private const float FanGap = 0.006f;
        private const float GatherGap = 0.08f;
        private const float FlipTime = 0.3f;
        private const float FlipAllGap = 0.2f;
        private const float RiffleTime = 0.9f;
        private const float WashTime = 1.6f;
        private const float PickPause = 0.45f;
        private const float SaveDelay = 2f;

        private static readonly Color Shade = new Color(0.16f, 0.1f, 0.18f, 0.75f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);

        private static Settings S => Plugin.Settings;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private readonly Store _store = new Store();
        private readonly Reading _reading = new Reading();
        private readonly System.Random _random = new System.Random();
        private TarotArt _art;
        private TableLayout _layout;

        private bool _open;
        private bool _iconClicked;
        // Button clicks, acted on in Update (outside the GUI pass).
        private readonly List<Action> _clicks = new List<Action>();
        private ClickBlocker _blocker;
        private Rect _screenRect;
        private int _drawnFrame = -100;
        private float _saveAt = -1f;

        // ---- Layout, worked out every OnGUI, in screen pixels ----
        private float _unit;
        private int _scale;      // screen pixels per art pixel (0 until the window is first drawn)
        private float _pad;
        private float _header;
        private float _gap;
        private float _status;
        private Rect _panel;
        private Rect _table;

        // ---- Dragging the window by its title row ----
        private int _windowControl;
        private bool _windowDragging;
        private Vector2 _windowGrab;
        private Vector2 _windowPos;

        // ---- The table ----
        private int _hoverCard = -1;   // the card under the mouse in the fan
        private int _hoverSpot = -1;   // the spot under the mouse on the spread
        private int _hoverPile = -1;   // the cut pile under the mouse
        private bool _hoverDeck;
        private int _detail = -1;      // the spot whose card shows big
        private bool _help;
        private bool _options;
        private float _finishPickAt = -1f;

        // Cards gliding to where they go, from where they were, starting at a time (some wait their turn).
        private struct Slide
        {
            public Vector2 From;
            public float Start;
        }

        private readonly Dictionary<int, Slide> _slides = new Dictionary<int, Slide>();
        // Spots whose card is turning over, and when it starts.
        private readonly Dictionary<int, float> _flips = new Dictionary<int, float>();

        // A shuffle or wash being shown (the deck is already shuffled; this is only the show).
        private enum Show
        {
            None,
            Riffle,
            Wash,
        }

        private Show _show;
        private float _showStart = -100f;

        private struct Swirl
        {
            public float Angle;
            public float Radius;
            public float Speed;
            public float Spin;
            public float Tilt;
        }

        private readonly List<Swirl> _swirls = new List<Swirl>();

        private float _styleUnit = -1f;
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _helpStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _detailStyle;

        private void Awake()
        {
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "KittyTarot ClickBlocker");
            _store.Load();
            if (_store.Saved != null && !_reading.Load(_store.Saved))
            {
                Plugin.Log.LogWarning("The saved deck didn't add up, so you have a new one.");
            }
            _reading.SetMajorsOnly(S.MajorsOnly.Value);
            _layout = new TableLayout(_reading.Spread);
        }

        private void OnDestroy()
        {
            if (IsMouseOver)
            {
                IsMouseOver = false;
            }
            if (_saveAt >= 0f)
            {
                SaveNow();
            }
            _art?.Destroy();
            _blocker?.Destroy();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        // ---- Every frame ----

        private void Update()
        {
            try
            {
                bool inWorld = SceneManager.GetActiveScene().buildIndex == WorldScene;
                bool clicked = _iconClicked;
                _iconClicked = false;
                if (inWorld && (clicked || (S.WindowKey.Value != KeyCode.None && Input.GetKeyDown(S.WindowKey.Value) && !IsTyping())))
                {
                    Toggle();
                }
                if (!inWorld && _open)
                {
                    Close();
                }

                List<Action> clicks = _clicks.ToList();
                _clicks.Clear();
                foreach (Action click in clicks)
                {
                    click();
                }

                float now = Time.unscaledTime;
                if (_finishPickAt >= 0f && now >= _finishPickAt)
                {
                    _finishPickAt = -1f;
                    FinishPick();
                }
                if (_slides.Count > 0)
                {
                    foreach (int card in _slides.Where(p => now >= p.Value.Start + SlideTime).Select(p => p.Key).ToList())
                    {
                        _slides.Remove(card);
                    }
                }
                if (_flips.Count > 0)
                {
                    foreach (int spot in _flips.Where(p => now >= p.Value + FlipTime).Select(p => p.Key).ToList())
                    {
                        _flips.Remove(spot);
                    }
                }
                if (_show != Show.None && now >= _showStart + (_show == Show.Riffle ? RiffleTime : WashTime))
                {
                    _show = Show.None;
                }
                if (_saveAt >= 0f && now >= _saveAt)
                {
                    SaveNow();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        // ---- Opening and closing ----

        private void Toggle()
        {
            if (_open)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            _open = true;
            if (!S.HelpSeen.Value)
            {
                _help = true;
                S.HelpSeen.Value = true;
            }
        }

        // Closing keeps the deck and the reading; anything moving lands where it was going.
        private void Close()
        {
            _open = false;
            _help = false;
            _options = false;
            _detail = -1;
            _windowDragging = false;
            _slides.Clear();
            _flips.Clear();
            _show = Show.None;
            if (_finishPickAt >= 0f)
            {
                _finishPickAt = -1f;
                _reading.FinishPick();
            }
            SaveNow();
        }

        private void MarkDirty()
        {
            if (_saveAt < 0f)
            {
                _saveAt = Time.unscaledTime + SaveDelay;
            }
        }

        private void SaveNow()
        {
            _saveAt = -1f;
            _store.Save(_reading);
        }

        // Cards moving, a shuffle showing, or the pause after the last pick: the buttons wait.
        private bool Busy => _slides.Count > 0 || _flips.Count > 0 || _show != Show.None || _finishPickAt >= 0f;

        private bool Overlay => _help || _options || _detail >= 0;

        // ---- Doing things ----

        // Runs a change to the reading. The cards that moved glide from where they were showing to
        // where they go now, each after its delay (in seconds; none by default).
        private bool Change(Func<bool> action, Func<int, float> delay = null)
        {
            float now = Time.unscaledTime;
            var before = new Dictionary<int, Vector2>();
            foreach (int card in OnTable())
            {
                before[card] = VisualPos(card, now);
            }
            if (!action())
            {
                return false;
            }
            if (S.Animations.Value)
            {
                foreach (int card in OnTable())
                {
                    if (before.TryGetValue(card, out Vector2 from) && (from - Spot(card)).sqrMagnitude > 0.01f)
                    {
                        _slides[card] = new Slide { From = from, Start = now + Mathf.Max(0f, delay?.Invoke(card) ?? 0f) };
                    }
                    else
                    {
                        _slides.Remove(card);
                    }
                }
            }
            else
            {
                _slides.Clear();
            }
            MarkDirty();
            return true;
        }

        private void Riffle()
        {
            if (_reading.Riffle(_random))
            {
                StartShow(Show.Riffle);
                MarkDirty();
            }
        }

        private void Wash()
        {
            if (!_reading.Wash(_random))
            {
                return;
            }
            _swirls.Clear();
            for (int i = 0; i < 16; i++)
            {
                float direction = _random.Next(2) == 0 ? -1f : 1f;
                _swirls.Add(new Swirl
                {
                    // Small enough that a card spun round still stays on the table.
                    Angle = (float)(_random.NextDouble() * Math.PI * 2),
                    Radius = 20f + (float)_random.NextDouble() * 50f,
                    Speed = direction * (1.6f + (float)_random.NextDouble() * 2.2f),
                    Spin = direction * (40f + (float)_random.NextDouble() * 160f),
                    Tilt = -40f + (float)_random.NextDouble() * 80f,
                });
            }
            StartShow(Show.Wash);
            MarkDirty();
        }

        private void StartShow(Show show)
        {
            if (S.Animations.Value)
            {
                _show = show;
                _showStart = Time.unscaledTime;
            }
        }

        private void StartCut()
        {
            Change(() => _reading.StartCut(_random), card => PileOf(card) * 0.1f);
        }

        private void TakePile(int pile)
        {
            Change(() => _reading.TakePile(pile));
        }

        private void Deal()
        {
            Change(() => _reading.Deal(), card => Array.IndexOf(_reading.Slots, card) * DealGap);
        }

        private void StartPick()
        {
            Change(() => _reading.StartPick(), card => _reading.Deck.IndexOf(card) * FanGap);
        }

        private void ClickFanCard(int card)
        {
            if (_reading.TogglePick(card) && _reading.PickedAll)
            {
                // A moment to see the last card rise before they all go.
                _finishPickAt = Time.unscaledTime + (S.Animations.Value ? PickPause : 0f);
            }
        }

        private void FinishPick()
        {
            List<int> picked = _reading.Picked.ToList();
            Change(() => _reading.FinishPick(), card =>
            {
                int slot = picked.IndexOf(card);
                return slot >= 0 ? slot * DealGap : 0.2f + _reading.Deck.IndexOf(card) * 0.003f;
            });
        }

        private void CancelPick()
        {
            Change(() => _reading.CancelPick(), card => _reading.Deck.IndexOf(card) * 0.003f);
        }

        private void Flip(int spot, float delay = 0f)
        {
            if (_reading.Flip(spot))
            {
                if (S.Animations.Value)
                {
                    _flips[spot] = Time.unscaledTime + delay;
                }
                MarkDirty();
            }
        }

        private void FlipAll()
        {
            int next = 0;
            for (int spot = 0; spot < _reading.Slots.Length; spot++)
            {
                if (!_reading.FaceUp[spot])
                {
                    Flip(spot, next++ * FlipAllGap);
                }
            }
        }

        // A new reading: the cards go back under the deck.
        private void Gather()
        {
            int[] slots = _reading.Slots.ToArray();
            _detail = -1;
            Change(() => _reading.Gather(S.MajorsOnly.Value), card => Array.IndexOf(slots, card) * GatherGap);
        }

        private void SetMajorsOnly(bool on)
        {
            S.MajorsOnly.Value = on;
            if (_reading.Phase == Phase.Ready && _reading.SetMajorsOnly(on))
            {
                _slides.Clear();
                MarkDirty();
            }
        }

        // ---- Where the cards are, in art pixels from the table's top-left ----

        private IEnumerable<int> OnTable()
        {
            return _reading.Deck.Concat(_reading.Slots.Where(card => card >= 0)).Concat(_reading.Piles.SelectMany(pile => pile));
        }

        private int PileOf(int card)
        {
            for (int pile = 0; pile < 3; pile++)
            {
                if (_reading.Piles[pile].Contains(card))
                {
                    return pile;
                }
            }
            return -1;
        }

        private static Vector2 V((int x, int y) p) => new Vector2(p.x, p.y);

        // Where a card belongs now, risen in the fan if it's picked or under the mouse.
        private Vector2 Spot(int card)
        {
            switch (_reading.Phase)
            {
                case Phase.Cutting:
                {
                    int pile = PileOf(card);
                    return pile >= 0 ? V(_layout.CutPiles[pile]) : V(_layout.Deck);
                }
                case Phase.Picking:
                {
                    (float x, float y) = _layout.FanPos(_reading.Deck.IndexOf(card), _reading.Deck.Count);
                    float lift = _reading.Picked.Contains(card) ? TableLayout.PickLift : card == _hoverCard ? TableLayout.HoverLift : 0f;
                    return new Vector2(x, y - lift);
                }
                default:
                {
                    int slot = Array.IndexOf(_reading.Slots, card);
                    return slot >= 0 ? V(_layout.Slots[slot]) : V(_layout.Deck);
                }
            }
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        // Where a card shows right now: gliding, or in its place.
        private Vector2 VisualPos(int card, float now)
        {
            Vector2 target = Spot(card);
            if (_slides.TryGetValue(card, out Slide slide))
            {
                float t = (now - slide.Start) / SlideTime;
                if (t <= 0f)
                {
                    return slide.From;
                }
                if (t < 1f)
                {
                    return Vector2.Lerp(slide.From, target, Ease(t));
                }
            }
            return target;
        }

        private bool Reversed(int card) => S.Reversals.Value && _reading.Upside[card];

        private static bool InCard(Vector2 at, Vector2 corner, float extraTop = 0f)
        {
            return at.x >= corner.x && at.x < corner.x + CardW && at.y >= corner.y - extraTop && at.y < corner.y + CardH;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value)
            {
                IconBar.State state = _open ? IconBar.State.Open : IconBar.State.Plain;
                if (IconBar.Draw("KittyTarot", IconBar.KittyTarotOrder, Icon.Pixels, state, "Kitty Tarot", S.WindowKey.Value) == IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
            }

            // Hidden while Photo Mode takes a photo, and while the game's eye button hides the UI.
            if (!_open || PanelStack.Hidden || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                // A drag cut short would keep every IMGUI control busy.
                if (GUIUtility.hotControl != 0 && GUIUtility.hotControl == _windowControl)
                {
                    GUIUtility.hotControl = 0;
                }
                _windowDragging = false;
                return;
            }

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                if (_art == null)
                {
                    _art = new TarotArt();
                }
                Layout();
                MakeStyles();
                DrawWindow();
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        private float Units(float units) => Mathf.Round(units * _unit);

        // Works out the window's size and place, in screen pixels.
        private void Layout()
        {
            _unit = Screen.height / 1080f;
            _pad = Units(PadUnits);
            _header = Units(HeaderUnits);
            _gap = Units(GapUnits);
            _status = Units(StatusUnits);

            int wanted = S.Size.Value == TableSize.Large ? 3 : 2;
            int scale = Mathf.Max(1, Mathf.RoundToInt(wanted * _unit));
            Vector2 size;
            while (true)
            {
                size = new Vector2(_layout.Width * scale + 2 * _pad,
                    _pad + _header + _gap + _layout.Height * scale + _gap + _status + _pad);
                bool fits = size.y <= Screen.height - Units(40f) && size.x <= Screen.width - Units(40f);
                if (fits || scale <= 1)
                {
                    break;
                }
                scale--;
            }
            _scale = scale;

            Vector2 pos;
            if (_windowDragging)
            {
                pos = ClampToScreen(_windowPos, size);
            }
            else if (S.WindowX.Value >= 0f)
            {
                pos = ClampToScreen(new Vector2(S.WindowX.Value, S.WindowY.Value) * _unit, size);
            }
            else
            {
                // In the stack with the other mods' windows, which is laid out in 1080p units.
                Rect placed = PanelStack.Place(PanelId, PanelStack.KittyTarotOrder, size / _unit);
                pos = placed.position * _unit;
            }
            pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            _panel = new Rect(pos, size);
            _table = new Rect(pos.x + _pad, pos.y + _pad + _header + _gap, _layout.Width * scale, _layout.Height * scale);
            _screenRect = _panel;
            _drawnFrame = Time.frameCount;
        }

        // Kept wholly on screen, in screen pixels.
        private static Vector2 ClampToScreen(Vector2 position, Vector2 size)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0f, Mathf.Max(0f, Screen.width - size.x)),
                Mathf.Clamp(position.y, 0f, Mathf.Max(0f, Screen.height - size.y)));
        }

        private void DrawWindow()
        {
            float now = Time.unscaledTime;
            // First, so it keeps the same id from frame to frame.
            _windowControl = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);

            GUI.Box(_panel, GUIContent.none, _boxStyle);

            // Title row: name and the spread on the left, buttons on the right.
            float x = _panel.x + _pad;
            float y = _panel.y + _pad;
            float right = _panel.xMax - _pad;
            right = HeaderButton(right, y, "X", Close);
            right = HeaderButton(right, y, S.Size.Value == TableSize.Large ? "L" : "M", () =>
                S.Size.Value = S.Size.Value == TableSize.Large ? TableSize.Medium : TableSize.Large);
            right = HeaderButton(right, y, "?", () =>
            {
                _help = !_help;
                _options = false;
                _detail = -1;
            });
            right = HeaderButton(right, y, "Options", () =>
            {
                _options = !_options;
                _help = false;
                _detail = -1;
            });

            // The rest of the title row moves the window (the buttons above have taken their clicks).
            HandleWindowDrag(new Rect(_panel.x, _panel.y, right - _panel.x, _pad + _header));

            var title = new GUIContent("<b>KITTY TAROT</b>");
            float titleWidth = _titleStyle.CalcSize(title).x + Units(10f);
            GUI.Label(new Rect(x, y, titleWidth, _header), title, _titleStyle);
            GUI.Label(new Rect(x + titleWidth, y, Mathf.Max(0f, right - x - titleWidth - Units(ButtonGapUnits)), _header),
                "Three cards", _hintStyle);

            // A box over the table takes the clicks; otherwise the table does.
            if (Overlay)
            {
                ClearHover();
            }
            else
            {
                HandleTable(now);
            }
            DrawTable(now);
            DrawStatusRow();

            if (_detail >= 0)
            {
                DrawDetail();
            }
            else if (_help)
            {
                DrawHelp();
            }
            else if (_options)
            {
                DrawOptions();
            }
        }

        // ---- The table ----

        private void ClearHover()
        {
            _hoverCard = -1;
            _hoverSpot = -1;
            _hoverPile = -1;
            _hoverDeck = false;
        }

        // What's under the mouse, and clicks on the table. Clicks act straight away.
        private void HandleTable(float now)
        {
            Event e = Event.current;
            bool inside = _table.Contains(e.mousePosition);
            Vector2 at = (e.mousePosition - _table.position) / _scale;
            ClearHover();
            if (inside && !Busy)
            {
                switch (_reading.Phase)
                {
                    case Phase.Ready:
                        _hoverDeck = InCard(at, V(_layout.Deck));
                        break;
                    case Phase.Cutting:
                        for (int pile = 0; pile < 3; pile++)
                        {
                            if (_reading.Piles[pile].Count > 0 && InCard(at, V(_layout.CutPiles[pile])))
                            {
                                _hoverPile = pile;
                            }
                        }
                        break;
                    case Phase.Picking:
                        _hoverCard = FanCardAt(at);
                        break;
                    case Phase.Laid:
                        for (int spot = 0; spot < _layout.Slots.Length; spot++)
                        {
                            if (_reading.Slots[spot] >= 0 && InCard(at, V(_layout.Slots[spot])))
                            {
                                _hoverSpot = spot;
                            }
                        }
                        break;
                }
            }

            if (e.type != EventType.MouseDown || e.button != 0 || !inside)
            {
                return;
            }
            e.Use();
            if (Busy)
            {
                return;
            }
            if (_hoverDeck)
            {
                Riffle();
            }
            else if (_hoverPile >= 0)
            {
                TakePile(_hoverPile);
            }
            else if (_hoverCard >= 0)
            {
                ClickFanCard(_hoverCard);
            }
            else if (_hoverSpot >= 0)
            {
                if (_reading.FaceUp[_hoverSpot])
                {
                    _detail = _hoverSpot;
                }
                else
                {
                    Flip(_hoverSpot);
                }
            }
        }

        // The card in the fan under a point: the top one, so the one drawn last. A picked card can be
        // clicked by the part of it that has risen.
        private int FanCardAt(Vector2 at)
        {
            List<int> deck = _reading.Deck;
            for (int i = deck.Count - 1; i >= 0; i--)
            {
                (float x, float y) = _layout.FanPos(i, deck.Count);
                float rise = _reading.Picked.Contains(deck[i]) ? TableLayout.PickLift : 0f;
                if (InCard(at, new Vector2(x, y), rise))
                {
                    return deck[i];
                }
            }
            return -1;
        }

        private void DrawTable(float now)
        {
            GUI.DrawTexture(_table, _art.Table(_layout));
            DrawText(_layout.Spread.Title, Palette.Pink4, _layout.Width / 2f, _layout.TitleY);
            for (int spot = 0; spot < _layout.Slots.Length; spot++)
            {
                float middle = _layout.Slots[spot].x + CardW / 2f;
                DrawText(_layout.Spread.Positions[spot].Name, Palette.Ink, middle, _layout.LabelY);
                int card = _reading.Slots[spot];
                if (card >= 0 && _reading.FaceUp[spot] && !_flips.ContainsKey(spot) && !_slides.ContainsKey(card) && Reversed(card))
                {
                    DrawText("reversed", Palette.Pink4, middle, _layout.LabelY + TableLayout.LabelLine);
                }
            }

            Phase phase = _reading.Phase;
            if (_show == Show.Riffle)
            {
                DrawRiffle(now);
            }
            else if (_show == Show.Wash)
            {
                DrawWash(now);
            }
            else if (phase != Phase.Picking)
            {
                DrawPile(V(_layout.Deck), _reading.Deck.Count(card => !_slides.ContainsKey(card)), _hoverDeck);
            }

            if (phase == Phase.Cutting)
            {
                for (int pile = 0; pile < 3; pile++)
                {
                    DrawPile(V(_layout.CutPiles[pile]), _reading.Piles[pile].Count(card => !_slides.ContainsKey(card)), pile == _hoverPile);
                }
            }
            else if (phase == Phase.Laid)
            {
                for (int spot = 0; spot < _layout.Slots.Length; spot++)
                {
                    DrawSpotCard(spot, now);
                }
            }
            else if (phase == Phase.Picking)
            {
                DrawFan(now);
            }

            DrawSliding(now);
        }

        private void DrawSpotCard(int spot, float now)
        {
            int card = _reading.Slots[spot];
            if (card < 0 || _slides.ContainsKey(card))
            {
                return;
            }
            Vector2 pos = V(_layout.Slots[spot]);
            bool faceUp = _reading.FaceUp[spot];
            float squash = 1f;
            if (_flips.TryGetValue(spot, out float start))
            {
                // Narrows to nothing showing the back, then widens showing the face.
                float u = (now - start) / FlipTime;
                if (u < 0.5f)
                {
                    faceUp = false;
                    squash = u <= 0f ? 1f : 1f - 2f * u;
                }
                else
                {
                    squash = Mathf.Min(1f, 2f * u - 1f);
                }
            }
            bool hover = spot == _hoverSpot;
            if (hover && !faceUp)
            {
                pos.y -= 2f;
            }
            if (hover && squash >= 1f)
            {
                DrawGlow(pos);
            }
            DrawCard(faceUp ? _art.Face(card, Reversed(card)) : _art.Back, pos, squash);
        }

        private void DrawFan(float now)
        {
            List<int> deck = _reading.Deck;
            for (int i = 0; i < deck.Count; i++)
            {
                int card = deck[i];
                if (_slides.ContainsKey(card))
                {
                    continue;
                }
                Vector2 pos = VisualPos(card, now);
                int picked = _reading.Picked.IndexOf(card);
                if (picked >= 0 || card == _hoverCard)
                {
                    DrawGlow(pos);
                }
                DrawCard(_art.Back, pos);
                if (picked >= 0)
                {
                    // Which spot it goes to.
                    DrawText((picked + 1).ToString(), Palette.Pink4, pos.x + CardW / 2f, pos.y - 10f);
                }
            }
        }

        // Cards on the move go over everything: the ones still waiting their turn first (deepest first),
        // then the moving ones in the order they set off.
        private void DrawSliding(float now)
        {
            if (_slides.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<int, Slide> slide in _slides.Where(s => now < s.Value.Start).OrderByDescending(s => s.Value.Start))
            {
                DrawCard(_art.Back, slide.Value.From);
            }
            foreach (KeyValuePair<int, Slide> slide in _slides.Where(s => now >= s.Value.Start).OrderBy(s => s.Value.Start))
            {
                DrawCard(_art.Back, VisualPos(slide.Key, now));
            }
        }

        // A riffle: half the deck slides out, the two halves fall together card by card in the middle,
        // and the deck goes back.
        private void DrawRiffle(float now)
        {
            float t = now - _showStart;
            int n = _reading.Deck.Count;
            Vector2 deck = V(_layout.Deck);
            Vector2 side = deck + new Vector2(CardW + 24f, 0f);
            Vector2 middle = (deck + side) / 2f + new Vector2(0f, 4f);
            if (t < 0.7f)
            {
                float apart = Ease(Mathf.Clamp01(t / 0.15f));
                float left = Mathf.Clamp01(1f - (t - 0.15f) / 0.55f);
                int half = Mathf.CeilToInt(n / 2f * left);
                DrawPile(deck, half);
                DrawPile(Vector2.Lerp(deck, side, apart), half);
                if (t > 0.15f)
                {
                    DrawPile(middle, Mathf.Max(1, n - 2 * half));
                }
                for (int i = 0; i < 10; i++)
                {
                    float u = (t - 0.15f - i * 0.05f) / 0.12f;
                    if (u > 0f && u < 1f)
                    {
                        Vector2 from = i % 2 == 0 ? deck : side;
                        DrawCard(_art.Back, Vector2.Lerp(from, middle, u) + new Vector2(0f, -8f * Mathf.Sin(Mathf.PI * u)));
                    }
                }
            }
            else
            {
                DrawPile(Vector2.Lerp(middle, deck, Ease(Mathf.Clamp01((t - 0.7f) / 0.2f))), n);
            }
        }

        // A wash: the cards spread out face down and swirl round, turning, then gather back up.
        private void DrawWash(float now)
        {
            float t = now - _showStart;
            Vector2 deck = V(_layout.Deck);
            (float cx, float cy) = _layout.SpreadMiddle;
            var centre = new Vector2(cx - CardW / 2f, cy - CardH / 2f);
            float spread = t < 0.3f ? Ease(t / 0.3f) : t > WashTime - 0.35f ? 1f - Ease(Mathf.Clamp01((t - (WashTime - 0.35f)) / 0.35f)) : 1f;
            DrawPile(deck, t > WashTime - 0.1f ? _reading.Deck.Count : 0);
            foreach (Swirl s in _swirls)
            {
                float angle = s.Angle + s.Speed * t;
                var round = new Vector2(Mathf.Cos(angle) * s.Radius, Mathf.Sin(angle) * Mathf.Min(s.Radius * 0.4f, 12f));
                Vector2 pos = Vector2.Lerp(deck, centre + round, spread);
                DrawCardTurned(_art.Back, pos, (s.Tilt + s.Spin * t) * spread);
            }
        }

        private void DrawPile(Vector2 pos, int count, bool glow = false)
        {
            if (count <= 0)
            {
                return;
            }
            for (int e = TableLayout.Edges(count); e >= 1; e--)
            {
                DrawCard(_art.PileEdge, pos + new Vector2(e, e));
            }
            if (glow)
            {
                DrawGlow(pos);
            }
            DrawCard(_art.Back, pos);
        }

        private Rect CardRect(Vector2 pos, int width = CardW, int height = CardH)
        {
            return new Rect(_table.x + Mathf.Round(pos.x * _scale), _table.y + Mathf.Round(pos.y * _scale), width * _scale, height * _scale);
        }

        // squash: how much of its width shows (a card turning over).
        private void DrawCard(Texture2D texture, Vector2 pos, float squash = 1f)
        {
            Rect rect = CardRect(pos);
            if (squash < 1f)
            {
                float width = Mathf.Max(_scale, Mathf.Round(rect.width * squash));
                rect = new Rect(Mathf.Round(rect.center.x - width / 2f), rect.y, width, rect.height);
            }
            GUI.DrawTexture(rect, texture);
        }

        private void DrawCardTurned(Texture2D texture, Vector2 pos, float degrees)
        {
            Rect rect = CardRect(pos);
            GUIUtility.RotateAroundPivot(degrees, rect.center);
            GUI.DrawTexture(rect, texture);
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawGlow(Vector2 pos)
        {
            GUI.DrawTexture(CardRect(pos - new Vector2(2f, 2f), CardW + 4, CardH + 4), _art.Glow);
        }

        // A line of pixel text centred on x (art pixels).
        private void DrawText(string text, uint colour, float middle, float y)
        {
            Texture2D texture = _art.Text(text, colour);
            float width = texture.width * _scale;
            GUI.DrawTexture(new Rect(_table.x + Mathf.Round(middle * _scale - width / 2f), _table.y + Mathf.Round(y * _scale),
                width, texture.height * _scale), texture);
        }

        // ---- The row under the table ----

        private void DrawStatusRow()
        {
            float y = _table.yMax + _gap;
            float right = _panel.xMax - _pad;
            bool ready = !Busy && !Overlay;
            switch (_reading.Phase)
            {
                case Phase.Ready:
                    right = StatusButton(right, y, "Pick", ready, StartPick);
                    right = StatusButton(right, y, "Deal", ready, Deal);
                    right = StatusButton(right, y, "Cut", ready, StartCut);
                    right = StatusButton(right, y, "Wash", ready, Wash);
                    right = StatusButton(right, y, "Shuffle", ready, Riffle);
                    break;
                case Phase.Picking:
                    right = StatusButton(right, y, "Cancel", _finishPickAt < 0f && !Overlay, CancelPick);
                    break;
                case Phase.Laid:
                    right = StatusButton(right, y, "New reading", ready, Gather);
                    if (!_reading.AllFaceUp)
                    {
                        right = StatusButton(right, y, "Turn all over", ready, FlipAll);
                    }
                    break;
            }
            GUI.Label(new Rect(_panel.x + _pad, y, Mathf.Max(0f, right - _panel.x - _pad), _status), StatusText(), _statusStyle);
        }

        private string StatusText()
        {
            switch (_reading.Phase)
            {
                case Phase.Cutting:
                    return _reading.Taken.Count == 0
                        ? "Click the piles to stack them back up"
                        : $"Click the piles to stack them back up ({3 - _reading.Taken.Count} left)";
                case Phase.Picking:
                {
                    int left = _reading.Spread.Count - _reading.Picked.Count;
                    if (left == 0)
                    {
                        return "Laying them out...";
                    }
                    return _reading.Picked.Count == 0 ? $"Pick {left} cards from the fan" : $"Pick {left} more";
                }
                case Phase.Laid:
                    return _reading.AllFaceUp ? "Click a card to see it big" : "Click a card to turn it over";
                default:
                {
                    var parts = new List<string> { $"{_reading.Deck.Count} cards" };
                    if (_reading.Shuffles > 0)
                    {
                        parts.Add(_reading.Shuffles == 1 ? "shuffled once" : $"shuffled {_reading.Shuffles} times");
                    }
                    if (_reading.Washes > 0)
                    {
                        parts.Add(_reading.Washes == 1 ? "washed" : $"washed {_reading.Washes} times");
                    }
                    if (_reading.WasCut)
                    {
                        parts.Add("cut");
                    }
                    return string.Join(" · ", parts);
                }
            }
        }

        private float StatusButton(float right, float y, string label, bool enabled, Action action)
        {
            float width = Mathf.Max(Units(40f), _buttonStyle.CalcSize(new GUIContent(label)).x + Units(16f));
            var rect = new Rect(right - width, y + Units(2f), width, _status - Units(4f));
            bool was = GUI.enabled;
            GUI.enabled = enabled;
            if (GUI.Button(rect, label, _buttonStyle))
            {
                _clicks.Add(action);
            }
            GUI.enabled = was;
            return rect.x - Units(ButtonGapUnits);
        }

        private float HeaderButton(float right, float y, string label, Action action)
        {
            float width = Mathf.Max(Units(28f), _buttonStyle.CalcSize(new GUIContent(label)).x + Units(12f));
            var rect = new Rect(right - width, y + Units(2f), width, _header - Units(4f));
            if (GUI.Button(rect, label, _buttonStyle))
            {
                _clicks.Add(action);
            }
            return rect.x - Units(ButtonGapUnits);
        }

        // Drag the title row to move the window anywhere on screen; double-click it to put the window
        // back with the other mods' windows. Saved in 1080p units, like other mods' windows.
        private void HandleWindowDrag(Rect area)
        {
            Event e = Event.current;
            switch (e.GetTypeForControl(_windowControl))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && area.Contains(e.mousePosition))
                    {
                        if (e.clickCount == 2)
                        {
                            _windowDragging = false;
                            S.WindowX.Value = -1f;
                            S.WindowY.Value = -1f;
                        }
                        else
                        {
                            GUIUtility.hotControl = _windowControl;
                            _windowDragging = true;
                            _windowGrab = e.mousePosition - _panel.position;
                            _windowPos = _panel.position;
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == _windowControl)
                    {
                        _windowPos = ClampToScreen(e.mousePosition - _windowGrab, _panel.size);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == _windowControl)
                    {
                        GUIUtility.hotControl = 0;
                        _windowDragging = false;
                        Vector2 at = ClampToScreen(_windowPos, _panel.size) / _unit;
                        S.WindowX.Value = Mathf.Round(at.x);
                        S.WindowY.Value = Mathf.Round(at.y);
                        e.Use();
                    }
                    break;
            }
        }

        // ---- Boxes over the table ----

        private void ShadeOver(Rect area)
        {
            Color was = GUI.color;
            GUI.color = Shade;
            GUI.DrawTexture(area, _art.Pixel);
            GUI.color = was;
        }

        private Rect CentreBox(float width, float height)
        {
            ShadeOver(_table);
            var box = new Rect(Mathf.Round(_table.center.x - width / 2f), Mathf.Round(_table.center.y - height / 2f), width, height);
            GUI.Box(box, GUIContent.none, _boxStyle);
            GUI.Box(box, GUIContent.none, _boxStyle);
            return box;
        }

        // A card turned over, big, with its name, the spot it's in and which way up it lies.
        private void DrawDetail()
        {
            int card = _detail < _reading.Slots.Length ? _reading.Slots[_detail] : -1;
            if (card < 0 || !_reading.FaceUp[_detail])
            {
                _detail = -1;
                return;
            }
            // A click anywhere on the window closes it. First, so it gets the click before the labels.
            if (GUI.Button(_panel, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => _detail = -1);
            }
            ShadeOver(_panel);
            int big = Mathf.Max(_scale, Mathf.FloorToInt((_panel.height - 2 * _pad) / CardH));
            float w = CardW * big, h = CardH * big;
            var rect = new Rect(Mathf.Round(_panel.x + _pad + Units(24f)), Mathf.Round(_panel.center.y - h / 2f), w, h);
            GUI.DrawTexture(rect, _art.Face(card, Reversed(card)));

            float x = rect.xMax + Units(28f);
            float width = Mathf.Max(0f, _panel.xMax - _pad - x);
            float y = rect.y + Units(16f);
            GUI.Label(new Rect(x, y, width, Units(40f)), $"<b>{Deck.Name(card)}</b>", _nameStyle);
            y += Units(44f);
            string way = !S.Reversals.Value ? "" : _reading.Upside[card] ? "\nReversed (upside down)" : "\nUpright";
            string picture = CardFaces.HasPicture(card) ? "" : "\n\n<i>This card's picture is still being drawn.</i>";
            string text = $"{Deck.Kind(card)}\n\n<b>{_reading.Spread.Positions[_detail].Name}</b>{way}{picture}\n\n<color=#ffffff88>Click to close</color>";
            GUI.Label(new Rect(x, y, width, rect.yMax - y), text, _detailStyle);
        }

        private const string HelpText =
            "<b>How it works</b>\n" +
            "Your deck starts new, in order, so shuffle it first.\n\n" +
            "<b>Shuffle</b> riffles the deck once: shuffle as often as feels right (clicking the deck does too). " +
            "<b>Wash</b> spreads the cards out face down and swirls them round, which turns some upside down. " +
            "<b>Cut</b> splits the deck into three piles: click them to stack them back up, in any order.\n\n" +
            "<b>Deal</b> lays the top cards on the spread. <b>Pick</b> fans the deck out so you choose them yourself " +
            "(click a picked card again to put it back).\n\n" +
            "Click a card on the spread to turn it over, and again to see it big. A card lying upside down is " +
            "reversed (Options can turn that off). <b>New reading</b> puts the cards back under the deck. Your deck " +
            "and reading keep when you close the window or the game.\n\n" +
            "Click to close.";

        private void DrawHelp()
        {
            // A click anywhere on the table closes the card. First, so it gets the click before the labels.
            if (GUI.Button(_table, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => _help = false);
            }
            float width = Mathf.Min(_table.width - Units(24f), Units(560f));
            float height = _helpStyle.CalcHeight(new GUIContent(HelpText), width - Units(32f)) + Units(24f);
            Rect box = CentreBox(width, Mathf.Min(_table.height - Units(12f), height));
            GUI.Label(new Rect(box.x + Units(16f), box.y + Units(12f), box.width - Units(32f), box.height - Units(24f)), HelpText, _helpStyle);
        }

        private void DrawOptions()
        {
            Rect box = CentreBox(Units(360f), Units(190f));
            // A click on the table outside the box closes it.
            Event e = Event.current;
            if (e.type == EventType.MouseDown && _table.Contains(e.mousePosition) && !box.Contains(e.mousePosition))
            {
                _clicks.Add(() => _options = false);
                e.Use();
            }
            float x = box.x + Units(16f);
            float w = box.width - Units(32f);
            float y = box.y + Units(12f);
            GUI.Label(new Rect(x, y, w, Units(24f)), "<b>Options</b>", _helpStyle);
            y += Units(32f);
            if (GUI.Button(new Rect(x, y, w, Units(28f)), "Reversed cards: " + (S.Reversals.Value ? "on" : "off"), _buttonStyle))
            {
                _clicks.Add(() => S.Reversals.Value = !S.Reversals.Value);
            }
            y += Units(34f);
            if (GUI.Button(new Rect(x, y, w, Units(28f)), "Major cards only: " + (S.MajorsOnly.Value ? "on" : "off"), _buttonStyle))
            {
                _clicks.Add(() => SetMajorsOnly(!S.MajorsOnly.Value));
            }
            y += Units(34f);
            string note = S.MajorsOnly.Value != _reading.MajorsOnly
                ? "The deck changes when you start a new reading."
                : "Click outside this box to close it.";
            GUI.Label(new Rect(x, y, w, Units(40f)), note, _hintStyle);
        }

        private void MakeStyles()
        {
            if (_boxStyle != null && Mathf.Approximately(_styleUnit, _unit))
            {
                return;
            }
            _styleUnit = _unit;
            int Font(float size) => Mathf.Max(8, Mathf.RoundToInt(size * _unit));

            _boxStyle = new GUIStyle(GUI.skin.box);
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(15f),
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };
            _titleStyle.normal.textColor = Color.white;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(13f),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            _hintStyle.normal.textColor = Dim;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Font(14f) };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(14f),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _statusStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            _helpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(14f),
                richText = true,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            _helpStyle.normal.textColor = Color.white;
            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(26f),
                richText = true,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            _nameStyle.normal.textColor = new Color32(0xFF, 0xC4, 0xDA, 0xFF);
            _detailStyle = new GUIStyle(_helpStyle) { fontSize = Font(16f) };
        }

        // The chat box. Only looked up while it matters: on the main menu MonoSingleton.I searches the scene.
        private static bool IsTyping()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }
    }
}
