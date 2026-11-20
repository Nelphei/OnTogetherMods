using System;
using System.Collections.Generic;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardTable
{
    // Players' names for the Go Fish table, as IMGUI rich text (see RichName): the name on their name
    // tag, with its colours, or their Steam name if they have no tag yet. Looked up at most once a
    // second per player, since OnGUI runs several times a frame.
    internal static class Players
    {
        private const int WorldScene = 2;
        private const float Refresh = 1f;
        private const string BotColour = "#DCE3F0FF";

        private static readonly Dictionary<ulong, string> Names = new Dictionary<ulong, string>();
        private static readonly Dictionary<ulong, float> LookedAt = new Dictionary<ulong, float>();
        // The last name seen on each player's name tag, so it still shows after they leave.
        private static readonly Dictionary<ulong, string> NameTags = new Dictionary<ulong, string>();

        public static string Name(ulong steamId)
        {
            float now = Time.unscaledTime;
            if (Names.TryGetValue(steamId, out string name) && LookedAt.TryGetValue(steamId, out float at) && now - at < Refresh)
            {
                return name;
            }
            name = RichName.ForImgui(TagName(steamId));
            Names[steamId] = name;
            LookedAt[steamId] = now;
            return name;
        }

        public static string Bot(int index)
        {
            string name = GoFishHost.BotNames[((index % GoFishHost.BotNames.Length) + GoFishHost.BotNames.Length) % GoFishHost.BotNames.Length];
            return $"<color={BotColour}>Bot {name}</color>";
        }

        private static string TagName(ulong steamId)
        {
            string name = PlayerNames.Of(PlayerOf(steamId));
            if (name != null)
            {
                NameTags[steamId] = name;
                return name;
            }
            return NameTags.TryGetValue(steamId, out string seen) ? seen : PlayerNames.Plain(CardNet.NameOf(steamId));
        }

        // This player's character, found through the player list, which keeps each player's Steam ID
        // next to them. Null if they aren't in the world on your screen.
        private static PlayerController PlayerOf(ulong steamId)
        {
            if (steamId == 0 || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                return null;
            }
            try
            {
                PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
                if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerControllers == null)
                {
                    return null;
                }
                int i = panel.PlayerSteamIDs.IndexOf(steamId.ToString());
                PlayerController player = i >= 0 && i < panel.PlayerControllers.Count ? panel.PlayerControllers[i] : null;
                return player != null ? player : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
