using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;

namespace DeskSnap
{
    internal enum SnapResult
    {
        Snapped,
        AlreadyThere,
        NoDesk,   // no other desk close enough
        NoRoom,   // a desk is close, but there's no room beside it
    }

    // When you sit down at your desk near a friend's desk, moves you into line beside it, facing the
    // same way. Your position is shared with everyone anyway, so they see the neat row without the mod.
    //
    // With Emote Sync, your focus can start because a friend picked it. Their desk may take a moment
    // to show on your screen, so it keeps looking for a few seconds then. If two people snap into the
    // same spot at once, the one with the higher player number moves along the row.
    internal sealed class SnapController : MonoBehaviour
    {
        private const float JoinWaitSeconds = 3f;
        private const float SettleSeconds = 3f;
        private const float CheckEvery = 0.2f;
        private const int MaxResettles = 3;

        public static SnapController Instance { get; private set; }

        private static Settings S => Plugin.Settings;

        private PlayerFocusController _me;
        private bool _sitting;              // you've sat down and haven't stood up yet
        private Vector3 _sitPosition;       // where you sat down, before any snap
        private Quaternion _sitRotation;
        private bool _snapped;
        private PlayerFocusController _neighbour;
        private float _joinUntil;           // still looking for the desk of the friend you joined
        private float _settleUntil;         // still watching for someone snapping into your spot
        private int _resettles;
        private float _nextCheck;
        // When each other desk first showed up on your screen since you sat down.
        private readonly Dictionary<PlayerFocusController, float> _firstSeen = new Dictionary<PlayerFocusController, float>();

        // Drawn with TextMeshPro, so names keep the styling they have on name tags.
        private readonly TextBox _noticeBox = new TextBox("DeskSnap Notice", TextAlignmentOptions.Center);
        private string _notice;
        private float _noticeUntil;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            _noticeBox.Destroy();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Called after any focus activity is picked (by you, or by Emote Sync for you).
        public void OnFocusPicked(PlayerFocusController focus)
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            if (channel == null || focus != channel.MainFocusController || !focus.IsFocus)
            {
                return;
            }

            bool switching = _sitting && _me == focus;
            if (!switching)
            {
                _me = focus;
                _sitting = true;
                _sitPosition = focus.transform.position;
                _sitRotation = focus.transform.rotation;
                _snapped = false;
                _neighbour = null;
                _firstSeen.Clear();
            }
            _joinUntil = 0f;
            _settleUntil = 0f;

            // Once lined up, switching activities keeps you where you are.
            if (!S.Enabled.Value || _snapped || !Game.IsAtOwnDesk(focus))
            {
                return;
            }

            bool joining = Game.EmoteSyncJoining();
            SnapResult result = TrySnap(joining, out string message);
            if (result == SnapResult.NoDesk)
            {
                if (joining)
                {
                    _joinUntil = Time.unscaledTime + JoinWaitSeconds;
                    _nextCheck = Time.unscaledTime + CheckEvery;
                }
            }
            else if (message != null)
            {
                Notice(message);
            }
        }

        private void LateUpdate()
        {
            _noticeBox.LateUpdate();
        }

        private void Update()
        {
            if (Input.GetKeyDown(S.ToggleKey.Value) && !IsTyping())
            {
                Toggle();
            }

            if (!_sitting)
            {
                return;
            }
            if (_me == null || !_me.IsFocus)
            {
                // Stood up (the game puts you where you last sat or snapped), or left the world.
                ClearSit();
                return;
            }

            float now = Time.unscaledTime;
            if (now < _nextCheck || (now >= _joinUntil && now >= _settleUntil))
            {
                return;
            }
            _nextCheck = now + CheckEvery;

            if (!_snapped && now < _joinUntil)
            {
                if (!S.Enabled.Value || !Game.IsAtOwnDesk(_me))
                {
                    _joinUntil = 0f;
                    return;
                }
                SnapResult result = TrySnap(true, out string message);
                if (result != SnapResult.NoDesk)
                {
                    _joinUntil = 0f;
                    if (message != null)
                    {
                        Notice(message);
                    }
                }
            }
            else if (_snapped && now < _settleUntil)
            {
                Settle();
            }
        }

        // The key: turns snapping on or off. While sitting at your desk, it also snaps you now (on)
        // or moves you back to where you sat down (off).
        private void Toggle()
        {
            bool on = !S.Enabled.Value;
            S.Enabled.Value = on;
            _joinUntil = 0f;
            _settleUntil = 0f;
            bool sitting = _sitting && _me != null && _me.IsFocus && Game.IsFreeFocus(_me);

            if (!on)
            {
                if (sitting && _snapped)
                {
                    Game.MoveSeated(_me, _sitPosition, _sitRotation);
                    _snapped = false;
                    _neighbour = null;
                    Notice("Desk Snap off. You're back where you sat down.");
                }
                else
                {
                    Notice("Desk Snap off");
                }
                return;
            }

            if (sitting && !_snapped && Game.IsAtOwnDesk(_me))
            {
                SnapResult result = TrySnap(false, out string message);
                Notice(result == SnapResult.AlreadyThere || message == null ? "Desk Snap on" : $"Desk Snap on. {message}");
            }
            else
            {
                Notice("Desk Snap on. Sit at your desk near a friend's desk to line up beside it.");
            }
        }

        // Moves you beside the nearest desk within range (or, after an Emote Sync join, the one that
        // was out first, which is the friend whose pick you joined).
        private SnapResult TrySnap(bool preferFirstOut, out string message)
        {
            message = null;
            float now = Time.unscaledTime;
            List<Seat> seats = Desks.OthersAtDesks(_me);
            foreach (Seat seat in seats)
            {
                if (!_firstSeen.ContainsKey(seat.Player))
                {
                    _firstSeen[seat.Player] = now;
                }
            }

            float range = S.Range.Value;
            Vector3 from = _sitPosition;
            float Distance(Seat seat)
            {
                Vector3 offset = seat.Position - from;
                return new Vector2(offset.x, offset.z).magnitude;
            }
            List<Seat> near = seats.Where(s => Distance(s) <= range && Mathf.Abs(s.Position.y - from.y) < 2f).ToList();
            if (near.Count == 0)
            {
                message = $"No other desk within {range:0.#} m";
                if (S.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"No desk to snap to ({seats.Count} other desks out, none within {range:0.#} m).");
                }
                return SnapResult.NoDesk;
            }

            Seat target = preferFirstOut
                ? near.OrderBy(s => _firstSeen[s.Player]).ThenBy(Distance).First()
                : near.OrderBy(Distance).First();

            Span mine = Desks.Measure(_me);
            Vector3 targetRight = target.Facing * Vector3.right;
            int side = Vector3.Dot(from - target.Position, targetRight) >= 0f ? 1 : -1;

            if (!Row.TryPlace(target, side, mine, seats, S.Gap.Value, out Placement place, out string why))
            {
                message = $"No room next to {PlayerNames.ForText(target.Name)}'s desk: {why}";
                Plugin.Log.LogInfo($"No room next to {target.Name}'s desk: {why}.");
                return SnapResult.NoRoom;
            }
            return MoveTo(place, out message);
        }

        private SnapResult MoveTo(Placement place, out string message)
        {
            Transform t = _me.transform;
            _neighbour = place.Neighbour.Player;
            if ((t.position - place.Position).sqrMagnitude < 0.0004f && Quaternion.Angle(t.rotation, place.Facing) < 1f)
            {
                _snapped = true;
                message = null;
                return SnapResult.AlreadyThere;
            }

            float moved = Vector3.Distance(t.position, place.Position);
            Game.MoveSeated(_me, place.Position, place.Facing);
            _snapped = true;
            _settleUntil = Time.unscaledTime + SettleSeconds;
            _nextCheck = Time.unscaledTime + CheckEvery;

            string name = place.Neighbour.Name;
            message = $"Snapped next to {PlayerNames.ForText(name)}'s desk. Press {S.ToggleKey.Value} to turn snapping off.";
            Plugin.Log.LogInfo($"Snapped next to {name}'s desk (moved {moved:0.00} m).");
            return SnapResult.Snapped;
        }

        // Right after snapping: if someone else snapped into the same spot at the same moment, one of
        // you moves along the row. Both games pick the same one: the higher player number moves.
        private void Settle()
        {
            Transform t = _me.transform;
            Span mine = Desks.Measure(_me);
            List<Seat> seats = Desks.OthersAtDesks(_me);
            Seat neighbour = seats.FirstOrDefault(s => s.Player == _neighbour);
            Seat clash = seats.FirstOrDefault(s => s.Player != _neighbour && Row.Overlaps(t.position, Desks.FacingOf(t), mine, s));
            if (clash == null || neighbour == null || Game.IdOf(_me) <= Game.IdOf(clash.Player) || _resettles >= MaxResettles)
            {
                return;
            }

            _resettles++;
            Plugin.Log.LogInfo($"{clash.Name} snapped into the same spot, so moving along the row.");
            int side = Vector3.Dot(t.position - neighbour.Position, neighbour.Facing * Vector3.right) >= 0f ? 1 : -1;
            if (Row.TryPlace(neighbour, side, mine, seats, S.Gap.Value, out Placement place, out string why))
            {
                MoveTo(place, out string message);
                if (message != null)
                {
                    Notice(message);
                }
            }
            else
            {
                Plugin.Log.LogInfo($"Couldn't move along: {why}.");
            }
        }

        private void ClearSit()
        {
            _sitting = false;
            _snapped = false;
            _neighbour = null;
            _joinUntil = 0f;
            _settleUntil = 0f;
            _resettles = 0;
            _firstSeen.Clear();
        }

        private static bool IsTyping()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
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
            // Hidden while Photo Mode takes a photo.
            if (_notice == null || Time.unscaledTime >= _noticeUntil || PanelStack.Hidden)
            {
                return;
            }
            // Below Photo Mode's (y = 40) and Emote Sync's (y = 90) messages, so they can all show at once.
            Vector2 size = _noticeBox.Measure(_notice);
            _noticeBox.Draw(new Rect((PanelStack.Width - size.x) / 2f, 140f, size.x, size.y), _notice);
        }
    }
}
