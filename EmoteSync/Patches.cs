using HarmonyLib;

namespace EmoteSync
{
    // Both ways of emoting end here: clicking an emote on the wheel, and tapping the emote key to
    // repeat the last one. When sync is on, hold the emote back so everyone can start it together.
    // See reference/Assembly-CSharp/EmoteController.cs.
    [HarmonyPatch(typeof(EmoteController), "SetEmoteAnim")]
    internal static class SetEmoteAnimPatch
    {
        private static bool Prefix(int emoteIndex)
        {
            try
            {
                SyncController sync = SyncController.Instance;
                return sync == null || !sync.TryTakeOver(emoteIndex);
            }
            catch (System.Exception e)
            {
                // Never break normal emotes: on any error, let the game play it as usual.
                Plugin.Log.LogError(e);
                return true;
            }
        }
    }

    // Runs after you pick a focus activity (starting or switching). Tells the group, unless the
    // mod itself started it because someone else in the group picked it.
    // See reference/Assembly-CSharp/PlayerFocusController.cs.
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class SetFocusPatch
    {
        private static void Postfix(PlayerFocusController __instance)
        {
            try
            {
                if (!FocusJoin.Joining)
                {
                    SyncController.Instance?.OnLocalFocusPicked(__instance);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
