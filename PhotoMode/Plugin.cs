using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PhotoMode
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.photomode";
        public const string PluginName = "Photo Mode";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();

            // Photo mode runs on its own object so it keeps working across scene loads,
            // even in setups where BepInEx's manager object gets disabled.
            _host = new GameObject("PhotoMode");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<PhotoModeController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.ToggleKey.Value} in a lobby to start.");
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
