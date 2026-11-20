using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;

namespace BuddyUp
{
    // The buddy, ride and walk commands in Bubble Shell's console, if it's installed (see
    // Shared/ShellLink.cs). They do what the window's buttons do.
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("buddy", Plugin.PluginName, "buddy [name | yes <name> | no <name> | bye <name> | stop | shake]",
                "buddy shows your buddies and who else has Buddy Up. buddy <name> asks them to be your buddy (or says " +
                "yes, if they asked you); buddy no <name> says no; buddy bye <name> ends it. buddy stop gets you off a " +
                "head or stops walking; buddy shake shakes everyone off your head.",
                Buddy, CompleteBuddy);
            ShellLink.Add("ride", Plugin.PluginName, "ride <buddy>",
                "Climbs onto a buddy's head. buddy stop (or the stand-up key) hops you off.",
                args => Together(args, ride: true), CompleteBuddies);
            ShellLink.Add("walk", Plugin.PluginName, "walk <buddy> | walk stop",
                "Walks with a buddy, by itself. walk stop stops.",
                args => Together(args, ride: false), CompleteBuddies);
        }

        private static string Buddy(string[] args)
        {
            BuddyController controller = BuddyController.Instance;
            if (controller == null || !Game.InWorld)
            {
                return "! Join a world first.";
            }
            Buddies buddies = controller.BuddyList;
            if (!buddies.InLobby)
            {
                return "! You're not in a session.";
            }
            if (args.Length == 0)
            {
                return Status(controller, buddies);
            }
            string first = args[0].ToLowerInvariant();
            string rest = string.Join(" ", args.Skip(1));
            switch (first)
            {
                case "stop":
                    if (controller.RidingOn != 0)
                    {
                        controller.CommandGetOff();
                        return "Hopped off.";
                    }
                    if (controller.WalkingWith != 0)
                    {
                        controller.CommandStopWalking();
                        return "Stopped walking.";
                    }
                    return "~ You're not riding or walking with anyone.";
                case "shake":
                    if (!buddies.RidersOf(buddies.Me).Any())
                    {
                        return "~ Nobody's on your head.";
                    }
                    controller.CommandShakeOff();
                    return "Shook them off!";
                case "yes":
                case "no":
                {
                    ulong id = Find(buddies.Requests, rest, "asking to be your buddy", out string why);
                    if (id == 0)
                    {
                        return "! " + why;
                    }
                    buddies.Answer(id, first == "yes");
                    return first == "yes" ? $"You and {Name(id)} are buddies now!" : $"Said no to {Name(id)}.";
                }
                case "bye":
                {
                    ulong id = Find(buddies.List, rest, "your buddy", out string why);
                    if (id == 0)
                    {
                        return "! " + why;
                    }
                    buddies.End(id);
                    return $"You and {Name(id)} aren't buddies any more.";
                }
                default:
                {
                    string typed = string.Join(" ", args);
                    ulong buddy = Find(buddies.List, typed, null, out _);
                    if (buddy != 0)
                    {
                        return $"~ {Name(buddy)} is already your buddy. ride or walk with them.";
                    }
                    ulong id = Find(buddies.Requests.Concat(buddies.Others), typed, "in this session with Buddy Up", out string why);
                    if (id == 0)
                    {
                        return "! " + why;
                    }
                    bool theyAsked = buddies.Requests.Contains(id);
                    buddies.Ask(id);
                    return theyAsked ? $"You and {Name(id)} are buddies now!" : $"Asked {Name(id)} to be your buddy.";
                }
            }
        }

        private static string Status(BuddyController controller, Buddies buddies)
        {
            var text = new StringBuilder();
            text.Append(buddies.List.Count > 0 ? "Buddies: " + Names(buddies.List) : "No buddies yet.");
            List<ulong> requests = buddies.Requests.ToList();
            if (requests.Count > 0)
            {
                text.Append("\nAsking to be your buddy: ").Append(Names(requests)).Append("  (buddy yes <name>)");
            }
            List<ulong> others = buddies.Others.Where(id => !requests.Contains(id)).ToList();
            if (others.Count > 0)
            {
                text.Append("\nAlso have Buddy Up: ").Append(Names(others)).Append("  (buddy <name> asks them)");
            }
            if (controller.RidingOn != 0)
            {
                text.Append($"\nYou're on {Name(controller.RidingOn)}'s head.");
            }
            if (controller.WalkingWith != 0)
            {
                text.Append($"\nYou're walking with {Name(controller.WalkingWith)}.");
            }
            List<ulong> riders = buddies.RidersOf(buddies.Me).ToList();
            if (riders.Count > 0)
            {
                text.Append("\nOn your head: ").Append(Names(riders));
            }
            if (buddies.List.Count == 0 && others.Count == 0 && requests.Count == 0)
            {
                text.Append("\n~ Nobody else here has Buddy Up yet.");
            }
            return text.ToString();
        }

        // ride <buddy> / walk <buddy>
        private static string Together(string[] args, bool ride)
        {
            BuddyController controller = BuddyController.Instance;
            if (controller == null || !Game.InWorld)
            {
                return "! Join a world first.";
            }
            Buddies buddies = controller.BuddyList;
            if (args.Length == 0)
            {
                return ride ? "! Whose head? Usage: ride <buddy>" : "! Who with? Usage: walk <buddy>, or walk stop";
            }
            if (!ride && string.Equals(args[0], "stop", StringComparison.OrdinalIgnoreCase))
            {
                if (controller.WalkingWith == 0)
                {
                    return "~ You're not walking with anyone.";
                }
                controller.CommandStopWalking();
                return "Stopped walking.";
            }
            ulong id = Find(buddies.List, string.Join(" ", args), "your buddy", out string why);
            if (id == 0)
            {
                return "! " + why + (buddies.List.Count == 0 ? " buddy <name> asks someone first." : "");
            }
            if (ride)
            {
                if (controller.RidingOn == id)
                {
                    return $"~ You're already on {Name(id)}'s head. buddy stop hops off.";
                }
                controller.CommandRide(id);
                return $"Climbing onto {Name(id)}'s head...";
            }
            if (controller.WalkingWith == id)
            {
                return $"~ You're already walking with {Name(id)}. walk stop stops.";
            }
            controller.CommandWalk(id);
            return $"Walking with {Name(id)}.";
        }

        // The player among these whose name is (or starts with, or has in it) what was typed.
        private static ulong Find(IEnumerable<ulong> ids, string typed, string who, out string why)
        {
            why = null;
            List<ulong> list = ids.Distinct().ToList();
            if (string.IsNullOrWhiteSpace(typed))
            {
                why = "Which player?";
                return 0;
            }
            if (list.Count == 0)
            {
                why = who != null ? $"Nobody is {who}." : "Nobody matches.";
                return 0;
            }
            foreach (Func<string, bool> rule in new Func<string, bool>[]
                     {
                         n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase),
                         n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                         n => n.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0,
                     })
            {
                List<ulong> found = list.Where(id => rule(Name(id))).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }
                if (found.Count > 1)
                {
                    why = $"{found.Count} players match {typed}. Type more of the name.";
                    return 0;
                }
            }
            why = who != null ? $"Nobody called {typed} is {who}." : $"Nobody called {typed}.";
            return 0;
        }

        private static string Name(ulong id) => PlayerNames.WithoutTags(Game.PlayerName(id)).Trim();

        private static string Names(IEnumerable<ulong> ids) => string.Join(", ", ids.Select(Name));

        private static string[] CompleteBuddy(string[] args, int index)
        {
            Buddies buddies = BuddyController.Instance?.BuddyList;
            if (buddies == null)
            {
                return null;
            }
            if (index == 0)
            {
                return new[] { "yes", "no", "bye", "stop", "shake" }.Concat(buddies.Others.Concat(buddies.Requests).Select(Name)).ToArray();
            }
            if (index == 1 && args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "yes":
                    case "no":
                        return buddies.Requests.Select(Name).ToArray();
                    case "bye":
                        return buddies.List.Select(Name).ToArray();
                }
            }
            return null;
        }

        private static string[] CompleteBuddies(string[] args, int index)
        {
            Buddies buddies = BuddyController.Instance?.BuddyList;
            return index == 0 && buddies != null ? buddies.List.Select(Name).Concat(new[] { "stop" }).ToArray() : null;
        }
    }
}
