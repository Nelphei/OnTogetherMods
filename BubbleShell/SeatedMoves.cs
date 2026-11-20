using System.Collections.Generic;
using UnityEngine;

namespace BubbleShell
{
    // The game keeps track of the areas you're in (a seat spot's "press F" area, a shop, an instrument,
    // a chest, a drawing board...) through trigger events on your movement collider. Sitting down turns
    // that collider off, and a collider that's off never gets the "left this area" event. So after tp
    // moves you while you sit, the game would still think you're by the bench or shop where you sat
    // down. When you stand up, this sends those "left" events itself, to the same scripts Unity would.
    // The same as Desk Swarm's and Buddy Up's AreaExits. See reference/Assembly-CSharp/PlayerAreaChecker.cs.
    internal static class SeatedMoves
    {
        private static PlayerFocusController _sitter;
        private static HashSet<Collider> _areas;

        // Call just before moving a sitter. The first move of a sit notes the areas they sat down in.
        public static void BeforeMove(PlayerFocusController me)
        {
            if (_sitter == me)
            {
                return;
            }
            _sitter = me;
            // Desk Swarm notes them itself when it starts flying you, and tells the game at stand-up.
            _areas = Game.DeskSwarmPiloting() ? new HashSet<Collider>() : At(me, me.transform.position);
        }

        // Call every frame: once the sitter stands up (or leaves), tells the game about the areas left behind.
        public static void Update()
        {
            if (ReferenceEquals(_sitter, null))
            {
                return;
            }
            // Leaving a session destroys your character (Unity then calls it null).
            if (_sitter != null && _sitter.IsFocus)
            {
                return;
            }
            PlayerFocusController sitter = _sitter;
            HashSet<Collider> areas = _areas;
            _sitter = null;
            _areas = null;
            if (sitter != null)
            {
                LeaveOld(sitter, areas);
            }
        }

        // The area triggers your collider would touch at this spot.
        private static HashSet<Collider> At(PlayerFocusController me, Vector3 position)
        {
            var found = new HashSet<Collider>();
            CharacterController body = me.GetComponent<CharacterController>();
            if (body == null)
            {
                return found;
            }
            float radius = body.radius;
            Vector3 centre = position + body.center;
            float half = Mathf.Max(0f, body.height / 2f - radius);
            foreach (Collider hit in Physics.OverlapCapsule(centre + Vector3.up * half, centre - Vector3.up * half, radius,
                ~0, QueryTriggerInteraction.Collide))
            {
                // Only the world's areas: not your own parts or another player's.
                if (hit.isTrigger && hit.GetComponentInParent<PlayerController>() == null)
                {
                    found.Add(hit);
                }
            }
            return found;
        }

        // Tells the game you've left every area in `before` that you aren't in now.
        private static void LeaveOld(PlayerFocusController me, HashSet<Collider> before)
        {
            if (before == null || before.Count == 0)
            {
                return;
            }
            HashSet<Collider> now = At(me, me.transform.position);
            CharacterController body = me.GetComponent<CharacterController>();
            PlayerAreaChecker checker = me.GetComponent<PlayerAreaChecker>() ?? me.GetComponentInChildren<PlayerAreaChecker>();
            int left = 0;
            foreach (Collider area in before)
            {
                if (area == null || now.Contains(area))
                {
                    continue;
                }
                // Both sides hear about it, as with Unity's own event: your player's area checker, and the
                // area's own scripts (doors, stairs, the concert, the waterfall).
                if (checker != null)
                {
                    checker.gameObject.SendMessage("OnTriggerExit", area, SendMessageOptions.DontRequireReceiver);
                }
                if (body != null)
                {
                    area.gameObject.SendMessage("OnTriggerExit", body, SendMessageOptions.DontRequireReceiver);
                }
                left++;
            }
            if (left > 0)
            {
                Plugin.Log.LogInfo($"Stood up away from where you sat down (tp), so told the game you left {left} area(s) there.");
            }
        }
    }
}
