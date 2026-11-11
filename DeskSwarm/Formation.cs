using System.Collections.Generic;
using UnityEngine;

namespace DeskSwarm
{
    // The shapes a swarm can fly in. The names go over the network, so keep them as they are, and add
    // new ones at the end. Players on an older version ignore a swarm in a shape they don't know.
    public enum Formation
    {
        // Flying (0.1.0).
        Ring,     // circles around the leader, overhead
        V,        // a V trailing behind, like geese
        Line,     // a row beside the leader, like Desk Snap's
        Tower,    // stacked on top of the leader
        Trail,    // one behind another, along the leader's path

        // Flying (0.2.0).
        Heart,    // a heart standing up over the leader's head
        Wheel,    // a Ferris wheel turning over the leader's head
        Twister,  // a spinning spiral winding up around the leader
        Wave,     // a row beside the leader with a ripple running along it
        Stairs,   // steps climbing into the sky ahead of the leader

        // Landing (0.2.0): the desks come down and sit on the ground.
        Row,      // side by side with the leader, facing the same way
        Class,    // rows in front of the leader, facing them
        Circle,   // a circle with the leader in it, everyone facing the middle
        Table,    // two rows facing each other, the leader in the middle of one
        Grid,     // an even square grid, like drones laid out before a show
    }

    // Where each desk goes in a formation. Every follower works out its own spot from the same
    // things (the leader's position and facing, its place in the leader's list, the shared clock),
    // so the swarm keeps its shape on every screen without anyone sending positions.
    internal static class Formations
    {
        // Centre to centre, in metres, at a spread of 1. A desk and its chair are about 1.3 m wide.
        private const float Spacing = 1.7f;
        // Desks in a ring or wheel move round it at about this speed, in metres per second.
        private const float OrbitSpeed = 1f;
        // How far the trail keeps the leader's path, in metres past the last desk.
        private const float PathSpare = 6f;

        // The twister: desks per turn, and how fast it spins (radians per second).
        private const int TwisterPerTurn = 6;
        private const float TwisterSpin = 0.5f;
        // The wave: seconds for one rise and fall, and desks from one crest to the next.
        private const double WaveSeconds = 3.0;
        private const float WaveDesks = 6f;
        private const float WaveHeight = 1f;

        // A seated player's desk reaches about this far in front of them (Desk Snap measures
        // 0.8 m front, 0.5 m back for the chair, 0.6 m to each side).
        private const float DeskFront = 0.8f;

        public static readonly Formation[] Flying =
        {
            Formation.Ring, Formation.V, Formation.Line, Formation.Tower, Formation.Trail,
            Formation.Heart, Formation.Wheel, Formation.Twister, Formation.Wave, Formation.Stairs,
        };

        public static readonly Formation[] Landing =
        {
            Formation.Row, Formation.Class, Formation.Circle, Formation.Table, Formation.Grid,
        };

        // True for the shapes whose desks come down and sit on the ground (Flight.cs finds it).
        public static bool Lands(Formation formation)
        {
            switch (formation)
            {
                case Formation.Row:
                case Formation.Class:
                case Formation.Circle:
                case Formation.Table:
                case Formation.Grid:
                    return true;
                default:
                    return false;
            }
        }

        // `index` is this desk's place among `count` followers. `path` is where the leader has been,
        // newest first (only Trail uses it). Returns the spot and which way to face. For a landing
        // shape the spot is at the leader's height, and Flight puts it on the ground.
        public static Vector3 Place(Formation formation, int index, int count, float spread, Vector3 leader, float leaderYaw,
            double time, List<Vector3> path, out float yaw)
        {
            count = Mathf.Max(count, index + 1);
            float d = Spacing * spread;
            Quaternion turn = Quaternion.Euler(0f, leaderYaw, 0f);
            yaw = leaderYaw;

            // On the ground, desks are never packed so close that they'd overlap.
            float beside = Mathf.Max(1.3f, 1.5f * spread);     // side by side
            float behind = Mathf.Max(1.5f, 1.8f * spread);     // one row behind another, same way round
            float facing = Mathf.Max(1.8f, 2.2f * spread);     // face to face, desks between them

            switch (formation)
            {
                case Formation.Ring:
                {
                    // Wide enough for everyone, and never close enough to bump the leader.
                    float radius = Mathf.Max(2.6f * spread, count * d / (2f * Mathf.PI));
                    // The ring turns on the shared clock, so every desk agrees where the others are.
                    float angle = 2f * Mathf.PI * index / count + Spin(time, OrbitSpeed / radius);
                    var offset = new Vector3(radius * Mathf.Sin(angle), 1f, radius * Mathf.Cos(angle));
                    yaw = RoundYaw(angle);
                    return leader + offset;
                }

                case Formation.V:
                {
                    // Left, right, left, right..., each pair a step further back and out.
                    int step = index / 2 + 1;
                    float side = index % 2 == 0 ? -1f : 1f;
                    var offset = new Vector3(side * step * d * 0.8f, 0f, -(0.6f + step * d * 0.8f));
                    return leader + turn * offset;
                }

                case Formation.Line:
                    return leader + turn * new Vector3(Beside(index) * d, 0f, 0f);

                case Formation.Tower:
                {
                    // A desk with someone sitting at it is about 1.3 m tall.
                    float floor = Mathf.Max(1.5f, 1.7f * spread);
                    return leader + Vector3.up * (floor * (index + 1));
                }

                case Formation.Trail:
                    return AlongPath(leader, leaderYaw, path, (index + 1) * d * 1.1f + 0.4f, out yaw) + Vector3.up * 0.3f;

                case Formation.Heart:
                {
                    // Standing up across the leader's way, its point just above their head, big
                    // enough to space everyone out along it. Everyone faces the way the leader does.
                    float scale = Mathf.Max(HeartSmallest * spread, count * d / HeartLength);
                    Vector2 point = HeartPoint((float)index / count);
                    return leader + turn * new Vector3(point.x * scale, 2.5f + point.y * scale, 0f);
                }

                case Formation.Wheel:
                {
                    // Upright, across the leader's way, its lowest desk just over their head. The
                    // desks stay level, like a Ferris wheel's cars.
                    float radius = Mathf.Max(2.4f * spread, count * d / (2f * Mathf.PI));
                    float angle = 2f * Mathf.PI * index / count + Spin(time, OrbitSpeed / radius);
                    var offset = new Vector3(radius * Mathf.Sin(angle), 2.2f + radius * (1f + Mathf.Cos(angle)), 0f);
                    return leader + turn * offset;
                }

                case Formation.Twister:
                {
                    // Each desk a little higher and further out than the one before, the whole
                    // spiral spinning together, everyone facing the way they're going round.
                    float radius = Mathf.Max(2f, 2.2f * spread) + 0.3f * spread * index;
                    float rise = Mathf.Max(0.5f, 0.65f * spread);
                    float angle = 2f * Mathf.PI * index / TwisterPerTurn + Spin(time, TwisterSpin);
                    var offset = new Vector3(radius * Mathf.Sin(angle), 1f + rise * index, radius * Mathf.Cos(angle));
                    yaw = RoundYaw(angle);
                    return leader + offset;
                }

                case Formation.Wave:
                {
                    // A line beside the leader, with a ripple running along it from left to right:
                    // each desk rises and falls a moment after the one on its left.
                    float across = Beside(index) * d;
                    float phase = (float)(time / WaveSeconds % 1.0) - across / (WaveDesks * d);
                    float height = 1f + WaveHeight + WaveHeight * Mathf.Sin(2f * Mathf.PI * phase);
                    return leader + turn * new Vector3(across, height, 0f);
                }

                case Formation.Stairs:
                {
                    float run = Mathf.Max(1.4f, 1.6f * spread);
                    float step = Mathf.Max(0.6f, 0.8f * spread);
                    return leader + turn * new Vector3(0f, step * (index + 1), 1.2f + run * (index + 1));
                }

                // ---- Landing ----

                case Formation.Row:
                    // Like Desk Snap's rows, with the leader in the middle.
                    return leader + turn * new Vector3(Beside(index) * beside, 0f, 0f);

                case Formation.Class:
                {
                    // Rows in front of the leader, wider than they're deep, the last one centred.
                    // The leader's own desk is the teacher's.
                    int columns = Mathf.Min(count, Mathf.CeilToInt(Mathf.Sqrt(2f * count)));
                    int row = index / columns;
                    int inRow = Mathf.Min(columns, count - row * columns);
                    float x = (index % columns - (inRow - 1) / 2f) * beside;
                    float z = facing * 1.3f + row * behind;
                    yaw = leaderYaw + 180f;
                    return leader + turn * new Vector3(x, 0f, z);
                }

                case Formation.Circle:
                {
                    // The leader's seat is one of the circle's, with the middle straight ahead of
                    // them. Wide enough that neighbouring desks don't touch, since desks point in.
                    int seats = count + 1;
                    float radius = facing / 2f;
                    if (seats > 2)
                    {
                        radius = Mathf.Max(radius, DeskFront + beside / (2f * Mathf.Sin(Mathf.PI / seats)));
                    }
                    float angle = 2f * Mathf.PI * (index + 1) / seats;
                    var offset = new Vector3(-radius * Mathf.Sin(angle), 0f, radius * (1f - Mathf.Cos(angle)));
                    yaw = leaderYaw + angle * Mathf.Rad2Deg;
                    return leader + turn * offset;
                }

                case Formation.Table:
                {
                    // Seats fill in pairs, one on the far side and one on the leader's side,
                    // spreading out from the middle: opposite the leader first, then right, left...
                    int seat = index + 1;
                    bool farSide = seat % 2 == 1;
                    int pair = seat / 2;
                    float x = (pair == 0 ? 0 : pair % 2 == 1 ? (pair + 1) / 2 : -(pair / 2)) * beside;
                    if (farSide)
                    {
                        yaw = leaderYaw + 180f;
                    }
                    return leader + turn * new Vector3(x, 0f, farSide ? facing : 0f);
                }

                case Formation.Grid:
                {
                    // As square as it can be, the same gap both ways, everyone facing the way the
                    // leader does. The leader has the middle of the front row (always full), and
                    // the others fill in row by row behind, the last row centred on the grid.
                    int seats = count + 1;
                    int columns = Mathf.CeilToInt(Mathf.Sqrt(seats));
                    int middle = (columns - 1) / 2;
                    int seat = index < middle ? index : index + 1;
                    int row = seat / columns;
                    int inRow = Mathf.Min(columns, seats - row * columns);
                    int column = seat % columns + (columns - inRow) / 2;
                    return leader + turn * new Vector3((column - middle) * behind, 0f, -row * behind);
                }

                default:
                    return leader;
            }
        }

        // How much of the leader's path a trail of this many desks needs, in metres.
        public static float PathNeeded(int count, float spread)
        {
            return (count + 1) * Spacing * spread * 1.1f + PathSpare;
        }

        // Steps out to the side from the leader: left 1, right 1, left 2, right 2...
        private static float Beside(int index)
        {
            int step = index / 2 + 1;
            return index % 2 == 0 ? -step : step;
        }

        // How far round something turning at this speed is, on the shared clock, so every desk
        // agrees where the others are.
        private static float Spin(double time, float radiansPerSecond)
        {
            return (float)(time * radiansPerSecond % (2.0 * Mathf.PI));
        }

        // Facing the way round a ring or spiral it's going (offset sin, cos at this angle).
        private static float RoundYaw(float angle)
        {
            return Mathf.Atan2(Mathf.Cos(angle), -Mathf.Sin(angle)) * Mathf.Rad2Deg;
        }

        // The point `distance` metres back along the leader's path, facing along it towards the leader.
        // Past the end of the path, it carries straight on behind where the path started.
        private static Vector3 AlongPath(Vector3 leader, float leaderYaw, List<Vector3> path, float distance, out float yaw)
        {
            Vector3 ahead = leader;
            Vector3 back = Quaternion.Euler(0f, leaderYaw, 0f) * Vector3.back;
            float left = distance;
            if (path != null)
            {
                foreach (Vector3 point in path)
                {
                    Vector3 step = point - ahead;
                    float length = step.magnitude;
                    if (length < 0.001f)
                    {
                        continue;
                    }
                    var flat = new Vector3(step.x, 0f, step.z);
                    if (flat.sqrMagnitude > 0.0001f)
                    {
                        back = flat.normalized;
                    }
                    if (length >= left)
                    {
                        yaw = YawOf(-back);
                        return ahead + step * (left / length);
                    }
                    left -= length;
                    ahead = point;
                }
            }
            yaw = YawOf(-back);
            return ahead + back * left;
        }

        private static float YawOf(Vector3 direction)
        {
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        // ---- The heart ----

        // The classic heart curve: x = 16 sin³t, y = 13 cos t - 5 cos 2t - 2 cos 3t - cos 4t.
        // 32 across, 29 tall, its point at y = -17, about 102 round.
        private const int HeartSamples = 256;
        // The smallest heart, as a scale of the curve (about 4.8 m across at a spread of 1).
        private const float HeartSmallest = 0.15f;
        private static float[] _heartLengths;
        private static float HeartLength => HeartLengths()[HeartSamples];

        private static Vector2 HeartCurve(float t)
        {
            float s = Mathf.Sin(t);
            return new Vector2(16f * s * s * s,
                13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t) + 17f);
        }

        // The distance round the curve to each sample, so desks can go round it evenly spaced.
        private static float[] HeartLengths()
        {
            if (_heartLengths == null)
            {
                var lengths = new float[HeartSamples + 1];
                Vector2 last = HeartCurve(0f);
                for (int i = 1; i <= HeartSamples; i++)
                {
                    Vector2 next = HeartCurve(2f * Mathf.PI * i / HeartSamples);
                    lengths[i] = lengths[i - 1] + Vector2.Distance(last, next);
                    last = next;
                }
                _heartLengths = lengths;
            }
            return _heartLengths;
        }

        // The point `fraction` of the way round, starting from the dip at the top, with the point
        // of the heart at y = 0. Spots spaced evenly round it come out the same on both sides.
        private static Vector2 HeartPoint(float fraction)
        {
            float[] lengths = HeartLengths();
            float wanted = Mathf.Repeat(fraction, 1f) * lengths[HeartSamples];
            int i = 1;
            while (i < HeartSamples && lengths[i] < wanted)
            {
                i++;
            }
            float span = lengths[i] - lengths[i - 1];
            float part = span > 0f ? (wanted - lengths[i - 1]) / span : 0f;
            return HeartCurve(2f * Mathf.PI * (i - 1 + part) / HeartSamples);
        }
    }
}
