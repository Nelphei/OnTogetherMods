using HarmonyLib;

namespace HelloTogether
{
    // Runs our code right after the game's own MainMenuUIController.Start().
    // See reference/Assembly-CSharp/MainMenuUIController.cs for the original.
    [HarmonyPatch(typeof(MainMenuUIController), "Start")]
    internal static class MainMenuPatch
    {
        private static void Postfix()
        {
            Plugin.Log.LogInfo("Main menu opened - Harmony patching works!");
        }
    }
}
