using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;

namespace BubbleShell
{
    // open: does what clicking (or right-clicking) another mod's icon does. Works with every mod that
    // has an icon in the row, without changing them: the icon row keeps a click slot for each icon in
    // shared data (IconBar.ClickIcon), and the mod picks the click up from its next Draw as usual.
    internal static class ModCommands
    {
        public const string Group = "Mods";

        private const string OwnId = "BubbleShell";

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "open",
                Aliases = new[] { "click" },
                Group = Group,
                Usage = "open [mod] [right]",
                Help = "Does what clicking a mod's icon does: opens its window, or switches it on or off. " +
                       "Add right for its right-click (Outfit Slots: next outfit, Desk Swarm: take off...). " +
                       "Just open lists the mods you can open from here.",
                Run = Open,
                Complete = (s, a, i) => i == 0 ? Icons().Select(Key) : i == 1 ? new[] { "right" } : null,
            });
        }

        private static IEnumerator Open(Shell shell, Args args)
        {
            List<IconBar.IconInfo> icons = Icons();
            if (args.Count == 0)
            {
                if (icons.Count == 0)
                {
                    shell.Warn(Game.InWorld ? "No other mod icons are showing." : "Join a world first.");
                    return null;
                }
                shell.Print("Mods you can open (like clicking their icon):");
                foreach (IconBar.IconInfo icon in icons.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string state = StateText(icon.State);
                    string hint = string.IsNullOrEmpty(icon.Hint) ? "" : "  " + icon.Hint.Replace("\n", " ");
                    shell.Print($"  {Key(icon),-15} {state,-5}{hint}");
                }
                shell.Hint("open doomtogether opens it; open outfitslots right does its right-click.");
                return null;
            }

            bool right = args.Count > 1 && args.Is(args.Count - 1, "right", "r", "rightclick");
            string typed = right ? string.Join(" ", Enumerable.Range(0, args.Count - 1).Select(i => args[i])) : args.Rest();
            List<IconBar.IconInfo> matches = Match(icons, typed);
            if (matches.Count == 0)
            {
                shell.Warn($"No mod icon called {typed} is showing. Just open lists them.");
                return null;
            }
            if (matches.Count > 1)
            {
                shell.Warn($"{typed} could be {string.Join(" or ", matches.Select(m => m.Name))}. Type more of the name.");
                return null;
            }
            IconBar.IconInfo chosen = matches[0];
            if (!IconBar.ClickIcon(chosen.Id, right))
            {
                shell.Warn($"{chosen.Name} isn't showing its icon right now.");
                return null;
            }
            shell.Print(right ? $"Right-clicked {chosen.Name}." : $"Clicked {chosen.Name}.");
            // A window it opens may want the keyboard (DOOM does), so the console lets go of it.
            // Switches (Desk Snap, Focus Anywhere...) keep the console typing.
            if (chosen.State != IconBar.State.On && chosen.State != IconBar.State.Off)
            {
                ShellWindow.LetGoOfKeys();
                shell.Hint("(Click the console to type again.)");
            }
            return null;
        }

        private static List<IconBar.IconInfo> Icons()
        {
            return Game.InWorld ? IconBar.ShownIcons().Where(i => i.Id != OwnId).ToList() : new List<IconBar.IconInfo>();
        }

        // "Desk Snap" -> "desksnap", for typing.
        private static string Key(IconBar.IconInfo icon) => Squash(icon.Name);

        private static string Squash(string text) => new string((text ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        // The one icon this names: its whole name first, then the start of it, then any part.
        private static List<IconBar.IconInfo> Match(List<IconBar.IconInfo> icons, string typed)
        {
            string key = Squash(typed);
            if (key.Length == 0)
            {
                return new List<IconBar.IconInfo>();
            }
            foreach (Func<IconBar.IconInfo, bool> rule in new Func<IconBar.IconInfo, bool>[]
                     {
                         i => Key(i) == key || Squash(i.Id) == key,
                         i => Key(i).StartsWith(key, StringComparison.Ordinal),
                         i => Key(i).Contains(key),
                     })
            {
                List<IconBar.IconInfo> found = icons.Where(rule).ToList();
                if (found.Count > 0)
                {
                    return found;
                }
            }
            return new List<IconBar.IconInfo>();
        }

        private static string StateText(IconBar.State state)
        {
            switch (state)
            {
                case IconBar.State.Open: return "open";
                case IconBar.State.On: return "on";
                case IconBar.State.Off: return "off";
                default: return "";
            }
        }
    }
}
