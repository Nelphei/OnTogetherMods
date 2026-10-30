using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FocusAnywhere
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.focusanywhere";
        public const string PluginName = "Focus Anywhere";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Every activity: {Settings.AllActivities.Value}, " +
                $"any ground: {Settings.AnyGround.Value}, mid-air: {Settings.MidAir.Value}.");
        }
    }
}
