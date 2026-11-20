using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddyUp
{
    // Walking with a buddy: your character walks after them by itself, the way it would if you held
    // the movement keys (Patches.cs hands the game these steering values), so you get the real walk,
    // stairs, swimming and bumping into things. Your own keys always win; let go and you walk back
    // to them. Beside: next to them, one follower on each side. Behind: in a line along the way they
    // walked, so the line goes round corners the way they did.
    //
    // You walk with the front of your buddy's group (Buddies.GroupLeader): if they walk with or ride
    // on someone, you walk with that player, so a group has one leader and nobody trails a trailer.
    //
    // While they walk you go at their speed plus a little to close the gap, aimed a moment ahead of
    // where you see them (your screen shows them a moment late), so you keep up instead of trailing.
    //
    // When they sit down in free focus, you walk up beside them and sit down in the same activity
    // (SitTogether), facing the same way. Another mod sitting you down in their activity as they sit
    // (Emote Sync's focus join) counts too. When they stand up, so do you, unless you've picked your
    // own activity since.
    internal sealed class Following
    {
        // Close enough to stop, and how far they have to get before you set off again (while they
        // stand still).
        private const float ArriveDistance = 0.3f;
        private const float SetOffDistance = 0.7f;
        // Walking up to them while they stand still: slow down within this distance, but never
        // below this much of a key press.
        private const float SlowDistance = 1.5f;
        private const float MinInput = 0.45f;
        // Keeping up while they walk: their speed, plus this much of the gap each second.
        private const float CatchUpRate = 3f;
        // Aim this far ahead of where you see them, since the network shows them a moment late.
        private const float LeadSeconds = 0.1f;
        // Faster than this (metres a second) and they're on the move.
        private const float MovingSpeed = 0.3f;
        // How quickly their speed is followed (seconds); a move this big in one frame is a hop.
        private const float SpeedSmoothing = 0.1f;
        private const float Hopped = 3f;
        // Never faster than walking diagonally with the keys (the game doesn't slow that down).
        private const float MaxPush = 1.41f;
        // How quickly the steering follows what it wants (per second), so it doesn't twitch.
        private const float SteerEase = 15f;
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
        // Sat down in their activity this soon before their message came: another mod sat you down
        // with them (Emote Sync's message can beat ours).
        private const float JoinedBefore = 5f;
        // Their seat moved this far while they were on the move: their desk is flying (Desk Swarm),
        // so walk along under it instead of sitting down beside it.
        private const float SeatMoved = 2.5f;
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
        private bool _leaderWasMoving;
        private float _h;
        private float _v;
        private Vector3 _checkFrom;
        private float _checkAt;
        private int _jumps;
        private float _hoppedAt = -100f;

        // The leader's speed and direction on the ground, worked out from where you see them.
        private Vector3 _leaderAt;
        private bool _haveLeaderAt;
        private Vector3 _leaderVelocity;

        // Sitting down with them: the sit we last dealt with (sat down, or gave up on), and the one
        // we sat down with while you're still in it.
        private Sit _sitDone;
        private Sit _sitWalking;
        private float _sitWalkSince;
        private float _nextSitTry;
        private ulong _satWith;
        private int _satAnim;
        private float _mySatAt = -100f;
        // Their newest sit, where their seat was while it stood still, and since when it's been
        // possible to sit down beside it.
        private Sit _sitSeen;
        private Vector3 _seatAt;
        private bool _seatMoving;
        private float _sitFrom;

        public Following(SitTracker tracker)
        {
            _tracker = tracker;
        }

        // Something happened that the player should hear about.
        public event Action<string> Notice;

        // The buddy you chose to walk with, or 0.
        public ulong Target { get; private set; }

        // The one you actually walk with: the front of your buddy's group (often your buddy).
        public ulong Leader { get; private set; }

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

        // You sat down or switched activity (anyone's doing: you, this mod, another mod).
        public void NoteSatDown()
        {
            _mySatAt = Time.unscaledTime;
        }

        public void Start(ulong target, Buddies buddies)
        {
            if (Target == target)
            {
                return;
            }
            Target = target;
            Leader = 0;
            _moving = true;
            _jumps = 0;
            _checkAt = Time.unscaledTime;
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
            Leader = 0;
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
            ulong leader = buddies.GroupLeader(Target);
            if (leader == 0 || leader == buddies.Me)
            {
                // Going round in a circle: someone's about to make way (Buddies.GiveWayTo).
                Status = "Sorting out who leads...";
                return;
            }
            if (leader != Leader)
            {
                // A new front of the group (or just started): start afresh.
                Leader = leader;
                _path.Clear();
                _haveLeaderAt = false;
                _leaderVelocity = Vector3.zero;
                _leaderWasMoving = false;
                _sitDone = null;
                _sitWalking = null;
                _sitSeen = null;
                _satWith = 0;
            }
            string name = Game.PlayerName(leader);
            PlayerController buddy = Game.PlayerOf(leader);
            if (buddy == null || me == null || move == null)
            {
                Status = $"Looking for {name}...";
                return;
            }

            Transform them = buddy.transform;
            RecordPath(them.position);
            TrackSpeed(them.position);

            if (riding.Carrier != 0)
            {
                return;
            }

            // What they sat down to do, unless they're up on someone's head (that's for riding,
            // not sitting with) or their seat is on the move.
            Sit heard = buddies.SitOf(leader);
            Sit sit = heard;
            if (sit != null && (sit.Seat || buddies.Carrying(leader) != 0))
            {
                sit = null;
            }
            bool seatMoving = sit != null && SeatMoving(sit, them);
            if (seatMoving)
            {
                sit = null;
            }

            if (me.IsFocus)
            {
                _moving = false;
                _sitWalking = null;
                int slot = Slot(buddies);
                SatWithAnyway(me, them, sit, slot);
                SwitchAlong(me, sit);
                StandAlong(me, heard, name);
                Status = _satWith == Leader ? $"Sitting with {name}" : $"Stand up to walk with {name}";
                return;
            }
            _satWith = 0;

            Vector3 at = move.transform.position;
            int mySlot = Slot(buddies);
            bool toSit = sit != null && sit != _sitDone && S.SitTogether.Value;
            Vector3 ahead = Vector3.zero;
            Vector3 spot = toSit ? SitSpot(them, sit.Desk, mySlot) : WalkPlace(them, mySlot, at, out ahead);

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
            else if (seatMoving)
            {
                Status = $"Walking along under {name}";
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
            Steer(move, them, spot, ahead, at, name, toSit);
        }

        // ---- Walking ----

        // `ahead` is how the spot moves while they walk (metres a second, on the ground).
        private void Steer(PlayerMovementController move, Transform them, Vector3 spot, Vector3 ahead,
            Vector3 at, string name, bool toSit)
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            float speed = Game.WalkSpeed(move);
            bool leaderMoving = ahead.magnitude > MovingSpeed;
            Vector3 to = Flat(spot + ahead * LeadSeconds - at);
            float distance = to.magnitude;
            if (leaderMoving)
            {
                _moving = true;
            }
            else if (_leaderWasMoving)
            {
                // They just stopped: stay put if you're about there, rather than stepping back a bit.
                _moving = distance > SetOffDistance;
            }
            else
            {
                _moving = _moving ? distance > ArriveDistance : distance > SetOffDistance;
            }
            _leaderWasMoving = leaderMoving;

            if (!_moving)
            {
                _h = _v = 0f;
                _jumps = 0;
                _checkFrom = at;
                _checkAt = now;
                FaceAlong(move, them, toSit, dt);
                return;
            }

            // How fast you want to go, as a velocity: their pace plus some of the gap while they
            // walk; otherwise straight to the spot, slowing down as you get there.
            Vector3 want = leaderMoving
                ? ahead + to * CatchUpRate
                : to / distance * speed * Mathf.Clamp(distance / SlowDistance, MinInput, 1f);
            float most = speed * MaxPush;
            if (want.sqrMagnitude > most * most)
            {
                want = want.normalized * most;
            }
            // As keys, relative to the camera like walking.
            Vector3 local = Quaternion.Euler(0f, -Game.CameraYaw(), 0f) * (want / speed);
            float ease = 1f - Mathf.Exp(-SteerEase * dt);
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

        // Their speed and direction on the ground, smoothed (the network moves them in small steps).
        private void TrackSpeed(Vector3 position)
        {
            float dt = Time.deltaTime;
            Vector3 flat = Flat(position);
            if (!_haveLeaderAt || dt <= 0f || (flat - _leaderAt).sqrMagnitude > Hopped * Hopped)
            {
                _leaderVelocity = Vector3.zero;
            }
            else
            {
                Vector3 step = (flat - _leaderAt) / dt;
                _leaderVelocity = Vector3.Lerp(_leaderVelocity, step, 1f - Mathf.Exp(-dt / SpeedSmoothing));
            }
            _leaderAt = flat;
            _haveLeaderAt = true;
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
        // or along the way they walked, one gap per follower. `ahead` is how that spot moves.
        private Vector3 WalkPlace(Transform them, int slot, Vector3 at, out Vector3 ahead)
        {
            float gap = S.WalkGap.Value;
            if (S.Walk.Value == BuddyUp.WalkSpot.Beside)
            {
                int side = slot % 2 == 0 ? 1 : -1;
                Vector3 spot = them.position + them.right * (side * gap * (slot / 2 + 1));
                // A wall between you and that spot: walk behind them instead.
                if (!Game.Blocked(them.position + Vector3.up * 0.8f, spot + Vector3.up * 0.8f))
                {
                    ahead = _leaderVelocity;
                    return spot;
                }
            }
            Vector3 point = PathPoint(them.position, gap * (slot + 1), at, out Vector3 along);
            ahead = along * _leaderVelocity.magnitude;
            return point;
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

        // Your place among everyone walking with this leader, by Steam ID so every screen agrees.
        private int Slot(Buddies buddies)
        {
            ulong me = buddies.Me;
            int slot = 0;
            foreach (ulong id in buddies.WalkersWith(Leader))
            {
                if (id < me)
                {
                    slot++;
                }
            }
            return slot;
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

        // The point this far back along the way they walked, and which way the path runs there
        // (towards them). Before they've walked that far, on the line from the oldest point towards
        // you (never further than you are).
        private Vector3 PathPoint(Vector3 from, float distance, Vector3 at, out Vector3 along)
        {
            float left = distance;
            Vector3 previous = from;
            foreach (Vector3 point in _path)
            {
                float step = Flat(point - previous).magnitude;
                if (step >= left && step > 0.0001f)
                {
                    along = Flat(previous - point) / step;
                    return Vector3.Lerp(previous, point, left / step);
                }
                left -= step;
                previous = point;
            }
            along = _leaderVelocity.sqrMagnitude > 0.0001f ? _leaderVelocity.normalized : Vector3.zero;
            Vector3 away = Flat(at - previous);
            float far = away.magnitude;
            return far < 0.01f ? at : previous + away / far * Mathf.Min(left, far);
        }

        // ---- Sitting down with them ----

        // True while their seat is on the move (their desk flying with Desk Swarm). Where the seat
        // stands still is noted, so a single move (Desk Snap lining their desk up) doesn't count.
        private bool SeatMoving(Sit sit, Transform them)
        {
            float now = Time.unscaledTime;
            if (sit != _sitSeen)
            {
                _sitSeen = sit;
                _seatAt = them.position;
                _seatMoving = false;
                _sitFrom = now;
            }
            if (_leaderVelocity.magnitude <= MovingSpeed)
            {
                if (_seatMoving)
                {
                    // Landed: sitting down beside them is on again, with fresh patience.
                    _seatMoving = false;
                    _sitFrom = now;
                }
                _seatAt = them.position;
            }
            else if ((them.position - _seatAt).sqrMagnitude > SeatMoved * SeatMoved)
            {
                _seatMoving = true;
            }
            return _seatMoving;
        }

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
            if (now - _sitFrom > SitPatience)
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
                    _satWith = Leader;
                    _satAnim = sit.Anim;
                    PlaceBeside(me, them, sit, spot);
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

        // Exactly beside them, facing the same way. Desk Snap lines desks up itself (it ran as you
        // sat down), so desk activities are left to it when it's on.
        private void PlaceBeside(PlayerFocusController me, Transform them, Sit sit, Vector3 spot)
        {
            if ((sit.Desk && Game.DeskSnapOn()) || !Game.SitsFree(me))
            {
                return;
            }
            float yaw = them.eulerAngles.y;
            _tracker.BeforeMove(me);
            Game.PlaceSeated(me, spot, yaw);
            Game.SetStandSpot(me, spot, yaw);
        }

        // Another mod sat you down in their activity as they sat down (Emote Sync's focus join):
        // that's sitting down together, so you're moved beside them like when this mod does it.
        private void SatWithAnyway(PlayerFocusController me, Transform them, Sit sit, int slot)
        {
            if (sit == null || sit == _sitDone || _satWith == Leader || !S.SitTogether.Value ||
                _mySatAt < sit.At - JoinedBefore || !Game.SitsFree(me) || Game.MyAnim() != sit.Anim)
            {
                return;
            }
            _sitDone = sit;
            _satWith = Leader;
            _satAnim = sit.Anim;
            PlaceBeside(me, them, sit, SitSpot(them, sit.Desk, slot));
            Plugin.Log.LogInfo("Sat down with your buddy (another mod sat you down).");
        }

        // Sitting with them and they switched activity: switch too, unless you picked your own since.
        private void SwitchAlong(PlayerFocusController me, Sit sit)
        {
            if (_satWith != Leader)
            {
                return;
            }
            int anim = Game.MyAnim();
            if (sit != null && sit != _sitDone && anim == sit.Anim)
            {
                // Already switched (Emote Sync got there first).
                _sitDone = sit;
                _satAnim = anim;
                return;
            }
            if (anim != _satAnim)
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

        // Sat down with them and they stood up: you stand up too and walk on together, unless
        // you've picked your own activity since (or are flying your desk with Desk Swarm).
        private void StandAlong(PlayerFocusController me, Sit heard, string name)
        {
            if (_satWith != Leader || heard != null)
            {
                return;
            }
            _satWith = 0;
            if (!S.SitTogether.Value || Game.MyAnim() != _satAnim || !Game.SitsFree(me) || Game.DeskSwarmPiloting())
            {
                return;
            }
            Game.StandUp();
            Notice?.Invoke($"{name} stood up, so you did too");
            Plugin.Log.LogInfo("Your buddy stood up, so you did too.");
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
