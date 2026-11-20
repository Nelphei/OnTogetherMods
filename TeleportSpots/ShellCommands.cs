using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;
using UnityEngine.SceneManagement;

namespace TeleportSpots
{
    // The spot command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs). Works on
    // the same list as the window.
    internal static class ShellCommands
    {
        // The game's scenes by build index: 2 is the world.
        private const int WorldScene = 2;

        public static void Register()
        {
            ShellLink.Add("spot", Plugin.PluginName, "spot [name | save <name> | delete <name> | rename <name> <new name>]",
                "spot lists your saved spots. spot home teleports you to the spot called home (or its number). " +
                "spot save home saves where you're standing as home (moving it, if you had one already); " +
                "spot delete home and spot rename home house change the list.",
                Run, Complete);
        }

        private static string Run(string[] args)
        {
            SpotStore store = SpotsWindow.Store;
            if (store == null)
            {
                return "! Teleport Spots isn't ready yet.";
            }
            if (args.Length == 0 || Is(args[0], "list"))
            {
                return List(store);
            }
            if (Is(args[0], "save"))
            {
                return Save(store, string.Join(" ", args.Skip(1)));
            }
            if (Is(args[0], "delete") || Is(args[0], "remove"))
            {
                if (args.Length < 2)
                {
                    return "! Which spot? Usage: spot delete <name>";
                }
                Spot spot = Find(store, string.Join(" ", args.Skip(1)), out string why);
                if (spot == null)
                {
                    return "! " + why;
                }
                store.Spots.Remove(spot);
                return Saved(store, "Deleted " + spot.Name + ".");
            }
            if (Is(args[0], "rename"))
            {
                if (args.Length < 3)
                {
                    return "! Usage: spot rename <name> <new name> (put a name with spaces in \"quotes\").";
                }
                Spot spot = Find(store, args[1], out string why);
                if (spot == null)
                {
                    return "! " + why;
                }
                string name = SpotStore.CleanName(string.Join(" ", args.Skip(2)));
                if (name.Length == 0)
                {
                    return "! The new name is empty.";
                }
                string old = spot.Name;
                spot.Name = name;
                return Saved(store, $"Renamed {old} to {name}.");
            }
            return Go(store, string.Join(" ", args));
        }

        private static string List(SpotStore store)
        {
            if (store.Spots.Count == 0)
            {
                return "~ No spots saved yet. spot save home saves where you're standing.";
            }
            var text = new StringBuilder("Your spots:");
            for (int i = 0; i < store.Spots.Count; i++)
            {
                text.Append('\n').Append($"  {i + 1,2}  {store.Spots[i].Name}");
            }
            text.Append("\n~ spot <name> takes you there.");
            return text.ToString();
        }

        private static string Go(SpotStore store, string typed)
        {
            Spot spot = Find(store, typed, out string why);
            if (spot == null)
            {
                return "! " + why;
            }
            TextChannelManager channel = Channel();
            if (channel == null)
            {
                return "! Join a world first.";
            }
            string busy = Game.WhyBusy(channel);
            if (busy != null)
            {
                return "! " + busy + ".";
            }
            Game.Teleport(channel.MainMovementController, spot, Plugin.Settings.RestoreView.Value);
            return "Teleported to " + spot.Name + ".";
        }

        private static string Save(SpotStore store, string typed)
        {
            TextChannelManager channel = Channel();
            if (channel == null)
            {
                return "! Join a world first.";
            }
            string busy = Game.WhyBusy(channel);
            if (busy != null)
            {
                return "! " + busy + ".";
            }
            string name = SpotStore.CleanName(typed);
            if (name.Length == 0)
            {
                name = store.NextDefaultName();
            }
            Spot here = Game.Capture(channel.MainMovementController, name);
            int existing = store.Spots.FindIndex(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                here.Name = store.Spots[existing].Name;
                store.Spots[existing] = here;
                return Saved(store, $"Moved {here.Name} to where you're standing.");
            }
            store.Spots.Add(here);
            return Saved(store, $"Saved {name}.");
        }

        private static string Saved(SpotStore store, string message)
        {
            return store.Save() ? message : "! Couldn't save the spots file. See the BepInEx log.";
        }

        // A spot by its number or name: the whole name first, then the start of one.
        private static Spot Find(SpotStore store, string typed, out string why)
        {
            why = null;
            List<Spot> spots = store.Spots;
            if (spots.Count == 0)
            {
                why = "No spots saved yet. spot save home saves where you're standing.";
                return null;
            }
            if (int.TryParse(typed, out int number))
            {
                if (number >= 1 && number <= spots.Count)
                {
                    return spots[number - 1];
                }
                why = $"Pick a spot from 1 to {spots.Count}.";
                return null;
            }
            foreach (Func<Spot, bool> rule in new Func<Spot, bool>[]
                     {
                         s => string.Equals(s.Name, typed, StringComparison.OrdinalIgnoreCase),
                         s => s.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                     })
            {
                List<Spot> found = spots.Where(rule).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }
                if (found.Count > 1)
                {
                    why = $"{found.Count} spots start with {typed}: {string.Join(", ", found.Select(s => s.Name))}.";
                    return null;
                }
            }
            why = $"No spot called {typed}. spot lists them.";
            return null;
        }

        private static TextChannelManager Channel()
        {
            // Only in the world: on the main menu this would search the scene.
            return SceneManager.GetActiveScene().buildIndex == WorldScene ? NetworkSingleton<TextChannelManager>.I : null;
        }

        private static bool Is(string word, string expected) => string.Equals(word, expected, StringComparison.OrdinalIgnoreCase);

        private static string[] Complete(string[] args, int index)
        {
            SpotStore store = SpotsWindow.Store;
            IEnumerable<string> names = store != null ? store.Spots.Select(s => s.Name) : Enumerable.Empty<string>();
            if (index == 0)
            {
                return new[] { "save", "delete", "rename", "list" }.Concat(names).ToArray();
            }
            if (index == 1 && args.Length > 0 && (Is(args[0], "delete") || Is(args[0], "rename") || Is(args[0], "save")))
            {
                return names.ToArray();
            }
            return null;
        }
    }
}
