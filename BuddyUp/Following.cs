using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuddyUp
{
    // Walking with a buddy: your character walks after them by itself, the way it would if you held
    // the movement keys (Patches.cs hands the game these steering values), so you get the real walk,
    // stairs, swimming and bumping into things. Your own keys always win; let go and you walk back
    // to them. Beside: next to them, one follower on each side. Behind: in a line along the way they
    // walked, so the line goes round corners the way they did.
    //
    // When your buddy sits down in free focus, you walk up beside them and sit down in the same
    // activity (SitTogether), facing the same way. Standing up again is up to you.
    internal sealed class Following
    {
        // Close enough to stop, and how far they have to get before you set off again.
        private const float ArriveDistance = 0.3f;
        private const float SetOffDistance = 0.7f;
        // Slow down within this distance, but never below this much of a key press.
        private const float SlowDistance = 1.5f;
        private const float MinInput = 0.45f;
        // How long you can push without getting anywhere before jumping, and how many jumps before
        // hopping over (CatchUp). Not counted when you're nearly there.
        private const float StuckSeconds = 0.8f;
        private const float StuckMoved = 0.15f;
        private const float StuckFar = 1f;
        private const int JumpsBeforeHop = 2;
        private const float HopEvery = 2f;
        // Sitting down beside them: close enough to the spot, or near them after walking this long.
        private const float SitReach = 0.4f;
        private const float SitWalkSeconds = 5f;
        private const float SitNear = 3f;
        // Keep trying to sit down (while you're typing, in a menu...) for this long after they did.
        private const float SitPatience = 30f;
        private const float SitRetry = 0.5f;
        // How far apart two seats are side by side: with desks (Desk Snap's desk is 1.2 m wide),
        // and without.
        private const float DeskSpacing = 1.25f;
        private const float PlainSpacing = 0.85f;
        // The way they walked: a point every this far, and a jump this big means they teleported.
        private const float PathStep = 0.25f;
        private const float Teleported = 10f;

        private static Settings S => Plugin.Settings;

        // What the input patch hands the game (Patches.cs).
        public static bool Steering { get; private set; }
        public static float Horizontal { get; private set; }
        public static float Vertical { get; private set; }
        private static bool _jump;

        // When you last walked with your own keys while following.
        private static float _ownKeysAt = -100f;

        private readonly SitTracker _tracker;
        private readonly List<Vector3> _path = new List<Vector3>();
        private bool _moving;
        private float _h;
        private float _v;
        private Vector3 _checkFrom;
        private float _checkAt;
        private int _jumps;
        private float _hoppedAt = -100f;

        // Sitting down with them: the sit we last dealt with (sat down, or gave up on), and the one
        // we sat down with while you're still in it.
        private Sit _sitDone;
        private Sit _sitWalking;
        private float _sitWalkSince;
        private float _nextSitTry;
        private ulong _satWith;
        private int _satAnim;

        public Following(SitTracker tracker)
        {
            _tracker = tracker;
        }

        // Something happened that the player should hear about.
        public event Action<string> Notice;

        // The buddy you walk with, or 0.
        public ulong Target { get; private set; }

        // A line for the window about what's going on, or null.
        public string Status { get; private set; }

        // The input patch saw your own movement keys.
        public static void OwnKeys()
        {
            _ownKeysAt = Time.unscaledTime;
        }

        // A jump to hand the game once.
        public static bool TakeJump()
        {
            bool jump = _jump;
            _jump = false;
            return jump;
        }

        public void Start(ulong target, Buddies buddies)
        {
            if (Target == target)
            {
                return;
            }
            Target = target;
            _path.Clear();
            _moving = true;
            _jumps = 0;
            _checkAt = Time.unscaledTime;
            // If they're already sitting, go and sit beside them.
            _sitDone = null;
            _sitWalking = null;
            _satWith = 0;
            buddies.SendFollow(target);
            Plugin.Log.LogInfo($"Walking with {LobbyChannel.NameOf(new Steamworks.CSteamID(target))}.");
        }

        public void Stop(Buddies buddies)
        {
            if (Target == 0)
            {
                return;
            }
            Target = 0;
            Steering = false;
            _jump = false;
            _satWith = 0;
            buddies.SendFollow(0);
            Plugin.Log.LogInfo("Stopped walking with your buddy.");
        }

        // Call every frame.
        public void Update(PlayerFocusController me, PlayerMovementController move, Buddies buddies, Riding riding)
        {
            Steering = false;
            Status = null;
            if (Target == 0)
            {
                _jump = false;
                return;
            }
            string name = Game.PlayerName(Target);
            PlayerController buddy = Game.PlayerOf(Target);
            if (buddy == null || me == null || move == null)
            {
                Status = $"Looking for {name}...";
                return;
            }
            // Both walking with each other would go round in circles: one of you stops (every screen
            // agrees which, by Steam ID).
            if (buddies.Me < Target && buddies.FollowersOf(buddies.Me).Contains(Target))
            {
                Notice?.Invoke($"{name} is walking with you, so you stopped walking with them");
                Stop(buddies);
                return;
            }

            Transform them = buddy.transform;
            RecordPath(them.position);

            if (riding.Carrier != 0)
            {
                return;
            }

            // Their sit, unless they're up on someone's head (that's for riding, not sitting with).
            Sit sit = buddies.SitOf(Target);
            if (sit != null && (sit.Seat || buddies.Carrying(Target) != 0))
            {
                sit = null;
            }

            if (me.IsFocus)
            {
                _moving = false;
                _sitWalking = null;
                SwitchAlong(me, sit);
                Status = _satWith == Target ? $"Sitting with {name}" : $"Stand up to walk with {name}";
                return;
            }
            _satWith = 0;

            Vector3 at = move.transform.position;
            int slot = Slot(buddies);
            bool toSit = sit != null && sit != _sitDone && S.SitTogether.Value;
            Vector3 spot = toSit ? SitSpot(them, sit.Desk, slot) : WalkPlace(them, slot, at);

            // Fell far behind (they teleported, or you got lost): hop over.
            if (S.CatchUp.Value && Flat(them.position - at).magnitude > S.CatchUpDistance.Value &&
                CanMove(move, me) && !Game.KeysBusy())
            {
                HopTo(move, them, spot, name);
                return;
            }

            if (toSit)
            {
                Status = $"Going to sit with {name}";
                if (SitBeside(me, move, them, sit, spot, at, name))
                {
                    return;
                }
            }
            else
            {
                Status = S.Walk.Value == BuddyUp.WalkSpot.Behind ? $"Walking behind {name}" : $"Walking beside {name}";
            }

            if (!CanMove(move, me) || Game.KeysBusy())
            {
                _jump = false;
                return;
            }
            Steer(move, me, them, spot, at, name, toSit);
        }

        // ---- Walking ----

        private void Steer(PlayerMovementController move, PlayerFocusController me, Transform them, Vector3 spot,
            Vector3 at, string name, bool toSit)
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            Vector3 to = Flat(spot - at);
            float distance = to.magnitude;
            _moving = _moving ? distance > ArriveDistance : distance > SetOffDistance;

            if (!_moving)
            {
                _h = _v = 0f;
                _jumps = 0;
                _checkFrom = at;
                _checkAt = now;
                FaceAlong(move, them, toSit, dt);
                return;
            }

            // The keys, relative to the camera like walking.
            float push = Mathf.Clamp(distance / SlowDistance, MinInput, 1f);
            Vector3 local = Quaternion.Euler(0f, -Game.CameraYaw(), 0f) * (to / distance) * push;
            float ease = 1f - Mathf.Exp(-12f * dt);
            _h = Mathf.Lerp(_h, local.x, ease);
            _v = Mathf.Lerp(_v, local.z, ease);
            Horizontal = _h;
            Vertical = _v;
            Steering = true;

            // Stuck against something: jump, then hop over.
            if (now - _checkAt < StuckSeconds)
            {
                return;
            }
            bool stuck = Flat(at - _checkFrom).magnitude < StuckMoved && distance > StuckFar && now - _ownKeysAt > StuckSeconds;
            _checkFrom = at;
            _checkAt = now;
            if (!stuck)
            {
                _jumps = 0;
                return;
            }
            if (_jumps < JumpsBeforeHop)
            {
                _jumps++;
                _jump = !move.IsInWater;
                if (S.DebugLog.Value)
                {
                    Plugin.Log.LogInfo("Stuck, so jumping.");
                }
            }
            else if (S.CatchUp.Value)
            {
                HopTo(move, them, spot, name);
            }
        }

        // Standing still next to them: face the way they face (beside, or about to sit), or them (behind).
        private static void FaceAlong(PlayerMovementController move, Transform them, bool toSit, float dt)
        {
            if (move.IsAnimation)
            {
                return;
            }
            Quaternion want;
            if (toSit || S.Walk.Value == BuddyUp.WalkSpot.Beside)
            {
                want = Quaternion.Euler(0f, them.eulerAngles.y, 0f);
            }
            else
            {
                Vector3 look = Flat(them.position - move.transform.position);
                if (look.sqrMagnitude < 0.01f)
                {
                    return;
                }
                want = Quaternion.LookRotation(look);
            }
            Transform t = move.transform;
            if (Quaternion.Angle(t.rotation, want) > 2f)
            {
                t.rotation = Quaternion.Slerp(t.rotation, want, 1f - Mathf.Exp(-4f * dt));
            }
        }

        private void HopTo(PlayerMovementController move, Transform them, Vector3 spot, string name)
        {
            float now = Time.unscaledTime;
            if (now - _hoppedAt < HopEvery)
            {
                return;
            }
            _hoppedAt = now;
            _jumps = 0;
            _jump = false;

            // On the ground there (looking from just above their height, so a roof they're on counts),
            // unless a wall is between you; otherwise right where they stand.
            Vector3 chest = them.position + Vector3.up * 0.8f;
            if (Game.Blocked(chest, spot + Vector3.up * 0.8f))
            {
                spot = them.position;
            }
            else if (Game.Ground(new Vector3(spot.x, them.position.y + 1.5f, spot.z), 4f, out float ground))
            {
                spot.y = ground;
            }
            else
            {
                spot.y = them.position.y;
            }
            Game.Hop(move, spot, them.eulerAngles.y);
            _checkFrom = spot;
            _checkAt = now;
            Notice?.Invoke($"Hopped over to {name}");
            Plugin.Log.LogInfo("Hopped over to your buddy.");
        }

        // Where you walk: beside them (followers take turns right and left, further out each pair),
        // or along the way they walked, one gap per follower.
        private Vector3 WalkPlace(Transform them, int slot, Vector3 at)
        {
            float gap = S.WalkGap.Value;
            if (S.Walk.Value == BuddyUp.WalkSpot.Beside)
            {
                int side = slot % 2 == 0 ? 1 : -1;
                Vector3 spot = them.position + them.right * (side * gap * (slot / 2 + 1));
                // A wall between you and that spot: walk behind them instead.
                if (!Game.Blocked(them.position + Vector3.up * 0.8f, spot + Vector3.up * 0.8f))
                {
                    return spot;
                }
            }
            return PathPoint(them.position, gap * (slot + 1), at);
        }

        // Your seat beside theirs, facing the same way, at their height (so rows stay level).
        private static Vector3 SitSpot(Transform them, bool desk, int slot)
        {
            int side = slot % 2 == 0 ? 1 : -1;
            float spacing = desk ? DeskSpacing : PlainSpacing;
            Vector3 spot = them.position + them.right * (side * spacing * (slot / 2 + 1));
            // Not sunk into rising ground.
            if (Game.Ground(spot + Vector3.up * 0.6f, 0.6f, out float ground) && ground > spot.y + 0.15f)
            {
                spot.y = ground;
            }
            return spot;
        }

        // Your place among everyone walking with this buddy, by Steam ID so every screen agrees.
        private int Slot(Buddies buddies)
        {
            ulong me = buddies.Me;
            return buddies.FollowersOf(Target).Count(id => id < me);
        }

        private void RecordPath(Vector3 at)
        {
            if (_path.Count > 0 && (at - _path[0]).sqrMagnitude > Teleported * Teleported)
            {
                _path.Clear();
            }
            if (_path.Count == 0 || (at - _path[0]).sqrMagnitude >= PathStep * PathStep)
            {
                _path.Insert(0, at);
            }
            // Keep enough for a long line of followers.
            float keep = S.WalkGap.Value * 12f + 5f;
            float length = 0f;
            for (int i = 1; i < _path.Count; i++)
            {
                length += Vector3.Distance(_path[i - 1], _path[i]);
                if (length > keep)
                {
                    _path.RemoveRange(i + 1, _path.Count - i - 1);
                    break;
                }
            }
        }

        // The point this far back along the way they walked. Before they've walked that far, on the
        // line from the oldest point towards you (never further than you are).
        private Vector3 PathPoint(Vector3 from, float distance, Vector3 at)
        {
            float left = distance;
            Vector3 previous = from;
            foreach (Vector3 point in _path)
            {
                float step = Flat(point - previous).magnitude;
                if (step >= left && step > 0.0001f)
                {
                    return Vector3.Lerp(previous, point, left / step);
                }
                left -= step;
                previous = point;
            }
            Vector3 away = Flat(at - previous);
            float far = away.magnitude;
            return far < 0.01f ? at : previous + away / far * Mathf.Min(left, far);
        }

        // ---- Sitting down with them ----

        // Walks up to the spot and sits down there. Returns true while it's handling this frame
        // (sat down, or standing still waiting to).
        private bool SitBeside(PlayerFocusController me, PlayerMovementController move, Transform them, Sit sit,
            Vector3 spot, Vector3 at, string name)
        {
            float now = Time.unscaledTime;
            if (_sitWalking != sit)
            {
                _sitWalking = sit;
                _sitWalkSince = now;
                _nextSitTry = 0f;
            }
            if (now - sit.At > SitPatience)
            {
                _sitDone = sit;
                Notice?.Invoke($"Couldn't sit down with {name} in time");
                return false;
            }
            bool there = Flat(spot - at).magnitude < SitReach ||
                         (now - _sitWalkSince > SitWalkSeconds && Flat(them.position - at).magnitude < SitNear);
            if (!there || now < _nextSitTry)
            {
                return false;
            }

            SitResult result = Sitting.Sit(sit.Type, sit.Anim, out string why);
            switch (result)
            {
                case SitResult.Sat:
                    _sitDone = sit;
                    _satWith = Target;
                    _satAnim = sit.Anim;
                    // Desk Snap lines the desks up itself (it ran as you sat down). Otherwise sit
                    // exactly beside them.
                    if (!(sit.Desk && Game.DeskSnapOn()) && Game.SitsFree(me))
                    {
                        float yaw = them.eulerAngles.y;
                        _tracker.BeforeMove(me);
                        Game.PlaceSeated(me, spot, yaw);
                        Game.SetStandSpot(me, spot, yaw);
                    }
                    Plugin.Log.LogInfo("Sat down with your buddy.");
                    return true;

                case SitResult.Wait:
                    _nextSitTry = now + SitRetry;
                    if (S.DebugLog.Value)
                    {
                        Plugin.Log.LogInfo($"Waiting to sit down: {why}.");
                    }
                    return false;

                default:
                    _sitDone = sit;
                    Notice?.Invoke($"Couldn't sit down with {name}: {why}");
                    return false;
            }
        }

        // Sitting with them and they switched activity: switch too, unless you picked your own since.
        private void SwitchAlong(PlayerFocusController me, Sit sit)
        {
            if (_satWith != Target)
            {
                return;
            }
            if (Game.MyAnim() != _satAnim)
            {
                _satWith = 0;   // you picked something else
                return;
            }
            if (sit == null || sit == _sitDone || !S.SitTogether.Value || !Game.SitsFree(me) || Time.unscaledTime < _nextSitTry)
            {
                return;
            }
            SitResult result = Sitting.Sit(sit.Type, sit.Anim, out _);
            if (result == SitResult.Wait)
            {
                _nextSitTry = Time.unscaledTime + SitRetry;
                return;
            }
            _sitDone = sit;
            if (result == SitResult.Sat)
            {
                _satAnim = sit.Anim;
            }
            else
            {
                _satWith = 0;
            }
        }

        // Standing on your feet and free to walk: not mid-emote, fishing, in a bubble...
        private static bool CanMove(PlayerMovementController move, PlayerFocusController me)
        {
            if (me.IsFocus || move.IsAnimation || move.IsStair || move.MovementState == MovementState.Bubble)
            {
                return false;
            }
            FishingManager fishing = MonoSingleton<FishingManager>.I;
            return fishing == null || fishing.FishingState == FishingState.None;
        }

        private static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }
    }
}
