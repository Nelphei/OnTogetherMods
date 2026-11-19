using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DoomTogether
{
    internal enum ScreenSize
    {
        Small,
        Medium,
        Large,
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.doomtogether.cfg after the first
    // launch. DOOM's own settings (its menu's volumes, key bindings...) are in
    // BepInEx\config\DoomTogether\managed-doom.cfg, and saved games in the folders beside it.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<string> Wad;
        public readonly ConfigEntry<string> WadFolder;
        public readonly ConfigEntry<bool> SitDown;
        public readonly ConfigEntry<bool> PauseWithoutKeys;
        public readonly ConfigEntry<int> Volume;
        public readonly ConfigEntry<bool> Music;
        public readonly ConfigEntry<ScreenSize> Size;
        public readonly ConfigEntry<bool> Smooth;
        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<bool> ControlsSeen;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Opens and closes the DOOM window. None by default (every F key is taken by other mods): " +
                "click the demon icon instead.");

            Wad = config.Bind("Game", "Wad", "",
                "The game file (IWAD) last played, by file name, e.g. freedoom2.wad. Empty picks one by itself. " +
                "Also the WAD button in the window.");
            WadFolder = config.Bind("Game", "WadFolder", "",
                "Where to look for your own WAD files (DOOM.WAD, DOOM2.WAD...). Empty means " +
                "Documents/On-Together/Doom.");
            PauseWithoutKeys = config.Bind("Game", "PauseWithoutKeys", true,
                "Freeze DOOM while it doesn't have the keyboard (you clicked outside the window, or opened chat).");

            SitDown = config.Bind("Character", "SitDown", true,
                "Opening DOOM while you're standing sits your character down with the handheld console. " +
                "If you're already sitting (at your desk, flying with Desk Swarm...), nothing changes. " +
                "Closing DOOM never stands you up.");

            Volume = config.Bind("Sound", "Volume", 70,
                new ConfigDescription(
                    "How loud DOOM is, in percent, on top of the volumes in DOOM's own options menu. " +
                    "Also the speaker button in the window.",
                    new AcceptableValueRange<int>(0, 100)));
            Music = config.Bind("Sound", "Music", true,
                "Play DOOM's music.");

            Size = config.Bind("Display", "Size", ScreenSize.Medium,
                "How big the DOOM screen is: Small, Medium or Large. Also the size button in the window.");
            Smooth = config.Bind("Display", "Smooth", false,
                "Smooth the picture when it's stretched, instead of keeping the pixels sharp.");
            WindowX = config.Bind("Display", "WindowX", -1f,
                "Where you dragged the window to: its left edge, in pixels on a 1080-pixel-high screen. -1 keeps " +
                "it with the other mods' windows. Double-clicking the window's title row sets this back to -1.");
            WindowY = config.Bind("Display", "WindowY", -1f,
                "Where you dragged the window to: its top edge, in pixels on a 1080-pixel-high screen.");
            ControlsSeen = config.Bind("Display", "ControlsSeen", false,
                "The controls card (the ? button) shows by itself the first time you open DOOM, and this " +
                "remembers that it has. Set it to false to see it by itself again.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a demon icon in the row of mod icons at the bottom-right. Clicking it opens and closes the window.");
        }

        // Where players put their own WADs.
        public string Folder()
        {
            string folder = WadFolder.Value;
            if (string.IsNullOrWhiteSpace(folder))
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documents))
                {
                    documents = Application.persistentDataPath;
                }
                folder = Path.Combine(documents, "On-Together", "Doom");
            }
            return folder;
        }

        // DOOM's own settings file and saved games: BepInEx\config\DoomTogether.
        public static string DataFolder()
        {
            return Path.Combine(Paths.ConfigPath, "DoomTogether");
        }
    }
}
