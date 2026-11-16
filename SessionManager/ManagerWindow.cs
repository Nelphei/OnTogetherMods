using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OnTogetherMods.Shared;
using PurrNet;
using Steamworks;
using TMPro;
using UnityEngine;

namespace SessionManager
{
    // The Session Manager window and everything it does while you host: the session's settings, the
    // players (kick and ban), join requests, and the ban list. Only the host can use it; for everyone
    // else the icon is greyed out. Session.cs, PlayerLog.cs, JoinRequests.cs and Game.cs do the work.
    internal sealed class ManagerWindow : MonoBehaviour
    {
        private const string PanelId = "SessionManager";
        private const string NameField = "SessionManager.Name";
        private const string NoteField = "SessionManager.Note";

        private const float Width = 480f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float TabHeight = 26f;
        private const float RowHeight = 28f;
        private const float Gap = 4f;
        private const float SectionGap = 8f;
        private const float LabelWidth = 72f;
        private const float SmallButton = 66f;
        private const float TimeWidth = 92f;
        private const int RowsPerPage = 8;

        // Name Designer colours at most 40 letters.
        private const int NameLetterLimit = 40;
        private const int MaxNameChars = 2000;
        private const int MaxNoteChars = 200;

        private const float RefreshInterval = 0.5f;
        private const float BanCheckInterval = 30f;
        private const float ConfirmSeconds = 3f;
        private const float StatusSeconds = 6f;
        private const float NoticeSeconds = 5f;
        private const float NoteSaveDelay = 1f;
        // How long a kicked or banned player's game gets to leave by itself before the host drops them.
        private const float DisconnectAfter = 5f;

        private enum Tab { Session, Players, Requests, Bans }

        private enum BanLength { Hour, Day, Week, Forever }

        private sealed class PendingKick
        {
            public string SteamId;
            public PlayerID Id;
            public float At;
        }

        private static readonly string[] TabNames = { "Session", "Players", "Requests", "Bans" };
        private static readonly string[] BanLengthNames = { "1 hour", "1 day", "1 week", "Forever" };

        private static readonly Color White = Color.white;
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);
        private static readonly Color StatusColour = new Color32(0xFF, 0xD9, 0x66, 0xFF);

        private static ManagerWindow _instance;

        // Read by the patches. True while you type in the window, so the game ignores the keys. A
        // window hidden with the game's eye button (PanelStack.Hidden) gives them back.
        internal static bool HoldsKeys => _instance != null && _instance._open && _instance._typing && !PanelStack.Hidden;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly Session _session = new Session();
        private readonly PlayerLog _log = new PlayerLog();
        private readonly SaveFile _save = new SaveFile();
        private readonly List<JoinRequest> _requests = new List<JoinRequest>();
        private readonly List<PendingKick> _kicks = new List<PendingKick>();

        private CSteamID _lobby = CSteamID.Nil;
        private bool _host;
        private bool _open;
        private Tab _tab;
        private readonly int[] _pages = new int[4];
        private bool _away;
        private BanLength _banLength = BanLength.Day;

        // The name box. It follows the session's name until you change it.
        private string _nameText = "";
        private bool _nameEdited;

        private string _selectedBan;
        private string _noteText = "";
        private bool _noteDirty;
        private float _noteSaveAt;

        private string _confirm;
        private float _confirmUntil;
        private string _status;
        private float _statusUntil;
        private string _notice;
        private float _noticeUntil;

        private bool _typing;
        private bool _clearFocus;
        private bool _iconClicked;
        // Clicks in the window, acted on in Update (outside the GUI pass).
        private readonly List<Action> _clicks = new List<Action>();

        private float _nextRefresh;
        private float _nextBanCheck = 5f;

        private readonly WindowCanvas _canvas = new WindowCanvas("SessionManager Window");
        private readonly TextBox _noticeBox = new TextBox("SessionManager Notice", TextAlignmentOptions.Center);
        private ClickBlocker _blocker;
        private Rect _screenRect;
        private int _drawnFrame = -100;

        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _rightStyle;
        private GUIStyle _centredStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pickedStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _fieldStyle;

        // Made once, since they're handed out every frame.
        private Func<CSteamID, Answer> _rule;
        private Func<string> _readName;
        private Action<string> _writeName;

        private void Awake()
        {
            _instance = this;
            _rule = Rule;
            _readName = () => _nameText;
            _writeName = NameFromDesigner;
            _save.Load();
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "SessionManager ClickBlocker");
            JoinRequests.Answered += OnAutoAnswered;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            JoinRequests.Answered -= OnAutoAnswered;
            JoinRequests.Rule = null;
            _blocker?.Destroy();
            _canvas.Destroy();
            _noticeBox.Destroy();
        }

        // ---- Every frame ----

        private void Update()
        {
            try
            {
                CSteamID lobby = Game.Lobby;
                bool host = lobby != CSteamID.Nil && Game.IsHost;
                if (lobby != _lobby || host != _host)
                {
                    SessionChanged(lobby, host);
                }

                if (Time.unscaledTime >= _nextRefresh)
                {
                    _nextRefresh = Time.unscaledTime + RefreshInterval;
                    _session.Refresh(_host ? _lobby : CSteamID.Nil);
                    RefreshRequests();
                }
                _log.Update(_host ? _lobby : CSteamID.Nil);
                JoinRequests.Rule = _host ? _rule : null;

                bool clicked = _iconClicked;
                _iconClicked = false;
                if (clicked || (S.WindowKey.Value != KeyCode.None && Input.GetKeyDown(S.WindowKey.Value) && Game.InWorld && !Game.IsTyping()))
                {
                    Toggle();
                }

                List<Action> clicks = _clicks.ToList();
                _clicks.Clear();
                foreach (Action click in clicks)
                {
                    click();
                }

                if (!_nameEdited)
                {
                    _nameText = _session.Name;
                }
                if (_noteDirty && Time.unscaledTime >= _noteSaveAt)
                {
                    SaveNote();
                }
                DisconnectStayers();
                if (Time.unscaledTime >= _nextBanCheck)
                {
                    _nextBanCheck = Time.unscaledTime + BanCheckInterval;
                    CheckBans();
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

        private void LateUpdate()
        {
            _canvas.LateUpdate();
            _noticeBox.LateUpdate();
        }

        private void SessionChanged(CSteamID lobby, bool host)
        {
            bool newSession = lobby != _lobby;
            _lobby = lobby;
            _host = host;
            if (newSession)
            {
                _nameEdited = false;
                _away = false;
                _kicks.Clear();
                _selectedBan = null;
                Array.Clear(_pages, 0, _pages.Length);
            }
            _nextRefresh = 0f;
        }

        private void Toggle()
        {
            _open = !_open;
            _confirm = null;
            if (!_open)
            {
                StopTyping();
                if (_noteDirty)
                {
                    SaveNote();
                }
            }
        }

        private void StopTyping()
        {
            _typing = false;
            _clearFocus = true;
        }

        private void RefreshRequests()
        {
            if (_host)
            {
                JoinRequests.Waiting(_requests);
            }
            else
            {
                _requests.Clear();
            }
        }

        // ---- Join requests ----

        // What to do with a request without asking you.
        private Answer Rule(CSteamID id)
        {
            if (_session.Locked || _away)
            {
                return Answer.Reject;
            }
            if (S.LetFriendsIn.Value && Game.IsFriend(id))
            {
                return Answer.Accept;
            }
            return Answer.Wait;
        }

        private void OnAutoAnswered(string name, Answer answer)
        {
            if (answer == Answer.Accept)
            {
                Notice($"Let {name} in (a Steam friend)");
            }
            else
            {
                Notice(_session.Locked ? $"Turned {name} away: the session is locked" : $"Turned {name} away (you're away)");
            }
            RefreshRequests();
        }

        private void AnswerRequest(JoinRequest request, bool accept)
        {
            bool done = accept ? JoinRequests.Accept(request.Id) : JoinRequests.Reject(request.Id);
            if (!done)
            {
                Status($"{request.Name} isn't waiting anymore");
            }
            else
            {
                Status(accept ? $"Let {request.Name} in" : $"Turned {request.Name} down");
            }
            RefreshRequests();
        }

        private void AcceptAll()
        {
            int accepted = 0;
            foreach (JoinRequest request in _requests.ToList())
            {
                if (JoinRequests.Accept(request.Id))
                {
                    accepted++;
                }
            }
            Status(accepted == 1 ? "Let 1 player in" : $"Let {accepted} players in");
            RefreshRequests();
        }

        // ---- Session settings ----

        private void Rename()
        {
            string name = _nameText ?? "";
            if (string.IsNullOrWhiteSpace(PlayerNames.WithoutTags(name)))
            {
                Status("Type a name first");
                return;
            }
            if (System.Text.Encoding.UTF8.GetByteCount(name) > Session.MaxNameBytes)
            {
                Status("That name is too long for Steam");
                return;
            }
            if (!_session.SetName(name))
            {
                Status("Steam didn't take the new name");
                return;
            }
            _nameEdited = false;
            Status("Renamed. Everyone sees the new name.");
        }

        // From Name Designer, while its Session tab is open beside the window.
        private void NameFromDesigner(string text)
        {
            _nameText = text ?? "";
            _nameEdited = true;
        }

        private void ChangeCap(int direction, bool shift)
        {
            int step = shift ? S.SizeStep.Value : 1;
            int players = Mathf.Clamp(_session.Cap + direction * step, 1, Lift.Max);
            if (players != _session.Cap)
            {
                Changed(_session.SetCap(players));
            }
        }

        private void Changed(bool ok)
        {
            if (!ok)
            {
                Status("Steam didn't take that change");
            }
        }

        private void Invite()
        {
            if (!Game.OpenInviteWindow(_session.Lobby))
            {
                Status("Steam's overlay is off, so its invite window can't open");
            }
        }

        private void SaveSetup()
        {
            _save.Setup = new SavedSetup
            {
                Name = _session.Name,
                Players = _session.Cap,
                Listed = _session.Listed,
                AskToJoin = _session.AskToJoin,
                Tags = Session.TagKeys.Where((key, i) => _session.Tags[i]).ToList(),
            };
            Status(_save.Save() ? "Saved. \"Use saved\" puts it all back in a later session." : "Couldn't save. See the BepInEx log.");
        }

        private void UseSetup()
        {
            SavedSetup setup = _save.Setup;
            if (setup == null)
            {
                Status("Nothing saved yet. Click Save setup first.");
                return;
            }
            bool ok = true;
            if (!string.IsNullOrWhiteSpace(PlayerNames.WithoutTags(setup.Name ?? "")))
            {
                ok &= _session.SetName(setup.Name);
            }
            ok &= _session.SetCap(Mathf.Clamp(setup.Players, 1, Lift.Max));
            ok &= _session.SetListed(setup.Listed);
            ok &= _session.SetAskToJoin(setup.AskToJoin);
            for (int i = 0; i < Session.TagKeys.Length; i++)
            {
                ok &= _session.SetTag(i, setup.Tags != null && setup.Tags.Contains(Session.TagKeys[i]));
            }
            _nameEdited = false;
            Status(ok ? "Your saved setup is back" : "Steam didn't take some of it. Try again.");
        }

        // ---- Kicks and bans ----

        private void Kick(LoggedPlayer player)
        {
            if (!Game.IsHere(player.SteamId, out PlayerID id))
            {
                Status($"{player.Name} already left");
                return;
            }
            Game.SendAway(id);
            WatchLeaving(player.SteamId, id);
            Status($"Kicked {player.Name}. They can join again.");
        }

        private void Ban(LoggedPlayer player)
        {
            DateTime now = DateTime.UtcNow;
            DateTime? until;
            switch (_banLength)
            {
                case BanLength.Hour:
                    until = now.AddHours(1);
                    break;
                case BanLength.Day:
                    until = now.AddDays(1);
                    break;
                case BanLength.Week:
                    until = now.AddDays(7);
                    break;
                default:
                    until = null;
                    break;
            }

            Game.AddBan(player.SteamId, player.Nick);
            BanInfo info = _save.Ban(player.SteamId);
            if (info == null)
            {
                info = new BanInfo { SteamId = player.SteamId };
                _save.Bans.Add(info);
            }
            info.Name = player.Nick;
            info.Banned = now;
            info.Until = until;
            _save.Save();

            if (Game.IsHere(player.SteamId, out PlayerID id))
            {
                Game.SendAway(id);
                WatchLeaving(player.SteamId, id);
            }
            string length = until.HasValue ? "for " + BanLengthNames[(int)_banLength] : "for good";
            Status($"Banned {player.Name} {length}");
        }

        private void Unban(string steamId, string name)
        {
            Game.RemoveBan(steamId);
            BanInfo info = _save.Ban(steamId);
            if (info != null)
            {
                _save.Bans.Remove(info);
                _save.Save();
            }
            if (_selectedBan == steamId)
            {
                _selectedBan = null;
            }
            Status($"Unbanned {name}");
        }

        // A kicked player's game leaves by itself. One that doesn't (a modded game can ignore it) is
        // dropped from the session a few seconds later.
        private void WatchLeaving(string steamId, PlayerID id)
        {
            _kicks.RemoveAll(k => k.SteamId == steamId);
            _kicks.Add(new PendingKick { SteamId = steamId, Id = id, At = Time.unscaledTime + DisconnectAfter });
        }

        private void DisconnectStayers()
        {
            for (int i = _kicks.Count - 1; i >= 0; i--)
            {
                PendingKick kick = _kicks[i];
                if (Time.unscaledTime < kick.At)
                {
                    continue;
                }
                _kicks.RemoveAt(i);
                if (_host && Game.IsHere(kick.SteamId, out PlayerID id) && id == kick.Id)
                {
                    Plugin.Log.LogInfo($"{kick.SteamId} didn't leave when told to, so they were disconnected.");
                    Game.Disconnect(id);
                }
            }
        }

        // Ends timed bans that are up. Runs in every scene, since a ban can end while you're on the
        // main menu.
        private void CheckBans()
        {
            BanData bans = Game.BanData;
            if (bans == null || bans.BanServerPlayers == null)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            bool changed = false;
            foreach (BanInfo info in _save.Bans.ToList())
            {
                // Unbanned some other way (the game's player list, another mod).
                if (!bans.BanServerPlayers.Contains(info.SteamId))
                {
                    _save.Bans.Remove(info);
                    changed = true;
                    continue;
                }
                if (info.Until.HasValue && info.Until.Value <= now)
                {
                    Game.RemoveBan(info.SteamId);
                    _save.Bans.Remove(info);
                    changed = true;
                    Plugin.Log.LogInfo($"The ban on {info.SteamId} ended.");
                    Notice($"{Game.Styled(Game.Filter(info.Name))}'s ban has ended");
                }
            }
            if (changed)
            {
                _save.Save();
            }
        }

        private void SelectBan(string steamId)
        {
            if (_noteDirty)
            {
                SaveNote();
            }
            _selectedBan = _selectedBan == steamId ? null : steamId;
            _noteText = _selectedBan != null ? _save.Ban(_selectedBan)?.Note ?? "" : "";
            StopTyping();
        }

        private void SaveNote()
        {
            _noteDirty = false;
            string steamId = _selectedBan;
            if (steamId == null || !Game.IsBanned(steamId))
            {
                return;
            }
            BanInfo info = _save.Ban(steamId);
            if (info == null)
            {
                if (string.IsNullOrWhiteSpace(_noteText))
                {
                    return;
                }
                // A ban made before Session Manager: when it started isn't known.
                BanData bans = Game.BanData;
                int i = bans.BanServerPlayers.IndexOf(steamId);
                info = new BanInfo { SteamId = steamId, Name = Game.BanNick(bans, i) };
                _save.Bans.Add(info);
            }
            info.Note = _noteText.Trim();
            _save.Save();
        }

        // ---- Messages ----

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // A message at the top of the screen, for things done without a click in the window.
        private void Notice(string text)
        {
            if (S.ShowNotices.Value)
            {
                _notice = text;
                _noticeUntil = Time.unscaledTime + NoticeSeconds;
            }
        }

        // Two clicks for anything that sends a player away or lets one back: the first turns the
        // button into "Sure?" for a few seconds.
        private bool Confirmed(string key)
        {
            if (_confirm == key && Time.unscaledTime < _confirmUntil)
            {
                _confirm = null;
                return true;
            }
            _confirm = key;
            _confirmUntil = Time.unscaledTime + ConfirmSeconds;
            return false;
        }

        private string ConfirmText(string key, string text)
        {
            return _confirm == key && Time.unscaledTime < _confirmUntil ? "Sure?" : text;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value)
            {
                IconBar.State state = !_host ? IconBar.State.Off : _open ? IconBar.State.Open : IconBar.State.Plain;
                string hint = !_host ? "Only the session's host can use it"
                    : _requests.Count > 0 ? Count(_requests.Count, "player") + " asking to join"
                    : null;
                if (IconBar.Draw("SessionManager", IconBar.SessionManagerOrder, Icon.For(S.Icon.Value), state,
                        "Session Manager", S.WindowKey.Value, hint) == IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
            }

            if (_clearFocus)
            {
                GUIUtility.keyboardControl = 0;
                _clearFocus = false;
            }
            // Leaving the world mid-typing mustn't keep the keys from the game.
            if (!_open || !Game.InWorld)
            {
                _typing = false;
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
                    // Below the other mods' messages (Photo Mode 40, Emote Sync 90, Desk Snap 140,
                    // Desk Swarm 190).
                    Vector2 size = _noticeBox.Measure(_notice);
                    _noticeBox.Draw(new Rect((width - size.x) / 2f, 240f, size.x, size.y), _notice);
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
            if (!_host)
            {
                rows.Add(r => GUI.Label(r, "Only the session's host can use Session Manager.", _dimStyle));
            }
            else
            {
                switch (_tab)
                {
                    case Tab.Session:
                        AddSessionRows(rows);
                        break;
                    case Tab.Players:
                        AddPlayerRows(rows);
                        break;
                    case Tab.Requests:
                        AddRequestRows(rows);
                        break;
                    default:
                        AddBanRows(rows);
                        break;
                }
            }

            float tabs = _host ? TabHeight + SectionGap : 0f;
            float height = Pad + HeaderHeight + Gap + tabs + rows.Count * (RowHeight + Gap) + SectionGap + RowHeight + Pad;
            Rect panel = PanelStack.Place(PanelId, PanelStack.SessionManagerOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            HandleKeys();
            HandleClickAway(panel);

            _canvas.Back(panel);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>SESSION MANAGER</b>", _titleStyle);
            if (S.WindowKey.Value != KeyCode.None)
            {
                GUI.Label(new Rect(x, y, w, HeaderHeight), $"{S.WindowKey.Value} to close", _keyStyle);
            }
            y += HeaderHeight + Gap;

            if (_host)
            {
                DrawTabs(new Rect(x, y, w, TabHeight));
                y += TabHeight + SectionGap;
            }
            foreach (Action<Rect> row in rows)
            {
                row(new Rect(x, y, w, RowHeight));
                y += RowHeight + Gap;
            }
            y += SectionGap - Gap;
            DrawStatus(new Rect(x, y, w, RowHeight));

            string focused = GUI.GetNameOfFocusedControl();
            _typing = focused == NameField || focused == NoteField;

            // Name Designer, if installed, can write into the name box from beside the window.
            if (_host && _tab == Tab.Session)
            {
                SessionNameLink.ShowBox(panel, _readName, _writeName, NameLetterLimit);
            }
        }

        private void DrawTabs(Rect row)
        {
            float cell = (row.width - Gap * (TabNames.Length - 1)) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (Tab)i;
                string text = TabNames[i];
                int count = tab == Tab.Players ? _log.Here.Count
                    : tab == Tab.Requests ? _requests.Count
                    : tab == Tab.Bans ? BanCount()
                    : 0;
                if (count > 0)
                {
                    text += $" ({count})";
                }
                if (GUI.Button(new Rect(row.x + i * (cell + Gap), row.y, cell, row.height), text,
                        tab == _tab ? _pickedStyle : _buttonStyle) && tab != _tab)
                {
                    _clicks.Add(() =>
                    {
                        _tab = tab;
                        _confirm = null;
                        StopTyping();
                    });
                }
            }
        }

        private void DrawStatus(Rect row)
        {
            if (_status != null && Time.unscaledTime < _statusUntil)
            {
                // Statuses can have player names in them, so they're drawn like names.
                _canvas.Name(row, _status, StatusColour);
                return;
            }
            string tip;
            switch (_host ? _tab : (Tab)(-1))
            {
                case Tab.Session:
                    tip = "Changes reach everyone straight away";
                    break;
                case Tab.Players:
                    tip = "Kick lets them join again. Ban keeps them out.";
                    break;
                case Tab.Requests:
                    tip = "Requests also show in the game's popup";
                    break;
                case Tab.Bans:
                    tip = "Click a name for its details and a note";
                    break;
                default:
                    tip = "The host is whoever started the session";
                    break;
            }
            GUI.Label(row, tip, _dimStyle);
        }

        // ---- Session tab ----

        private void AddSessionRows(List<Action<Rect>> rows)
        {
            rows.Add(NameRow);
            string shown = string.IsNullOrWhiteSpace(PlayerNames.WithoutTags(_nameText ?? ""))
                ? "<alpha=#80>(no name)"
                : PlayerNames.ForText(_nameText);
            rows.Add(r =>
            {
                GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Shows as", _dimStyle);
                _canvas.Name(new Rect(r.x + LabelWidth, r.y, r.width - LabelWidth, r.height), shown, White);
            });
            rows.Add(PlayersRow);
            rows.Add(ListedRow);
            rows.Add(JoiningRow);
            rows.Add(TagsRow);
            rows.Add(SetupRow);
        }

        private void NameRow(Rect r)
        {
            bool designer = SessionNameLink.HasDesigner;
            const float renameWidth = 76f;
            const float designWidth = 70f;
            float buttons = renameWidth + (designer ? designWidth + Gap : 0f);
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Name", _labelStyle);

            var field = new Rect(r.x + LabelWidth, r.y, r.width - LabelWidth - buttons - Gap, r.height);
            GUI.SetNextControlName(NameField);
            string typed = GUI.TextField(field, _nameText ?? "", MaxNameChars, _fieldStyle);
            if (typed != (_nameText ?? ""))
            {
                _nameText = typed;
                _nameEdited = true;
            }

            float x = field.xMax + Gap;
            if (designer)
            {
                if (GUI.Button(new Rect(x, r.y, designWidth, r.height), "Design", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        StopTyping();
                        SessionNameLink.OpenDesigner();
                    });
                }
                x += designWidth + Gap;
            }
            bool was = GUI.enabled;
            GUI.enabled = _nameEdited && _nameText != _session.Name;
            if (GUI.Button(new Rect(x, r.y, renameWidth, r.height), "Rename", _buttonStyle))
            {
                _clicks.Add(() =>
                {
                    StopTyping();
                    Rename();
                });
            }
            GUI.enabled = was;
        }

        private void PlayersRow(Rect r)
        {
            const float arrow = 30f;
            const float number = 46f;
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Players", _labelStyle);
            float x = r.x + LabelWidth;
            if (GUI.Button(new Rect(x, r.y, arrow, r.height), "-", _buttonStyle))
            {
                bool shift = Event.current.shift;
                _clicks.Add(() => ChangeCap(-1, shift));
            }
            GUI.Label(new Rect(x + arrow, r.y, number, r.height), _session.Cap.ToString(), _centredStyle);
            if (GUI.Button(new Rect(x + arrow + number, r.y, arrow, r.height), "+", _buttonStyle))
            {
                bool shift = Event.current.shift;
                _clicks.Add(() => ChangeCap(1, shift));
            }

            string help = _session.Members >= _session.Cap
                ? $"{_session.Members} here: full"
                : $"{_session.Members} here, up to {Lift.Max}";
            if (Lift.Experimental(_session.Cap))
            {
                help += ". Over 16 is untested";
            }
            float helpX = x + 2 * arrow + number + 8f;
            GUI.Label(new Rect(helpX, r.y, r.xMax - helpX, r.height), help, _dimStyle);
        }

        private void ListedRow(Rect r)
        {
            const float button = 80f;
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Listed", _labelStyle);
            float x = r.x + LabelWidth;
            if (GUI.Button(new Rect(x, r.y, button, r.height), "Public", _session.Listed ? _pickedStyle : _buttonStyle) && !_session.Listed)
            {
                _clicks.Add(() => Changed(_session.SetListed(true)));
            }
            if (GUI.Button(new Rect(x + button + Gap, r.y, button, r.height), "Private", _session.Listed ? _buttonStyle : _pickedStyle) && _session.Listed)
            {
                _clicks.Add(() => Changed(_session.SetListed(false)));
            }
            float helpX = x + 2 * button + Gap + 8f;
            GUI.Label(new Rect(helpX, r.y, r.xMax - helpX, r.height),
                _session.Listed ? "In everyone's session list" : "Friends and invites only", _dimStyle);
        }

        private void JoiningRow(Rect r)
        {
            const float boxHeight = 18f;
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Joining", _labelStyle);
            float x = r.x + LabelWidth;
            float boxY = r.y + (r.height - boxHeight) / 2f;

            const string askText = " Ask to join";
            float askWidth = _toggleStyle.CalcSize(new GUIContent(askText)).x;
            bool ask = GUI.Toggle(new Rect(x, boxY, askWidth, boxHeight), _session.AskToJoin, askText, _toggleStyle);
            if (ask != _session.AskToJoin)
            {
                _clicks.Add(() => Changed(_session.SetAskToJoin(ask)));
            }
            x += askWidth + 14f;

            const string lockText = " Locked";
            float lockWidth = _toggleStyle.CalcSize(new GUIContent(lockText)).x;
            bool locked = GUI.Toggle(new Rect(x, boxY, lockWidth, boxHeight), _session.Locked, lockText, _toggleStyle);
            if (locked != _session.Locked)
            {
                _clicks.Add(() => Changed(_session.SetLocked(locked)));
            }
            x += lockWidth + 10f;

            string help = _session.Locked ? "No one new can join"
                : _session.AskToJoin ? "You let each player in"
                : "Anyone can join";
            GUI.Label(new Rect(x, r.y, r.xMax - x, r.height), help, _dimStyle);
        }

        private void TagsRow(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Tags", _labelStyle);
            float x = r.x + LabelWidth;
            int count = Session.TagNames.Length;
            float cell = (r.xMax - x - Gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                int tag = i;
                bool on = _session.Tags[i];
                if (GUI.Button(new Rect(x + i * (cell + Gap), r.y, cell, r.height), Session.TagNames[i], on ? _pickedStyle : _buttonStyle))
                {
                    _clicks.Add(() => Changed(_session.SetTag(tag, !on)));
                }
            }
        }

        private void SetupRow(Rect r)
        {
            float cell = (r.width - 2 * Gap) / 3f;
            if (GUI.Button(new Rect(r.x, r.y, cell, r.height), "Invite friends", _buttonStyle))
            {
                _clicks.Add(Invite);
            }
            if (GUI.Button(new Rect(r.x + cell + Gap, r.y, cell, r.height), "Save setup", _buttonStyle))
            {
                _clicks.Add(SaveSetup);
            }
            bool was = GUI.enabled;
            GUI.enabled = _save.Setup != null;
            if (GUI.Button(new Rect(r.x + 2 * (cell + Gap), r.y, cell, r.height), "Use saved", _buttonStyle))
            {
                _clicks.Add(UseSetup);
            }
            GUI.enabled = was;
        }

        // ---- Players tab ----

        private void AddPlayerRows(List<Action<Rect>> rows)
        {
            rows.Add(BanLengthRow);

            var items = new List<Action<Rect>>();
            DateTime now = DateTime.UtcNow;
            foreach (LoggedPlayer player in _log.Here)
            {
                LoggedPlayer p = player;
                string time = now - p.Joined < TimeSpan.FromMinutes(1) ? "just joined" : Span(now - p.Joined);
                items.Add(r => PlayerRow(r, p, time, true));
            }
            if (_log.Left.Count > 0)
            {
                items.Add(r => GUI.Label(r, "Left this session", _labelStyle));
                foreach (LoggedPlayer player in _log.Left)
                {
                    LoggedPlayer p = player;
                    string time = now - p.Left < TimeSpan.FromMinutes(1) ? "just left" : "left " + Span(now - p.Left) + " ago";
                    items.Add(r => PlayerRow(r, p, time, false));
                }
            }
            AddPaged(rows, items, Tab.Players, "No one else is here yet");
        }

        private void BanLengthRow(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Ban for", _labelStyle);
            float x = r.x + LabelWidth;
            int count = BanLengthNames.Length;
            float cell = (r.xMax - x - Gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                var length = (BanLength)i;
                if (GUI.Button(new Rect(x + i * (cell + Gap), r.y, cell, r.height), BanLengthNames[i],
                        length == _banLength ? _pickedStyle : _buttonStyle))
                {
                    _clicks.Add(() => _banLength = length);
                }
            }
        }

        private void PlayerRow(Rect r, LoggedPlayer player, string time, bool here)
        {
            float buttons = here ? 2 * SmallButton + Gap : SmallButton;
            float nameWidth = r.width - buttons - TimeWidth - 2 * Gap;
            _canvas.Name(new Rect(r.x, r.y, nameWidth, r.height), player.Name, here ? White : Dim);
            GUI.Label(new Rect(r.x + nameWidth + Gap, r.y, TimeWidth, r.height), time, _rightStyle);

            float x = r.xMax - buttons;
            if (here)
            {
                string kickKey = "kick:" + player.SteamId;
                if (GUI.Button(new Rect(x, r.y, SmallButton, r.height), ConfirmText(kickKey, "Kick"), _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        if (Confirmed(kickKey))
                        {
                            Kick(player);
                        }
                    });
                }
                x += SmallButton + Gap;
            }

            if (Game.IsBanned(player.SteamId) && !here)
            {
                GUI.Label(new Rect(x, r.y, SmallButton, r.height), "banned", _centredStyle);
                return;
            }
            string banKey = "ban:" + player.SteamId;
            if (GUI.Button(new Rect(x, r.y, SmallButton, r.height), ConfirmText(banKey, "Ban"), _buttonStyle))
            {
                _clicks.Add(() =>
                {
                    if (Confirmed(banKey))
                    {
                        Ban(player);
                    }
                });
            }
        }

        // ---- Requests tab ----

        private void AddRequestRows(List<Action<Rect>> rows)
        {
            rows.Add(RequestSwitchesRow);
            string state = !_session.AskToJoin ? "Ask to join is off (Session tab), so players join straight in."
                : _session.Locked ? "The session is locked, so requests are turned away."
                : _away ? "You're away, so requests are turned away."
                : null;
            if (state != null)
            {
                rows.Add(r => GUI.Label(r, state, _dimStyle));
            }
            if (_requests.Count > 1)
            {
                rows.Add(r =>
                {
                    if (GUI.Button(new Rect(r.x, r.y, 140f, r.height), "Accept all", _buttonStyle))
                    {
                        _clicks.Add(AcceptAll);
                    }
                });
            }

            var items = new List<Action<Rect>>();
            DateTime now = DateTime.UtcNow;
            foreach (JoinRequest request in _requests)
            {
                JoinRequest q = request;
                string time = now - q.Asked < TimeSpan.FromMinutes(1) ? "just asked" : "waiting " + Span(now - q.Asked);
                items.Add(r => RequestRow(r, q, time));
            }
            AddPaged(rows, items, Tab.Requests, "No one is asking to join");
        }

        private void RequestSwitchesRow(Rect r)
        {
            const float boxHeight = 18f;
            float boxY = r.y + (r.height - boxHeight) / 2f;

            const string friendsText = " Let Steam friends in";
            float friendsWidth = _toggleStyle.CalcSize(new GUIContent(friendsText)).x;
            bool friends = GUI.Toggle(new Rect(r.x, boxY, friendsWidth, boxHeight), S.LetFriendsIn.Value, friendsText, _toggleStyle);
            if (friends != S.LetFriendsIn.Value)
            {
                _clicks.Add(() => S.LetFriendsIn.Value = friends);
            }

            const string awayText = " Away (turn everyone down)";
            float awayX = r.x + friendsWidth + 16f;
            float awayWidth = _toggleStyle.CalcSize(new GUIContent(awayText)).x;
            bool away = GUI.Toggle(new Rect(awayX, boxY, awayWidth, boxHeight), _away, awayText, _toggleStyle);
            if (away != _away)
            {
                _clicks.Add(() => _away = away);
            }
        }

        private void RequestRow(Rect r, JoinRequest request, string time)
        {
            float buttons = 2 * SmallButton + Gap;
            float nameWidth = r.width - buttons - TimeWidth - 2 * Gap;
            _canvas.Name(new Rect(r.x, r.y, nameWidth, r.height), request.Name, White);
            GUI.Label(new Rect(r.x + nameWidth + Gap, r.y, TimeWidth, r.height), time, _rightStyle);
            float x = r.xMax - buttons;
            if (GUI.Button(new Rect(x, r.y, SmallButton, r.height), "Accept", _buttonStyle))
            {
                _clicks.Add(() => AnswerRequest(request, true));
            }
            if (GUI.Button(new Rect(x + SmallButton + Gap, r.y, SmallButton, r.height), "Reject", _buttonStyle))
            {
                _clicks.Add(() => AnswerRequest(request, false));
            }
        }

        // ---- Bans tab ----

        private int BanCount()
        {
            BanData bans = Game.BanData;
            return bans != null && bans.BanServerPlayers != null ? bans.BanServerPlayers.Count : 0;
        }

        private void AddBanRows(List<Action<Rect>> rows)
        {
            BanData bans = Game.BanData;
            if (bans == null || bans.BanServerPlayers == null)
            {
                rows.Add(r => GUI.Label(r, "The game hasn't loaded its ban list yet", _dimStyle));
                return;
            }

            var items = new List<Action<Rect>>();
            for (int i = 0; i < bans.BanServerPlayers.Count; i++)
            {
                int index = i;
                // Worked out only for the rows on the page showing.
                items.Add(r =>
                {
                    string steamId = bans.BanServerPlayers[index];
                    BanInfo info = _save.Ban(steamId);
                    DateTime now = DateTime.UtcNow;
                    string time = info == null ? ""
                        : !info.Until.HasValue ? "for good"
                        : info.Until.Value - now < TimeSpan.FromMinutes(1) ? "ending"
                        : Span(info.Until.Value - now) + " left";
                    BanRow(r, steamId, Game.Styled(Game.Filter(Game.BanNick(bans, index))), time);
                });
            }
            AddPaged(rows, items, Tab.Bans, "No one is banned");

            if (_selectedBan != null && bans.BanServerPlayers.Contains(_selectedBan))
            {
                AddBanDetailRows(rows, _selectedBan);
            }
        }

        private void BanRow(Rect r, string steamId, string name, string time)
        {
            const float marker = 16f;
            float nameWidth = r.width - SmallButton - TimeWidth - 2 * Gap;
            var nameRect = new Rect(r.x, r.y, nameWidth, r.height);
            // Names are drawn under the GUI, so the selected one gets a marker beside it rather than a box.
            bool selected = steamId == _selectedBan;
            GUI.Label(new Rect(r.x, r.y, marker, r.height), selected ? ">" : "", _labelStyle);
            _canvas.Name(new Rect(r.x + marker, r.y, nameWidth - marker, r.height), name, selected ? StatusColour : White);
            // Clicking the name selects it.
            if (GUI.Button(nameRect, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => SelectBan(steamId));
            }
            GUI.Label(new Rect(nameRect.xMax + Gap, r.y, TimeWidth, r.height), time, _rightStyle);

            string key = "unban:" + steamId;
            if (GUI.Button(new Rect(r.xMax - SmallButton, r.y, SmallButton, r.height), ConfirmText(key, "Unban"), _buttonStyle))
            {
                _clicks.Add(() =>
                {
                    if (Confirmed(key))
                    {
                        Unban(steamId, name);
                    }
                });
            }
        }

        private void AddBanDetailRows(List<Action<Rect>> rows, string steamId)
        {
            BanInfo info = _save.Ban(steamId);
            string when = info != null && info.Banned != default
                ? "Banned " + info.Banned.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.InvariantCulture)
                : "Banned before Session Manager";
            rows.Add(r =>
            {
                const float button = 80f;
                GUI.Label(new Rect(r.x, r.y, r.width - 2 * (button + Gap), r.height), when, _dimStyle);
                if (GUI.Button(new Rect(r.xMax - 2 * button - Gap, r.y, button, r.height), "Copy ID", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        GUIUtility.systemCopyBuffer = steamId;
                        Status("Steam ID copied");
                    });
                }
                if (GUI.Button(new Rect(r.xMax - button, r.y, button, r.height), "Profile", _buttonStyle))
                {
                    _clicks.Add(() =>
                    {
                        if (!Game.OpenProfile(steamId))
                        {
                            Status("Steam's overlay is off, so the profile can't open");
                        }
                    });
                }
            });
            rows.Add(r =>
            {
                GUI.Label(new Rect(r.x, r.y, LabelWidth, r.height), "Note", _labelStyle);
                GUI.SetNextControlName(NoteField);
                string typed = GUI.TextField(new Rect(r.x + LabelWidth, r.y, r.width - LabelWidth, r.height), _noteText ?? "",
                    MaxNoteChars, _fieldStyle);
                if (typed != (_noteText ?? ""))
                {
                    _noteText = typed;
                    _noteDirty = true;
                    _noteSaveAt = Time.unscaledTime + NoteSaveDelay;
                }
            });
        }

        // ---- Shared bits ----

        // Lists longer than a page get page buttons.
        private void AddPaged(List<Action<Rect>> rows, List<Action<Rect>> items, Tab tab, string empty)
        {
            if (items.Count == 0)
            {
                rows.Add(r => GUI.Label(r, empty, _dimStyle));
                return;
            }
            int pages = (items.Count + RowsPerPage - 1) / RowsPerPage;
            int page = Mathf.Clamp(_pages[(int)tab], 0, pages - 1);
            _pages[(int)tab] = page;
            rows.AddRange(items.Skip(page * RowsPerPage).Take(RowsPerPage));
            if (pages > 1)
            {
                rows.Add(r =>
                {
                    const float arrow = 40f;
                    bool was = GUI.enabled;
                    GUI.enabled = page > 0;
                    if (GUI.Button(new Rect(r.x, r.y, arrow, r.height), "<", _buttonStyle))
                    {
                        _clicks.Add(() => _pages[(int)tab] = page - 1);
                    }
                    GUI.enabled = page < pages - 1;
                    if (GUI.Button(new Rect(r.xMax - arrow, r.y, arrow, r.height), ">", _buttonStyle))
                    {
                        _clicks.Add(() => _pages[(int)tab] = page + 1);
                    }
                    GUI.enabled = was;
                    GUI.Label(new Rect(r.x + arrow, r.y, r.width - 2 * arrow, r.height), $"Page {page + 1} of {pages}", _centredStyle);
                });
            }
        }

        // Enter renames (in the name box) or finishes the note; Esc stops typing. The game doesn't see
        // either (see Patches.cs).
        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || !_typing)
            {
                return;
            }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                bool renaming = GUI.GetNameOfFocusedControl() == NameField;
                _clicks.Add(() =>
                {
                    StopTyping();
                    if (renaming && _nameEdited && _nameText != _session.Name)
                    {
                        Rename();
                    }
                    else if (_noteDirty)
                    {
                        SaveNote();
                    }
                });
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _clicks.Add(StopTyping);
                e.Use();
            }
        }

        // Clicking outside the window stops typing in it.
        private static void HandleClickAway(Rect panel)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !panel.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
            }
        }

        // "12 min", "3 h 5 min", "2 days".
        private static string Span(TimeSpan t)
        {
            if (t.TotalHours < 1)
            {
                return $"{Math.Max(1, (int)t.TotalMinutes)} min";
            }
            if (t.TotalDays < 1)
            {
                return t.Minutes > 0 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{(int)t.TotalHours} h";
            }
            return Count((int)t.TotalDays, "day");
        }

        private static string Count(int n, string thing) => n == 1 ? $"1 {thing}" : $"{n} {thing}s";

        private void MakeStyles()
        {
            _titleStyle = Label(15, White, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, Dim, TextAnchor.MiddleRight);
            _labelStyle = Label(15, White, TextAnchor.MiddleLeft);
            _dimStyle = Label(14, Dim, TextAnchor.MiddleLeft);
            _rightStyle = Label(13, Dim, TextAnchor.MiddleRight);
            _centredStyle = Label(15, White, TextAnchor.MiddleCenter);

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(4, 4, 2, 2) };
            // The tab you're on, and settings that are on, look pressed in.
            _pickedStyle = new GUIStyle(_buttonStyle);
            _pickedStyle.normal = GUI.skin.button.onNormal;
            _pickedStyle.hover = GUI.skin.button.onHover;
            _pickedStyle.active = GUI.skin.button.onActive;

            _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 14, wordWrap = false };
            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
                richText = false,
                clipping = TextClipping.Clip,
            };
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
