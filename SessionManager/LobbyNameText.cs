using System;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;

namespace SessionManager
{
    // The game writes the session name into the player list only once, when you enter the world
    // (PlayerPanelController.Start), so a rename never showed there, not even in the host's own list.
    // Every game with Session Manager (host or not) keeps that text in step with the Steam lobby's
    // Name, filtered like the game does. Players without the mod see a new name once they rejoin.
    // See reference/Assembly-CSharp/PlayerPanelController.cs (Start).
    internal static class LobbyNameText
    {
        private const float CheckInterval = 1f;

        private static AccessTools.FieldRef<PlayerPanelController, TextMeshProUGUI> _text;
        private static bool _broken;
        private static float _nextCheck;
        private static string _raw;
        private static string _shown;

        // Checks on the next Update, right after a rename.
        public static void CheckSoon()
        {
            _nextCheck = 0f;
        }

        // Every frame while you're in a session (lobby isn't Nil); does its work once a second.
        public static void Update(CSteamID lobby)
        {
            if (_broken || lobby == CSteamID.Nil || Time.unscaledTime < _nextCheck)
            {
                return;
            }
            _nextCheck = Time.unscaledTime + CheckInterval;
            try
            {
                // Empty until Steam has sent a joiner the lobby's data.
                string raw = SteamMatchmaking.GetLobbyData(lobby, "Name");
                if (string.IsNullOrEmpty(raw))
                {
                    return;
                }
                if (raw != _raw)
                {
                    _raw = raw;
                    _shown = Game.Filter(raw);
                }
                PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
                if (panel == null)
                {
                    return;
                }
                if (_text == null)
                {
                    _text = AccessTools.FieldRefAccess<PlayerPanelController, TextMeshProUGUI>("_lobbyNameText");
                }
                TextMeshProUGUI text = _text(panel);
                if (text != null && text.text != _shown)
                {
                    text.text = _shown;
                }
            }
            catch (Exception e)
            {
                // Most likely a game update renamed the field. Stop trying rather than log every second.
                _broken = true;
                Plugin.Log.LogWarning($"Couldn't update the session name in the player list, so a new name " +
                                      $"shows there only after rejoining: {e.Message}");
            }
        }
    }
}
