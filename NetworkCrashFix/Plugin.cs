using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace NetworkCrashFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.networkcrashfix";
        public const string PluginName = "Network Crash Fix";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private Harmony _harmony;
        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            SafeReads.On = Settings.Enabled.Value;
            Settings.Enabled.SettingChanged += (sender, args) =>
            {
                SafeReads.On = Settings.Enabled.Value;
                Log.LogInfo(SafeReads.On
                    ? "Switched on: the end of each network packet is read safely."
                    : "Switched off: the game reads network packets its own way again.");
            };
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // Now, before the game first reads a packet.
            _harmony = new Harmony(PluginGuid);
            bool patched = SafeReads.Apply(_harmony);

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("NetworkCrashFix");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<IconButton>();

            if (patched)
            {
                Log.LogInfo($"{PluginName} {PluginVersion} loaded. " + (SafeReads.On
                    ? "The end of each network packet is read safely."
                    : "Switched off in its settings, so the game reads network packets its own way."));
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_host != null)
            {
                Destroy(_host);
            }
        }
    }
}
