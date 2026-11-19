using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace CardDesigner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.carddesigner";
        public const string PluginName = "Card Designer";
        public const string PluginVersion = "0.1.2";

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
            _host = new GameObject("CardDesigner");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<CardWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Click its icon, or the Design card button " +
                "on the game's ID card, to open it.");
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
