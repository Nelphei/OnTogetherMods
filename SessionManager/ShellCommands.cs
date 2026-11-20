using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;

namespace SessionManager
{
    // The host commands in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs): kick,
    // ban, unban, bans, lock, unlock, rename and cap. They do what the window's buttons do, and only
    // work while you host.
    internal static class ShellCommands
    {
        private static readonly string[] Lengths = { "1h", "1d", "1w", "forever" };

        public static void Register()
        {
            string mod = Plugin.PluginName;
            ShellLink.Add("kick", mod, "kick <player>",
                "Host only: sends a player out of your session. They can join again (ban stops that).",
                Kick, (args, i) => i == 0 ? Names(Here()) : null);
            ShellLink.Add("ban", mod, "ban <player> [1h | 1d | 1w | forever]",
                "Host only: bans a player from your session (also one who just left) for an hour, a day (if you don't " +
                "say), a week or for good, and sends them out. unban lets them back.",
                Ban, (args, i) => i == 0 ? Names(Here().Concat(Left())) : i == 1 ? Lengths : null);
            ShellLink.Add("unban", mod, "unban <player>",
                "Host only: takes a player off your ban list. bans lists it.",
                Unban, (args, i) => i == 0 ? BanList().Select(b => b.Name).ToArray() : null);
            ShellLink.Add("bans", mod, "bans",
                "Lists your ban list.",
                args => ListBans());
            ShellLink.Add("lock", mod, "lock",
                "Host only: turns away anyone new (the session leaves the session list) until unlock.",
                args => Lock(true));
            ShellLink.Add("unlock", mod, "unlock",
                "Host only: lets new players join again.",
                args => Lock(false));
            ShellLink.Add("rename", mod, "rename <new name>",
                "Host only: renames your session. Colour tags work, like in Name Designer.",
                Rename);
            ShellLink.Add("cap", mod, "cap <players>",
                "Host only: how many players can be in your session.",
                Cap);
        }

        // The window, if you're hosting; otherwise null and why not.
        private static ManagerWindow Host(out string why)
        {
            ManagerWindow window = ManagerWindow.Instance;
            why = null;
            if (window == null || !Game.InWorld)
            {
                why = "! Join a world first.";
                return null;
            }
            if (!window.Hosting)
            {
                why = "! Only the session's host can do that.";
                return null;
            }
            return window;
        }

        private static string Kick(string[] args)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            LoggedPlayer player = Find(Here(), string.Join(" ", args), "in your session", out why);
            return player == null ? why : Plain(window.CommandKick(player));
        }

        private static string Ban(string[] args)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            if (args.Length == 0)
            {
                return "! Usage: ban <player> [1h | 1d | 1w | forever]";
            }
            int length = 1;
            List<string> words = args.ToList();
            int parsed = Length(words[words.Count - 1]);
            if (parsed >= 0 && words.Count > 1)
            {
                length = parsed;
                words.RemoveAt(words.Count - 1);
            }
            LoggedPlayer player = Find(Here().Concat(Left()), string.Join(" ", words), "here (or here lately)", out why);
            if (player == null)
            {
                return why;
            }
            try
            {
                return Plain(window.CommandBan(player, length));
            }
            catch (InvalidOperationException e)
            {
                return "! " + e.Message + ".";
            }
        }

        private static string Unban(string[] args)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            string typed = string.Join(" ", args);
            List<(string SteamId, string Name)> bans = BanList();
            if (bans.Count == 0)
            {
                return "~ Nobody is banned.";
            }
            foreach (Func<(string SteamId, string Name), bool> rule in new Func<(string SteamId, string Name), bool>[]
                     {
                         b => b.SteamId == typed || string.Equals(b.Name, typed, StringComparison.OrdinalIgnoreCase),
                         b => b.Name.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                     })
            {
                var found = bans.Where(rule).ToList();
                if (found.Count == 1)
                {
                    return Plain(window.CommandUnban(found[0].SteamId, found[0].Name));
                }
                if (found.Count > 1)
                {
                    return $"! {found.Count} banned players match {typed}. Type more, or their Steam ID (bans lists them).";
                }
            }
            return $"! Nobody called {typed} is banned. bans lists them.";
        }

        private static string ListBans()
        {
            List<(string SteamId, string Name)> bans = BanList();
            if (bans.Count == 0)
            {
                return "~ Nobody is banned.";
            }
            var text = new StringBuilder("Banned:");
            foreach ((string steamId, string name) in bans)
            {
                text.Append('\n').Append($"  {name}  ({steamId})");
            }
            return text.ToString();
        }

        private static string Lock(bool locked)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            if (!window.CommandLock(locked))
            {
                return "! Steam didn't take that change.";
            }
            return locked ? "Locked: nobody new can join until unlock." : "Unlocked: new players can join again.";
        }

        private static string Rename(string[] args)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            if (args.Length == 0)
            {
                return "! Usage: rename <new name>";
            }
            return Plain(window.CommandRename(string.Join(" ", args)));
        }

        private static string Cap(string[] args)
        {
            ManagerWindow window = Host(out string why);
            if (window == null)
            {
                return why;
            }
            if (args.Length == 0 || !int.TryParse(args[0], out int players) || players < 1)
            {
                return $"! Usage: cap <players>, from 1 to {Lift.Max} (it's {window.SessionInfo.Cap} now).";
            }
            if (players > Lift.Max)
            {
                return $"! At most {Lift.Max} (MaxPlayers in the settings file).";
            }
            if (!window.CommandCap(players))
            {
                return "! Steam didn't take that change.";
            }
            return Lift.Experimental(players)
                ? $"Up to {players} players now. (Sessions over 16 haven't been tried.)"
                : $"Up to {players} players now.";
        }

        // ---- Players ----

        private static IEnumerable<LoggedPlayer> Here() => ManagerWindow.Instance?.Players.Here ?? Enumerable.Empty<LoggedPlayer>();

        private static IEnumerable<LoggedPlayer> Left() => ManagerWindow.Instance?.Players.Left ?? Enumerable.Empty<LoggedPlayer>();

        private static string Name(LoggedPlayer player) => PlayerNames.WithoutTags(player.Name ?? player.Nick ?? "").Trim();

        private static string[] Names(IEnumerable<LoggedPlayer> players) => players.Select(Name).Distinct().ToArray();

        private static LoggedPlayer Find(IEnumerable<LoggedPlayer> players, string typed, string where, out string why)
        {
            why = null;
            List<LoggedPlayer> list = players.GroupBy(p => p.SteamId).Select(g => g.First()).ToList();
            if (string.IsNullOrWhiteSpace(typed))
            {
                why = "! Which player?";
                return null;
            }
            foreach (Func<LoggedPlayer, bool> rule in new Func<LoggedPlayer, bool>[]
                     {
                         p => p.SteamId == typed || string.Equals(Name(p), typed, StringComparison.OrdinalIgnoreCase),
                         p => Name(p).StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                         p => Name(p).IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0,
                     })
            {
                List<LoggedPlayer> found = list.Where(rule).ToList();
                if (found.Count == 1)
                {
                    return found[0];
                }
                if (found.Count > 1)
                {
                    why = $"! {found.Count} players match {typed}. Type more of the name.";
                    return null;
                }
            }
            why = $"! Nobody called {typed} is {where}.";
            return null;
        }

        private static List<(string SteamId, string Name)> BanList()
        {
            var list = new List<(string, string)>();
            BanData bans = Game.BanData;
            if (bans?.BanServerPlayers == null)
            {
                return list;
            }
            for (int i = 0; i < bans.BanServerPlayers.Count; i++)
            {
                list.Add((bans.BanServerPlayers[i], PlayerNames.WithoutTags(Game.BanNick(bans, i)).Trim()));
            }
            return list;
        }

        // "1h", "day", "forever"... -> 0-3, or -1.
        private static int Length(string word)
        {
            switch (word.ToLowerInvariant())
            {
                case "1h":
                case "h":
                case "hour":
                    return 0;
                case "1d":
                case "d":
                case "day":
                    return 1;
                case "1w":
                case "w":
                case "week":
                    return 2;
                case "forever":
                case "perm":
                case "permanent":
                case "good":
                    return 3;
                default:
                    return -1;
            }
        }

        // The window's status lines have players' styled names in them; the console shows plain text.
        private static string Plain(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return "";
            }
            string text = PlayerNames.WithoutTags(status).TrimEnd();
            return text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?") ? text : text + ".";
        }
    }
}
