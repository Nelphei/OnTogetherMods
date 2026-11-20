using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Steamworks;

namespace SessionManager
{
    // The session's settings, which the game keeps as Steam lobby data. Players read them fresh
    // whenever they look at the session list or join, so changes reach new joiners straight away.
    // See reference/Assembly-CSharp/MultiplayerManager.cs (CreateLobby, RefreshSessionList, JoinByCode)
    // and SteamLobbyManager.cs (OnLobbyDataUpdated).
    internal sealed class Session
    {
        // The game's social tags, as the lobby data stores them (MultiplayerManager._socialTagStrings).
        public static readonly string[] TagKeys = { "focus", "mature", "chill", "break", "modded" };
        public static readonly string[] TagNames = { "Focus", "Mature", "Chill", "Break", "Modded" };

        // Steam keeps at most 8 KB per lobby data value.
        public const int MaxNameBytes = 8000;

        private CSteamID _lobby = CSteamID.Nil;

        // Read twice a second while you host, so the window isn't asking Steam on every GUI pass.
        public string Name { get; private set; } = "";
        public int Cap { get; private set; }
        public int Members { get; private set; }
        public bool Listed { get; private set; }
        public bool AskToJoin { get; private set; }
        public readonly bool[] Tags = new bool[TagKeys.Length];

        // Locked sessions turn everyone new away. Steam has no way to read this back, so it's
        // remembered here, and a new session starts unlocked.
        public bool Locked { get; private set; }

        public CSteamID Lobby => _lobby;

        public void Refresh(CSteamID lobby)
        {
            if (lobby != _lobby)
            {
                _lobby = lobby;
                Locked = false;
            }
            if (lobby == CSteamID.Nil)
            {
                return;
            }
            Name = SteamMatchmaking.GetLobbyData(lobby, "Name") ?? "";
            // The game makes the Steam lobby one bigger than the number picked, and counts the session
            // full one member before Steam does (MultiplayerManager.CreateLobby, JoinByCode).
            Cap = Math.Max(1, SteamMatchmaking.GetLobbyMemberLimit(lobby) - 1);
            Members = SteamMatchmaking.GetNumLobbyMembers(lobby);
            Listed = SteamMatchmaking.GetLobbyData(lobby, "public") != "f";
            AskToJoin = SteamMatchmaking.GetLobbyData(lobby, "request") == "t";
            for (int i = 0; i < TagKeys.Length; i++)
            {
                Tags[i] = SteamMatchmaking.GetLobbyData(lobby, TagKeys[i]) == "t";
            }
        }

        // ---- Changes (host only) ----

        // False if Steam refused it (a name too long, or you aren't the lobby's owner).
        public bool SetName(string name)
        {
            if (Encoding.UTF8.GetByteCount(name) > MaxNameBytes || !Set("Name", name))
            {
                return false;
            }
            Name = name;
            // The game never updates the player list's session name by itself.
            LobbyNameText.CheckSoon();
            return true;
        }

        public bool SetCap(int players)
        {
            players = Math.Max(1, Math.Min(players, Settings.LargestSession));
            if (!SteamMatchmaking.SetLobbyMemberLimit(_lobby, players + 1))
            {
                return false;
            }
            // The session list's player filter matches on this.
            Set("count", (players + 1).ToString());
            Cap = players;
            Log($"Player cap set to {players}");
            return true;
        }

        // Listed sessions show in everyone's session list. Unlisted ones are the game's "private":
        // only friends (in their Friends list) and invites see them.
        public bool SetListed(bool listed)
        {
            if (!Set("public", listed ? "t" : "f"))
            {
                return false;
            }
            Listed = listed;
            return true;
        }

        public bool SetAskToJoin(bool ask)
        {
            if (!Set("request", ask ? "t" : "f"))
            {
                return false;
            }
            AskToJoin = ask;
            return true;
        }

        public bool SetTag(int i, bool on)
        {
            if (!Set(TagKeys[i], on ? "t" : "f"))
            {
                return false;
            }
            Tags[i] = on;
            return true;
        }

        // Steam refuses anyone new, and the session drops out of the session list. Requests to join are
        // turned away while it's locked (JoinRequests).
        public bool SetLocked(bool locked)
        {
            if (!SteamMatchmaking.SetLobbyJoinable(_lobby, !locked))
            {
                return false;
            }
            Locked = locked;
            Log(locked ? "Locked" : "Unlocked");
            return true;
        }

        private bool Set(string key, string value)
        {
            if (_lobby == CSteamID.Nil || !SteamMatchmaking.SetLobbyData(_lobby, key, value))
            {
                return false;
            }
            Log($"{key} = {value}");
            return true;
        }

        private static void Log(string text)
        {
            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo(text);
            }
        }
    }

    // How big a session can be. The game's picker stops at 16 (MultiplayerManager.ChangeFilterPlayer);
    // Session Manager lets it go up to MaxPlayers. When another lobby-size mod is installed
    // (PlayerLimitLift, from BlueSage or officerballs), Session Manager leaves the picker to it and
    // only goes as high as it does.
    internal static class Lift
    {
        private const int GameLimit = 16;
        private const int OtherModFallback = 128;

        private static bool _looked;
        private static string _otherMod;
        private static int _otherMax;

        // The other lobby-size mod's name, or null.
        public static string OtherMod
        {
            get
            {
                Look();
                return _otherMod;
            }
        }

        // True when the main menu's picker is ours to change.
        public static bool OwnsPicker => OtherMod == null;

        public static int Max
        {
            get
            {
                Look();
                int max = _otherMod != null ? _otherMax : Plugin.Settings.MaxPlayers.Value;
                return Math.Max(GameLimit, Math.Min(max, Settings.LargestSession));
            }
        }

        public static bool Experimental(int players) => players > GameLimit;

        // Every plugin has loaded by the time anyone opens a menu, so this waits for first use.
        private static void Look()
        {
            if (_looked)
            {
                return;
            }
            _looked = true;
            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                string guid = info?.Metadata?.GUID ?? "";
                string name = info?.Metadata?.Name ?? "";
                if (guid == Plugin.PluginGuid ||
                    (guid.IndexOf("PlayerLimit", StringComparison.OrdinalIgnoreCase) < 0 &&
                     name.IndexOf("PlayerLimit", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }
                _otherMod = string.IsNullOrEmpty(name) ? guid : name;
                _otherMax = ReadMax(info) ?? OtherModFallback;
                Plugin.Log.LogInfo($"{_otherMod} is installed, so it handles the main menu's session size " +
                                   $"(up to {_otherMax}). Session Manager's player cap goes as high.");
                return;
            }
        }

        // BlueSage's PlayerLimitLift keeps its biggest size as MaxLobbySize.
        private static int? ReadMax(PluginInfo info)
        {
            try
            {
                ConfigFile config = info.Instance != null ? info.Instance.Config : null;
                if (config == null)
                {
                    return null;
                }
                ConfigDefinition key = config.Keys.FirstOrDefault(k => k.Key == "MaxLobbySize");
                if (key != null && config[key].BoxedValue is int max && max > 0)
                {
                    return max;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read {info.Metadata.Name}'s biggest session size: {e.Message}");
            }
            return null;
        }

        // The main menu picker's next number, for a click on its arrows (direction 1 or -1). Shift
        // jumps by SizeStep. Like the game's, it wraps around at either end.
        public static int Next(int current, int direction, bool shift)
        {
            int max = Max;
            int step = shift ? Plugin.Settings.SizeStep.Value : 1;
            if (direction == 1)
            {
                return current >= max ? 1 : Math.Min(max, current + step);
            }
            return current <= 1 ? max : Math.Max(1, current - step);
        }
    }
}
