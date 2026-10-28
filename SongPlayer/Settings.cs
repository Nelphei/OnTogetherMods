using System;
using System.IO;
using BepInEx.Configuration;

namespace SongPlayer
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.songplayer.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<string> SongFolder;
        public readonly ConfigEntry<int> MaxNotesAtOnce;
        public readonly ConfigEntry<int> VisibleRows;

        public Settings(ConfigFile config)
        {
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
        }

        public string Folder()
        {
            string folder = SongFolder.Value;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "On-Together");
            }
            return folder;
        }
    }
}
