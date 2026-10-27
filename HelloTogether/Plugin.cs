using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace HelloTogether
{
    // BepInEx finds this class, creates it at game startup, and calls Awake().
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        // The GUID must be unique across all mods. Keep it stable once you release.
        public const string PluginGuid = "nelphei.ontogether.hellotogether";
        public const string PluginName = "Hello Together";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            // Applies every [HarmonyPatch] class in this mod.
            new Harmony(PluginGuid).PatchAll();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded!");
        }
    }
}
