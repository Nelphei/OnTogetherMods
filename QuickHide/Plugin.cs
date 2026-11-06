using BepInEx;
using BepInEx.Logging;
using OnTogetherMods.Shared;
using UnityEngine;

namespace QuickHide
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.quickhide";
        public const string PluginName = "Quick Hide";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("QuickHide");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<HideController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.HideKey.Value} in split screen or " +
                "mini mode to hide the game, and again to bring it back.");
            if (!Application.runInBackground)
            {
                Log.LogWarning("The game pauses while another app is focused, so the hide key only works while " +
                    "the game is focused.");
            }
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
