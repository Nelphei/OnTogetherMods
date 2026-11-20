using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace BubbleShell
{
    // A dev console that drops down from the top of the screen (the ` key): commands to move around,
    // sit, emote and look things up, plus toys and silly looks only you see.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.bubbleshell";
        public const string PluginName = "Bubble Shell";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;
        // The folder this DLL is in: the pixel font sits beside it.
        internal static string ModFolder;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            ModFolder = System.IO.Path.GetDirectoryName(Info.Location);

            new Harmony(PluginGuid).PatchAll();
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever mod loads first.
            IconBar.Patch();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("BubbleShell");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<ShellWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press {Settings.ConsoleKey.Value} (or click the bubble icon) for the console.");
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
