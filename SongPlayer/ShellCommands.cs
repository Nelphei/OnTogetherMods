using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;

namespace SongPlayer
{
    // The song command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs). Works
    // while you sit at an instrument, like the window.
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("song", Plugin.PluginName, "song [name | play <name> | stop]",
                "song lists the songs in your songs folder. song <name> (or song play <name>, or its number) plays one " +
                "on the instrument you're sitting at; song stop stops it. Others hear it too.",
                Run, Complete);
        }

        private static string Run(string[] args)
        {
            SongWindow window = SongWindow.Instance;
            if (window == null)
            {
                return "! Song Player isn't ready yet.";
            }
            List<string> files = window.SongFiles();
            if (args.Length == 0 || Is(args[0], "list"))
            {
                return List(window, files);
            }
            if (Is(args[0], "stop"))
            {
                return window.CommandStop() ? "Stopped." : "~ Nothing's playing.";
            }
            if (!window.AtInstrument)
            {
                return Plugin.Settings.Enabled.Value
                    ? "! Sit at an instrument first."
                    : "! Song Player is switched off. Click its icon (or open songplayer) to switch it on.";
            }
            string typed = string.Join(" ", Is(args[0], "play") ? args.Skip(1) : args);
            string file = Find(files, typed, out string why);
            if (file == null)
            {
                return "! " + why;
            }
            string error = window.CommandPlay(file);
            return error == null ? $"Playing {Path.GetFileNameWithoutExtension(file)}." : "! " + error.TrimEnd('.') + ".";
        }

        private static string List(SongWindow window, List<string> files)
        {
            if (files.Count == 0)
            {
                return $"~ No songs yet. Put .mid files in {Plugin.Settings.Folder()}.";
            }
            var text = new StringBuilder("Your songs:");
            for (int i = 0; i < files.Count; i++)
            {
                text.Append('\n').Append($"  {i + 1,2}  {Path.GetFileNameWithoutExtension(files[i])}");
            }
            string playing = window.PlayingName;
            text.Append(playing != null ? $"\n~ Playing: {playing}. song stop stops it." : "\n~ song <name> plays one, sitting at an instrument.");
            return text.ToString();
        }

        // A song by its number in the list, or its name: the whole name, then the start, then any part.
        private static string Find(List<string> files, string typed, out string why)
        {
            why = null;
            if (files.Count == 0)
            {
                why = $"No songs yet. Put .mid files in {Plugin.Settings.Folder()}.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(typed))
            {
                why = "Which song? song lists them.";
                return null;
            }
            if (int.TryParse(typed, out int number))
            {
                if (number >= 1 && number <= files.Count)
                {
                    return files[number - 1];
                }
                why = $"Pick a song from 1 to {files.Count}.";
                return null;
            }
            foreach (Func<string, bool> rule in new Func<string, bool>[]
                     {
                         n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase),
                         n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                         n => n.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0,
                     })
            {
                List<string> found = files.Where(f => rule(Path.GetFileNameWithoutExtension(f))).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }
                if (found.Count > 1)
                {
                    why = $"{found.Count} songs match {typed}. Type more of the name, or use its number.";
                    return null;
                }
            }
            why = $"No song called {typed}. song lists them.";
            return null;
        }

        private static bool Is(string word, string expected) => string.Equals(word, expected, StringComparison.OrdinalIgnoreCase);

        private static string[] Complete(string[] args, int index)
        {
            SongWindow window = SongWindow.Instance;
            IEnumerable<string> names = window != null ? window.SongFiles().Select(Path.GetFileNameWithoutExtension) : Enumerable.Empty<string>();
            if (index == 0)
            {
                return new[] { "play", "stop", "list" }.Concat(names).ToArray();
            }
            return index == 1 && args.Length > 0 && Is(args[0], "play") ? names.ToArray() : null;
        }
    }
}
