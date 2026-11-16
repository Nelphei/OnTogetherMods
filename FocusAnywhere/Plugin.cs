using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace FocusAnywhere
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.focusanywhere";
        public const string PluginName = "Focus Anywhere";
        public const string PluginVersion = "0.6.0";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);
            Choices.Init();

            new Harmony(PluginGuid).PatchAll();
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // The icon and the objects window run on their own object so they survive scene loads.
            _host = new GameObject("FocusAnywhere");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<IconButton>();
            _host.AddComponent<ObjectsWindow>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. On: {Settings.Enabled.Value}, every activity: " +
                $"{Settings.AllActivities.Value}, any ground: {Settings.AnyGround.Value}, mid-air: {Settings.MidAir.Value}. " +
                $"Objects: {Choices.Summary()}.");
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
