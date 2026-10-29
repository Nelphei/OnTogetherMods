using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DeskSnap
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.desksnap";
        public const string PluginName = "Desk Snap";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("DeskSnap");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<SnapController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Snapping is {(Settings.Enabled.Value ? "on" : "off")}; " +
                $"press {Settings.ToggleKey.Value} to turn it on or off.");
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
