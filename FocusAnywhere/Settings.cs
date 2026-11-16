using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace FocusAnywhere
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.focusanywhere.cfg after the first launch.
    // Changes made in the file take effect the next time the game starts. Enabled and the [Objects]
    // settings also change from the mod's icon and its objects window while you play.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> AllActivities;
        public readonly ConfigEntry<bool> AnyGround;
        public readonly ConfigEntry<bool> MidAir;

        // What shows while you focus (Choices.cs). True = shown, the game's normal look.
        public readonly ConfigEntry<bool> ShowDesk;
        public readonly ConfigEntry<bool> ShowPlaced;
        public readonly ConfigEntry<bool> ShowHands;
        public readonly ConfigEntry<bool> ShowHeadphones;
        public readonly ConfigEntry<bool> ShowFloaties;
        public readonly ConfigEntry<bool> ShowEffects;
        public readonly ConfigEntry<string> Exceptions;

        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            Enabled = config.Bind("Focus", "Enabled", true,
                "Switches the whole mod on or off. The mod's icon at the bottom-right changes this, and it's " +
                "remembered between games. Switched off, the focus menu goes back to the game's own activities " +
                "the next time you're not focusing, and every object shows again.");
            AllActivities = config.Bind("Focus", "AllActivities", true,
                "The focus menu shows every activity in the game: the normal ones, the water ones, and the " +
                "ones from seat spots (play tower, bean bag, tree hanger...). Seat spots keep their own menu.");
            AnyGround = config.Bind("Focus", "AnyGround", true,
                "The focus key opens the menu on any ground, near the music hill, and next to a seat someone " +
                "else is using. The basketball court stays off limits.");
            MidAir = config.Bind("Focus", "MidAir", true,
                "The focus key works in the air too: you stop where you are and focus floating there. " +
                "Closing the menu without picking, or standing up, lets you fall again. Needs AnyGround on " +
                "(or BlueSage QoL's own Focus Anywhere).");

            // 0.4.0's one switch, read before the new settings are added (see MoveOldHideObjects).
            string oldHideObjects = TakeOrphan(config, new ConfigDefinition("Focus", "HideObjects"), out bool newSettingsExist,
                new ConfigDefinition("Objects", "ShowDesk"));

            const string everyone = " Everyone sees it, with or without the mod. Right-clicking the mod's icon opens a " +
                "window with this switch, also while you're focusing, and it's remembered between games.";
            ShowDesk = config.Bind("Objects", "ShowDesk", true,
                "Show your personal desk in desk activities. Hidden, your pet sits on your head instead." + everyone);
            ShowPlaced = config.Bind("Objects", "ShowPlaced", true,
                "Show the objects set down in front of you: laptop, tablet, notebook, easel, watercolour set, " +
                "ironing board, treadmill, bento box, typewriter, and the laptop or notebook on a floater or water bed." + everyone);
            ShowHands = config.Bind("Objects", "ShowHands", true,
                "Show what you hold: book, phone, latte, pens and brushes, iron, game controller, knitting, jump rope..." + everyone);
            ShowHeadphones = config.Bind("Objects", "ShowHeadphones", true,
                "Show the headphones some activities put on your head." + everyone);
            ShowFloaties = config.Bind("Objects", "ShowFloaties", true,
                "Show the floater or water bed in their activities. Hidden, you sit lower (at swimming height) and " +
                "don't spin, because every player's game works that out from whether the floatie shows." + everyone);
            ShowEffects = config.Bind("Objects", "ShowEffects", true,
                "Show the sleep Zzz." + everyone);
            Exceptions = config.Bind("Objects", "Exceptions", "",
                "Objects that don't follow their group, set in the window's \"Right now\" list. Each is the game's " +
                "object name and show or hide, separated by commas, for example MD_IroinTable:show, MD_Iron:hide.");

            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show an icon in the row of mod icons at the bottom-right, with a green dot while the mod is on. " +
                "Clicking it switches Enabled, and right-clicking it opens the window for the [Objects] settings.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write extra details to the BepInEx log (the activity lists, the menu buttons, and each activity's objects).");

            MoveOldHideObjects(config, oldHideObjects, newSettingsExist);
        }

        // 0.4.0 had one switch, HideObjects, that hid the desk and every object but the Zzz (and never
        // touched floaters). Carry it over once, then take it out of the file so it doesn't look like it
        // still does something.
        private void MoveOldHideObjects(ConfigFile config, string old, bool newSettingsExist)
        {
            if (old == null)
            {
                return;
            }
            if (!newSettingsExist && old.Trim().ToLowerInvariant() == "true")
            {
                ShowDesk.Value = false;
                ShowPlaced.Value = false;
                ShowHands.Value = false;
                ShowHeadphones.Value = false;
            }
            config.Save();
        }

        // Removes a setting this version no longer reads from the file and returns its value (null if
        // it wasn't there). newSettingsExist: whether `probe`, a setting from this version, was already
        // in the file before this run.
        private static string TakeOrphan(ConfigFile config, ConfigDefinition old, out bool newSettingsExist, ConfigDefinition probe)
        {
            newSettingsExist = false;
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")
                    ?.GetValue(config) as Dictionary<ConfigDefinition, string>;
                if (orphans == null)
                {
                    return null;
                }
                newSettingsExist = orphans.ContainsKey(probe);
                if (orphans.TryGetValue(old, out string value))
                {
                    orphans.Remove(old);
                    return value;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogDebug($"Couldn't read old settings from the settings file: {e.Message}");
            }
            return null;
        }
    }
}
