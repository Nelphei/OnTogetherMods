using System.Collections.Generic;
using UnityEngine;

namespace DeskSwarm
{
    // The game keeps track of the areas you're in (a seat spot's "press F" area, a shop, an
    // instrument, a chest, a drawing board...) through trigger events on your movement collider.
    // Sitting down turns that collider off, and a collider that's off never gets the "left this
    // area" event. Sitting and standing in the same spot doesn't matter, but after flying away the
    // game would still think you're by the bench or shop where you sat down. So when you stand up
    // somewhere else, this sends those "left" events itself, to the same scripts Unity would.
    // See reference/Assembly-CSharp/PlayerAreaChecker.cs (OnTriggerExit).
    internal static class AreaExits
    {
        // The area triggers your collider would touch at this spot.
        public static HashSet<Collider> At(PlayerFocusController me, Vector3 position)
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
        public static void LeaveOld(PlayerFocusController me, HashSet<Collider> before)
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
                // Both sides hear about it, as with Unity's own event: your player's area checker, and
                // the area's own scripts (doors, stairs, the concert, the waterfall).
                if (checker != null)
                {
                    checker.gameObject.SendMessage("OnTriggerExit", area, SendMessageOptions.DontRequireReceiver);
                }
                if (body != null)
                {
                    area.gameObject.SendMessage("OnTriggerExit", body, SendMessageOptions.DontRequireReceiver);
                }
                left++;
                if (Plugin.Settings.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"Left the area {area.name} after flying away from it.");
                }
            }
            if (left > 0)
            {
                Plugin.Log.LogInfo($"Stood up away from where you sat down, so told the game you left {left} area(s) there.");
            }
        }
    }
}
