using System.Collections.Generic;
using UnityEngine;

namespace DeskSnap
{
    // How far a player's desk reaches, in metres from where the player is, along their own right
    // (Left is negative) and forward (Back is negative). Measured from the desk's models, so it fits
    // every desk shape and accessory.
    internal struct Span
    {
        public float Left;
        public float Right;
        public float Back;
        public float Front;

        public float Width => Right - Left;

        public override string ToString() =>
            $"left {Left:0.00}, right {Right:0.00}, back {Back:0.00}, front {Front:0.00}";
    }

    // Another player sitting at their desk, as seen right now.
    internal sealed class Seat
    {
        public PlayerFocusController Player;
        public Vector3 Position;
        public Quaternion Facing;
        public Span Span;

        public string Name => Game.NameOf(Player);
    }

    // Finding desks and measuring them.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus) and
    // PlayerCustomizationController.cs (CustomizeDesk).
    internal static class Desks
    {
        // Used if a desk can't be measured. About the size of the default desk and chair.
        private static readonly Span Fallback = new Span { Left = -0.6f, Right = 0.6f, Back = -0.5f, Front = 0.8f };

        // Focus animations that always bring out the desk, and ones that never do. An animation
        // used both ways (or one this game doesn't know, from another player's mod) falls back to
        // whether their desk object is showing.
        private static readonly HashSet<int> DeskAnims = new HashSet<int>();
        private static readonly HashSet<int> NoDeskAnims = new HashSet<int>();
        private static float _animsReadAt = -100f;

        public static Quaternion FacingOf(Transform t) => Quaternion.Euler(0f, t.eulerAngles.y, 0f);

        // Everyone except you who's sitting at their own desk in free focus.
        public static List<Seat> OthersAtDesks(PlayerFocusController me)
        {
            var seats = new List<Seat>();
            foreach (PlayerFocusController player in Object.FindObjectsByType<PlayerFocusController>(FindObjectsSortMode.None))
            {
                if (player == me || !player.gameObject.activeInHierarchy || !IsAtFreeDesk(player))
                {
                    continue;
                }
                seats.Add(new Seat
                {
                    Player = player,
                    Position = player.transform.position,
                    Facing = FacingOf(player.transform),
                    Span = Measure(player),
                });
            }
            return seats;
        }

        // Another player's game only tells us their animation, so tell from that (and their desk).
        public static bool IsAtFreeDesk(PlayerFocusController player)
        {
            if (Game.IsAtSeatSpot(player))
            {
                return false;   // a classroom desk is part of the room, so it can't move
            }
            GameObject desk = Game.Desk(player);
            bool deskShown = desk != null && desk.activeSelf;
            Animator animator = Game.AnimatorOf(player);
            if (animator == null)
            {
                return deskShown;
            }

            ReadAnims();
            int anim = animator.GetInteger(Game.AnimStateParam);
            if (DeskAnims.Contains(anim))
            {
                return true;
            }
            if (NoDeskAnims.Contains(anim))
            {
                return false;
            }
            return deskShown;
        }

        // The desk's reach, from the corners of each desk part's model (desk, chair, lamp, plant...).
        // Parts that are switched off (an empty accessory slot) are left out.
        public static Span Measure(PlayerFocusController player)
        {
            GameObject desk = Game.Desk(player);
            if (desk == null)
            {
                return Fallback;
            }

            Transform root = player.transform;
            Quaternion facing = FacingOf(root);
            Vector3 right = facing * Vector3.right;
            Vector3 forward = facing * Vector3.forward;
            Vector3 origin = root.position;
            bool debug = Plugin.Settings.DebugLog.Value;

            var span = new Span { Left = float.MaxValue, Right = float.MinValue, Back = float.MaxValue, Front = float.MinValue };
            bool any = false;
            foreach (MeshRenderer renderer in desk.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !IsShown(renderer.transform, desk.transform))
                {
                    continue;
                }
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                Bounds bounds = mesh.bounds;
                Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
                float partLeft = float.MaxValue, partRight = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    Vector3 offset = toWorld.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign)) - origin;
                    float x = Vector3.Dot(offset, right);
                    float z = Vector3.Dot(offset, forward);
                    partLeft = Mathf.Min(partLeft, x);
                    partRight = Mathf.Max(partRight, x);
                    span.Back = Mathf.Min(span.Back, z);
                    span.Front = Mathf.Max(span.Front, z);
                }
                span.Left = Mathf.Min(span.Left, partLeft);
                span.Right = Mathf.Max(span.Right, partRight);
                any = true;

                if (debug)
                {
                    Plugin.Log.LogInfo($"  {Game.NameOf(player)}'s desk part {renderer.name}: {partLeft:0.00} to {partRight:0.00} across.");
                }
            }

            if (!any || span.Width < 0.2f || span.Width > 6f || span.Front - span.Back > 6f)
            {
                if (debug)
                {
                    Plugin.Log.LogInfo($"Couldn't measure {Game.NameOf(player)}'s desk, so using the usual size.");
                }
                return Fallback;
            }
            if (debug)
            {
                Plugin.Log.LogInfo($"{Game.NameOf(player)}'s desk: {span}.");
            }
            return span;
        }

        // True if every object from this one up to (not including) the desk is switched on.
        // The desk itself is off while its owner isn't sitting at it.
        private static bool IsShown(Transform t, Transform desk)
        {
            for (; t != null && t != desk; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    return false;
                }
            }
            return true;
        }

        // Re-read now and then: Focus Anywhere adds activities to these lists after the game loads.
        private static void ReadAnims()
        {
            if (Time.unscaledTime - _animsReadAt < 5f)
            {
                return;
            }
            _animsReadAt = Time.unscaledTime;
            DeskAnims.Clear();
            NoDeskAnims.Clear();

            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            List<int> anims = settings != null ? Game.LandAnims(settings) : null;
            List<bool> isDesk = settings != null ? Game.LandIsDesk(settings) : null;
            if (anims == null || isDesk == null)
            {
                return;
            }
            var both = new HashSet<int>();
            for (int i = 0; i < anims.Count && i < isDesk.Count; i++)
            {
                HashSet<int> mine = isDesk[i] ? DeskAnims : NoDeskAnims;
                HashSet<int> other = isDesk[i] ? NoDeskAnims : DeskAnims;
                if (other.Contains(anims[i]))
                {
                    both.Add(anims[i]);
                }
                mine.Add(anims[i]);
            }
            DeskAnims.ExceptWith(both);
            NoDeskAnims.ExceptWith(both);
        }
    }
}
