using System.Collections.Generic;
using UnityEngine;

namespace DeskSnap
{
    internal struct Placement
    {
        public Vector3 Position;
        public Quaternion Facing;
        public Seat Neighbour;   // whose desk you end up next to
    }

    // Working out where your desk goes in a row, and checking there's room for it there.
    internal static class Row
    {
        // How far the ground may rise above your seat at the new spot, in metres, before you'd be
        // sitting in it. Ground lower than your seat (or none at all) is fine: you float in line.
        private const float MaxSink = 0.15f;
        // How many desks along the row to look past for a free end.
        private const int MaxChain = 8;
        // Desks overlapping by less than this (in metres) don't count as in the spot.
        private const float Overlap = 0.05f;

        private static int _mask;

        // Everything solid in the world: not players, water, areas or invisible triggers.
        // Close to the game's own ground check (PlayerMovementController._groundDistanceCheckLayers).
        private static int Mask
        {
            get
            {
                if (_mask == 0)
                {
                    _mask = ~LayerMask.GetMask("Ignore Raycast", "Player", "Water", "Area", "AreaTrigger",
                        "Customization", "IsGroundedChecker", "DeskRender");
                }
                return _mask;
            }
        }

        // Finds a free spot for your desk beside `target`'s, facing the same way: on `side` first
        // (+1 their right, -1 their left), then the other side. If a desk is already there, it moves
        // along the row to the next free spot.
        public static bool TryPlace(Seat target, int side, Span mine, List<Seat> seats, float gap,
            out Placement placement, out string why)
        {
            why = null;
            foreach (int s in new[] { side, -side })
            {
                if (TryPlaceOnSide(target, s, mine, seats, gap, out placement, out string sideWhy))
                {
                    return true;
                }
                why = why ?? sideWhy;
            }
            placement = default;
            return false;
        }

        private static bool TryPlaceOnSide(Seat target, int side, Span mine, List<Seat> seats, float gap,
            out Placement placement, out string why)
        {
            placement = default;
            Quaternion facing = target.Facing;
            Vector3 right = facing * Vector3.right;
            var passed = new HashSet<Seat> { target };
            Seat current = target;
            for (int step = 0; step < MaxChain; step++)
            {
                // Your desk's near edge sits `gap` past their far edge.
                float along = side > 0 ? current.Span.Right + gap - mine.Left : current.Span.Left - gap - mine.Right;
                Vector3 spot = current.Position + right * along;

                Seat occupant = OccupantAt(spot, facing, mine, seats, passed);
                if (occupant != null)
                {
                    passed.Add(occupant);
                    current = occupant;
                    continue;
                }
                if (!Fits(spot, facing, current, mine, out Vector3 placed, out why))
                {
                    return false;
                }
                placement = new Placement { Position = placed, Facing = facing, Neighbour = current };
                return true;
            }
            why = "the row is full";
            return false;
        }

        // True if `other`'s desk takes up some of the spot where your desk would go.
        public static bool Overlaps(Vector3 spot, Quaternion facing, Span mine, Seat other)
        {
            return OverlapWith(spot, facing, mine, other, out _) > Overlap;
        }

        private static float OverlapWith(Vector3 spot, Quaternion facing, Span mine, Seat other, out float across)
        {
            Vector3 offset = other.Position - spot;
            across = Vector3.Dot(offset, facing * Vector3.right);
            float depth = Vector3.Dot(offset, facing * Vector3.forward);
            if (Mathf.Abs(depth) > 1f || Mathf.Abs(offset.y) > 1f)
            {
                return 0f;   // in another row, or on another floor
            }
            return Mathf.Min(mine.Right, across + other.Span.Right) - Mathf.Max(mine.Left, across + other.Span.Left);
        }

        private static Seat OccupantAt(Vector3 spot, Quaternion facing, Span mine, List<Seat> seats, HashSet<Seat> passed)
        {
            Seat best = null;
            float bestAcross = float.MaxValue;
            foreach (Seat seat in seats)
            {
                if (passed.Contains(seat))
                {
                    continue;
                }
                if (OverlapWith(spot, facing, mine, seat, out float across) > Overlap && Mathf.Abs(across) < bestAcross)
                {
                    best = seat;
                    bestAcross = Mathf.Abs(across);
                }
            }
            return best;
        }

        // Checks the ground doesn't rise into your seat, nothing's between you and the neighbour's desk,
        // and there's room for your desk and chair. `placed` is the spot at the neighbour's exact height,
        // so a row stays level wherever you started from, on the ground or in the air.
        private static bool Fits(Vector3 spot, Quaternion facing, Seat neighbour, Span mine,
            out Vector3 placed, out string why)
        {
            placed = new Vector3(spot.x, neighbour.Position.y, spot.z);

            // Ground higher than your seat. (Ground more than 0.6 m higher is caught by the room check.)
            if (Ground(placed + Vector3.up * MaxSink, 0.45f, 0f, out _))
            {
                why = "the ground is higher there";
                return false;
            }

            // A wall or fence between the two desks.
            Vector3 from = neighbour.Position + Vector3.up * 0.9f;
            Vector3 to = placed + Vector3.up * 0.9f;
            if (Blocked(from, to))
            {
                why = "something's in the way";
                return false;
            }

            // Room for the desk and chair, from knee height up (so bumps in the ground don't count).
            Vector3 centre = placed + facing * new Vector3((mine.Left + mine.Right) / 2f, 0.65f, (mine.Back + mine.Front) / 2f);
            var half = new Vector3(
                Mathf.Max(0.05f, mine.Width / 2f - 0.05f),
                0.35f,
                Mathf.Max(0.05f, (mine.Front - mine.Back) / 2f - 0.05f));
            foreach (Collider hit in Physics.OverlapBox(centre, half, facing, Mask, QueryTriggerInteraction.Ignore))
            {
                if (!IsIgnored(hit))
                {
                    if (Plugin.Settings.DebugLog.Value)
                    {
                        Plugin.Log.LogInfo($"No room for the desk: {hit.name} is there.");
                    }
                    why = "something's in the way";
                    return false;
                }
            }

            why = null;
            return true;
        }

        // The nearest solid ground below `at`, looking from `up` above it to `down` below it.
        private static bool Ground(Vector3 at, float up, float down, out float y)
        {
            y = 0f;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * up, Vector3.down, up + down, Mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < nearest && !IsIgnored(hit.collider))
                {
                    nearest = hit.distance;
                    y = hit.point.y;
                }
            }
            return nearest < float.MaxValue;
        }

        private static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            foreach (RaycastHit hit in Physics.RaycastAll(from, line.normalized, line.magnitude, Mask, QueryTriggerInteraction.Ignore))
            {
                if (!IsIgnored(hit.collider))
                {
                    if (Plugin.Settings.DebugLog.Value)
                    {
                        Plugin.Log.LogInfo($"Between the desks: {hit.collider.name}.");
                    }
                    return true;
                }
            }
            return false;
        }

        // Players, their desks and their pets never get in the way.
        private static bool IsIgnored(Collider collider)
        {
            return collider.GetComponentInParent<PlayerController>() != null ||
                   collider.GetComponentInParent<PetController>() != null;
        }
    }
}
