using System;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;

namespace DeskSwarm
{
    // Ties it together: the keys, the icon, the window and the messages at the top of the screen.
    // Swarms.cs keeps track of who's in which swarm, and Flight.cs moves your desk.
    internal sealed class SwarmController : MonoBehaviour
    {
        private const string PanelId = "DeskSwarm";
        private const float Width = 360f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 28f;
        private const float Gap = 4f;
        private const float SectionGap = 8f;
        private const int MaxListed = 6;

        public static SwarmController Instance { get; private set; }

        // True while the mouse is over the window, so clicks don't reach the world (Patches.cs).
        public static bool IsMouseOverWindow { get; private set; }

        private static Settings S => Plugin.Settings;

        private static readonly Color White = Color.white;
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);

        private readonly Swarms _swarms = new Swarms();
        private readonly Flight _flight = new Flight();
        private PlayerFocusController _me;

        private bool _open;
        private IconBar.Click _iconClick;
        // Clicks in the window, acted on in Update (outside the GUI pass).
        private readonly List<Action> _clicks = new List<Action>();

        // Names in the window, refreshed twice a second rather than on every GUI pass.
        private float _namesAt = -100f;
        private readonly List<KeyValuePair<ulong, string>> _swarmRows = new List<KeyValuePair<ulong, string>>();
        private string _followers;
        private string _leaderName;

        private readonly WindowCanvas _canvas = new WindowCanvas("DeskSwarm Window");
        private readonly TextBox _noticeBox = new TextBox("DeskSwarm Notice", TextAlignmentOptions.Center);
        private ClickBlocker _blocker;
        private Rect _screenRect;
        private int _drawnFrame = -100;
        private string _notice;
        private float _noticeUntil;

        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pickedStyle;

        private void Awake()
        {
            Instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "DeskSwarm ClickBlocker");
            _swarms.Notice += Notice;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsMouseOverWindow = false;
            }
            _blocker?.Destroy();
            _canvas.Destroy();
            _noticeBox.Destroy();
        }

        public void OnFocusPicked()
        {
            _flight.OnFocusPicked();
        }

        private void Update()
        {
            try
            {
                _me = Game.Me;

                IconBar.Click icon = _iconClick;
                _iconClick = IconBar.Click.None;
                List<Action> clicks = _clicks.ToList();
                _clicks.Clear();

                if (icon == IconBar.Click.Left || (Input.GetKeyDown(S.WindowKey.Value) && !Game.IsTyping()))
                {
                    _open = !_open;
                }
                if (icon == IconBar.Click.Right || (S.FlyKey.Value != KeyCode.None && Input.GetKeyDown(S.FlyKey.Value) && !Game.IsTyping()))
                {
                    ToggleFlying();
                }
                foreach (Action click in clicks)
                {
                    click();
                }

                _swarms.Update();
                _flight.Update(_me, _swarms);
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
            _canvas.LateUpdate();
            _noticeBox.LateUpdate();
        }

        private void ToggleFlying()
        {
            if (_flight.Piloting)
            {
                _flight.StopPiloting(_me);
                Notice(Game.CanFly(_me) ? $"Stopped flying. {KeyName(Game.StandUpKey())} stands you up." : "Stopped flying");
                return;
            }
            if (_swarms.Following != 0)
            {
                Notice("Your desk is following a swarm. Leave it to fly your desk yourself.");
                return;
            }
            if (!Game.CanFly(_me))
            {
                Notice("Sit at your desk first (or do any activity on open ground)");
                return;
            }
            _flight.StartPiloting(_me);
            Notice($"Flying! Move keys to fly, {KeyName(S.UpKey.Value)} up, {KeyName(S.DownKey.Value)} down");
        }

        private void RefreshNames()
        {
            if (Time.unscaledTime - _namesAt < 0.5f)
            {
                return;
            }
            _namesAt = Time.unscaledTime;

            _swarmRows.Clear();
            foreach (Swarm swarm in _swarms.Others.OrderBy(s => s.Leader).Take(MaxListed))
            {
                int desks = swarm.Members.Count;
                _swarmRows.Add(new KeyValuePair<ulong, string>(swarm.Leader,
                    $"{Game.PlayerName(swarm.Leader)}'s swarm  <alpha=#A6>{swarm.Formation}, {Count(desks, "desk")}"));
            }

            Swarm mine = _swarms.Mine;
            if (mine != null && mine.Members.Count > 0)
            {
                IEnumerable<string> names = mine.Members.Select(Game.PlayerName)
                    .OrderBy(n => PlayerNames.WithoutTags(n), StringComparer.OrdinalIgnoreCase);
                _followers = "With " + string.Join(", ", names);
            }
            else
            {
                _followers = null;
            }

            _leaderName = _swarms.Following != 0 ? Game.PlayerName(_swarms.Following) : null;
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

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons.
            if (S.ShowIcon.Value)
            {
                bool flying = _flight.Piloting || _flight.InFormation;
                IconBar.State state = _open ? IconBar.State.Open : flying ? IconBar.State.On : IconBar.State.Plain;
                string hint = null;
                if (_flight.Piloting)
                {
                    hint = "Right-click: stop flying";
                }
                else if (Game.CanFly(_me) && _swarms.Following == 0)
                {
                    hint = "Right-click: take off";
                }
                IconBar.Click click = IconBar.Draw("DeskSwarm", IconBar.DeskSwarmOrder, Icon.Pixels, state, "Desk Swarm",
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
                    // Below Photo Mode's (y = 40), Emote Sync's (90) and Desk Snap's (140) messages.
                    Vector2 size = _noticeBox.Measure(_notice);
                    _noticeBox.Draw(new Rect((width - size.x) / 2f, 190f, size.x, size.y), _notice);
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
            var rows = new List<Action<Rect>>();
            AddFlyRows(rows);
            int flyRows = rows.Count;
            AddSwarmRows(rows);

            float height = Pad + HeaderHeight + Gap + rows.Count * (RowHeight + Gap) - Gap + SectionGap + Pad;
            Rect panel = PanelStack.Place(PanelId, PanelStack.DeskSwarmOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            _canvas.Back(panel);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>DESK SWARM</b>", _titleStyle);
            GUI.Label(new Rect(x, y, w, HeaderHeight), $"{S.WindowKey.Value} to close", _keyStyle);
            y += HeaderHeight + Gap;

            for (int i = 0; i < rows.Count; i++)
            {
                if (i == flyRows)
                {
                    y += SectionGap;
                }
                rows[i](new Rect(x, y, w, RowHeight));
                y += RowHeight + Gap;
            }
        }

        // Taking off, or what's stopping you.
        private void AddFlyRows(List<Action<Rect>> rows)
        {
            if (_swarms.Following != 0)
            {
                rows.Add(r => GUI.Label(r, "Your desk flies with the swarm. Leave it to fly yourself.", _dimStyle));
                return;
            }
            bool canFly = Game.CanFly(_me);
            rows.Add(r =>
            {
                const float buttonWidth = 120f;
                bool was = GUI.enabled;
                GUI.enabled = canFly || _flight.Piloting;
                if (GUI.Button(new Rect(r.x, r.y, buttonWidth, r.height), _flight.Piloting ? "Stop flying" : "Take off", _buttonStyle))
                {
                    _clicks.Add(ToggleFlying);
                }
                GUI.enabled = was;

                string help = _flight.Piloting
                    ? $"Move keys, {KeyName(S.UpKey.Value)} up, {KeyName(S.DownKey.Value)} down"
                    : canFly ? "Fly your desk around" : "Sit at your desk first";
                GUI.Label(new Rect(r.x + buttonWidth + 8f, r.y, r.width - buttonWidth - 8f, r.height), help, _dimStyle);
            });
        }

        private void AddSwarmRows(List<Action<Rect>> rows)
        {
            if (!_swarms.InLobby)
            {
                rows.Add(r => GUI.Label(r, "Swarms start once you're in a session", _dimStyle));
                return;
            }
            if (_swarms.Mine != null)
            {
                AddLeaderRows(rows, _swarms.Mine);
            }
            else if (_swarms.Following != 0)
            {
                AddFollowerRows(rows);
            }
            else
            {
                AddJoinRows(rows);
            }
        }

        private void AddJoinRows(List<Action<Rect>> rows)
        {
            rows.Add(r =>
            {
                const float buttonWidth = 140f;
                if (GUI.Button(new Rect(r.x, r.y, buttonWidth, r.height), "Start a swarm", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        _swarms.Start();
                        Notice("Swarm started. Friends with Desk Swarm can join it in their window.");
                    });
                }
                GUI.Label(new Rect(r.x + buttonWidth + 8f, r.y, r.width - buttonWidth - 8f, r.height), "Others fly with you", _dimStyle);
            });

            if (_swarmRows.Count == 0)
            {
                rows.Add(r => GUI.Label(r, "No one else has a swarm here yet", _dimStyle));
                return;
            }
            foreach (KeyValuePair<ulong, string> swarm in _swarmRows)
            {
                rows.Add(r =>
                {
                    const float buttonWidth = 64f;
                    _canvas.Name(new Rect(r.x, r.y, r.width - buttonWidth - 8f, r.height), swarm.Value, White);
                    if (GUI.Button(new Rect(r.xMax - buttonWidth, r.y, buttonWidth, r.height), "Join", _buttonStyle))
                    {
                        _clicks.Add(() =>
                        {
                            if (_flight.Piloting)
                            {
                                _flight.StopPiloting(_me);
                            }
                            _swarms.Join(swarm.Key);
                            Notice(Game.CanFly(_me)
                                ? $"Joined {Game.PlayerName(swarm.Key)}'s swarm"
                                : $"Joined {Game.PlayerName(swarm.Key)}'s swarm. Sit at your desk to fly to your spot.");
                        });
                    }
                });
            }
        }

        private void AddLeaderRows(List<Action<Rect>> rows, Swarm mine)
        {
            rows.Add(r =>
            {
                const float buttonWidth = 96f;
                GUI.Label(new Rect(r.x, r.y, r.width - buttonWidth - 8f, r.height),
                    $"Your swarm: {Count(mine.Members.Count, "desk")} following", _labelStyle);
                if (GUI.Button(new Rect(r.xMax - buttonWidth, r.y, buttonWidth, r.height), "End swarm", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        _swarms.End();
                        Notice("Swarm ended");
                    });
                }
            });

            rows.Add(r =>
            {
                Formation[] all = Formations.All;
                float buttonWidth = (r.width - Gap * (all.Length - 1)) / all.Length;
                for (int i = 0; i < all.Length; i++)
                {
                    Formation formation = all[i];
                    var button = new Rect(r.x + i * (buttonWidth + Gap), r.y, buttonWidth, r.height);
                    if (GUI.Button(button, formation.ToString(), formation == mine.Formation ? _pickedStyle : _buttonStyle))
                    {
                        _clicks.Add(() => _swarms.SetFormation(formation));
                    }
                }
            });

            rows.Add(r =>
            {
                const float small = 32f;
                GUI.Label(new Rect(r.x, r.y, 70f, r.height), "Spread", _labelStyle);
                if (GUI.Button(new Rect(r.x + 70f, r.y, small, r.height), "-", _buttonStyle))
                {
                    _clicks.Add(() => _swarms.SetSpread(mine.Spread - 0.2f));
                }
                GUI.Label(new Rect(r.x + 70f + small, r.y, 56f, r.height), $"{mine.Spread:0.0}x", _centredStyle);
                if (GUI.Button(new Rect(r.x + 70f + small + 56f, r.y, small, r.height), "+", _buttonStyle))
                {
                    _clicks.Add(() => _swarms.SetSpread(mine.Spread + 0.2f));
                }
            });

            string followers = _followers;
            rows.Add(r =>
            {
                if (followers != null)
                {
                    _canvas.Name(r, followers, Dim);
                }
                else
                {
                    GUI.Label(r, "Others click Join in their own window", _dimStyle);
                }
            });
        }

        private void AddFollowerRows(List<Action<Rect>> rows)
        {
            string leader = _leaderName ?? "someone";
            rows.Add(r =>
            {
                const float buttonWidth = 64f;
                _canvas.Name(new Rect(r.x, r.y, r.width - buttonWidth - 8f, r.height), $"Following {leader}'s swarm", White);
                if (GUI.Button(new Rect(r.xMax - buttonWidth, r.y, buttonWidth, r.height), "Leave", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        _swarms.Leave();
                        Notice("Left the swarm. Your desk stays where it is.");
                    });
                }
            });

            Swarm swarm = _swarms.Followed;
            int spot = _swarms.MySpot;
            string status;
            if (swarm == null || spot < 0)
            {
                status = "Waiting for a spot...";
            }
            else if (!Game.CanFly(_me))
            {
                status = "Sit at your desk to fly to your spot";
            }
            else if (_flight.LeaderMissing)
            {
                status = $"Looking for {leader}...";
            }
            else
            {
                status = $"{swarm.Formation}, spot {spot + 1} of {swarm.Members.Count}";
            }
            rows.Add(r => _canvas.Name(r, status, Dim));
        }

        private static string Count(int n, string thing) => n == 1 ? $"1 {thing}" : $"{n} {thing}s";

        // "Shift" rather than "LeftShift".
        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                default:
                    return key.ToString();
            }
        }

        private GUIStyle _centredStyle;

        private void MakeStyles()
        {
            _titleStyle = Label(15, White, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, Dim, TextAnchor.MiddleRight);
            _labelStyle = Label(15, White, TextAnchor.MiddleLeft);
            _dimStyle = Label(14, Dim, TextAnchor.MiddleLeft);
            _centredStyle = Label(15, White, TextAnchor.MiddleCenter);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            // The formation you're flying looks pressed in.
            _pickedStyle = new GUIStyle(_buttonStyle);
            _pickedStyle.normal = GUI.skin.button.onNormal;
            _pickedStyle.hover = GUI.skin.button.onHover;
            _pickedStyle.active = GUI.skin.button.onActive;
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
