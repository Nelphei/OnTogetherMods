using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuddyUp
{
    // How you get off a head.
    internal enum Dismount
    {
        Stay,     // stay where you are, still sitting (Desk Swarm took off)
        Beside,   // beside your buddy: on your feet if you climbed on standing, else still sitting
        Stand,    // on your feet beside your buddy (you're off to walk)
    }

    // Riding on a buddy's head: you sit (in the riding activity, or whatever you were already doing)
    // and your game puts you on top of their head bone every frame, so you go wherever they go and
    // bob along with their animations. Only ever your own character: your game sends your position
    // to everyone as it does while you walk, so players without the mod see you up there too (a
    // little behind while your buddy walks, since your spot reaches them one trip later).
    //
    // Players with the mod also put every rider they know of on the head themselves, so on their
    // screens riders stay right on top. Riders on riders (a totem pole) are placed bottom first.
    //
    // While you sit, the game skips gravity and ignores the movement keys, and standing up puts you
    // at the stand-up spot, which this keeps beside your buddy: getting up hops you off next to them.
    // Getting off any other way puts you back the way you were: on your feet if you were standing
    // when you climbed on, or still sitting (beside them) if you were already sitting.
    internal sealed class Riding
    {
        // How long your buddy can be missing (loading in, teleporting) before you get off.
        private const float MissingSeconds = 3f;
        // How far beside your buddy you land when you hop off.
        private const float HopOffStep = 0.7f;
        // Another player's rider is only put on the head while the network has them this near it, so
        // a missed "got off" message never leaves someone stuck up there on your screen.
        private const float GlueReach = 3f;

        private static Settings S => Plugin.Settings;

        private readonly SitTracker _tracker;
        private float _missingSince = -1f;
        private Vector3 _standSpot;
        private float _standYaw;
        private bool _haveStandSpot;
        // You were standing when you climbed on, so the mod sat you down in this riding pose.
        private bool _satToRide;
        private int _rideAnim;

        // Riders in the order they're placed, and whether each other player's rider is on a head on
        // this screen (decided in LateUpdate, used again just before drawing).
        private readonly List<ulong> _order = new List<ulong>();
        private readonly Dictionary<ulong, bool> _glued = new Dictionary<ulong, bool>();
        private PlayerController _mePlayer;

        public Riding(SitTracker tracker)
        {
            _tracker = tracker;
        }

        // Something happened that the player should hear about.
        public event Action<string> Notice;

        // The buddy whose head you're on, or 0.
        public ulong Carrier { get; private set; }

        // Waiting to see your buddy on your screen again.
        public bool CarrierMissing => Carrier != 0 && _missingSince >= 0f;

        // Climbs onto your buddy's head (or over to another head). Returns why not, or null when
        // you're on; `wait` says the reason will pass (a menu open, the stairs...).
        //
        // Riding on someone who goes along with you (rides on you, walks with you) is fine: yours is
        // newer, so they make way (Buddies.GiveWayTo).
        public string Start(PlayerFocusController me, ulong carrier, Buddies buddies, out bool wait)
        {
            wait = false;
            if (me == null || !buddies.InLobby)
            {
                return "Join a session first";
            }
            string name = Game.PlayerName(carrier);
            if (!buddies.IsBuddy(carrier))
            {
                return $"{name} isn't your buddy";
            }
            if (Game.PlayerOf(carrier) == null)
            {
                return $"Can't see {name} here";
            }
            if (Game.DeskSwarmPiloting())
            {
                return "Stop flying your desk first";
            }
            if (me.IsFocus && !Game.SitsFree(me))
            {
                return "Get up from the seat first";
            }
            if (!me.IsFocus)
            {
                SitResult sat = Sitting.SitToRide(out string why, out int anim);
                if (sat != SitResult.Sat)
                {
                    wait = sat == SitResult.Wait;
                    return $"Can't climb on: {why}";
                }
                _satToRide = true;
                _rideAnim = anim;
            }
            else if (Carrier == 0)
            {
                // Already sitting: you keep your activity up there. (Moving over to another head
                // keeps what you had.)
                _satToRide = false;
            }

            Carrier = carrier;
            _missingSince = -1f;
            _haveStandSpot = false;
            buddies.SendRide(carrier, S.RideHeight.Value);
            Plugin.Log.LogInfo($"Riding on {LobbyChannel.NameOf(new Steamworks.CSteamID(carrier))}'s head.");
            return null;
        }

        // Gets off (see Dismount).
        public void Stop(PlayerFocusController me, Buddies buddies, Dismount how)
        {
            if (Carrier == 0)
            {
                return;
            }
            Carrier = 0;
            _missingSince = -1f;
            buddies.SendRide(0, 0f);
            if (how != Dismount.Stay && Game.SitsFree(me))
            {
                if (_haveStandSpot)
                {
                    Game.PlaceSeated(me, _standSpot, _standYaw);
                    Game.SetStandSpot(me, _standSpot, _standYaw);
                }
                // Still in the pose you were sat down in to ride (not something you picked since).
                bool ridePose = _satToRide && Game.MyAnim() == _rideAnim;
                if (how == Dismount.Stand || ridePose)
                {
                    Game.StandUp();
                }
            }
            Plugin.Log.LogInfo("Got off.");
        }

        // The height changed (the window's buttons): riders on other screens move too.
        public void HeightChanged(Buddies buddies)
        {
            if (Carrier != 0)
            {
                buddies.SendRide(Carrier, S.RideHeight.Value);
            }
        }

        // Puts every rider on their buddy's head: in LateUpdate (late = true: after the animations,
        // before the camera moves), and again just before drawing, in case the network moved anyone
        // after that.
        public void Glue(PlayerFocusController me, Buddies buddies, bool late)
        {
            _order.Clear();
            foreach (KeyValuePair<ulong, Ride> ride in buddies.Rides)
            {
                _order.Add(ride.Key);
            }
            if (Carrier != 0)
            {
                _order.Add(buddies.Me);
            }
            if (_order.Count == 0)
            {
                return;
            }
            if (_order.Count > 1)
            {
                // Bottom of each stack first, so whoever rides on a rider goes where they are now.
                _order.Sort((a, b) => Depth(a, buddies).CompareTo(Depth(b, buddies)));
            }
            foreach (ulong rider in _order)
            {
                if (rider == buddies.Me)
                {
                    GlueMe(me, buddies, late);
                }
                else if (buddies.Rides.TryGetValue(rider, out Ride ride))
                {
                    if (InCircle(rider, buddies))
                    {
                        // Two riding on each other for a moment (one is about to get off): gluing
                        // them would send both up into the sky.
                        _glued.Remove(rider);
                        continue;
                    }
                    GlueOther(me, rider, ride, buddies, late);
                }
            }
        }

        // True if this rider is (through others) under themselves.
        private bool InCircle(ulong rider, Buddies buddies)
        {
            ulong carrier = CarrierOf(rider, buddies);
            for (int i = 0; i < 32 && carrier != 0; i++)
            {
                if (carrier == rider)
                {
                    return true;
                }
                carrier = CarrierOf(carrier, buddies);
            }
            return false;
        }

        private ulong CarrierOf(ulong rider, Buddies buddies)
        {
            return rider == buddies.Me ? Carrier : buddies.Carrying(rider);
        }

        private void GlueMe(PlayerFocusController me, Buddies buddies, bool late)
        {
            if (!Game.SitsFree(me))
            {
                if (late)
                {
                    // You stood up (the game put you at the stand-up spot beside your buddy), left the
                    // world, or moved to a seat spot.
                    Carrier = 0;
                    buddies.SendRide(0, 0f);
                    Plugin.Log.LogInfo("Hopped off.");
                }
                return;
            }

            PlayerController carrier = PlayerAt(Carrier, me, buddies);
            if (carrier == null)
            {
                if (late)
                {
                    if (_missingSince < 0f)
                    {
                        _missingSince = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - _missingSince > MissingSeconds)
                    {
                        Notice?.Invoke($"Lost sight of {Game.PlayerName(Carrier)}, so you got off");
                        Stop(me, buddies, Dismount.Beside);
                    }
                }
                return;
            }
            _missingSince = -1f;
            if (InCircle(buddies.Me, buddies))
            {
                // You and they climbed on each other: wait for one of you to get off.
                return;
            }

            float yaw = carrier.transform.eulerAngles.y;
            Vector3 spot = Game.Head(carrier).position + Vector3.up * S.RideHeight.Value;
            if (late)
            {
                _tracker.BeforeMove(me);
            }
            Game.PlaceSeated(me, spot, yaw, moveCamera: late);
            if (late)
            {
                _standSpot = HopOffSpot(carrier.transform);
                _standYaw = yaw;
                _haveStandSpot = true;
                Game.SetStandSpot(me, _standSpot, _standYaw);
            }
        }

        private void GlueOther(PlayerFocusController me, ulong rider, Ride ride, Buddies buddies, bool late)
        {
            PlayerController riderPlayer = Game.PlayerOf(rider);
            PlayerController carrier = PlayerAt(ride.Carrier, me, buddies);
            if (riderPlayer == null || carrier == null || riderPlayer == carrier)
            {
                _glued.Remove(rider);
                return;
            }
            Vector3 spot = Game.Head(carrier).position + Vector3.up * ride.Height;
            Transform t = riderPlayer.transform;
            if (late)
            {
                // Here the rider is where the network put them this frame.
                bool near = (t.position - spot).sqrMagnitude <= GlueReach * GlueReach;
                _glued[rider] = near;
                if (!near)
                {
                    return;
                }
            }
            else if (!_glued.TryGetValue(rider, out bool glued) || !glued)
            {
                return;
            }
            t.SetPositionAndRotation(spot, Quaternion.Euler(0f, carrier.transform.eulerAngles.y, 0f));
        }

        // How many riders are under this one (0 for a rider on someone standing on the ground).
        private int Depth(ulong rider, Buddies buddies)
        {
            int depth = 0;
            ulong carrier = CarrierOf(rider, buddies);
            while (carrier != 0 && depth < 16)
            {
                depth++;
                carrier = CarrierOf(carrier, buddies);
            }
            return depth;
        }

        // A player's character, you included.
        private PlayerController PlayerAt(ulong id, PlayerFocusController me, Buddies buddies)
        {
            PlayerController player = Game.PlayerOf(id);
            if (player != null || id != buddies.Me || me == null)
            {
                return player;
            }
            // You, if the player list doesn't have you.
            if (_mePlayer == null || _mePlayer.transform != me.transform)
            {
                _mePlayer = me.GetComponent<PlayerController>();
                if (_mePlayer == null)
                {
                    _mePlayer = me.GetComponentInParent<PlayerController>();
                }
            }
            return _mePlayer;
        }

        // Beside your buddy, on whichever side isn't blocked, at their feet.
        private static Vector3 HopOffSpot(Transform carrier)
        {
            Vector3 feet = carrier.position;
            Vector3 chest = feet + Vector3.up * 0.8f;
            Vector3 right = carrier.right;
            Vector3 back = -carrier.forward;
            if (Free(chest, feet + right * HopOffStep))
            {
                return feet + right * HopOffStep;
            }
            if (Free(chest, feet - right * HopOffStep))
            {
                return feet - right * HopOffStep;
            }
            if (Free(chest, feet + back * HopOffStep))
            {
                return feet + back * HopOffStep;
            }
            return feet;
        }

        private static bool Free(Vector3 chest, Vector3 spot)
        {
            return !Game.Blocked(chest, spot + Vector3.up * 0.8f);
        }
    }
}
