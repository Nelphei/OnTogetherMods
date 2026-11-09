using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NetworkCrashFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.networkcrashfix";
        public const string PluginName = "Network Crash Fix";
        public const string PluginVersion = "0.1.1";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private Harmony _harmony;
        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            // Now, before the game first reads a packet. Always on: there's no switch. If the fix
            // doesn't match the game's own reader, Apply leaves the game alone and logs why.
            _harmony = new Harmony(PluginGuid);
            bool patched = SafeReads.Apply(_harmony);

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("NetworkCrashFix");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<Reporter>();

            if (patched)
            {
                Log.LogInfo($"{PluginName} {PluginVersion} loaded. The end of each network packet is read safely.");
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
