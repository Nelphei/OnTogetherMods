using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BubbleShell
{
    // help, clear, echo, history, alias, bind, exec. No Unity in here (tested outside the game).
    internal static class BasicCommands
    {
        public const string Group = "Shell";

        // The order groups are listed in by help.
        public static readonly string[] GroupOrder = { Group, "You", "Mods", "Info", "Laughs", "Looks" };

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "help",
                Aliases = new[] { "?" },
                Group = Group,
                Usage = "help [command]",
                Help = "Lists every command, or tells you about one.",
                Run = Help,
                Complete = (s, a, i) => i == 0 ? s.Commands.Select(c => c.Name) : null,
            });
            shell.Register(new Command
            {
                Name = "clear",
                Aliases = new[] { "cls" },
                Group = Group,
                Usage = "clear",
                Help = "Wipes the console clean. (Ctrl+L does it too.)",
                Run = (s, a) =>
                {
                    s.Clear();
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "echo",
                Group = Group,
                Usage = "echo <text>",
                Help = "Prints the text. Handy in aliases and autoexec.",
                Run = (s, a) =>
                {
                    s.Print(a.Rest());
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "history",
                Group = Group,
                Usage = "history [how many]",
                Help = "Shows the lines you typed last (20 unless you say). ↑ and ↓ go through them too.",
                Run = History,
            });
            shell.Register(new Command
            {
                Name = "alias",
                Group = Group,
                Usage = "alias [name] [\"commands\"]",
                Help = "Makes a new command out of others: alias hi \"emote 1; say hello!\". Words typed after an " +
                       "alias go on the end (alias e \"emote\", then e 3). Just alias lists them; alias name shows one.",
                Run = Alias,
                Complete = (s, a, i) => i == 0 ? s.Aliases.Keys : null,
            });
            shell.Register(new Command
            {
                Name = "unalias",
                Group = Group,
                Usage = "unalias <name>",
                Help = "Forgets an alias.",
                Run = (s, a) =>
                {
                    if (a.Count == 0)
                    {
                        s.Warn("Which alias? Usage: unalias <name>");
                    }
                    else if (s.RemoveAlias(a[0]))
                    {
                        s.Print($"Forgot alias {a[0]}.");
                    }
                    else
                    {
                        s.Warn($"There's no alias called {a[0]}.");
                    }
                    return null;
                },
                Complete = (s, a, i) => i == 0 ? s.Aliases.Keys : null,
            });
            shell.Register(new Command
            {
                Name = "bind",
                Group = Group,
                Usage = "bind [key] [\"commands\"]",
                Help = "Runs commands when you press a key in the game (not while typing): bind g \"emote 3\". " +
                       "Just bind lists them; bind key shows one. Keys go by Unity's names: g, keypad1, f6, " +
                       "leftbracket...",
                Run = Bind,
                Complete = (s, a, i) => i == 0 ? s.Binds.Keys : null,
            });
            shell.Register(new Command
            {
                Name = "unbind",
                Group = Group,
                Usage = "unbind <key>",
                Help = "Takes a key's commands off it.",
                Run = (s, a) =>
                {
                    string key = a.Count > 0 ? s.KeyName(a[0]) ?? a[0] : null;
                    if (key == null)
                    {
                        s.Warn("Which key? Usage: unbind <key>");
                    }
                    else if (s.RemoveBind(key))
                    {
                        s.Print($"{key} does nothing now.");
                    }
                    else
                    {
                        s.Warn($"{key} isn't bound.");
                    }
                    return null;
                },
                Complete = (s, a, i) => i == 0 ? s.Binds.Keys : null,
            });
            shell.Register(new Command
            {
                Name = "binds",
                Group = Group,
                Usage = "binds",
                Help = "Lists your binds.",
                Run = (s, a) =>
                {
                    ListBinds(s);
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "exec",
                Group = Group,
                Usage = "exec <file>",
                Help = "Runs every line of a file in BepInEx/config/BubbleShell (exec party runs party.cfg).",
                Run = Exec,
                Complete = (s, a, i) => i == 0 ? Files(s) : null,
            });
        }

        private static IEnumerator Help(Shell shell, Args args)
        {
            if (args.Count > 0)
            {
                Command command = shell.Find(args[0]);
                if (command != null)
                {
                    shell.Print(command.Usage);
                    shell.Hint("  " + command.Help);
                    if (command.Aliases.Length > 0)
                    {
                        shell.Hint("  Also: " + string.Join(", ", command.Aliases));
                    }
                }
                else if (shell.Aliases.TryGetValue(args[0], out string alias))
                {
                    shell.Print($"{args[0]} is your alias for: {alias}");
                }
                else
                {
                    shell.Warn($"No command called {args[0]}.");
                }
                return null;
            }

            IEnumerable<string> groups = GroupOrder.Concat(shell.Commands.Select(c => c.Group)).Distinct();
            foreach (string group in groups)
            {
                List<Command> commands = shell.Commands.Where(c => c.Group == group).ToList();
                if (commands.Count == 0)
                {
                    continue;
                }
                shell.PrintRich(Shell.Colored(Shell.BrightColor, Shell.Escape(group)));
                shell.Print("  " + string.Join("  ", commands.Select(c => c.Name)));
            }
            if (shell.Aliases.Count > 0)
            {
                shell.PrintRich(Shell.Colored(Shell.BrightColor, "Your aliases"));
                shell.Print("  " + string.Join("  ", shell.Aliases.Keys));
            }
            shell.Hint("help <command> tells you more. Tab finishes words, ↑ ↓ go through what you typed, Ctrl+C stops things.");
            return null;
        }

        private static IEnumerator History(Shell shell, Args args)
        {
            int count = 20;
            if (args.Count > 0 && (!int.TryParse(args[0], out count) || count < 1))
            {
                shell.Warn("Usage: history [how many]");
                return null;
            }
            List<string> history = shell.History;
            // The history line itself was just added, so leave it out.
            int end = history.Count > 0 && history[history.Count - 1].TrimStart().StartsWith("history", StringComparison.OrdinalIgnoreCase)
                ? history.Count - 1
                : history.Count;
            int start = Math.Max(0, end - count);
            if (start >= end)
            {
                shell.Hint("Nothing typed yet.");
            }
            for (int i = start; i < end; i++)
            {
                shell.Print($"{i + 1,4}  {history[i]}");
            }
            return null;
        }

        private static IEnumerator Alias(Shell shell, Args args)
        {
            if (args.Count == 0)
            {
                if (shell.Aliases.Count == 0)
                {
                    shell.Hint("No aliases yet. Try: alias hi \"emote 1; say hello!\"");
                }
                foreach (KeyValuePair<string, string> alias in shell.Aliases)
                {
                    shell.Print($"{alias.Key} = {alias.Value}");
                }
                return null;
            }
            string name = args[0];
            if (args.Count == 1)
            {
                if (shell.Aliases.TryGetValue(name, out string line))
                {
                    shell.Print($"{name} = {line}");
                }
                else
                {
                    shell.Warn($"There's no alias called {name}.");
                }
                return null;
            }
            if (shell.Find(name) != null)
            {
                shell.Warn($"{name} is already a command, so it can't be an alias too.");
                return null;
            }
            if (name.Any(c => char.IsWhiteSpace(c) || c == ';' || c == '"'))
            {
                shell.Warn("An alias's name can't have spaces, quotes or semicolons in it.");
                return null;
            }
            string commands = args.Line(1);
            shell.SetAlias(name, commands);
            shell.Print($"{name} = {commands}");
            return null;
        }

        private static IEnumerator Bind(Shell shell, Args args)
        {
            if (args.Count == 0)
            {
                ListBinds(shell);
                return null;
            }
            string key = shell.KeyName(args[0]);
            if (key == null)
            {
                shell.Warn($"There's no key called {args[0]}. Keys go by Unity's names: a, g, keypad1, f6, alpha1 (the 1 above the letters)...");
                return null;
            }
            if (args.Count == 1)
            {
                if (shell.Binds.TryGetValue(key, out string bound))
                {
                    shell.Print($"{key} = {bound}");
                }
                else
                {
                    shell.Hint($"{key} isn't bound.");
                }
                return null;
            }
            string commands = args.Line(1);
            shell.SetBind(key, commands);
            shell.Print($"{key} = {commands}");
            if (KeyWarning != null)
            {
                string warning = KeyWarning(key);
                if (warning != null)
                {
                    shell.Warn(warning);
                }
            }
            return null;
        }

        // Says when a key is already used by the game or another mod (the game fills this in).
        public static Func<string, string> KeyWarning;

        private static void ListBinds(Shell shell)
        {
            if (shell.Binds.Count == 0)
            {
                shell.Hint("No binds yet. Try: bind g \"emote 3\"");
            }
            foreach (KeyValuePair<string, string> bind in shell.Binds)
            {
                shell.Print($"{bind.Key} = {bind.Value}");
            }
        }

        private static IEnumerator Exec(Shell shell, Args args)
        {
            if (args.Count == 0)
            {
                shell.Warn("Which file? Usage: exec <file>");
                return null;
            }
            string path = shell.FindFile(args[0]);
            if (path == null)
            {
                shell.Warn($"There's no {args[0]} in BepInEx/config/BubbleShell.");
                return null;
            }
            shell.Hint($"Running {Path.GetFileName(path)}...");
            shell.RunText(File.ReadAllText(path));
            return null;
        }

        private static IEnumerable<string> Files(Shell shell)
        {
            try
            {
                return Directory.Exists(shell.Folder)
                    ? Directory.GetFiles(shell.Folder, "*.cfg").Select(Path.GetFileNameWithoutExtension)
                    : Enumerable.Empty<string>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
