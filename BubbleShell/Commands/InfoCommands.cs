using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using PurrNet.Modules;
using Steamworks;
using UnityEngine;

namespace BubbleShell
{
    // players, mods, session, net, fps, version: looking things up.
    internal static class InfoCommands
    {
        public const string Group = "Info";

        private const string OurPrefix = "nelphei.ontogether.";

        // Free-focus poses on land and in water (GameSettings' private lists): a player playing one is sitting.
        private static readonly AccessTools.FieldRef<GameSettings, List<int>> LandFocusAnims =
            AccessTools.FieldRefAccess<GameSettings, List<int>>("_animationStates");

        private static readonly AccessTools.FieldRef<GameSettings, List<int>> WaterFocusAnims =
            AccessTools.FieldRefAccess<GameSettings, List<int>>("_animationStatesWater");

        // fps on or off (the window draws the counter).
        public static bool ShowFps;

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "players",
                Aliases = new[] { "who" },
                Group = Group,
                Usage = "players",
                Help = "Everyone in the session: how far away they are and whether they're sitting.",
                Run = Players,
            });
            shell.Register(new Command
            {
                Name = "mods",
                Group = Group,
                Usage = "mods",
                Help = "Every BepInEx mod you have loaded, with its version. o marks ours.",
                Run = Mods,
            });
            shell.Register(new Command
            {
                Name = "session",
                Aliases = new[] { "lobby" },
                Group = Group,
                Usage = "session",
                Help = "The session you're in: name, players, host, who can find it.",
                Run = Session,
            });
            shell.Register(new Command
            {
                Name = "net",
                Aliases = new[] { "ping" },
                Group = Group,
                Usage = "net",
                Help = "Network facts: host or not, ping to the host, ticks a second.",
                Run = Net,
            });
            shell.Register(new Command
            {
                Name = "fps",
                Group = Group,
                Usage = "fps [on|off]",
                Help = "A frames-a-second counter in the top-left corner.",
                Run = Fps,
                Complete = (s, a, i) => i == 0 ? new[] { "on", "off" } : null,
            });
            shell.Register(new Command
            {
                Name = "version",
                Group = Group,
                Usage = "version",
                Help = "Which Bubble Shell, game, Unity and BepInEx you're running.",
                Run = Version,
            });
        }

        private static IEnumerator Players(Shell shell, Args args)
        {
            List<PlayerEntry> players = Game.Players();
            if (players.Count == 0)
            {
                shell.Warn(Game.InWorld ? "The player list isn't ready yet." : "Join a world first.");
                return null;
            }
            PlayerMovementController me = Game.MyMovement;
            ulong host = 0;
            CSteamID lobby = Game.Lobby;
            if (lobby != CSteamID.Nil)
            {
                host = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
            }
            var sitting = new HashSet<int>();
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            sitting.UnionWith(LandFocusAnims(settings) ?? new List<int>());
            sitting.UnionWith(WaterFocusAnims(settings) ?? new List<int>());

            shell.Print($"{players.Count} here:");
            int number = 1;
            foreach (PlayerEntry player in players)
            {
                string distance = player.IsMe || me == null
                    ? "you"
                    : $"{Vector3.Distance(me.transform.position, player.Player.transform.position):0} m";
                string doing = sitting.Contains(Game.AnimOf(player.Player)) ? "sitting" : "up";
                string extra = player.SteamId != 0 && player.SteamId == host ? " (host)" : "";
                shell.PrintRich(Shell.Colored(Shell.TextColor, Shell.Escape($"{number,2}  {distance,6}  {doing,-8}")) +
                                player.Name + Shell.Colored(Shell.DimColor, extra));
                number++;
            }
            return null;
        }

        private static IEnumerator Mods(Shell shell, Args args)
        {
            List<PluginInfo> plugins = Chainloader.PluginInfos.Values
                .OrderBy(p => p.Metadata.GUID.StartsWith(OurPrefix, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(p => p.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int ours = 0;
            foreach (PluginInfo plugin in plugins)
            {
                bool isOurs = plugin.Metadata.GUID.StartsWith(OurPrefix, StringComparison.Ordinal);
                ours += isOurs ? 1 : 0;
                string line = $"{(isOurs ? "o" : " ")} {plugin.Metadata.Name} {plugin.Metadata.Version}";
                shell.Print(line, isOurs ? LineKind.Normal : LineKind.Hint);
            }
            shell.Hint($"{plugins.Count} mods loaded, {ours} of them Soapie Scripts.");
            return null;
        }

        private static IEnumerator Session(Shell shell, Args args)
        {
            CSteamID lobby = Game.Lobby;
            if (lobby == CSteamID.Nil)
            {
                shell.Warn("You're not in a session.");
                return null;
            }
            string name = SteamMatchmaking.GetLobbyData(lobby, "Name") ?? "";
            int members = SteamMatchmaking.GetNumLobbyMembers(lobby);
            // The game makes the Steam limit one more than the player count you pick.
            int cap = Math.Max(1, SteamMatchmaking.GetLobbyMemberLimit(lobby) - 1);
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
            string ownerName = owner == SteamUser.GetSteamID() ? "you" : SteamFriends.GetFriendPersonaName(owner);
            bool listed = SteamMatchmaking.GetLobbyData(lobby, "public") != "f";
            bool ask = SteamMatchmaking.GetLobbyData(lobby, "request") == "t";

            shell.PrintRich(Shell.Colored(Shell.TextColor, "Name: ") + Shell.Colored(Shell.BrightColor, Game.ConsoleName(name)));
            shell.Print($"Players: {members} of {cap}");
            shell.Print($"Host: {ownerName}");
            shell.Print($"In the session list: {(listed ? "yes" : "no (private)")}{(ask ? ", asks to join" : "")}");
            shell.Hint($"Steam lobby {lobby.m_SteamID}");
            return null;
        }

        private static IEnumerator Net(Shell shell, Args args)
        {
            TickManager ticks = Game.Ticks;
            if (ticks == null)
            {
                shell.Warn("Not connected.");
                return null;
            }
            bool host = Game.IsHost;
            shell.Print(host ? "You're the host: your game runs the session." : "You're a guest: the host's game runs the session.");
            if (!host)
            {
                shell.Print($"Ping to the host: {ticks.rtt * 1000:0} ms");
            }
            shell.Print($"{ticks.tickRate} network ticks a second, now at tick {ticks.syncedPreciseTick:0}");
            return null;
        }

        private static IEnumerator Fps(Shell shell, Args args)
        {
            ShowFps = args.Count == 0 ? !ShowFps : args.Is(0, "on", "1", "true", "yes");
            shell.Print(ShowFps ? "The fps counter is on (top-left)." : "The fps counter is off.");
            return null;
        }

        private static IEnumerator Version(Shell shell, Args args)
        {
            shell.Print($"{Plugin.PluginName} {Plugin.PluginVersion}");
            shell.Hint($"On-Together {Application.version}, Unity {Application.unityVersion}, " +
                       $"BepInEx {typeof(Chainloader).Assembly.GetName().Version}");
            return null;
        }
    }
}
