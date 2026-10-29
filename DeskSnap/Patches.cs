using System;
using HarmonyLib;

namespace DeskSnap
{
    // Runs after a focus activity is picked, whether you clicked it or Emote Sync started it
    // because someone in your group did (Emote Sync calls this same method).
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus).
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class SetFocusPatch
    {
        private static void Postfix(PlayerFocusController __instance)
        {
            try
            {
                SnapController.Instance?.OnFocusPicked(__instance);
            }
            catch (Exception e)
            {
                // Never break focusing: on any error, you just stay where you sat.
                Plugin.Log.LogError(e);
            }
        }
    }
}
