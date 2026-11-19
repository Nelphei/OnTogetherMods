using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace HellDesk
{
    // Plays DOOM in a window over the game, with Managed Doom (a C# port of the original DOOM,
    // GPLv2) running inside the mod. Freedoom (free DOOM-compatible game files) comes with it; players
    // can add their own WADs.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        // The mod was called Doom Together until 2026-11-19. The GUID keeps that name so the settings
        // file (named after it) carries over, and so an old DoomTogether folder left in the plugins
        // folder is skipped as a duplicate instead of running a second copy.
        public const string PluginGuid = "nelphei.ontogether.doomtogether";
        public const string PluginName = "Hell Desk";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;
        // The folder this DLL is in: Freedoom and the music sound bank sit beside it.
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
            _host = new GameObject("HellDesk");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<DoomWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Click the demon icon to play.");
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
