using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace NameDesigner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.namedesigner";
        public const string PluginName = "Name Designer";
        public const string PluginVersion = "0.2.2";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            new Harmony(PluginGuid).PatchAll();
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("NameDesigner");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<DesignerWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.ToggleKey.Value} to open it. " +
                "It also opens by itself next to the Create Session box.");
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
