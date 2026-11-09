using System.Collections.Generic;
using UnityEngine;

namespace DeskSwarm
{
    // The shapes a swarm can fly in. The names go over the network, so keep them as they are.
    public enum Formation
    {
        Ring,   // circles around the leader, overhead
        V,      // a V trailing behind, like geese
        Line,   // a row beside the leader, like Desk Snap's
        Tower,  // stacked on top of the leader
        Trail,  // one behind another, along the leader's path
    }

    // Where each desk goes in a formation. Every follower works out its own spot from the same
    // things (the leader's position and facing, its place in the leader's list, the shared clock),
    // so the swarm keeps its shape on every screen without anyone sending positions.
    internal static class Formations
    {
        // Centre to centre, in metres, at a spread of 1. A desk and its chair are about 1.3 m wide.
        private const float Spacing = 1.7f;
        // Desks in a ring move round it at about this speed, in metres per second.
        private const float OrbitSpeed = 1f;
        // How far the trail keeps the leader's path, in metres past the last desk.
        private const float PathSpare = 6f;

        public static readonly Formation[] All = { Formation.Ring, Formation.V, Formation.Line, Formation.Tower, Formation.Trail };

        // `index` is this desk's place among `count` followers. `path` is where the leader has been,
        // newest first (only Trail uses it). Returns the spot and which way to face.
        public static Vector3 Place(Formation formation, int index, int count, float spread, Vector3 leader, float leaderYaw,
            double time, List<Vector3> path, out float yaw)
        {
            count = Mathf.Max(count, index + 1);
            float d = Spacing * spread;
            Quaternion turn = Quaternion.Euler(0f, leaderYaw, 0f);
            yaw = leaderYaw;

            switch (formation)
            {
                case Formation.Ring:
                {
                    // Wide enough for everyone, and never close enough to bump the leader.
                    float radius = Mathf.Max(2.6f * spread, count * d / (2f * Mathf.PI));
                    // The ring turns on the shared clock, so every desk agrees where the others are.
                    double turned = time * OrbitSpeed / radius % (2.0 * Mathf.PI);
                    float angle = 2f * Mathf.PI * index / count + (float)turned;
                    var offset = new Vector3(radius * Mathf.Sin(angle), 1f, radius * Mathf.Cos(angle));
                    // Facing the way round it's going.
                    yaw = Mathf.Atan2(Mathf.Cos(angle), -Mathf.Sin(angle)) * Mathf.Rad2Deg;
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
                {
                    int step = index / 2 + 1;
                    float side = index % 2 == 0 ? -1f : 1f;
                    return leader + turn * new Vector3(side * step * d, 0f, 0f);
                }

                case Formation.Tower:
                {
                    // A desk with someone sitting at it is about 1.3 m tall.
                    float floor = Mathf.Max(1.5f, 1.7f * spread);
                    return leader + Vector3.up * (floor * (index + 1));
                }

                default:
                    return AlongPath(leader, leaderYaw, path, (index + 1) * d * 1.1f + 0.4f, out yaw) + Vector3.up * 0.3f;
            }
        }

        // How much of the leader's path a trail of this many desks needs, in metres.
        public static float PathNeeded(int count, float spread)
        {
            return (count + 1) * Spacing * spread * 1.1f + PathSpare;
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
    }
}
