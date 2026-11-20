using System;
using System.Collections;
using OnTogetherMods.Shared;

namespace BubbleShell
{
    // Commands other mods add through Shared/ShellLink.cs (outfit, spot, swarm...). They're picked up
    // whenever the list grows, since mods load in any order. A name that's already a Bubble Shell
    // command is left out.
    internal static class LinkedCommands
    {
        private static int _seen = -1;

        public static void Sync(Shell shell)
        {
            Hashtable table = ShellLink.Table();
            if (table.Count == _seen)
            {
                return;
            }
            _seen = table.Count;
            foreach (DictionaryEntry pair in table)
            {
                if (!(pair.Key is string name) || !(pair.Value is object[] entry) || entry.Length < ShellLink.Size ||
                    !(entry[ShellLink.RunIndex] is Func<string[], string> run))
                {
                    continue;
                }
                Command existing = shell.Find(name);
                if (existing != null)
                {
                    if (existing.Group != (entry[ShellLink.ModIndex] as string))
                    {
                        Plugin.Log.LogWarning($"{entry[ShellLink.ModIndex]} adds a command called {name}, but Bubble Shell has one already, so it's left out.");
                    }
                    continue;
                }
                var complete = entry[ShellLink.CompleteIndex] as Func<string[], int, string[]>;
                shell.Register(new Command
                {
                    Name = name,
                    Group = entry[ShellLink.ModIndex] as string ?? "Mods",
                    Usage = entry[ShellLink.UsageIndex] as string ?? name,
                    Help = entry[ShellLink.HelpIndex] as string ?? "",
                    Run = (s, a) =>
                    {
                        Show(s, run(Words(a)));
                        return null;
                    },
                    Complete = complete == null ? null : (CommandComplete)((s, a, i) => complete(Words(a), i)),
                });
            }
        }

        private static string[] Words(Args args)
        {
            var words = new string[args.Count];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = args[i];
            }
            return words;
        }

        // What the mod sent back: "! " lines are warnings, "~ " lines hints, the rest plain text.
        private static void Show(Shell shell, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string line in text.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith("! ", StringComparison.Ordinal))
                {
                    shell.Warn(line.Substring(2));
                }
                else if (line.StartsWith("~ ", StringComparison.Ordinal))
                {
                    shell.Hint(line.Substring(2));
                }
                else
                {
                    shell.Print(line);
                }
            }
        }
    }
}
