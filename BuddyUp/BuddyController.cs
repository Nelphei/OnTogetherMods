using System;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;

namespace BuddyUp
{
    // Ties it together: the icon, the window, the messages at the top of the screen, and when things
    // happen. Buddies.cs keeps track of who's whose buddy, Riding.cs puts riders on heads, and
    // Following.cs walks you after your buddy.
    //
    // Runs early in each frame's LateUpdate (after every Update and the animations, before the camera
    // follows you), so riders sit on the head where it is this frame.
    [DefaultExecutionOrder(-500)]
    internal sealed class BuddyController : MonoBehaviour
    {
        private const string PanelId = "BuddyUp";
        private const float Width = 420f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 28f;
        private const float Gap = 4f;
        private const float SectionGap = 8f;
        private const int MaxListed = 8;
        // Below Photo Mode's (y = 40), Emote Sync's (90), Desk Snap's (140) and Desk Swarm's (190) messages.
        private const float NoticeY = 240f;
        // Ride clicked while something's briefly in the way (stairs, a step off the ground, a menu):
        // keep trying this long.
        private const float RideWaitSeconds = 3f;

        public static BuddyController Instance { get; private set; }

        // True while the mouse is over the window, so clicks don't reach the world (Patches.cs).
        public static bool IsMouseOverWindow { get; private set; }

        private static Settings S => Plugin.Settings;

        private static readonly Color White = Color.white;
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);

        private readonly Buddies _buddies = new Buddies();
        private readonly SitTracker _tracker = new SitTracker();
        private Riding _riding;
        private Following _following;
        private PlayerFocusController _me;
        private PlayerMovementController _move;
        private bool _wasSitting;
        private bool _gluePaused;
        private ulong _pendingRide;
        private float _pendingRideUntil;

        private bool _open;
        private IconBar.Click _iconClick;
        // Clicks in the window, acted on in Update (outside the GUI pass).
        private readonly List<Action> _clicks = new List<Action>();

        // Names in the window, refreshed twice a second rather than on every GUI pass.
        private float _namesAt = -100f;
        private readonly List<KeyValuePair<ulong, string>> _requestRows = new List<KeyValuePair<ulong, string>>();
        private readonly List<KeyValuePair<ulong, string>> _buddyRows = new List<KeyValuePair<ulong, string>>();
        private readonly List<KeyValuePair<ulong, string>> _otherRows = new List<KeyValuePair<ulong, string>>();
        private string _onMyHead;
        private string _withMe;
        private string _ridingName;

        private readonly WindowCanvas _canvas = new WindowCanvas("BuddyUp Window");
        private readonly TextBox _noticeBox = new TextBox("BuddyUp Notice", TextAlignmentOptions.Center);
        private ClickBlocker _blocker;
        private Rect _screenRect;
        private int _drawnFrame = -100;
        private string _notice;
        private float _noticeUntil;

        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _centredStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pickedStyle;
        private GUIStyle _toggleStyle;

        private void Awake()
        {
            Instance = this;
            _riding = new Riding(_tracker);
            _following = new Following(_tracker);
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "BuddyUp ClickBlocker");

            _buddies.Notice += Notice;
            _buddies.Asked += OnAsked;
            _buddies.Ended += OnEnded;
            _buddies.ShakenOff += OnShakenOff;
            _riding.Notice += Notice;
            _following.Notice += Notice;
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            if (Instance == this)
            {
                Instance = null;
                IsMouseOverWindow = false;
            }
            _blocker?.Destroy();
            _canvas.Destroy();
            _noticeBox.Destroy();
        }

        // You picked a focus activity (also a switch, and when another mod sat you down).
        public void OnSatDown(PlayerFocusController me)
        {
            _following.NoteSatDown();
            if (_buddies.List.Count > 0 && _riding.Carrier == 0)
            {
                _buddies.SendSit(me);
            }
        }

        private void Update()
        {
            try
            {
                _me = Game.Me;
                _move = Game.MyMovement;

                IconBar.Click icon = _iconClick;
                _iconClick = IconBar.Click.None;
                List<Action> clicks = _clicks.ToList();
                _clicks.Clear();

                if (icon == IconBar.Click.Left ||
                    (S.WindowKey.Value != KeyCode.None && Input.GetKeyDown(S.WindowKey.Value) && !Game.IsTyping()))
                {
                    _open = !_open;
                }
                if (icon == IconBar.Click.Right)
                {
                    QuickAction();
                }
                foreach (Action click in clicks)
                {
                    click();
                }

                _buddies.Update();

                // One leader per group: someone newer goes along with you, so you stop going along with them.
                ulong newer = _buddies.GiveWayTo();
                if (newer != 0)
                {
                    GiveWay(newer);
                }
                TryPendingRide();

                // Stood up: anyone walking with you stops trying to sit down beside you.
                bool sitting = _me != null && _me.IsFocus;
                if (_wasSitting && !sitting)
                {
                    _buddies.SendUp();
                }
                _wasSitting = sitting;
                _tracker.Update(_me);

                if (_riding.Carrier != 0 && Game.DeskSwarmPiloting())
                {
                    _riding.Stop(_me, _buddies, Dismount.Stay);
                    Notice("Desk Swarm took off, so you got off");
                }
                _following.Update(_me, _move, _buddies, _riding);
                RefreshNames();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            IsMouseOverWindow = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        private void LateUpdate()
        {
            Glue(late: true);
            _canvas.LateUpdate();
            _noticeBox.LateUpdate();
        }

        // Just before drawing: puts riders back on heads in case the network moved anyone after LateUpdate.
        private void OnBeforeRender()
        {
            Glue(late: false);
        }

        private void Glue(bool late)
        {
            if (_gluePaused || !Game.InWorld)
            {
                return;
            }
            try
            {
                _riding.Glue(_me, _buddies, late);
            }
            catch (Exception e)
            {
                // Don't fill the log every frame: stop until the next ride changes something.
                _gluePaused = true;
                Plugin.Log.LogError($"Riding stopped working: {e}");
            }
        }

        // ---- For Bubble Shell's commands (ShellCommands) ----

        internal Buddies BuddyList => _buddies;
        internal ulong RidingOn => _riding.Carrier;
        internal ulong WalkingWith => _following.Target;
        internal void CommandRide(ulong id) => Ride(id);
        internal void CommandWalk(ulong id) => Walk(id);
        internal void CommandGetOff() => GetOff();
        internal void CommandStopWalking() => StopWalking();
        internal void CommandShakeOff() => ShakeOff();

        // ---- Actions ----

        // Right-click on the icon: hop off, or stop walking, or open the window.
        private void QuickAction()
        {
            if (_riding.Carrier != 0)
            {
                GetOff();
            }
            else if (_following.Target != 0)
            {
                StopWalking();
            }
            else
            {
                _open = true;
            }
        }

        // Climbs on (straight from walking with someone, too), or gets off if you're on their head.
        private void Ride(ulong id)
        {
            _pendingRide = 0;
            if (_riding.Carrier == id)
            {
                GetOff();
                return;
            }
            _gluePaused = false;
            string why = _riding.Start(_me, id, _buddies, out bool wait);
            if (why == null)
            {
                OnRiding(id);
            }
            else if (wait)
            {
                _pendingRide = id;
                _pendingRideUntil = Time.unscaledTime + RideWaitSeconds;
            }
            else
            {
                Notice(why);
            }
        }

        private void TryPendingRide()
        {
            ulong id = _pendingRide;
            if (id == 0)
            {
                return;
            }
            string why = _riding.Start(_me, id, _buddies, out bool wait);
            if (why == null)
            {
                _pendingRide = 0;
                OnRiding(id);
            }
            else if (!wait || Time.unscaledTime > _pendingRideUntil)
            {
                _pendingRide = 0;
                Notice(why);
            }
        }

        private void OnRiding(ulong id)
        {
            _following.Stop(_buddies);
            Notice($"On {Game.PlayerName(id)}'s head! {Game.StandUpKey()} hops off.");
        }

        // Back on your feet beside your buddy, or still sitting if you were sitting when you climbed on.
        private void GetOff()
        {
            ulong carrier = _riding.Carrier;
            _riding.Stop(_me, _buddies, Dismount.Beside);
            if (carrier != 0)
            {
                Notice($"Got off {Game.PlayerName(carrier)}'s head");
            }
        }

        // Starts walking with them (hopping off a head first, onto your feet), or stops.
        private void Walk(ulong id)
        {
            _pendingRide = 0;
            if (_following.Target == id)
            {
                StopWalking();
                return;
            }
            string name = Game.PlayerName(id);
            bool wasRiding = _riding.Carrier != 0;
            if (wasRiding)
            {
                _riding.Stop(_me, _buddies, Dismount.Stand);
            }
            _following.Start(id, _buddies);
            Notice(wasRiding ? $"Hopped off to walk with {name}"
                : _me != null && _me.IsFocus ? $"Walking with {name} once you stand up"
                : $"Walking with {name}");
        }

        // Someone started riding on or walking with you while you were going along with them (maybe
        // through others). Theirs is newer, so you stop yours: one leader per group.
        private void GiveWay(ulong to)
        {
            string name = Game.PlayerName(to);
            string what = _buddies.Carrying(to) == _buddies.Me ? $"{name} climbed onto your head" : $"{name} is walking with you";
            if (_riding.Carrier != 0)
            {
                _riding.Stop(_me, _buddies, Dismount.Beside);
                Notice($"{what}, so you got off");
            }
            else if (_following.Target != 0)
            {
                _following.Stop(_buddies);
                Notice($"{what}, so you stopped following");
            }
        }

        private void StopWalking()
        {
            ulong target = _following.Target;
            _following.Stop(_buddies);
            if (target != 0)
            {
                Notice($"Stopped walking with {Game.PlayerName(target)}");
            }
        }

        private void ShakeOff()
        {
            List<ulong> riders = _buddies.RidersOf(_buddies.Me).ToList();
            foreach (ulong rider in riders)
            {
                _buddies.SendShake(rider);
            }
            if (riders.Count > 0)
            {
                Notice("Shook them off");
            }
        }

        private void ChangeHeight(float by)
        {
            S.RideHeight.Value = Mathf.Clamp(Mathf.Round((S.RideHeight.Value + by) * 20f) / 20f, 0f, 1.5f);
            _riding.HeightChanged(_buddies);
        }

        private void OnAsked(ulong id)
        {
            Notice($"{Game.PlayerName(id)} wants to be your buddy");
            _open = true;
        }

        private void OnEnded(ulong id)
        {
            if (_pendingRide == id)
            {
                _pendingRide = 0;
            }
            if (_riding.Carrier == id)
            {
                _riding.Stop(_me, _buddies, Dismount.Beside);
            }
            if (_following.Target == id)
            {
                _following.Stop(_buddies);
            }
        }

        private void OnShakenOff(ulong from)
        {
            if (_riding.Carrier == from)
            {
                _riding.Stop(_me, _buddies, Dismount.Beside);
                Notice($"{Game.PlayerName(from)} shook you off!");
            }
        }

        private void Notice(string text)
        {
            if (!S.ShowNotices.Value)
            {
                return;
            }
            _notice = text;
            _noticeUntil = Time.unscaledTime + 4f;
        }

        private void RefreshNames()
        {
            if (Time.unscaledTime - _namesAt < 0.5f)
            {
                return;
            }
            _namesAt = Time.unscaledTime;

            Fill(_requestRows, _buddies.Requests);
            Fill(_buddyRows, _buddies.List);
            Fill(_otherRows, _buddies.Others.Where(id => !_buddies.Requests.Contains(id)));

            List<string> riders = _buddies.RidersOf(_buddies.Me).Select(Game.PlayerName).ToList();
            _onMyHead = riders.Count > 0 ? "On your head: " + string.Join(", ", riders) : null;
            List<string> walkers = _buddies.WalkersWith(_buddies.Me).Select(Game.PlayerName).ToList();
            _withMe = walkers.Count > 0 ? "Walking with you: " + string.Join(", ", walkers) : null;
            _ridingName = _riding.Carrier != 0 ? Game.PlayerName(_riding.Carrier) : null;
        }

        private static void Fill(List<KeyValuePair<ulong, string>> rows, IEnumerable<ulong> ids)
        {
            rows.Clear();
            foreach (ulong id in ids)
            {
                rows.Add(new KeyValuePair<ulong, string>(id, Game.PlayerName(id)));
            }
            rows.Sort((a, b) => string.Compare(PlayerNames.WithoutTags(a.Value), PlayerNames.WithoutTags(b.Value),
                StringComparison.OrdinalIgnoreCase));
            if (rows.Count > MaxListed)
            {
                rows.RemoveRange(MaxListed, rows.Count - MaxListed);
            }
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons.
            if (S.ShowIcon.Value)
            {
                bool busy = _riding.Carrier != 0 || _following.Target != 0;
                IconBar.State state = _open ? IconBar.State.Open : busy ? IconBar.State.On : IconBar.State.Plain;
                string hint = _riding.Carrier != 0 ? "Right-click: get off"
                    : _following.Target != 0 ? "Right-click: stop walking together"
                    : null;
                IconBar.Click click = IconBar.Draw("BuddyUp", IconBar.BuddyUpOrder, Icon.Pixels, state, "Buddy Up",
                    S.WindowKey.Value, hint);
                if (click != IconBar.Click.None)
                {
                    _iconClick = click;   // acted on in Update
                }
            }

            // Hidden while Photo Mode takes a photo, and while the game's eye button hides the UI.
            if (PanelStack.Hidden)
            {
                return;
            }
            if (_titleStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out float width);
            try
            {
                if (_open && Game.InWorld)
                {
                    DrawWindow();
                }
                if (_notice != null && Time.unscaledTime < _noticeUntil)
                {
                    Vector2 size = _noticeBox.Measure(_notice);
                    _noticeBox.Draw(new Rect((width - size.x) / 2f, NoticeY, size.x, size.y), _notice);
                }
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                PanelStack.End(previous);
            }
        }

        private void DrawWindow()
        {
            // Each section is a list of rows; a gap goes between sections.
            var sections = new List<List<Action<Rect>>>();
            if (!_buddies.InLobby)
            {
                sections.Add(new List<Action<Rect>> { r => GUI.Label(r, "Buddies show up once you're in a session", _dimStyle) });
            }
            else
            {
                AddSection(sections, RequestRows());
                AddSection(sections, BuddyRows());
                AddSection(sections, AboutMeRows());
                AddSection(sections, OtherRows());
                AddSection(sections, OptionRows());
            }

            int rowCount = sections.Sum(s => s.Count);
            float height = Pad + HeaderHeight + Gap + rowCount * (RowHeight + Gap) - Gap +
                           (sections.Count - 1) * SectionGap + Pad;
            Rect panel = PanelStack.Place(PanelId, PanelStack.BuddyUpOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            _canvas.Back(panel);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>BUDDY UP</b>", _titleStyle);
            if (S.WindowKey.Value != KeyCode.None)
            {
                GUI.Label(new Rect(x, y, w, HeaderHeight), $"{S.WindowKey.Value} to close", _keyStyle);
            }
            y += HeaderHeight + Gap;

            for (int s = 0; s < sections.Count; s++)
            {
                if (s > 0)
                {
                    y += SectionGap;
                }
                foreach (Action<Rect> row in sections[s])
                {
                    row(new Rect(x, y, w, RowHeight));
                    y += RowHeight + Gap;
                }
            }
        }

        private static void AddSection(List<List<Action<Rect>>> sections, List<Action<Rect>> rows)
        {
            if (rows.Count > 0)
            {
                sections.Add(rows);
            }
        }

        // People asking to be your buddy: Yes / No.
        private List<Action<Rect>> RequestRows()
        {
            var rows = new List<Action<Rect>>();
            foreach (KeyValuePair<ulong, string> request in _requestRows)
            {
                rows.Add(r =>
                {
                    const float button = 52f;
                    _canvas.Name(new Rect(r.x, r.y, r.width - 2 * button - 2 * Gap - 4f, r.height),
                        $"{request.Value} <alpha=#A6>wants to be your buddy", White);
                    if (GUI.Button(new Rect(r.xMax - 2 * button - Gap, r.y, button, r.height), "Yes", _buttonStyle))
                    {
                        _clicks.Add(() => _buddies.Answer(request.Key, true));
                    }
                    if (GUI.Button(new Rect(r.xMax - button, r.y, button, r.height), "No", _buttonStyle))
                    {
                        _clicks.Add(() => _buddies.Answer(request.Key, false));
                    }
                });
            }
            return rows;
        }

        // Your buddies, each with Ride, Walk and end buttons, then what you're doing with them.
        private List<Action<Rect>> BuddyRows()
        {
            var rows = new List<Action<Rect>>();
            foreach (KeyValuePair<ulong, string> buddy in _buddyRows)
            {
                ulong id = buddy.Key;
                bool riding = _riding.Carrier == id;
                bool climbing = _pendingRide == id;
                bool walking = _following.Target == id;
                rows.Add(r =>
                {
                    const float button = 72f;
                    const float small = 28f;
                    float buttons = 2 * button + small + 2 * Gap;
                    _canvas.Name(new Rect(r.x, r.y, r.width - buttons - 6f, r.height), buddy.Value, White);
                    float bx = r.xMax - buttons;
                    string rideText = riding ? "Get off" : climbing ? "Ride..." : "Ride";
                    if (GUI.Button(new Rect(bx, r.y, button, r.height), rideText, riding ? _pickedStyle : _buttonStyle))
                    {
                        _clicks.Add(() => Ride(id));
                    }
                    bx += button + Gap;
                    if (GUI.Button(new Rect(bx, r.y, button, r.height), walking ? "Stop" : "Walk", walking ? _pickedStyle : _buttonStyle))
                    {
                        _clicks.Add(() => Walk(id));
                    }
                    bx += button + Gap;
                    if (GUI.Button(new Rect(bx, r.y, small, r.height), new GUIContent("x", "Stop being buddies"), _buttonStyle))
                    {
                        _clicks.Add(() => _buddies.End(id));
                    }
                });
            }

            if (_riding.Carrier != 0)
            {
                string carrier = _ridingName ?? "your buddy";
                string text = _riding.CarrierMissing ? $"Looking for {carrier}..." : $"On {carrier}'s head. {Game.StandUpKey()} hops off.";
                rows.Add(r => _canvas.Name(r, text, Dim));
                rows.Add(HeightRow);
            }
            else if (_following.Status != null)
            {
                string status = _following.Status;
                rows.Add(r => _canvas.Name(r, status, Dim));
            }
            return rows;
        }

        private void HeightRow(Rect r)
        {
            const float label = 120f;
            const float small = 32f;
            GUI.Label(new Rect(r.x, r.y, label, r.height), "Riding height", _labelStyle);
            if (GUI.Button(new Rect(r.x + label, r.y, small, r.height), "-", _buttonStyle))
            {
                _clicks.Add(() => ChangeHeight(-0.05f));
            }
            GUI.Label(new Rect(r.x + label + small, r.y, 64f, r.height), $"{S.RideHeight.Value:0.00} m", _centredStyle);
            if (GUI.Button(new Rect(r.x + label + small + 64f, r.y, small, r.height), "+", _buttonStyle))
            {
                _clicks.Add(() => ChangeHeight(0.05f));
            }
        }

        // Who's on your head (with a Shake off button) and who's walking with you.
        private List<Action<Rect>> AboutMeRows()
        {
            var rows = new List<Action<Rect>>();
            string onHead = _onMyHead;
            if (onHead != null)
            {
                rows.Add(r =>
                {
                    const float button = 96f;
                    _canvas.Name(new Rect(r.x, r.y, r.width - button - 8f, r.height), onHead, White);
                    if (GUI.Button(new Rect(r.xMax - button, r.y, button, r.height), "Shake off", _buttonStyle))
                    {
                        _clicks.Add(ShakeOff);
                    }
                });
            }
            string withMe = _withMe;
            if (withMe != null)
            {
                rows.Add(r => _canvas.Name(r, withMe, White));
            }
            return rows;
        }

        // Everyone else here with Buddy Up: ask them.
        private List<Action<Rect>> OtherRows()
        {
            var rows = new List<Action<Rect>>();
            if (_otherRows.Count == 0)
            {
                if (_buddyRows.Count == 0 && _requestRows.Count == 0)
                {
                    rows.Add(r => GUI.Label(r, "No one else here has Buddy Up yet", _dimStyle));
                }
                return rows;
            }
            foreach (KeyValuePair<ulong, string> other in _otherRows)
            {
                ulong id = other.Key;
                rows.Add(r =>
                {
                    const float button = 96f;
                    _canvas.Name(new Rect(r.x, r.y, r.width - button - 8f, r.height), other.Value, Dim);
                    bool asked = _buddies.HaveAsked(id);
                    bool was = GUI.enabled;
                    GUI.enabled = !asked;
                    if (GUI.Button(new Rect(r.xMax - button, r.y, button, r.height), asked ? "Asked..." : "Buddy up", _buttonStyle))
                    {
                        _clicks.Add(() => _buddies.Ask(id));
                    }
                    GUI.enabled = was;
                });
            }
            return rows;
        }

        // Walking beside or behind, and sitting down together.
        private List<Action<Rect>> OptionRows()
        {
            var rows = new List<Action<Rect>>();
            if (_buddyRows.Count == 0)
            {
                return rows;
            }
            rows.Add(r =>
            {
                const float label = 50f;
                const float button = 80f;
                GUI.Label(new Rect(r.x, r.y, label, r.height), "Walk", _labelStyle);
                WalkSpot walk = S.Walk.Value;
                if (GUI.Button(new Rect(r.x + label, r.y, button, r.height), "Beside", walk == WalkSpot.Beside ? _pickedStyle : _buttonStyle))
                {
                    _clicks.Add(() => S.Walk.Value = WalkSpot.Beside);
                }
                if (GUI.Button(new Rect(r.x + label + button + Gap, r.y, button, r.height), "Behind", walk == WalkSpot.Behind ? _pickedStyle : _buttonStyle))
                {
                    _clicks.Add(() => S.Walk.Value = WalkSpot.Behind);
                }

                const string text = " Sit down together";
                const float boxHeight = 18f;
                float toggleWidth = _toggleStyle.CalcSize(new GUIContent(text)).x;
                bool on = S.SitTogether.Value;
                bool now = GUI.Toggle(new Rect(r.xMax - toggleWidth, r.y + (r.height - boxHeight) / 2f, toggleWidth, boxHeight),
                    on, text, _toggleStyle);
                if (now != on)
                {
                    _clicks.Add(() => S.SitTogether.Value = now);
                }
            });
            return rows;
        }

        private void MakeStyles()
        {
            _titleStyle = Label(15, White, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, Dim, TextAnchor.MiddleRight);
            _labelStyle = Label(15, White, TextAnchor.MiddleLeft);
            _dimStyle = Label(14, Dim, TextAnchor.MiddleLeft);
            _centredStyle = Label(15, White, TextAnchor.MiddleCenter);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            // What you're doing (riding, walking, the walk spot) looks pressed in.
            _pickedStyle = new GUIStyle(_buttonStyle);
            _pickedStyle.normal = GUI.skin.button.onNormal;
            _pickedStyle.hover = GUI.skin.button.onHover;
            _pickedStyle.active = GUI.skin.button.onActive;

            _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 14, wordWrap = false };
        }

        private static GUIStyle Label(int size, Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = alignment,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
