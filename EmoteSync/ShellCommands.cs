using System;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using Steamworks;

namespace EmoteSync
{
    // The sync command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs).
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("sync", Plugin.PluginName, "sync [on | off]",
                "sync says whether emote sync is on and who else has it on. sync on / sync off turn it on or off " +
                "(like its key). While it's on, Bubble Shell's emote and sit commands start in sync with the group too.",
                Run, (args, index) => index == 0 ? new[] { "on", "off" } : null);
        }

        private static string Run(string[] args)
        {
            SyncController sync = SyncController.Instance;
            if (sync == null)
            {
                return "! Emote Sync isn't ready yet.";
            }
            if (args.Length > 0)
            {
                string word = args[0].ToLowerInvariant();
                if (word == "on" || word == "off")
                {
                    bool on = word == "on";
                    if (sync.IsOn == on)
                    {
                        return $"~ Emote sync is already {word}.";
                    }
                    sync.SetOn(on);
                    return on ? "Emote sync is on." + Group(sync) : "Emote sync is off.";
                }
                return "! Usage: sync, sync on or sync off.";
            }
            return sync.IsOn ? "Emote sync is on." + Group(sync) : "Emote sync is off. sync on turns it on.";
        }

        private static string Group(SyncController sync)
        {
            if (!sync.InLobby)
            {
                return " It starts when you join a session.";
            }
            List<string> names = sync.GroupIds.Select(id => PlayerNames.WithoutTags(Game.PlayerName(new CSteamID(id))).Trim()).ToList();
            return names.Count == 0
                ? " Nobody else has it on yet."
                : $" In sync with: {string.Join(", ", names)}.";
        }
    }
}
