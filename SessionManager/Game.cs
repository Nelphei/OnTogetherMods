using System;
using System.Collections.Generic;
using System.Text;
using OnTogetherMods.Shared;
using PurrNet;
using Steamworks;
using UnityEngine.SceneManagement;

namespace SessionManager
{
    // One other player in the session, as the player list has them.
    internal struct PlayerRow
    {
        public string SteamId;
        public PlayerID Id;
        // With its colours and other styling, ready for TextMeshPro.
        public string Name;
        // The name as the game saves it in its ban list.
        public string Nick;
    }

    // Shortcuts into the game's code. See reference/Assembly-CSharp/PlayerPanelController.cs,
    // PlayerItemController.cs (ButtonBan), PlayerController.cs (BanRPC), DataManager.cs (SaveBanData),
    // MultiplayerManager.cs and TextChannelManager.cs.
    internal static class Game
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // Cheap to check every frame. Singletons are only looked up in the world: on the main menu
        // MonoSingleton.I searches the whole scene each time.
        public static bool InWorld => SceneManager.GetActiveScene().buildIndex == WorldScene;

        // The Steam lobby of the session you're in, or Nil. Only in the world.
        public static CSteamID Lobby
        {
            get
            {
                if (!InWorld)
                {
                    return CSteamID.Nil;
                }
                try
                {
                    MultiplayerManager multiplayer = MonoSingleton<MultiplayerManager>.I;
                    return multiplayer != null && ulong.TryParse(multiplayer.LobbyCode, out ulong id) && id != 0
                        ? new CSteamID(id)
                        : CSteamID.Nil;
                }
                catch (Exception)
                {
                    // No lobby yet.
                    return CSteamID.Nil;
                }
            }
        }

        // True while you host the session you're in: your game is its server.
        public static bool IsHost
        {
            get
            {
                if (!InWorld)
                {
                    return false;
                }
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                return channel != null && channel.isHost;
            }
        }

        public static string MySteamId => SteamManager.Initialized ? SteamUser.GetSteamID().ToString() : "";

        // True while the chat box is open (only looked up on key press).
        public static bool IsTyping()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // ---- Players ----

        // Everyone else in the session, in the player list's order.
        public static void Players(List<PlayerRow> rows)
        {
            rows.Clear();
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerIDs == null)
            {
                return;
            }
            string me = MySteamId;
            int count = Math.Min(panel.PlayerSteamIDs.Count, panel.PlayerIDs.Count);
            for (int i = 0; i < count; i++)
            {
                string id = panel.PlayerSteamIDs[i];
                if (string.IsNullOrEmpty(id) || id == me)
                {
                    continue;
                }
                string nick = Nick(panel, i);
                PlayerController player = panel.PlayerControllers != null && i < panel.PlayerControllers.Count
                    ? panel.PlayerControllers[i]
                    : null;
                rows.Add(new PlayerRow
                {
                    SteamId = id,
                    Id = panel.PlayerIDs[i],
                    Name = PlayerNames.Of(player) ?? Styled(nick),
                    Nick = nick,
                });
            }
        }

        // The name a player sent with their ID card, filtered like the game's player list does.
        private static string Nick(PlayerPanelController panel, int i)
        {
            try
            {
                if (panel.IDInfos != null && i < panel.IDInfos.Count && panel.IDInfos[i].Name != null)
                {
                    return Filter(Encoding.Unicode.GetString(panel.IDInfos[i].Name)).Trim();
                }
            }
            catch (Exception)
            {
                // A name that doesn't decode: fall back below.
            }
            return "Player";
        }

        // A name from the ban list or a join request, ready for TextMeshPro.
        public static string Styled(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Player" : PlayerNames.ForText(name.Trim());
        }

        // The game's bad word filter, as it shows names.
        public static string Filter(string text)
        {
            BadWordFilterManager filter = MonoSingleton<BadWordFilterManager>.I;
            return filter != null ? filter.FilterString(text) : text;
        }

        public static bool IsHere(string steamId, out PlayerID id)
        {
            id = default;
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerIDs == null)
            {
                return false;
            }
            int i = panel.PlayerSteamIDs.IndexOf(steamId);
            if (i < 0 || i >= panel.PlayerIDs.Count)
            {
                return false;
            }
            id = panel.PlayerIDs[i];
            return true;
        }

        // ---- Kicks and bans ----

        // Tells the player's game to leave, the way the game's own ban button does (it shows them the
        // "banned" screen, since the game has no kick screen). Only the host can.
        public static void SendAway(PlayerID id)
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            if (channel == null || channel.MainPlayerController == null)
            {
                throw new InvalidOperationException("The player list isn't ready");
            }
            channel.MainPlayerController.BanRPC(id, isFirstBan: true);
        }

        // Drops the player's connection to your game, for a player whose game didn't leave when told
        // to (a modded game can ignore it). Only the host can.
        public static void Disconnect(PlayerID id)
        {
            NetworkManager network = NetworkManager.main;
            if (network != null && network.isServer && network.playerModule != null)
            {
                network.playerModule.KickPlayer(id);
            }
        }

        // True once the game has read its ban file (DataManager.LoadBanData, at the start). Before
        // that its ban list can be empty, so timed bans wait for it (Patches.cs sets this).
        public static bool BansLoaded;

        // The game's own ban list (Steam IDs and the names they had), saved in the game's ban file.
        public static BanData BanData
        {
            get
            {
                // In a world the game finished starting long ago, even if the patch missed it.
                if (!BansLoaded && InWorld)
                {
                    BansLoaded = true;
                }
                if (!BansLoaded)
                {
                    return null;
                }
                DataManager data = MonoSingleton<DataManager>.I;
                return data != null ? data.BanData : null;
            }
        }

        public static bool IsBanned(string steamId)
        {
            BanData bans = BanData;
            return bans != null && bans.BanServerPlayers != null && bans.BanServerPlayers.Contains(steamId);
        }

        // Adds a player to the game's ban list, like its ban button: they can't join or ask to join
        // again. Returns false if they were already on it.
        public static bool AddBan(string steamId, string nick)
        {
            BanData bans = BanData;
            if (bans == null)
            {
                throw new InvalidOperationException("The game hasn't loaded its ban list yet");
            }
            bans.BanServerPlayers = bans.BanServerPlayers ?? new List<string>();
            bans.BanServerPlayerNicks = bans.BanServerPlayerNicks ?? new List<string>();
            if (bans.BanServerPlayers.Contains(steamId))
            {
                return false;
            }
            // The two lists go together by position.
            while (bans.BanServerPlayerNicks.Count < bans.BanServerPlayers.Count)
            {
                bans.BanServerPlayerNicks.Add("Player");
            }
            bans.BanServerPlayers.Add(steamId);
            bans.BanServerPlayerNicks.Insert(bans.BanServerPlayers.Count - 1, string.IsNullOrWhiteSpace(nick) ? "Player" : nick);
            SaveBans();
            return true;
        }

        public static bool RemoveBan(string steamId)
        {
            BanData bans = BanData;
            if (bans == null || bans.BanServerPlayers == null)
            {
                return false;
            }
            int i = bans.BanServerPlayers.IndexOf(steamId);
            if (i < 0)
            {
                return false;
            }
            bans.BanServerPlayers.RemoveAt(i);
            if (bans.BanServerPlayerNicks != null && i < bans.BanServerPlayerNicks.Count)
            {
                bans.BanServerPlayerNicks.RemoveAt(i);
            }
            SaveBans();
            return true;
        }

        public static string BanNick(BanData bans, int i)
        {
            return bans.BanServerPlayerNicks != null && i < bans.BanServerPlayerNicks.Count
                ? bans.BanServerPlayerNicks[i]
                : "Player";
        }

        private static void SaveBans()
        {
            MonoSingleton<DataManager>.I.SaveBanData();
            // The game's player list has an unban button per banned player that goes by its place in
            // the list, so it has to be rebuilt whenever the list changes.
            if (InWorld)
            {
                NetworkSingleton<PlayerPanelController>.I?.UpdateBannedPanel();
            }
        }

        // ---- Steam ----

        public static bool IsFriend(CSteamID id)
        {
            return SteamManager.Initialized &&
                   SteamFriends.GetFriendRelationship(id) == EFriendRelationship.k_EFriendRelationshipFriend;
        }

        // Steam's own window for inviting friends to the session. False if Steam's overlay is off.
        public static bool OpenInviteWindow(CSteamID lobby)
        {
            if (!SteamManager.Initialized || !SteamUtils.IsOverlayEnabled())
            {
                return false;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(lobby);
            return true;
        }

        public static bool OpenProfile(string steamId)
        {
            if (!SteamManager.Initialized || !SteamUtils.IsOverlayEnabled() || !ulong.TryParse(steamId, out ulong id))
            {
                return false;
            }
            SteamFriends.ActivateGameOverlayToUser("steamid", new CSteamID(id));
            return true;
        }
    }
}
