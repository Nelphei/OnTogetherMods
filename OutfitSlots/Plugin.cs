using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;

namespace OutfitSlots
{
    // Loads after BlueSage QoL (if it's installed), so its settings can be checked here.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.bluesage.ontogether.qoltweaks.beta", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "nelphei.ontogether.outfitslots";
        public const string PluginName = "Outfit Slots";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log;
        internal static Settings Settings;

        private Harmony _harmony;
        private GameObject _host;

        private void Awake()
        {
            Log = Logger;
            Settings = new Settings(Config);

            if (BlueSage.SlotsOn())
            {
                Log.LogWarning("BlueSage QoL's own style slots 4-9 are on, so Outfit Slots stays off: both change the same " +
                    "part of the game. Set EnableExtendedAvatarStyles = false in " + BlueSage.SettingsFile +
                    " (or switch them off in BlueSage's menu) and restart the game to use Outfit Slots.");
            }
            else
            {
                Slots.Store.Load();
                Slots.Active = true;
            }

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
            }
            catch (Exception e)
            {
                // Half the patches would be worse than none: the game's save could end up with a slot
                // number it can't load.
                Slots.Active = false;
                _harmony.UnpatchSelf();
                Log.LogError($"Outfit Slots couldn't hook into the game (has it been updated?), so it's off: {e}");
            }
            // Stops clicks on the row of mod icons reaching the game. Done once, by whichever
            // mod loads first.
            IconBar.Patch();

            // Runs on its own object so it survives scene loads.
            _host = new GameObject("OutfitSlots");
            _host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_host);
            _host.AddComponent<OutfitWindow>();

            if (Slots.Active)
            {
                Log.LogInfo($"{PluginName} {PluginVersion} loaded. Slots 4-9 are in the outfit screen's style list, " +
                    $"and {Settings.ToggleKey.Value} opens the quick-swap window. Saved in {Slots.Store.FilePath}.");
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
