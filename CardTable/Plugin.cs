using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace CardTable
{
    // Card games in a window over the game. Solitaire (Klondike) for now; Go Fish with other
    // players comes next.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.cardtable";
        public const string PluginName = "Card Table";
        public const string PluginVersion = "0.1.1";

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
            _host = new GameObject("CardTable");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<TableWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Click the cards icon to play.");
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
