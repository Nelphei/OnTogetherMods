using BepInEx;
using BepInEx.Logging;
using OnTogetherMods.Shared;
using UnityEngine;

namespace ChatRange
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.chatrange";
        public const string PluginName = "Chat Range";
        public const string PluginVersion = "0.2.0";

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
            _host = new GameObject("ChatRange");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<RangeDisplay>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. The list shows next to the chat box " +
                $"while its Local tab is open.");
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
