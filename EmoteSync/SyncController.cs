using System;
using System.Collections.Generic;
using PurrNet.Modules;
using OnTogetherMods.Shared;
using Steamworks;
using UnityEngine;

namespace EmoteSync
{
    // How it works:
    // 1. Each player turns sync on with the toggle key. Players with it on tell each other through
    //    Steam lobby chat (ON / HI / OFF), so everyone knows who's in the group.
    // 2. When someone in the group picks an emote, the game's emote call is held back (see Patches.cs)
    //    and "GO <emote> <tick>" is sent, where the tick is about one second ahead.
    // 3. Every player in the group, including the sender, plays the emote when the shared PurrNet
    //    tick reaches that number, so they all start together.
    // 4. Focus: picking a focus activity sends "FOCUS <anim> <type>", and the others start or switch
    //    to it (FocusJoin.cs). Anyone busy (typing, a window open, mid-emote...) joins once they're
    //    free, for up to JoinWaitSeconds. Standing up sends "UNFOCUS", so nobody waits to join an
    //    activity that's over. Focus animations loop, so instead of a start tick, FocusLocker.cs
    //    ties every loop to the shared clock to keep them in step.
    // Each player only ever animates their own character.
    internal sealed class SyncController : MonoBehaviour
    {
        // Messages that arrive this late are dropped instead of starting out of step.
        private const float MaxLateSeconds = 1f;

        private struct Pending
        {
            public int Emote;
            public uint Tick;
            public bool Mine;
            public ulong Sender;
        }

        // Someone's focus pick that arrived while we were busy.
        private struct WaitingFocus
        {
            public ulong Sender;
            public string Name;
            public string Activity;
            public int Anim;
            public FocusType Type;
            public float Until;
            public string WaitingFor;
            public bool CanClear;   // "Join now" can clear what's in the way
        }

        public static SyncController Instance { get; private set; }

        // True while the mouse is over the "Join now" prompt, so the click doesn't reach the world (Patches.cs).
        public static bool IsMouseOverPrompt { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly LobbyChannel _lobby = new LobbyChannel();
        private readonly PhaseAligner _aligner = new PhaseAligner();
        private readonly FocusLocker _focusLocker = new FocusLocker();
        private readonly HashSet<ulong> _group = new HashSet<ulong>();
        private Pending? _pending;
        private WaitingFocus? _waitingFocus;
        private bool _wasFocusing;
        private bool _on;
        private bool _firing;

        private GUIStyle _noticeStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _buttonStyle;
        private ClickBlocker _blocker;
        private Rect _promptScreenRect;
        private int _promptDrawnFrame = -100;
        private bool _joinNowClicked;
        private string _notice;
        private float _noticeUntil;
        private bool _showPanel;
        private string _groupNames;
        private float _groupNamesAt;

        private void Awake()
        {
            Instance = this;
            _blocker = new ClickBlocker("EmoteSync ClickBlocker");
            _lobby.MessageReceived += OnMessage;
            _lobby.MemberLeft += OnMemberLeft;
            _lobby.LobbyChanged += OnLobbyChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                IsMouseOverPrompt = false;
            }
            _blocker?.Destroy();
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(S.ToggleKey.Value) && !IsTyping())
                {
                    Toggle();
                }
                if (_on && Input.GetKeyDown(S.PanelKey.Value) && !IsTyping())
                {
                    _showPanel = !_showPanel;
                }

                _lobby.Poll();
                UpdatePending();
                UpdateFocus();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                _pending = null;
                _waitingFocus = null;
            }

            // The prompt's click blocker follows where OnGUI last drew it.
            bool promptShown = _waitingFocus != null && Time.frameCount - _promptDrawnFrame <= 2;
            IsMouseOverPrompt = promptShown && ClickBlocker.MouseIn(_promptScreenRect);
            _blocker?.Update(promptShown, _promptScreenRect);
        }

        private void LateUpdate()
        {
            try
            {
                _aligner.Update(Game.Ticks);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                _aligner.Stop();
            }

            try
            {
                if (_on && S.SyncFocus.Value && _lobby.InLobby)
                {
                    _focusLocker.Update(Game.Ticks);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                _focusLocker.Reset();
            }
        }

        // Called by the patch on EmoteController.SetEmoteAnim when this player picks an emote.
        // Returns true if the emote will be synced, so the game shouldn't play it right now.
        public bool TryTakeOver(int emoteIndex)
        {
            if (_firing || !_on || _group.Count == 0 || !_lobby.InLobby)
            {
                return false;
            }
            TickManager ticks = Game.Ticks;
            if (ticks == null)
            {
                return false;
            }

            uint tick = (uint)Math.Ceiling(ticks.syncedPreciseTick + S.LeadTime.Value * ticks.tickRate);
            if (!_lobby.Send($"GO {emoteIndex} {tick}"))
            {
                return false;
            }

            // The game's emote call would have unlocked the player after closing the emote wheel.
            MonoSingleton<TaskManager>.I.SetLockState(LockState.Free);
            Schedule(emoteIndex, tick, mine: true, _lobby.Me.m_SteamID, ticks);
            Notice(_group.Count == 1 ? "Syncing emote with 1 player" : $"Syncing emote with {_group.Count} players");
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Sent emote {emoteIndex} for tick {tick} (now {ticks.syncedPreciseTick:0.0}, " +
                    $"{ticks.tickRate} ticks/s, ping to host {ticks.rtt * 1000:0}ms).");
            }
            return true;
        }

        private void Toggle()
        {
            _on = !_on;
            if (_on)
            {
                _showPanel = S.ShowPanel.Value;
                _lobby.Send("ON");
                string what = S.SyncFocus.Value ? "Sync ON for emotes and focus." : "Sync ON for emotes.";
                Notice(!_lobby.InLobby ? $"{what} It starts when you join a lobby." : $"{what} {GroupText()}");
            }
            else
            {
                _lobby.Send("OFF");
                _pending = null;
                _waitingFocus = null;
                _aligner.Stop();
                _focusLocker.Reset();
                Notice("Sync OFF");
            }
            Plugin.Log.LogInfo(_on ? "Emote sync on." : "Emote sync off.");
        }

        private void OnLobbyChanged()
        {
            _group.Clear();
            _pending = null;
            _waitingFocus = null;
            _wasFocusing = false;
            _aligner.Stop();
            _focusLocker.Reset();
            if (_on && _lobby.InLobby)
            {
                _lobby.Send("ON");
            }
        }

        private void OnMemberLeft(CSteamID id)
        {
            if (_group.Remove(id.m_SteamID) && _on)
            {
                Notice($"{LobbyChannel.NameOf(id)} left. {GroupText()}");
            }
        }

        private void OnMessage(CSteamID sender, string[] words)
        {
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"From {LobbyChannel.NameOf(sender)}: {string.Join(" ", words)}");
            }

            switch (words[0])
            {
                // Someone turned sync on. Reply so they know we're in the group too.
                case "ON":
                    if (_on)
                    {
                        _lobby.Send("HI");
                    }
                    AddToGroup(sender);
                    break;

                // Reply to our ON.
                case "HI":
                    AddToGroup(sender);
                    break;

                case "OFF":
                    if (_group.Remove(sender.m_SteamID) && _on)
                    {
                        Notice($"{LobbyChannel.NameOf(sender)} turned emote sync off. {GroupText()}");
                    }
                    break;

                case "GO":
                    AddToGroup(sender);
                    if (_on && words.Length >= 3 &&
                        int.TryParse(words[1], out int emote) && emote >= 0 && emote < Game.EmoteCount &&
                        uint.TryParse(words[2], out uint tick))
                    {
                        ReceiveGo(sender, emote, tick);
                    }
                    break;

                case "FOCUS":
                    AddToGroup(sender);
                    if (_on && S.SyncFocus.Value && words.Length >= 3 &&
                        int.TryParse(words[1], out int anim) &&
                        int.TryParse(words[2], out int type) && Enum.IsDefined(typeof(FocusType), type))
                    {
                        ReceiveFocus(sender, anim, (FocusType)type);
                    }
                    break;

                // Someone stood up. Stop waiting to join their activity, since it's over.
                case "UNFOCUS":
                    AddToGroup(sender);
                    if (_waitingFocus is WaitingFocus waiting && waiting.Sender == sender.m_SteamID)
                    {
                        _waitingFocus = null;
                        Notice($"{waiting.Name} stopped focusing, so you won't join");
                    }
                    break;
            }
        }

        // Called by the patch on PlayerFocusController.SetFocus after this player picks an activity.
        public void OnLocalFocusPicked(PlayerFocusController focus)
        {
            // You picked your own activity, so don't switch you to someone else's later.
            _waitingFocus = null;

            if (!_on || !S.SyncFocus.Value || _group.Count == 0 || !_lobby.InLobby)
            {
                return;
            }
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            if (channel == null || focus != channel.MainFocusController || !focus.IsFocus)
            {
                return;
            }

            int anim = channel.MainAnimationController.GetState();
            if (_lobby.Send($"FOCUS {anim} {(int)focus.FocusType}"))
            {
                Notice(_group.Count == 1 ? "Focusing together with 1 player" : $"Focusing together with {_group.Count} players");
            }
        }

        private void ReceiveFocus(CSteamID sender, int anim, FocusType type)
        {
            string name = SafeName(sender);
            string activity = Game.FocusName(type);
            // A newer pick replaces one we were waiting on.
            _waitingFocus = null;

            JoinResult result = FocusJoin.TryJoin(anim, type, out string why, out bool canClear);
            if (result == JoinResult.Wait && S.JoinWaitSeconds.Value > 0f)
            {
                _waitingFocus = new WaitingFocus
                {
                    Sender = sender.m_SteamID,
                    Name = name,
                    Activity = activity,
                    Anim = anim,
                    Type = type,
                    Until = Time.unscaledTime + S.JoinWaitSeconds.Value,
                    WaitingFor = why,
                    CanClear = canClear,
                };
                _joinNowClicked = false;
            }
            else
            {
                ReportJoin(name, activity, result, why);
            }
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Focus {type} (anim {anim}) from {name}: {result}" + (why != null ? $" ({why})." : "."));
            }
        }

        // Runs every frame. Tells the group when we stand up, and joins a focus we're waiting on
        // as soon as we're free.
        private void UpdateFocus()
        {
            if (!_on || !S.SyncFocus.Value || !_lobby.InLobby)
            {
                _waitingFocus = null;
                _wasFocusing = false;
                return;
            }

            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            PlayerFocusController focus = channel != null ? channel.MainFocusController : null;
            bool focusing = focus != null && focus.IsFocus;
            if (_wasFocusing && !focusing)
            {
                // Anyone waiting to join us stops waiting. And if we were waiting to switch to someone
                // else's activity, standing up means we've chosen not to.
                if (_group.Count > 0)
                {
                    _lobby.Send("UNFOCUS");
                }
                _waitingFocus = null;
            }
            _wasFocusing = focusing;

            bool joinNow = _joinNowClicked;
            _joinNowClicked = false;
            if (!(_waitingFocus is WaitingFocus waiting))
            {
                return;
            }
            if (Time.unscaledTime > waiting.Until)
            {
                _waitingFocus = null;
                Notice($"Stopped waiting to join {waiting.Name} ({waiting.Activity})");
                if (S.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"Gave up joining {waiting.Name}'s focus, still waiting until {waiting.WaitingFor}.");
                }
                return;
            }

            JoinResult result = FocusJoin.TryJoin(waiting.Anim, waiting.Type, out string why, out bool canClear);
            if (result == JoinResult.Wait && joinNow && canClear)
            {
                // "Join now" was clicked: close the window, leave the chat box or stop the emote, then join.
                FocusJoin.ClearWay();
                result = FocusJoin.TryJoin(waiting.Anim, waiting.Type, out why, out canClear);
            }
            if (result == JoinResult.Wait)
            {
                if (joinNow)
                {
                    Notice($"Can't join yet: waiting until {why}");
                }
                waiting.WaitingFor = why;
                waiting.CanClear = canClear;
                _waitingFocus = waiting;
                return;
            }
            _waitingFocus = null;
            ReportJoin(waiting.Name, waiting.Activity, result, why);
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Waited to join {waiting.Name}'s focus: {result}" + (why != null ? $" ({why})." : "."));
            }
        }

        private void ReportJoin(string name, string activity, JoinResult result, string why)
        {
            switch (result)
            {
                case JoinResult.Joined:
                    Notice($"Focusing with {name} ({activity})");
                    break;
                case JoinResult.Wait:
                    // Only when waiting is turned off in the settings.
                    Notice($"Couldn't join {name} ({activity}) until {why}");
                    break;
                default:
                    Notice($"Couldn't join {name} ({activity}): {why}");
                    break;
            }
        }

        // "Joining Sam (Read) when you finish typing (12s)", or null when not waiting.
        private string WaitingLine()
        {
            if (!(_waitingFocus is WaitingFocus waiting))
            {
                return null;
            }
            float left = Mathf.Max(0f, waiting.Until - Time.unscaledTime);
            return $"Joining {waiting.Name} ({waiting.Activity}) when {waiting.WaitingFor} ({Mathf.CeilToInt(left)}s)";
        }

        // A player's name, kept from being read as formatting tags.
        private static string SafeName(CSteamID id)
        {
            return LobbyChannel.NameOf(id).Replace("<", "‹");
        }

        private void AddToGroup(CSteamID id)
        {
            if (_group.Add(id.m_SteamID) && _on)
            {
                Notice($"{LobbyChannel.NameOf(id)} has emote sync on");
            }
        }

        private void ReceiveGo(CSteamID sender, int emote, uint tick)
        {
            TickManager ticks = Game.Ticks;
            if (ticks == null)
            {
                return;
            }

            double late = (ticks.syncedPreciseTick - tick) * ticks.tickDeltaDouble;
            if (late > MaxLateSeconds)
            {
                Plugin.Log.LogWarning($"Emote from {LobbyChannel.NameOf(sender)} arrived {late:0.00}s late, so it was skipped. " +
                    "Raising LeadTime in the config may help.");
                return;
            }

            if (-late > S.LeadTime.Value + 1f)
            {
                // Further ahead than the sender could have asked for, so our clocks disagree.
                // Start now rather than waiting on a bad clock.
                Plugin.Log.LogWarning($"Emote from {LobbyChannel.NameOf(sender)} is set {-late:0.00}s ahead, " +
                    "which means the shared clock is off. Playing it now instead.");
                tick = (uint)Math.Floor(ticks.syncedPreciseTick);
            }

            if (!Schedule(emote, tick, mine: false, sender.m_SteamID, ticks))
            {
                return;
            }
            Notice($"{LobbyChannel.NameOf(sender)} started a synced emote");
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Emote {emote} from {LobbyChannel.NameOf(sender)} for tick {tick}, " +
                    $"{-late:0.000}s from now. Ping to host: {ticks.rtt * 1000:0}ms.");
            }
        }

        // If two people pick an emote at nearly the same time, messages can arrive in a different
        // order for each player. So don't just keep the newest message: the emote that starts
        // later wins (ties go to the higher Steam ID). Everyone picks the same winner that way.
        private bool Schedule(int emote, uint tick, bool mine, ulong sender, TickManager ticks)
        {
            if (_pending is Pending current &&
                (current.Tick > tick || (current.Tick == tick && current.Sender > sender)))
            {
                if (S.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"Ignored emote {emote} for tick {tick}: emote {current.Emote} for tick {current.Tick} wins.");
                }
                return false;
            }

            _pending = new Pending { Emote = emote, Tick = tick, Mine = mine, Sender = sender };
            _aligner.Begin(Game.EmoteAnimState(emote), tick, ticks);
            return true;
        }

        private void UpdatePending()
        {
            if (_pending == null)
            {
                return;
            }
            TickManager ticks = Game.Ticks;
            if (ticks == null)
            {
                _pending = null;
                return;
            }
            if (ticks.syncedPreciseTick < _pending.Value.Tick)
            {
                return;
            }

            Pending pending = _pending.Value;
            _pending = null;
            Fire(pending, ticks);
        }

        // Plays the emote on our own character, the same way the emote wheel does.
        private void Fire(Pending pending, TickManager ticks)
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            PlayerAnimationController player = channel != null ? channel.MainAnimationController : null;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (player == null || tasks == null)
            {
                return;
            }

            // Same check the game uses before opening the emote wheel: not typing, not focusing,
            // not swimming, not mid-emote, on the ground... Plus no menu open.
            if (tasks.LockState != LockState.Free || !tasks.IsReadyForEmotePanel())
            {
                Notice("Synced emote skipped because you were busy");
                return;
            }

            if (!pending.Mine)
            {
                // The emote wheel puts away held items before you can pick an emote.
                channel.MainMovementController.ClosePropsOnHand();
            }

            Animator animator = Game.GetAnimator(player);
            int animState = Game.EmoteAnimState(pending.Emote);
            // If we're still in this emote (a looping dance), setting the same value again does
            // nothing, so restart it by hand. NetworkAnimator.Play is sent to everyone too.
            bool restart = player.GetState() == animState && !animator.IsInTransition(0);
            int currentHash = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;

            _firing = true;
            try
            {
                Game.SetEmoteAnim(Game.GetEmoteController(player), pending.Emote);
            }
            finally
            {
                _firing = false;
            }

            if (restart)
            {
                Game.GetNetAnimator(player).Play(currentHash, 0, 0f);
            }

            if (S.DebugLog.Value)
            {
                double late = (ticks.syncedPreciseTick - pending.Tick) * ticks.tickDeltaDouble;
                Plugin.Log.LogInfo($"Played emote {pending.Emote} (anim {animState}) {late * 1000:0}ms after tick {pending.Tick}" +
                    (restart ? ", restarted it" : "") + ".");
            }
        }

        private string GroupText()
        {
            switch (_group.Count)
            {
                case 0: return "No one else has it on yet.";
                case 1: return "1 other player has it on.";
                default: return $"{_group.Count} other players have it on.";
            }
        }

        private static bool IsTyping()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            // IsWriting is the chat box itself. (IsCancelInputs would also be true in Photo Mode,
            // which would block our keys there.)
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        private void Notice(string text)
        {
            if (!S.ShowNotices.Value)
            {
                return;
            }
            _notice = text;
            _noticeUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            // The prompt shows whenever we're waiting to join a focus, even with notices turned off,
            // because it has a button.
            string prompt = WaitingLine();
            bool notice = _notice != null && Time.unscaledTime < _noticeUntil;
            bool panel = _on && _showPanel;
            // Hidden while Photo Mode takes a photo.
            if ((prompt == null && !notice && !panel) || PanelStack.Hidden)
            {
                return;
            }

            if (_panelStyle == null)
            {
                _panelStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
                _noticeStyle = PanelStack.MakeStyle(TextAnchor.MiddleCenter);
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(12, 12, 2, 2),
                };
            }

            // Lay out at 1080p and scale to the real screen, so text isn't tiny on 4K.
            Matrix4x4 previous = PanelStack.Begin(out float width);

            if (panel)
            {
                string text = PanelText();
                Vector2 size = _panelStyle.CalcSize(new GUIContent(text));
                // In the bottom window stack, above Photo Mode's controls when that's open.
                GUI.Box(PanelStack.Place("EmoteSync", PanelStack.EmoteSyncOrder, size), text, _panelStyle);
            }

            // Below Photo Mode's messages (y = 40), so both can show at once.
            float y = 90f;
            if (prompt != null)
            {
                y += DrawPrompt(prompt, width, y) + 6f;
            }
            if (notice)
            {
                Vector2 size = _noticeStyle.CalcSize(new GUIContent(_notice));
                GUI.Box(new Rect((width - size.x) / 2f, y, size.x, size.y), _notice, _noticeStyle);
            }

            PanelStack.End(previous);
        }

        // "Joining Sam (Read) when you finish typing (24s)  [Join now]", centred. Returns its height.
        private float DrawPrompt(string text, float width, float y)
        {
            bool button = _waitingFocus is WaitingFocus waiting && waiting.CanClear;
            const string label = "Join now";
            const float gap = 6f;
            Vector2 textSize = _noticeStyle.CalcSize(new GUIContent(text));
            float buttonWidth = button ? _buttonStyle.CalcSize(new GUIContent(label)).x : 0f;
            float total = textSize.x + (button ? gap + buttonWidth : 0f);

            var textRect = new Rect((width - total) / 2f, y, textSize.x, textSize.y);
            GUI.Box(textRect, text, _noticeStyle);
            Rect whole = textRect;
            if (button)
            {
                var buttonRect = new Rect(textRect.xMax + gap, y, buttonWidth, textSize.y);
                if (GUI.Button(buttonRect, label, _buttonStyle))
                {
                    // Acted on in Update, outside the GUI pass.
                    _joinNowClicked = true;
                }
                whole.xMax = buttonRect.xMax;
            }

            // Where it is on the real screen, for the click blocker.
            float scale = Screen.height / 1080f;
            _promptScreenRect = new Rect(whole.x * scale, whole.y * scale, whole.width * scale, whole.height * scale);
            _promptDrawnFrame = Time.frameCount;
            return textSize.y;
        }

        private string PanelText()
        {
            string group;
            if (!_lobby.InLobby)
            {
                group = "Waiting for you to join a lobby";
            }
            else if (_group.Count == 0)
            {
                group = $"Just you so far. Others press {S.ToggleKey.Value} to join.";
            }
            else
            {
                group = "With " + GroupNames();
            }

            string text =
                "<b>SYNC</b>   <color=#7CFC9A>ON</color>\n" +
                group + "\n" +
                "Emote wheel: everyone emotes together";
            if (S.SyncFocus.Value)
            {
                text += "\nFocus: the group joins what you pick";
            }

            TickManager ticks = Game.Ticks;
            if (_pending != null && ticks != null)
            {
                double wait = (_pending.Value.Tick - ticks.syncedPreciseTick) * ticks.tickDeltaDouble;
                text += $"\n<color=#FFD966>Emote starts in {Math.Max(0, wait):0.0}s</color>";
            }

            text += $"\n{S.ToggleKey.Value}: turn sync off      {S.PanelKey.Value}: hide this window";
            return text;
        }

        // Player names, refreshed twice a second rather than every time the window is drawn.
        private string GroupNames()
        {
            if (_groupNames == null || Time.unscaledTime - _groupNamesAt > 0.5f)
            {
                var names = new List<string>();
                foreach (ulong id in _group)
                {
                    // Keep names from being read as formatting tags.
                    names.Add(SafeName(new CSteamID(id)));
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
                _groupNames = string.Join(", ", names);
                _groupNamesAt = Time.unscaledTime;
            }
            return _groupNames;
        }
    }
}
