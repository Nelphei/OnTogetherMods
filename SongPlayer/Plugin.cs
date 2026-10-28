using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SongPlayer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.songplayer";
        public const string PluginName = "Song Player";
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
            _host = new GameObject("SongPlayer");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<SongWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Sit at an instrument to open the song list. Songs folder: {Settings.Folder()}");
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
