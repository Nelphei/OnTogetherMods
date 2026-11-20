using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;

namespace DeskSwarm
{
    // The swarm command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs). It does
    // what the window's buttons do.
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("swarm", Plugin.PluginName,
                "swarm [fly | land | lead | end | join <name> | leave | form <shape> | spread <0.6-2>]",
                "swarm shows what you and the swarms here are doing. swarm fly takes off (sit at your desk first) and " +
                "swarm land stops flying. swarm lead starts a swarm and swarm end ends it; swarm join <name> follows " +
                "someone's swarm and swarm leave stops. Leading, swarm form heart picks a shape (row, class, circle, " +
                "table and grid land on the ground) and swarm spread 1.4 how far apart the desks fly.",
                Run, Complete);
        }

        private static string Run(string[] args)
        {
            SwarmController controller = SwarmController.Instance;
            if (controller == null || !Game.InWorld)
            {
                return "! Join a world first.";
            }
            Swarms swarms = controller.SwarmList;
            if (args.Length == 0)
            {
                return Status(controller, swarms);
            }
            string rest = string.Join(" ", args.Skip(1));
            switch (args[0].ToLowerInvariant())
            {
                case "fly":
                case "takeoff":
                    if (controller.Piloting)
                    {
                        return "~ You're already flying. swarm land stops.";
                    }
                    if (swarms.Following != 0)
                    {
                        return "! Your desk is following a swarm. swarm leave first, to fly it yourself.";
                    }
                    if (!controller.CanFly)
                    {
                        return "! Sit at your desk first (or do any activity on open ground).";
                    }
                    controller.CommandToggleFlying();
                    // The movement keys steer, so the console hands the keyboard back.
                    ShellLink.HandBackKeys();
                    return "Flying! (The keys are the game's again: click the console to type.)";
                case "land":
                case "stop":
                    if (!controller.Piloting)
                    {
                        return "~ You're not flying.";
                    }
                    controller.CommandToggleFlying();
                    return "Stopped flying. Your desk stays where it is.";
                case "lead":
                case "start":
                    if (!swarms.InLobby)
                    {
                        return "! Swarms start once you're in a session.";
                    }
                    if (swarms.Mine != null)
                    {
                        return "~ You're already leading a swarm.";
                    }
                    swarms.Start();
                    return "Swarm started. Friends with Desk Swarm can join it (swarm join <your name>).";
                case "end":
                    if (swarms.Mine == null)
                    {
                        return "~ You're not leading a swarm.";
                    }
                    swarms.End();
                    return "Swarm ended.";
                case "join":
                {
                    if (!swarms.InLobby)
                    {
                        return "! Swarms start once you're in a session.";
                    }
                    ulong leader = FindLeader(swarms, rest, out string why);
                    if (leader == 0)
                    {
                        return "! " + why;
                    }
                    if (swarms.Following == leader)
                    {
                        return $"~ You're already in {Name(leader)}'s swarm.";
                    }
                    controller.CommandJoin(leader);
                    return controller.CanFly
                        ? $"Joined {Name(leader)}'s swarm."
                        : $"Joined {Name(leader)}'s swarm. Sit at your desk to fly to your spot.";
                }
                case "leave":
                    if (swarms.Following == 0)
                    {
                        return "~ You're not in anyone's swarm.";
                    }
                    swarms.Leave();
                    return "Left the swarm. Your desk stays where it is.";
                case "form":
                case "shape":
                {
                    if (swarms.Mine == null)
                    {
                        return "! Only a swarm's leader picks its shape. swarm lead starts one.";
                    }
                    if (!TryShape(rest, out Formation shape))
                    {
                        return $"! Shapes: {string.Join(", ", Formations.Flying.Select(Lower))}; landing: {string.Join(", ", Formations.Landing.Select(Lower))}.";
                    }
                    swarms.SetFormation(shape);
                    return Formations.Lands(shape) ? $"The swarm lands in a {Lower(shape)}." : $"The swarm flies in a {Lower(shape)}.";
                }
                case "spread":
                {
                    if (swarms.Mine == null)
                    {
                        return "! Only a swarm's leader sets its spread. swarm lead starts one.";
                    }
                    if (!float.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out float spread))
                    {
                        return $"! Usage: swarm spread <{Swarms.MinSpread:0.0}-{Swarms.MaxSpread:0.0}> (it's {swarms.Mine.Spread:0.0} now).";
                    }
                    swarms.SetSpread(spread);
                    return $"Spread: {swarms.Mine.Spread:0.0}x.";
                }
                default:
                    return "! Usage: swarm [fly | land | lead | end | join <name> | leave | form <shape> | spread <n>]";
            }
        }

        private static string Status(SwarmController controller, Swarms swarms)
        {
            var text = new StringBuilder(controller.Piloting ? "You're flying your desk (swarm land stops)." : "Not flying.");
            Swarm mine = swarms.Mine;
            if (mine != null)
            {
                text.Append($"\nLeading a swarm: {Lower(mine.Formation)}, spread {mine.Spread:0.0}x, ");
                text.Append(mine.Members.Count == 0 ? "nobody following yet." : $"followed by {string.Join(", ", mine.Members.Select(Name))}.");
            }
            else if (swarms.Following != 0)
            {
                Swarm followed = swarms.Followed;
                int spot = swarms.MySpot;
                text.Append($"\nIn {Name(swarms.Following)}'s swarm");
                text.Append(followed != null && spot >= 0 ? $": {Lower(followed.Formation)}, spot {spot + 1} of {followed.Members.Count}." : " (waiting for a spot).");
            }
            List<Swarm> others = swarms.Others.Where(s => s.Leader != swarms.Following).ToList();
            if (others.Count > 0)
            {
                text.Append("\nSwarms here: ").Append(string.Join(", ", others.Select(s => $"{Name(s.Leader)} ({Lower(s.Formation)}, {s.Members.Count + 1} desks)")));
                text.Append("\n~ swarm join <name> follows one.");
            }
            else if (mine == null && swarms.Following == 0)
            {
                text.Append("\n~ No swarms here yet. swarm lead starts one.");
            }
            return text.ToString();
        }

        private static ulong FindLeader(Swarms swarms, string typed, out string why)
        {
            why = null;
            List<ulong> leaders = swarms.Others.Select(s => s.Leader).ToList();
            if (leaders.Count == 0)
            {
                why = "Nobody here is leading a swarm.";
                return 0;
            }
            if (string.IsNullOrWhiteSpace(typed))
            {
                why = "Whose swarm? Usage: swarm join <name>";
                return 0;
            }
            foreach (Func<string, bool> rule in new Func<string, bool>[]
                     {
                         n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase),
                         n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                         n => n.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0,
                     })
            {
                List<ulong> found = leaders.Where(id => rule(Name(id))).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }
                if (found.Count > 1)
                {
                    why = $"{found.Count} leaders match {typed}. Type more of the name.";
                    return 0;
                }
            }
            why = $"Nobody called {typed} is leading a swarm. swarm lists them.";
            return 0;
        }

        private static bool TryShape(string typed, out Formation shape)
        {
            foreach (Formation formation in Formations.Flying.Concat(Formations.Landing))
            {
                if (string.Equals(formation.ToString(), typed.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    shape = formation;
                    return true;
                }
            }
            shape = default;
            return false;
        }

        private static string Lower(Formation formation) => formation.ToString().ToLowerInvariant();

        private static string Name(ulong id) => PlayerNames.WithoutTags(Game.PlayerName(id)).Trim();

        private static string[] Complete(string[] args, int index)
        {
            if (index == 0)
            {
                return new[] { "fly", "land", "lead", "end", "join", "leave", "form", "spread" };
            }
            if (index == 1 && args.Length > 0)
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "form":
                    case "shape":
                        return Formations.Flying.Concat(Formations.Landing).Select(Lower).ToArray();
                    case "join":
                        Swarms swarms = SwarmController.Instance?.SwarmList;
                        return swarms != null ? swarms.Others.Select(s => Name(s.Leader)).ToArray() : null;
                }
            }
            return null;
        }
    }
}
