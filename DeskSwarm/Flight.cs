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

        private static Settings S => Plugin.Settings;

        private int _ground;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

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
                Follow(followed, spot, leader.transform, dt);
                InFormation = true;
            }
            else if (Piloting)
            {
                Begin(me);
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

            _bob = Mathf.MoveTowards(_bob, 1f, dt);
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
            float floor = Floor(_base) + BobHeight * _bob;
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
        // sea. Lower ground doesn't matter: you just float above it.
        private float Floor(Vector3 at)
        {
            if (_ground == 0)
            {
                // Everything solid, water included, but not players, areas or invisible triggers.
                _ground = ~LayerMask.GetMask("Ignore Raycast", "Player", "Area", "AreaTrigger",
                    "Customization", "IsGroundedChecker", "DeskRender");
            }
            float floor = Game.SeaLevel + 0.1f;
            int hits = Physics.RaycastNonAlloc(at + Vector3.up * GroundSearch, Vector3.down, _hits, GroundSearch, _ground,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.point.y > floor && hit.collider.GetComponentInParent<PlayerController>() == null &&
                    hit.collider.GetComponentInParent<PetController>() == null)
                {
                    floor = hit.point.y;
                }
            }
            return floor;
        }
    }
}
