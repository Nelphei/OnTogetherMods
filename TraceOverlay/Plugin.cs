using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TraceOverlay
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.traceoverlay";
        public const string PluginName = "Trace Overlay";
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
            _host = new GameObject("TraceOverlay");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<TraceWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Start drawing on a board to open it. Images folder: {Settings.Folder()}");
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
