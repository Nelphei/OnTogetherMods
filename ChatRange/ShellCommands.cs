using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChatRange
{
    // The range command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs): who
    // would see a local chat message from you right now, and how far away they are.
    internal static class ShellCommands
    {
        // The game's scenes by build index: 2 is the world.
        private const int WorldScene = 2;

        public static void Register()
        {
            ShellLink.Add("range", Plugin.PluginName, "range",
                $"Who's close enough to see your local chat messages and chat bubbles (within {Game.LocalRange:0} m), " +
                "and how far away they are.",
                Run);
        }

        private static string Run(string[] args)
        {
            if (SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                return "! Join a world first.";
            }
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (channel == null || channel.MainPlayer == null || panel == null || panel.PlayerControllers == null)
            {
                return "! The game isn't ready yet.";
            }

            Vector3 me = channel.MainPlayer.position;
            var near = new List<(string Name, float Distance)>();
            foreach (PlayerController player in panel.PlayerControllers)
            {
                if (player == null || player == channel.MainPlayerController || player.transform == channel.MainPlayer)
                {
                    continue;
                }
                // The same check the other player's game makes when your message arrives (3D, so height counts).
                float distance = Vector3.Distance(player.transform.position, me);
                if (distance <= Game.LocalRange)
                {
                    string name = PlayerNames.Of(player);
                    near.Add((name != null ? PlayerNames.WithoutTags(name).Trim() : "someone", distance));
                }
            }

            string tab = channel.Islocal ? "" : "\n~ (Your chat is on its Global tab, which everyone sees.)";
            if (near.Count == 0)
            {
                return $"Nobody is within {Game.LocalRange:0} m, so nobody would see a local message." + tab;
            }
            var text = new StringBuilder($"In local chat range ({Game.LocalRange:0} m):");
            foreach ((string name, float distance) in near.OrderBy(n => n.Distance))
            {
                text.Append('\n').Append($"  {name}  {distance:0.0} m");
            }
            return text + tab;
        }
    }
}
