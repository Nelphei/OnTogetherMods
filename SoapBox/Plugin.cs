using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace SoapBox
{
    // Soap Box: a tiny games console in a window. Carts (small games written in Lua) run on a
    // 192 x 128, 16-colour screen inside a pastel handheld. The console itself is in Machine\ (no
    // Unity); this side draws it, feeds it the keyboard and mouse, and sits your character down.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.soapbox";
        public const string PluginName = "Soap Box";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;
        // The folder this DLL is in: the bundled carts sit beside it.
        internal static string ModFolder;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            ModFolder = System.IO.Path.GetDirectoryName(Info.Location);

            new Harmony(PluginGuid).PatchAll();
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("SoapBox");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<ConsoleWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Click the handheld icon to play.");
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
