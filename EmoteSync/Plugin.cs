using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EmoteSync
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.emotesync";
        public const string PluginName = "Emote Sync";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("EmoteSync");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<SyncController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.ToggleKey.Value} to turn emote sync on or off.");
        }

        private void OnDestroy()
        {
            if (_host != null)
            {
                Destroy(_host);
            }
        }
    }
}
