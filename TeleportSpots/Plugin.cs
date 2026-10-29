using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TeleportSpots
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.teleportspots";
        public const string PluginName = "Teleport Spots";
        public const string PluginVersion = "0.1.1";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("TeleportSpots");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<SpotsWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.ToggleKey.Value} to open the teleport window.");
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
