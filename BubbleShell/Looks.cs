using System.Collections.Generic;
using UnityEngine;

namespace BubbleShell
{
    // Silly looks only you see: big heads on everyone (bighead) and a small you (tiny). Changed on your
    // screen only, every frame after the animations: bones aren't sent over the network (each game
    // animates every player itself), so nobody else sees a thing. The player object itself is never
    // scaled, since its NetworkTransform could send that.
    internal static class Looks
    {
        public static float HeadSize = 1f;
        public static float BodySize = 1f;

        // Each head bone changed, with its size before.
        private static readonly Dictionary<Transform, Vector3> Heads = new Dictionary<Transform, Vector3>();
        private static readonly List<Transform> HeadBones = new List<Transform>();
        private static float _nextScan;

        private static Transform _body;
        private static Vector3 _bodyScale;

        public static void LateUpdate()
        {
            if (!Game.InWorld)
            {
                // Everything was unloaded with the world.
                Heads.Clear();
                HeadBones.Clear();
                _body = null;
                return;
            }
            UpdateHeads();
            UpdateBody();
        }

        private static void UpdateHeads()
        {
            if (Mathf.Approximately(HeadSize, 1f))
            {
                if (Heads.Count > 0)
                {
                    foreach (KeyValuePair<Transform, Vector3> head in Heads)
                    {
                        if (head.Key != null)
                        {
                            head.Key.localScale = head.Value;
                        }
                    }
                    Heads.Clear();
                    HeadBones.Clear();
                }
                return;
            }

            // Players come and go, so look again now and then.
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                HeadBones.Clear();
                foreach (PlayerController player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                {
                    Transform head = Game.Head(player);
                    if (head != null)
                    {
                        HeadBones.Add(head);
                    }
                }
            }
            foreach (Transform head in HeadBones)
            {
                if (head == null)
                {
                    continue;
                }
                if (!Heads.TryGetValue(head, out Vector3 before))
                {
                    before = head.localScale;
                    Heads[head] = before;
                }
                head.localScale = before * HeadSize;
            }
        }

        private static void UpdateBody()
        {
            if (Mathf.Approximately(BodySize, 1f))
            {
                if (_body != null)
                {
                    _body.localScale = _bodyScale;
                    _body = null;
                }
                return;
            }
            Transform body = Game.ArmatureParent(Game.MyPlayer);
            if (body == null)
            {
                return;
            }
            if (body != _body)
            {
                _body = body;
                _bodyScale = body.localScale;
            }
            body.localScale = _bodyScale * BodySize;
        }
    }
}
