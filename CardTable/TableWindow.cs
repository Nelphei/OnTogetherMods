using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = UnityEngine.EventType;

namespace CardTable
{
    // The Card Table window: a title row with a tab for each game, the green table, and a row of
    // buttons under it. It sits in the stack with the other mods' windows until you drag its title row.
    // Mouse only: the keyboard stays the game's. Solitaire is drawn here; Go Fish by GoFishBoard, whose
    // tables keep running (and talking to other players) while the window is closed.
    //
    // The cards are pixel art, so the table is laid out in screen pixels, at a whole number of screen
    // pixels per art pixel (the rest of the window scales with the screen like other mods' windows).
    internal sealed class TableWindow : MonoBehaviour
    {
        private const string PanelId = "CardTable";
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // ---- The table, in art pixels ----
        private const int CardW = CardPixels.Width;
        private const int CardH = CardPixels.Height;
        private const int ColumnGap = 5;
        private const int Rim = 4;
        private const int FeltPad = 5;
        private const int RowGap = 7;
        // Steps between the cards of a tableau pile. A face-up step shows the card's corner.
        private const int DownStep = 4;
        private const int UpStep = 13;
        private const int MinUpStep = 7;
        // The waste's spread-out cards (drawing three).
        private const int FanStep = 18;
        // Room for 6 face-down and 8 face-up cards; longer piles squeeze together.
        private const int TableauRoom = CardH + 6 * DownStep + 7 * UpStep;
        private const int Inset = Rim + FeltPad;
        private const int TableWidth = 7 * CardW + 6 * ColumnGap + 2 * Inset;
        private const int TableHeight = CardH + RowGap + TableauRoom + 2 * Inset;

        // ---- The rest of the window, in 1080p units ----
        private const float PadUnits = 8f;
        private const float HeaderUnits = 28f;
        private const float GapUnits = 6f;
        private const float StatusUnits = 28f;
        private const float ButtonGapUnits = 4f;
        // How far the mouse moves before a press on a card becomes a drag.
        private const float DragUnits = 4f;

        // ---- Timing, in seconds ----
        private const float SlideTime = 0.16f;
        private const float DealGap = 0.03f;
        private const float AutoStepTime = 0.09f;
        private const float SaveDelay = 2f;
        private const float ConfirmTime = 3f;
        private const float ConfettiTime = 7f;

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Gold = new Color32(0xF5, 0xD0, 0x61, 0xFF);
        private static readonly Color Shade = new Color(0f, 0f, 0f, 0.6f);

        private static TableWindow _instance;

        private static Settings S => Plugin.Settings;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private readonly Store _store = new Store();
        private readonly Solitaire _game = new Solitaire();
        private readonly System.Random _random = new System.Random();
        private CardArt _art;

        private CardNet _net;
        private GoFishTables _tables;
        private GoFishBoard _fish;

        private static bool FishTab => S.Game.Value == TableGame.GoFish;

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
        private Rect _table;     // the felt and its wooden rim
        private Vector2 _origin; // the top-left of the cards' area: card positions are measured from here

        // Where every card is: its pile and its place in it. Rebuilt after every change.
        private readonly PileId[] _pileOf = new PileId[Cards.Count];
        private readonly int[] _indexOf = new int[Cards.Count];

        // ---- Dragging the window by its title row ----
        private int _windowControl;
        private bool _windowDragging;
        private Vector2 _windowGrab;
        private Vector2 _windowPos;

        // ---- Pressing, clicking and dragging cards ----
        private int _boardControl;
        private bool _pressed;
        private bool _dragging;
        private PileId _pressPile;
        private int _pressIndex;
        private Vector2 _pressMouse;
        private Vector2 _grab;    // the mouse, from the top dragged card's corner
        private Vector2 _dragAt;  // the top dragged card's corner, from _origin
        private PileId? _dropTarget;
        private PileId _lastClickPile;

        // Cards gliding to where they go: from where they were, starting at a time (the deal staggers
        // them). A card that was turned over on the way shows its old side for the first half.
        private struct Slide
        {
            public Vector2 From;
            public float Start;
            public bool StartFaceUp;
        }

        private readonly Dictionary<int, Slide> _slides = new Dictionary<int, Slide>();

        private bool _autoFinishing;
        private float _nextAutoStep;
        // Whether Auto-finish would win, worked out once per table (moves made, and drawing one or three).
        private int _autoCheckedAt = -1;
        private bool _autoWins;

        // ---- Boxes over the table ----
        private bool _help;
        private bool _statsBox;
        private float _confirmUntil = -1f;
        private bool _wonCounted;
        // The "You won!" box, and when the confetti started.
        private bool _showWin;
        private float _wonAt = -1000f;

        private struct Confetto
        {
            public int Suit;
            public float X;
            public float Y;
            public float Speed;
            public float Sway;
            public float Phase;
        }

        private readonly List<Confetto> _confetti = new List<Confetto>();

        private float _styleUnit = -1f;
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _tabOnStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _bigStyle;
        private GUIStyle _centreStyle;
        private GUIStyle _helpStyle;

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "CardTable ClickBlocker");
            _store.Load();
            if (_game.Load(_store.SavedGame))
            {
                // A game won before closing already went into the stats.
                _wonCounted = _game.Won;
                _showWin = _game.Won;
            }
            else
            {
                _game.Deal(_random);
            }
            _game.DrawThree = S.DrawThree.Value;
            RebuildWhere();

            _net = new CardNet();
            _tables = new GoFishTables(_net, _random);
            _fish = new GoFishBoard(_tables, _store, MarkDirty);
            _net.Broadcasted += (from, words) => _tables.OnBroadcast(from, words, Time.unscaledTime);
            _net.Direct += (from, words) => _tables.OnDirect(from, words, Time.unscaledTime);
            _net.MemberLeft += id => _tables.OnMemberLeft(id, Time.unscaledTime);
            _net.LobbyChanged += hadLobby => _tables.OnLobbyChanged(hadLobby, Time.unscaledTime);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            if (_saveAt >= 0f)
            {
                SaveNow();
            }
            LeaveGoFish();
            _art?.Destroy();
            _blocker?.Destroy();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
            LeaveGoFish();
        }

        // Tells the other players at your Go Fish table that you've gone.
        private void LeaveGoFish()
        {
            try
            {
                _tables?.LeaveTable(Time.unscaledTime);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
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

                _game.DrawThree = S.DrawThree.Value;
                float now = Time.unscaledTime;

                // Go Fish runs with the window closed too: other players are waiting on your table.
                _net.Update();
                if (!inWorld && (_tables.Hosting != null || _tables.Joined != null))
                {
                    _tables.LeaveTable(now);
                }
                _tables.Update(now);
                _fish.Update(now, _open && FishTab && !PanelStack.Hidden);

                // The clock runs from the first move while the window shows Solitaire.
                if (_open && !FishTab && !PanelStack.Hidden && _game.Started && !_game.Won)
                {
                    _game.Seconds += Time.unscaledDeltaTime;
                }
                if (_autoFinishing && now >= _nextAutoStep)
                {
                    AutoStep(now);
                }
                if (_slides.Count > 0)
                {
                    foreach (int card in _slides.Where(p => now >= p.Value.Start + SlideTime).Select(p => p.Key).ToList())
                    {
                        _slides.Remove(card);
                    }
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
            ShowHelpOnce();
        }

        // Each game's how-to-play card shows by itself the first time you see that game.
        private void ShowHelpOnce()
        {
            ConfigEntry<bool> seen = FishTab ? S.GoFishHelpSeen : S.HelpSeen;
            if (!seen.Value)
            {
                _help = true;
                _statsBox = false;
                seen.Value = true;
            }
        }

        private void SwitchTo(TableGame game)
        {
            if (S.Game.Value == game)
            {
                return;
            }
            S.Game.Value = game;
            _help = false;
            _statsBox = false;
            _pressed = false;
            _dragging = false;
            _confirmUntil = -1f;
            ShowHelpOnce();
        }

        // Closing keeps your game; opening again carries on.
        private void Close()
        {
            _open = false;
            _help = false;
            _statsBox = false;
            _confirmUntil = -1f;
            _autoFinishing = false;
            _pressed = false;
            _dragging = false;
            _windowDragging = false;
            _slides.Clear();
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
            _store.Save(_game);
        }

        // ---- Changing the game ----

        // Runs a change to the game. The cards that moved glide from where they were showing to where
        // they are now. before: where they were showing, if not now (a drop: where they were dragged to).
        private bool Change(Func<bool> action, Dictionary<int, Vector2> before = null)
        {
            float now = Time.unscaledTime;
            if (before == null)
            {
                before = VisualPositions(now);
            }
            var wasFaceUp = new bool[Cards.Count];
            for (int card = 0; card < Cards.Count; card++)
            {
                wasFaceUp[card] = VisualFaceUp(card, now);
            }

            if (!action())
            {
                return false;
            }
            RebuildWhere();

            if (S.Animations.Value && _scale > 0)
            {
                for (int card = 0; card < Cards.Count; card++)
                {
                    Vector2 to = Target(card);
                    if (before.TryGetValue(card, out Vector2 from) && (from - to).sqrMagnitude > 0.25f)
                    {
                        _slides[card] = new Slide { From = from, Start = now, StartFaceUp = wasFaceUp[card] };
                    }
                }
            }
            else
            {
                _slides.Clear();
            }

            _confirmUntil = -1f;
            if (!_game.Counted && _game.Started)
            {
                _game.Counted = true;
                _store.Stats.Played++;
            }
            if (_game.Won && !_wonCounted)
            {
                Won(now);
            }
            MarkDirty();
            return true;
        }

        private void Won(float now)
        {
            _wonCounted = true;
            _autoFinishing = false;
            SolitaireStats stats = _store.Stats;
            stats.Won++;
            stats.Streak++;
            stats.BestStreak = Math.Max(stats.BestStreak, stats.Streak);
            if (stats.BestSeconds <= 0 || _game.Seconds < stats.BestSeconds)
            {
                stats.BestSeconds = _game.Seconds;
            }
            if (stats.FewestMoves <= 0 || _game.Moves < stats.FewestMoves)
            {
                stats.FewestMoves = _game.Moves;
            }
            _showWin = true;
            _wonAt = now;
            _confetti.Clear();
            for (int i = 0; i < 48; i++)
            {
                _confetti.Add(new Confetto
                {
                    Suit = _random.Next(4),
                    X = (float)_random.NextDouble(),
                    Y = -(float)_random.NextDouble() * 1.2f,
                    Speed = 0.25f + (float)_random.NextDouble() * 0.35f,
                    Sway = 4f + (float)_random.NextDouble() * 10f,
                    Phase = (float)_random.NextDouble() * 6.3f,
                });
            }
            SaveNow();
            Plugin.Log.LogInfo($"Solitaire won in {_game.Moves} moves, {TimeText(_game.Seconds)}.");
        }

        private void NewGame()
        {
            // Giving up on a game ends a winning streak.
            if (_game.Started && !_game.Won)
            {
                _store.Stats.Streak = 0;
            }
            _game.Deal(_random);
            _game.DrawThree = S.DrawThree.Value;
            _wonCounted = false;
            _showWin = false;
            _confetti.Clear();
            _autoFinishing = false;
            _autoCheckedAt = -1;
            _confirmUntil = -1f;
            _pressed = false;
            _dragging = false;
            _slides.Clear();
            RebuildWhere();
            if (S.Animations.Value && _scale > 0)
            {
                // Dealt from the deck, one card after another, turning over as they land.
                Vector2 deck = CardPos(PileId.Stock, 0);
                float start = Time.unscaledTime;
                foreach (int card in _game.DealOrder())
                {
                    _slides[card] = new Slide { From = deck, Start = start, StartFaceUp = false };
                    start += DealGap;
                }
            }
            SaveNow();
        }

        private void ClickNewGame()
        {
            float now = Time.unscaledTime;
            // A game you've started takes a second click, in case the first was a slip.
            if (_game.Started && !_game.Won && now > _confirmUntil)
            {
                _confirmUntil = now + ConfirmTime;
                return;
            }
            NewGame();
        }

        // Shown only when it would finish the game.
        private bool CanOfferAutoFinish
        {
            get
            {
                if (_autoFinishing || !_game.CanAutoFinish)
                {
                    return false;
                }
                int key = _game.Moves * 2 + (_game.DrawThree ? 1 : 0);
                if (key != _autoCheckedAt)
                {
                    _autoCheckedAt = key;
                    _autoWins = _game.AutoFinishWins();
                }
                return _autoWins;
            }
        }

        private void StartAutoFinish()
        {
            _autoFinishing = true;
            _game.StartAutoFinish();
            _nextAutoStep = Time.unscaledTime;
        }

        private void AutoStep(float now)
        {
            _nextAutoStep = now + AutoStepTime;
            if (!Change(() => _game.AutoFinishStep()))
            {
                _autoFinishing = false;
            }
        }

        // A click (not a drag) on the table: the deck turns cards over, a card goes where it fits.
        private void ClickCard(PileId pile, int index)
        {
            if (pile == PileId.Stock)
            {
                Change(() => _game.Draw());
                return;
            }
            PileId? to = _game.BestMove(pile, index);
            if (to.HasValue)
            {
                Change(() => _game.Move(pile, index, to.Value));
            }
        }

        private void Drop(float now)
        {
            Dictionary<int, Vector2> before = VisualPositions(now);
            var dragged = DraggedCards().ToList();
            PileId? target = _dropTarget;
            _dragging = false;
            _pressed = false;
            _dropTarget = null;
            if (target.HasValue && Change(() => _game.Move(_pressPile, _pressIndex, target.Value), before))
            {
                return;
            }
            // Nowhere to go: back where they came from.
            if (S.Animations.Value)
            {
                foreach (int card in dragged)
                {
                    _slides[card] = new Slide { From = before[card], Start = now, StartFaceUp = true };
                }
            }
        }

        // ---- Where the cards are ----

        private void RebuildWhere()
        {
            Note(PileId.Stock);
            Note(PileId.Waste);
            for (int f = 0; f < Solitaire.FoundationCount; f++)
            {
                Note(PileId.Foundation(f));
            }
            for (int t = 0; t < Solitaire.Piles; t++)
            {
                Note(PileId.Tableau(t));
            }
        }

        private void Note(PileId pile)
        {
            List<int> cards = _game.Pile(pile);
            for (int i = 0; i < cards.Count; i++)
            {
                _pileOf[cards[i]] = pile;
                _indexOf[cards[i]] = i;
            }
        }

        private float Column(int column) => column * (CardW + ColumnGap) * _scale;

        private float TableauTop => (CardH + RowGap) * _scale;

        // How many of the waste's top cards are spread out.
        private int Fanned()
        {
            int count = _game.Waste.Count;
            return count == 0 ? 0 : Mathf.Clamp(_game.WasteFanned, 1, Math.Min(3, count));
        }

        // A tableau pile's steps between cards, squeezed together when the pile is too long for the table.
        private void Steps(int pile, out float down, out float up)
        {
            int count = _game.Tableau[pile].Count;
            int faceDown = _game.FaceDown[pile];
            int faceUp = count - faceDown;
            down = DownStep * _scale;
            up = UpStep * _scale;
            float room = (TableauRoom - CardH) * _scale;
            if (faceDown * down + Math.Max(0, faceUp - 1) * up > room && faceUp > 1)
            {
                up = Math.Max(MinUpStep * _scale, (room - faceDown * down) / (faceUp - 1));
            }
            if (faceDown * down + Math.Max(0, faceUp - 1) * up > room && faceDown > 0)
            {
                down = Math.Max(_scale, (room - Math.Max(0, faceUp - 1) * up) / faceDown);
            }
        }

        // Where a card at this spot sits, from _origin, in screen pixels.
        private Vector2 CardPos(PileId pile, int index)
        {
            switch (pile.Kind)
            {
                case PileKind.Stock:
                    return new Vector2(Column(0), 0f);
                case PileKind.Waste:
                {
                    int firstFanned = _game.Waste.Count - Fanned();
                    return new Vector2(Column(1) + Math.Max(0, index - firstFanned) * FanStep * _scale, 0f);
                }
                case PileKind.Foundation:
                    return new Vector2(Column(3 + pile.Index), 0f);
                default:
                {
                    Steps(pile.Index, out float down, out float up);
                    int faceDown = _game.FaceDown[pile.Index];
                    float y = index < faceDown ? index * down : faceDown * down + (index - faceDown) * up;
                    return new Vector2(Column(pile.Index), TableauTop + Mathf.Floor(y));
                }
            }
        }

        private Vector2 Target(int card) => CardPos(_pileOf[card], _indexOf[card]);

        private bool IsDragged(int card) => _dragging && _pileOf[card] == _pressPile && _indexOf[card] >= _pressIndex;

        private IEnumerable<int> DraggedCards()
        {
            List<int> cards = _game.Pile(_pressPile);
            for (int i = Math.Max(0, _pressIndex); i < cards.Count; i++)
            {
                yield return cards[i];
            }
        }

        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        // Where a card shows right now: dragged, gliding, or in its place.
        private Vector2 VisualPos(int card, float now)
        {
            if (IsDragged(card))
            {
                return _dragAt + CardPos(_pressPile, _indexOf[card]) - CardPos(_pressPile, _pressIndex);
            }
            Vector2 target = Target(card);
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

        private bool VisualFaceUp(int card, float now)
        {
            if (_slides.TryGetValue(card, out Slide slide) && (now - slide.Start) / SlideTime < 0.5f)
            {
                return slide.StartFaceUp;
            }
            return _game.IsFaceUp(_pileOf[card], _indexOf[card]);
        }

        private bool IsSliding(int card, float now)
        {
            return _slides.TryGetValue(card, out Slide slide) && now < slide.Start + SlideTime;
        }

        private Dictionary<int, Vector2> VisualPositions(float now)
        {
            var positions = new Dictionary<int, Vector2>(Cards.Count);
            for (int card = 0; card < Cards.Count; card++)
            {
                positions[card] = VisualPos(card, now);
            }
            return positions;
        }

        private Rect CardRect(Vector2 pos) => new Rect(pos.x, pos.y, CardW * _scale, CardH * _scale);

        // The card (or empty spot) under the mouse, from _origin. index is -1 for an empty spot.
        private bool HitTest(Vector2 at, out PileId pile, out int index)
        {
            for (int t = 0; t < Solitaire.Piles; t++)
            {
                List<int> cards = _game.Tableau[t];
                for (int i = cards.Count - 1; i >= 0; i--)
                {
                    if (CardRect(CardPos(PileId.Tableau(t), i)).Contains(at))
                    {
                        pile = PileId.Tableau(t);
                        index = i;
                        return true;
                    }
                }
                if (cards.Count == 0 && CardRect(new Vector2(Column(t), TableauTop)).Contains(at))
                {
                    pile = PileId.Tableau(t);
                    index = -1;
                    return true;
                }
            }
            for (int f = 0; f < Solitaire.FoundationCount; f++)
            {
                if (CardRect(CardPos(PileId.Foundation(f), 0)).Contains(at))
                {
                    pile = PileId.Foundation(f);
                    index = _game.Foundations[f].Count - 1;
                    return true;
                }
            }
            for (int i = _game.Waste.Count - 1; i >= Math.Max(0, _game.Waste.Count - Fanned()); i--)
            {
                if (CardRect(CardPos(PileId.Waste, i)).Contains(at))
                {
                    pile = PileId.Waste;
                    index = i;
                    return true;
                }
            }
            if (CardRect(CardPos(PileId.Stock, 0)).Contains(at))
            {
                pile = PileId.Stock;
                index = _game.Stock.Count - 1;
                return true;
            }
            pile = PileId.Stock;
            index = -1;
            return false;
        }

        // Where the dragged cards would land: the legal pile they overlap most.
        private PileId? FindDrop()
        {
            Rect held = CardRect(_dragAt);
            PileId? best = null;
            float bestArea = 0f;
            for (int f = 0; f < Solitaire.FoundationCount; f++)
            {
                Try(PileId.Foundation(f), CardRect(CardPos(PileId.Foundation(f), 0)));
            }
            for (int t = 0; t < Solitaire.Piles; t++)
            {
                int count = _game.Tableau[t].Count;
                Vector2 top = count > 0 ? CardPos(PileId.Tableau(t), count - 1) : new Vector2(Column(t), TableauTop);
                Try(PileId.Tableau(t), CardRect(top));
            }
            return best;

            void Try(PileId pile, Rect spot)
            {
                float w = Mathf.Min(held.xMax, spot.xMax) - Mathf.Max(held.x, spot.x);
                float h = Mathf.Min(held.yMax, spot.yMax) - Mathf.Max(held.y, spot.y);
                if (w > 0f && h > 0f && w * h > bestArea && _game.CanMove(_pressPile, _pressIndex, pile))
                {
                    bestArea = w * h;
                    best = pile;
                }
            }
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value)
            {
                // A dot while it's your turn at Go Fish with the window closed.
                bool yourTurn = !_open && _fish.YourTurn;
                IconBar.State state = _open ? IconBar.State.Open : yourTurn ? IconBar.State.On : IconBar.State.Plain;
                if (IconBar.Draw("CardTable", IconBar.CardTableOrder, Icon.Pixels, state, "Card Table",
                        S.WindowKey.Value, yourTurn ? "Your turn at Go Fish" : null) == IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
            }

            // Hidden while Photo Mode takes a photo, and while the game's eye button hides the UI.
            if (!_open || PanelStack.Hidden || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                // A drag cut short would keep every IMGUI control busy.
                if (GUIUtility.hotControl != 0 && (GUIUtility.hotControl == _windowControl || GUIUtility.hotControl == _boardControl))
                {
                    GUIUtility.hotControl = 0;
                }
                _windowDragging = false;
                _pressed = false;
                _dragging = false;
                return;
            }

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                Layout();
                if (_art == null)
                {
                    _art = new CardArt(TableWidth, TableHeight, Rim);
                }
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
                size = new Vector2(TableWidth * scale + 2 * _pad, _pad + _header + _gap + TableHeight * scale + _gap + _status + _pad);
                bool fits = size.y <= Screen.height - Units(40f) && size.x <= Screen.width - Units(40f);
                if (fits || scale <= 1)
                {
                    break;
                }
                scale--;
            }
            if (scale != _scale)
            {
                // Positions are in screen pixels, so anything gliding or held would jump.
                _slides.Clear();
                _dragging = false;
                _pressed = false;
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
                Rect placed = PanelStack.Place(PanelId, PanelStack.CardTableOrder, size / _unit);
                pos = placed.position * _unit;
            }
            pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            _panel = new Rect(pos, size);
            _table = new Rect(pos.x + _pad, pos.y + _pad + _header + _gap, TableWidth * scale, TableHeight * scale);
            _origin = new Vector2(_table.x + Inset * scale, _table.y + Inset * scale);
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
            // First, so they keep the same ids from frame to frame.
            _windowControl = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);
            _boardControl = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);

            GUI.Box(_panel, GUIContent.none, _boxStyle);

            // Title row: name and hint on the left, buttons on the right.
            float x = _panel.x + _pad;
            float y = _panel.y + _pad;
            float right = _panel.xMax - _pad;
            right = HeaderButton(right, y, "X", Close);
            right = HeaderButton(right, y, S.Size.Value == TableSize.Large ? "L" : "M", () =>
                S.Size.Value = S.Size.Value == TableSize.Large ? TableSize.Medium : TableSize.Large);
            right = HeaderButton(right, y, "?", () =>
            {
                _help = !_help;
                _statsBox = false;
            });
            right = HeaderButton(right, y, "Stats", () =>
            {
                _statsBox = !_statsBox;
                _help = false;
            });

            // A tab for each game, after the title.
            var title = new GUIContent("<b>CARD TABLE</b>");
            float titleWidth = _titleStyle.CalcSize(title).x + Units(10f);
            float tabs = TabButton(x + titleWidth, y, "Solitaire", TableGame.Solitaire);
            tabs = TabButton(tabs, y, "Go Fish", TableGame.GoFish);

            // The rest of the title row moves the window (the buttons above have taken their clicks).
            HandleWindowDrag(new Rect(_panel.x, _panel.y, right - _panel.x, _pad + _header));

            GUI.Label(new Rect(x, y, titleWidth, _header), title, _titleStyle);
            float hintX = tabs + Units(4f);
            GUI.Label(new Rect(hintX, y, Mathf.Max(0f, right - hintX - Units(ButtonGapUnits)), _header),
                FishTab ? _fish.Hint() : "click or drag the cards", _hintStyle);

            if (FishTab)
            {
                _fish.DrawTable(_table, _scale, _unit, _art, _help || _statsBox);
                _fish.DrawStatus(new Rect(_panel.x + _pad, _table.yMax + _gap, _panel.width - 2 * _pad, _status), _unit);
            }
            else
            {
                DrawTable();
                DrawStatusRow();
            }

            if (_showWin && !FishTab)
            {
                DrawWin();
            }
            else if (_help)
            {
                DrawHelp();
            }
            else if (_statsBox)
            {
                DrawStats();
            }
        }

        private void DrawTable()
        {
            float now = Time.unscaledTime;
            bool boxOver = _showWin || _help || _statsBox;
            if (!boxOver && !_autoFinishing)
            {
                HandleBoard(now);
            }

            GUI.DrawTexture(_table, _art.Table);

            // The empty spots, under the cards.
            Texture2D deckSpot = _game.Stock.Count == 0 && _game.Waste.Count > 0 ? _art.RecycleSlot : _art.Slot;
            DrawTexture(deckSpot, CardPos(PileId.Stock, 0));
            for (int f = 0; f < Solitaire.FoundationCount; f++)
            {
                DrawTexture(_art.AceSlot, CardPos(PileId.Foundation(f), 0));
            }
            for (int t = 0; t < Solitaire.Piles; t++)
            {
                if (_game.Tableau[t].Count == 0)
                {
                    DrawTexture(_art.Slot, new Vector2(Column(t), TableauTop));
                }
            }

            // Cards in their places, pile by pile, bottom up.
            DrawPile(PileId.Stock, now);
            DrawPile(PileId.Waste, now);
            for (int f = 0; f < Solitaire.FoundationCount; f++)
            {
                DrawPile(PileId.Foundation(f), now);
            }
            for (int t = 0; t < Solitaire.Piles; t++)
            {
                DrawPile(PileId.Tableau(t), now);
            }

            // Where the held cards would land.
            if (_dragging && _dropTarget.HasValue)
            {
                PileId target = _dropTarget.Value;
                int count = _game.Pile(target).Count;
                Vector2 at = target.Kind == PileKind.Tableau && count == 0
                    ? new Vector2(Column(target.Index), TableauTop)
                    : CardPos(target, Math.Max(0, count - 1));
                DrawTexture(_art.Glow, at);
            }

            // Gliding cards over the rest, in the order they set off.
            foreach (KeyValuePair<int, Slide> slide in _slides.Where(s => now < s.Value.Start + SlideTime).OrderBy(s => s.Value.Start))
            {
                if (!IsDragged(slide.Key))
                {
                    DrawCard(slide.Key, VisualPos(slide.Key, now), VisualFaceUp(slide.Key, now));
                }
            }

            // Held cards on top of everything.
            if (_dragging)
            {
                foreach (int card in DraggedCards())
                {
                    DrawCard(card, VisualPos(card, now), true);
                }
            }

            if (_showWin)
            {
                DrawConfetti(now);
            }
        }

        private void DrawPile(PileId pile, float now)
        {
            List<int> cards = _game.Pile(pile);
            for (int i = 0; i < cards.Count; i++)
            {
                int card = cards[i];
                if (!IsDragged(card) && !IsSliding(card, now))
                {
                    DrawCard(card, CardPos(pile, i), _game.IsFaceUp(pile, i));
                }
            }
        }

        private void DrawCard(int card, Vector2 pos, bool faceUp)
        {
            DrawTexture(faceUp ? _art.Faces[card] : _art.Back, pos);
        }

        private void DrawTexture(Texture2D texture, Vector2 pos)
        {
            var rect = new Rect(_origin.x + Mathf.Round(pos.x), _origin.y + Mathf.Round(pos.y), CardW * _scale, CardH * _scale);
            GUI.DrawTexture(rect, texture);
        }

        // Presses, clicks and drags on the table.
        private void HandleBoard(float now)
        {
            Event e = Event.current;
            Vector2 at = e.mousePosition - _origin;
            switch (e.GetTypeForControl(_boardControl))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !_table.Contains(e.mousePosition) || !HitTest(at, out PileId pile, out int index))
                    {
                        break;
                    }
                    // The second click of a double-click on the same pile would move the card that the
                    // first click uncovered. Quick clicks on the deck all count.
                    if (e.clickCount >= 2 && pile == _lastClickPile && pile != PileId.Stock)
                    {
                        e.Use();
                        break;
                    }
                    if (pile == PileId.Stock || _game.CanPickUp(pile, index))
                    {
                        _pressed = true;
                        _dragging = false;
                        _pressPile = pile;
                        _pressIndex = index;
                        _pressMouse = e.mousePosition;
                        if (index >= 0)
                        {
                            _grab = at - VisualPos(_game.Pile(pile)[index], now);
                        }
                        GUIUtility.hotControl = _boardControl;
                    }
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != _boardControl || !_pressed)
                    {
                        break;
                    }
                    if (!_dragging && _pressPile != PileId.Stock && (e.mousePosition - _pressMouse).magnitude > Units(DragUnits))
                    {
                        _dragging = true;
                    }
                    if (_dragging)
                    {
                        _dragAt = at - _grab;
                        _dropTarget = FindDrop();
                    }
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != _boardControl)
                    {
                        break;
                    }
                    GUIUtility.hotControl = 0;
                    if (_dragging)
                    {
                        Drop(now);
                    }
                    else if (_pressed)
                    {
                        _pressed = false;
                        _lastClickPile = _pressPile;
                        ClickCard(_pressPile, _pressIndex);
                    }
                    _pressed = false;
                    _dragging = false;
                    _dropTarget = null;
                    e.Use();
                    break;
            }
        }

        private void DrawStatusRow()
        {
            float y = _table.yMax + _gap;
            float right = _panel.xMax - _pad;
            float now = Time.unscaledTime;
            bool won = _game.Won;

            string newGame = now <= _confirmUntil ? "Sure? Click again" : "New game";
            right = StatusButton(right, y, newGame, true, ClickNewGame);
            right = StatusButton(right, y, "Undo", _game.CanUndo && !_autoFinishing && !won, () => Change(() => _game.Undo()));
            right = StatusButton(right, y, S.DrawThree.Value ? "Draw 3" : "Draw 1", true, () =>
            {
                S.DrawThree.Value = !S.DrawThree.Value;
                _game.DrawThree = S.DrawThree.Value;
                MarkDirty();
            });
            if (CanOfferAutoFinish)
            {
                right = StatusButton(right, y, "Auto-finish", true, StartAutoFinish);
            }

            GUI.Label(new Rect(_panel.x + _pad, y, Mathf.Max(0f, right - _panel.x - _pad), _status),
                $"Moves {_game.Moves}      {TimeText(_game.Seconds)}", _statusStyle);
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

        // Returns where the next tab goes.
        private float TabButton(float x, float y, string label, TableGame game)
        {
            float width = Mathf.Max(Units(40f), _buttonStyle.CalcSize(new GUIContent(label)).x + Units(16f));
            var rect = new Rect(x, y + Units(2f), width, _header - Units(4f));
            if (GUI.Button(rect, label, S.Game.Value == game ? _tabOnStyle : _buttonStyle))
            {
                _clicks.Add(() => SwitchTo(game));
            }
            return rect.xMax + Units(ButtonGapUnits);
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

        // Little suits falling over the table after a win.
        private void DrawConfetti(float now)
        {
            float t = now - _wonAt;
            if (t > ConfettiTime)
            {
                return;
            }
            GUI.BeginClip(_table);
            float size = 7 * _scale;
            foreach (Confetto c in _confetti)
            {
                float y = (c.Y + c.Speed * t) * _table.height;
                if (y > _table.height || y < -size)
                {
                    continue;
                }
                float x = c.X * (_table.width - size) + Mathf.Sin(c.Phase + t * 2.2f) * c.Sway * _scale;
                GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), size, size), _art.Suits[c.Suit]);
            }
            GUI.EndClip();
        }

        // A box in the middle of the table, over a dark shade.
        private Rect CentreBox(float width, float height, bool shade)
        {
            if (shade)
            {
                Color was = GUI.color;
                GUI.color = Shade;
                GUI.DrawTexture(_table, _art.Pixel);
                GUI.color = was;
            }
            var box = new Rect(
                Mathf.Round(_table.center.x - width / 2f), Mathf.Round(_table.center.y - height / 2f), width, height);
            GUI.Box(box, GUIContent.none, _boxStyle);
            GUI.Box(box, GUIContent.none, _boxStyle);
            return box;
        }

        private void DrawWin()
        {
            SolitaireStats stats = _store.Stats;
            Rect box = CentreBox(Units(340f), Units(196f), false);
            float x = box.x + Units(16f);
            float w = box.width - Units(32f);
            float y = box.y + Units(14f);
            GUI.Label(new Rect(x, y, w, Units(34f)), "<b>You won!</b>", _bigStyle);
            y += Units(40f);
            GUI.Label(new Rect(x, y, w, Units(22f)), $"{_game.Moves} moves in {TimeText(_game.Seconds)}", _centreStyle);
            y += Units(24f);
            GUI.Label(new Rect(x, y, w, Units(22f)),
                $"Best time {TimeText(stats.BestSeconds)}     Fewest moves {stats.FewestMoves}", _centreStyle);
            y += Units(24f);
            GUI.Label(new Rect(x, y, w, Units(22f)),
                $"Won {stats.Won} of {stats.Played}     Streak {stats.Streak}", _centreStyle);
            y += Units(32f);
            float buttonWidth = Units(140f);
            if (GUI.Button(new Rect(Mathf.Round(box.center.x - buttonWidth / 2f), y, buttonWidth, Units(28f)), "New game", _buttonStyle))
            {
                _clicks.Add(NewGame);
            }
        }

        private const string HelpText =
            "<b>How to play</b>\n" +
            "Build the four piles at the top right up from Ace to King, one suit each.\n" +
            "In the seven piles below, stack cards downwards (King, Queen, Jack, 10 ... 2), red on black or " +
            "black on red. You can move a run of cards together. Only a King, or a run that starts with one, " +
            "goes on an empty pile.\n" +
            "Click the deck at the top left to turn cards over. When it runs out, click its spot to turn the " +
            "pile back over.\n\n" +
            "<b>Controls</b>\n" +
            "Drag a card (and the cards on it) onto a pile, or click it to send it where it fits: up top first, " +
            "if it can go there.\n" +
            "Undo takes back moves. Draw 1 / Draw 3 picks how many cards the deck turns over.\n" +
            "Auto-finish shows up once no card is face down.\n" +
            "Your game keeps when you close the window or the game.\n\n" +
            "Click to close.";

        private void DrawHelp()
        {
            // A click anywhere on the table closes the card. First, so it gets the click before the labels.
            if (GUI.Button(_table, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => _help = false);
            }
            string text = FishTab ? GoFishBoard.HelpText : HelpText;
            float width = Mathf.Min(_table.width - Units(24f), Units(520f));
            float height = _helpStyle.CalcHeight(new GUIContent(text), width - Units(32f)) + Units(24f);
            Rect box = CentreBox(width, Mathf.Min(_table.height - Units(24f), height), true);
            GUI.Label(new Rect(box.x + Units(16f), box.y + Units(12f), box.width - Units(32f), box.height - Units(24f)), text, _helpStyle);
        }

        private void DrawStats()
        {
            if (GUI.Button(_table, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => _statsBox = false);
            }
            if (FishTab)
            {
                Rect fishBox = CentreBox(Units(300f), Units(210f), true);
                GUI.Label(new Rect(fishBox.x + Units(16f), fishBox.y + Units(12f), fishBox.width - Units(32f), fishBox.height - Units(24f)),
                    _fish.StatsText(), _helpStyle);
                return;
            }
            SolitaireStats stats = _store.Stats;
            int rate = stats.Played > 0 ? Mathf.RoundToInt(100f * stats.Won / stats.Played) : 0;
            string text =
                "<b>Solitaire stats</b>\n\n" +
                $"Played {stats.Played}\n" +
                $"Won {stats.Won} ({rate}%)\n" +
                $"Streak {stats.Streak} (best {stats.BestStreak})\n" +
                $"Best time {(stats.BestSeconds > 0 ? TimeText(stats.BestSeconds) : "none yet")}\n" +
                $"Fewest moves {(stats.FewestMoves > 0 ? stats.FewestMoves.ToString() : "none yet")}\n\n" +
                "Click to close.";
            Rect box = CentreBox(Units(300f), Units(240f), true);
            GUI.Label(new Rect(box.x + Units(16f), box.y + Units(12f), box.width - Units(32f), box.height - Units(24f)), text, _helpStyle);
        }

        private static string TimeText(double seconds)
        {
            int total = (int)Math.Floor(seconds);
            int hours = total / 3600;
            int minutes = total / 60 % 60;
            int secs = total % 60;
            return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes}:{secs:00}";
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
                wordWrap = false,
                clipping = TextClipping.Clip,
                richText = true,   // player names (see RichName)
            };
            _hintStyle.normal.textColor = Dim;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Font(14f) };
            // The game you're looking at: pressed in.
            _tabOnStyle = new GUIStyle(_buttonStyle);
            _tabOnStyle.normal = GUI.skin.button.onNormal;
            _tabOnStyle.hover = GUI.skin.button.onHover;
            _tabOnStyle.active = GUI.skin.button.onActive;
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(14f),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _statusStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            _bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(26f),
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
            _bigStyle.normal.textColor = Gold;
            _centreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(15f),
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
            };
            _centreStyle.normal.textColor = Color.white;
            _helpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Font(14f),
                richText = true,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            _helpStyle.normal.textColor = Color.white;
        }

        // ---- The game's state ----

        // The chat box. Only looked up while it matters: on the main menu MonoSingleton.I searches the scene.
        private static bool IsTyping()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }
    }
}
