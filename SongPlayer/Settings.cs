using System;
using System.IO;
using BepInEx.Configuration;

namespace SongPlayer
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.songplayer.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<string> SongFolder;
        public readonly ConfigEntry<int> MaxNotesAtOnce;
        public readonly ConfigEntry<int> VisibleRows;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            Enabled = config.Bind("Songs", "Enabled", true,
                "Show the song window when you sit at an instrument. The mod's icon at the bottom-right switches " +
                "this on and off, and it's remembered between games. Switching it off stops a song that's playing.");
            SongFolder = config.Bind("Songs", "SongFolder", "",
                "Folder the song list reads .mid files from. Leave empty to use Music\\On-Together.");
            MaxNotesAtOnce = config.Bind("Songs", "MaxNotesAtOnce", 6,
                new ConfigDescription(
                    "The most keys held down at the same time. Extra notes in big chords are skipped, " +
                    "which keeps busy songs from sounding muddy.",
                    new AcceptableValueRange<int>(1, 13)));

            VisibleRows = config.Bind("Display", "VisibleRows", 6,
                new ConfigDescription(
                    "How many songs the window shows before it scrolls.",
                    new AcceptableValueRange<int>(3, 15)));
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a music notes icon in the row of mod icons at the bottom-right, with a green dot while " +
                "Enabled is on. Clicking it switches Enabled.");
        }

        public string Folder()
        {
            string folder = SongFolder.Value;
            if (string.IsNullOrWhiteSpace(folder))
            {
                string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                if (string.IsNullOrEmpty(music))
                {
                    music = UnityEngine.Application.persistentDataPath;
                }
                folder = Path.Combine(music, "On-Together");
            }
            return folder;
        }
    }
}
