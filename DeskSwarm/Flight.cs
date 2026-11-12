using System.Collections.Generic;
using UnityEngine;

namespace DeskSwarm
{
    // Moves you while you sit: flying with the movement keys, or to your spot in the swarm you follow.
    // Only ever your own character. Your game sends your position to everyone as it does while you
    // walk, so everyone sees the desk fly, mod or not.
    //
    // While you sit, the game ignores the movement keys and skips gravity (PlayerMovementController
    // Update), and nothing puts you back until you stand up, which happens where you are now.
    internal sealed class Flight
    {
        // How quickly a follower closes in on its spot, in seconds (lower is snappier).
        private const float FollowSmoothing = 0.45f;
        private const float FollowSpeed = 14f;
        // Drone wobble: how far up and down, and how long one bob takes.
        private const float BobHeight = 0.06f;
        private const float BobSeconds = 2.4f;
        // Most the desk tips, in degrees, and how much per metre per second.
        private const float MaxTilt = 12f;
        private const float TiltPerSpeed = 1.6f;
        // How far above your spot to look for ground to stay on top of. Enough for any slope at full
        // speed, but under most roofs and treetops, so you don't pop up on top of them.
        private const float GroundSearch = 1.2f;
        private const float TrailStep = 0.25f;
        // Landing shapes: a desk still on its way to its spot flies this high per metre it has to go
        // (so it hops over the others), up to the most, and has landed once it's this low.
        private const float LiftPerMetre = 0.4f;
        private const float MaxLift = 2.5f;
        private const float Touchdown = 0.15f;
        // Where to look for the ground a desk lands on: from this far above the leader's feet (under
        // the roof of a building they're in), or, if that finds nothing near, from higher up (the
        // spot is on a hill above them).
        private const float LandSearchAbove = 2.5f;
        private const float LandSearchHigh = 25f;
        private const float LandSearchNear = 3f;
        private const float LandSearchDepth = 200f;
        // Flying through buildings, only the land and water stop you. The game doesn't mark which is
        // which, but the land reaches down into the sea (islands, hills, mountains, rocks, shores, the
        // plaza; the sea's surface is at y -0.5), and in the game's world no building, bridge, dock or
        // lighthouse does. A few posts and tree trunks do, so it also has to be this wide. Land up in
        // the air (the Halloween island) is named as ground.
        private const float SeaBed = -1f;
        private const float RootedWidth = 2f;
        private static readonly string[] GroundNames = { "Ground", "Island", "Hill", "Mountain", "Shore" };

        private static Settings S => Plugin.Settings;

        private int _ground;
        private int _water = -1;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        // Whether each collider met so far is land or water (IsLand).
        private readonly Dictionary<Collider, bool> _isLand = new Dictionary<Collider, bool>();

        // This sit: who's sitting, whether we've moved them, and the areas they sat down in.
        private PlayerFocusController _sitter;
        private bool _flown;
        private HashSet<Collider> _sitAreas;

        // The pose we're flying, without the bob and tilt.
        private bool _managing;
        private Vector3 _base;
        private Vector3 _velocity;
        private float _yaw;
        private float _pitch;
        private float _roll;
        private float _bob;   // 0 to 1: how much of the bob is showing
        // Flying through buildings: yours while you pilot, the leader's while you follow. Kept while
        // you slow to a stop.
        private bool _through;

        // Where the leader has been, newest first, for the Trail formation.
        private readonly List<Vector3> _path = new List<Vector3>();
        private ulong _pathLeader;

        // Flying with the keys (not following).
        public bool Piloting
        {
            get => IsPiloting;
            private set => IsPiloting = value;
        }

        // The same, for the input patch (Patches.cs).
        public static bool IsPiloting { get; private set; }

        // Moving to your spot in a swarm this frame.
        public bool InFormation { get; private set; }

        // Waiting to see the leader on your screen.
        public bool LeaderMissing { get; private set; }

        // Sitting on the ground at your spot in a landing shape (or just about to touch down).
        public bool Landed { get; private set; }

        public void StartPiloting(PlayerFocusController me)
        {
            if (Piloting || !Game.CanFly(me))
            {
                return;
            }
            Piloting = true;
            Game.CameraDrift(false);
            Plugin.Log.LogInfo("Took off.");
        }

        public void StopPiloting(PlayerFocusController me)
        {
            if (!Piloting)
            {
                return;
            }
            Piloting = false;
            // Still sitting: the camera drifts again, as the game does while you sit.
            if (Game.CanFly(me))
            {
                Game.CameraDrift(true);
            }
            Plugin.Log.LogInfo("Stopped flying.");
        }

        // After you pick a focus activity (also when switching while flying): the game starts the
        // camera drifting again.
        public void OnFocusPicked()
        {
            if (Piloting)
            {
                Game.CameraDrift(false);
            }
        }

        public void Update(PlayerFocusController me, Swarms swarms)
        {
            InFormation = false;
            LeaderMissing = false;
            Landed = false;

            bool sitting = me != null && me.IsFocus;
            if (_sitter != null && (_sitter != me || !sitting))
            {
                EndSit();
            }
            if (!Game.CanFly(me))
            {
                // At a seat spot, or not sitting: nothing to fly.
                Piloting = false;
                return;
            }
            if (_sitter == null)
            {
                _sitter = me;
                _flown = false;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Swarm followed = swarms.Followed;
            int spot = swarms.MySpot;
            if (followed != null && spot >= 0)
            {
                if (Piloting)
                {
                    StopPiloting(me);
                }
                PlayerController leader = Game.PlayerOf(followed.Leader);
                if (leader == null)
                {
                    LeaderMissing = true;
                    Settle(me, dt);
                    return;
                }
                Begin(me);
                _through = followed.Through;
                Follow(followed, spot, leader.transform, dt);
                InFormation = true;
            }
            else if (Piloting)
            {
                Begin(me);
                _through = S.ThroughBuildings.Value;
                Pilot(dt);
            }
            else if (_managing)
            {
                Settle(me, dt);
                return;
            }
            else
            {
                return;
            }

            // No drone wobble once landed, so the desk sits still on the ground.
            _bob = Mathf.MoveTowards(_bob, Landed ? 0f : 1f, dt);
            Apply(me, dt, follower: InFormation, spot);
        }

        // Called when you stand up or leave: if you flew, you've left the areas where you sat down.
        private void EndSit()
        {
            PlayerFocusController sitter = _sitter;
            _sitter = null;
            _managing = false;
            _path.Clear();
            if (Piloting)
            {
                Piloting = false;
                Plugin.Log.LogInfo("Stood up, so stopped flying.");
            }
            if (_flown && sitter != null && !sitter.IsFocus)
            {
                AreaExits.LeaveOld(sitter, _sitAreas);
            }
            _flown = false;
            _sitAreas = null;
        }

        // The first move of a flight picks up from where you are.
        private void Begin(PlayerFocusController me)
        {
            if (!_flown)
            {
                _flown = true;
                _sitAreas = AreaExits.At(me, me.transform.position);
            }
            if (!_managing)
            {
                _managing = true;
                Transform t = me.transform;
                _base = t.position;
                _yaw = t.eulerAngles.y;
                _velocity = Vector3.zero;
                _pitch = 0f;
                _roll = 0f;
                _bob = 0f;
            }
        }

        private void Pilot(float dt)
        {
            float forward = 0f, across = 0f, up = 0f;
            if (!Game.KeysBusy())
            {
                // The game's own movement keys (whatever they're set to), already eased in and out.
                InputManager input = MonoSingleton<InputManager>.I;
                forward = input.Vertical;
                across = input.Horizontal;
                up = (Input.GetKey(S.UpKey.Value) ? 1f : 0f) - (Input.GetKey(S.DownKey.Value) ? 1f : 0f);
            }

            // Relative to the camera, like walking.
            Quaternion camera = Quaternion.Euler(0f, Game.CameraYaw(), 0f);
            Vector3 flat = camera * new Vector3(across, 0f, forward);
            if (flat.sqrMagnitude > 1f)
            {
                flat.Normalize();
            }
            Vector3 wanted = flat * S.FlySpeed.Value + Vector3.up * (up * S.ClimbSpeed.Value);
            _velocity = Vector3.Lerp(_velocity, wanted, 1f - Mathf.Exp(-3f * dt));
            _base += _velocity * dt;

            // Turn to face the way you're flying, like walking does.
            var moving = new Vector3(_velocity.x, 0f, _velocity.z);
            if (moving.sqrMagnitude > 0.16f)
            {
                _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(moving.x, moving.z) * Mathf.Rad2Deg, 1f - Mathf.Exp(-5f * dt));
            }
        }

        private void Follow(Swarm swarm, int spot, Transform leader, float dt)
        {
            Vector3 leaderAt = leader.position;
            float leaderYaw = leader.eulerAngles.y;
            RecordPath(swarm, leaderAt);

            Vector3 target = Formations.Place(swarm.Formation, spot, swarm.Members.Count, swarm.Spread,
                leaderAt, leaderYaw, Game.SharedTime, _path, out float yaw);

            if (Formations.Lands(swarm.Formation))
            {
                // Come down on the ground under the spot. While it's still some way off (just
                // picked, or the leader walked on), fly a little above the ground, and touch down
                // as it arrives, so the desks take off and land as the leader moves and stops.
                float ground = GroundUnder(target, leaderAt.y);
                float away = new Vector2(target.x - _base.x, target.z - _base.z).magnitude;
                float lift = Mathf.Min(away * LiftPerMetre, MaxLift);
                target.y = ground + lift;
                Landed = lift < Touchdown;
            }

            // Far away (just joined, or the leader teleported): fly faster, so it never takes long.
            float distance = Vector3.Distance(_base, target);
            _base = Vector3.SmoothDamp(_base, target, ref _velocity, FollowSmoothing, Mathf.Max(FollowSpeed, distance * 1.5f), dt);
            _yaw = Mathf.LerpAngle(_yaw, yaw, 1f - Mathf.Exp(-4f * dt));
        }

        // Slows to a stop and levels out after you stop flying or leave a swarm, then lets go.
        private void Settle(PlayerFocusController me, float dt)
        {
            if (!_managing)
            {
                return;
            }
            float ease = Mathf.Exp(-4f * dt);
            _velocity *= ease;
            _base += _velocity * dt;
            _bob = Mathf.MoveTowards(_bob, 0f, dt);
            Apply(me, dt, follower: false, 0);

            if (_velocity.sqrMagnitude < 0.0004f && Mathf.Abs(_pitch) < 0.2f && Mathf.Abs(_roll) < 0.2f && _bob <= 0f)
            {
                Game.MoveSeated(me, _base, Quaternion.Euler(0f, _yaw, 0f), _yaw);
                _managing = false;
            }
        }

        // Puts you at the flown pose, on top of the ground or water, with a little bob and tilt.
        private void Apply(PlayerFocusController me, float dt, bool follower, int spot)
        {
            // Room for the bob, so it never dips into the ground.
            float floor = Floor(_base, _through) + BobHeight * _bob;
            if (_base.y < floor)
            {
                _base.y = floor;
                _velocity.y = Mathf.Max(0f, _velocity.y);
            }

            // Tip into the way it's flying: nose down going forward, leaning into sideways moves.
            float pitch = 0f, roll = 0f;
            if (S.Tilt.Value)
            {
                Quaternion facing = Quaternion.Euler(0f, _yaw, 0f);
                Vector3 local = Quaternion.Inverse(facing) * _velocity;
                pitch = Mathf.Clamp(local.z * TiltPerSpeed, -MaxTilt, MaxTilt);
                roll = Mathf.Clamp(-local.x * TiltPerSpeed, -MaxTilt, MaxTilt);
            }
            float tiltEase = 1f - Mathf.Exp(-5f * dt);
            _pitch = Mathf.Lerp(_pitch, pitch, tiltEase);
            _roll = Mathf.Lerp(_roll, roll, tiltEase);

            // Each spot bobs out of step with its neighbours, on the shared clock.
            float phase = follower ? spot * 1.7f : 0f;
            float bob = _bob * BobHeight * Mathf.Sin((float)(Game.SharedTime % BobSeconds / BobSeconds) * 2f * Mathf.PI + phase);

            Quaternion rotation = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(_pitch, 0f, _roll);
            Game.MoveSeated(me, _base + Vector3.up * bob, rotation, _yaw);
        }

        private void RecordPath(Swarm swarm, Vector3 leaderAt)
        {
            if (_pathLeader != swarm.Leader || (_path.Count > 0 && (leaderAt - _path[0]).sqrMagnitude > 25f * 25f))
            {
                // A new leader, or they teleported: start a new path.
                _pathLeader = swarm.Leader;
                _path.Clear();
            }
            if (_path.Count == 0 || (leaderAt - _path[0]).sqrMagnitude >= TrailStep * TrailStep)
            {
                _path.Insert(0, leaderAt);
            }

            // Drop what the last desk no longer needs.
            float keep = Formations.PathNeeded(swarm.Members.Count, swarm.Spread);
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

        // The top of the ground or water under this spot (looking from a little above it), or the
        // sea. Lower ground doesn't matter: you just float above it. Flying through buildings, only
        // land and water count; otherwise anything solid does.
        private float Floor(Vector3 at, bool through)
        {
            return Highest(at + Vector3.up * GroundSearch, GroundSearch, landOnly: through);
        }

        // The ground or water a landing desk comes down on. Looking from just above the leader's
        // height finds the floor of a building they're in rather than its roof. But a ray that
        // starts inside a hill goes straight through it, so if that finds nothing near the
        // leader's height, look again from higher up, for a hill or rock the spot is on.
        private float GroundUnder(Vector3 spot, float leaderY)
        {
            float ground = Highest(new Vector3(spot.x, leaderY + LandSearchAbove, spot.z), LandSearchDepth);
            if (ground < leaderY - LandSearchNear)
            {
                float top = leaderY + LandSearchHigh;
                ground = Mathf.Max(ground, Highest(new Vector3(spot.x, top, spot.z), top - (leaderY - LandSearchNear)));
            }
            return ground;
        }

        // The highest ground or water up to `depth` below `from`, or the sea. With `landOnly`,
        // buildings, bridges and everything else on the land are left out.
        private float Highest(Vector3 from, float depth, bool landOnly = false)
        {
            if (_ground == 0)
            {
                // Everything solid, water included, but not players, areas or invisible triggers.
                _ground = ~LayerMask.GetMask("Ignore Raycast", "Player", "Area", "AreaTrigger",
                    "Customization", "IsGroundedChecker", "DeskRender");
                _water = LayerMask.NameToLayer("Water");
            }
            float floor = Game.SeaLevel + 0.1f;
            int hits = Physics.RaycastNonAlloc(from, Vector3.down, _hits, depth, _ground,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.point.y > floor && (!landOnly || IsLand(hit.collider)) &&
                    hit.collider.GetComponentInParent<PlayerController>() == null &&
                    hit.collider.GetComponentInParent<PetController>() == null)
                {
                    floor = hit.point.y;
                }
            }
            return floor;
        }

        // Land or water, which you can't fly through even while flying through buildings (see SeaBed).
        private bool IsLand(Collider collider)
        {
            if (_isLand.TryGetValue(collider, out bool land))
            {
                return land;
            }
            if (_isLand.Count > 4096)
            {
                // Colliders from an earlier visit to the world.
                _isLand.Clear();
            }

            GameObject obj = collider.gameObject;
            Bounds bounds = collider.bounds;
            land = obj.layer == _water ||
                (bounds.min.y < SeaBed && Mathf.Max(bounds.size.x, bounds.size.z) >= RootedWidth);
            for (int i = 0; !land && i < GroundNames.Length; i++)
            {
                land = obj.name.IndexOf(GroundNames[i], System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
            _isLand[collider] = land;
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"{obj.name}: {(land ? "land, can't fly through" : "can fly through")}");
            }
            return land;
        }
    }
}
