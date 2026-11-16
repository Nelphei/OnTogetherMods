using System;
using System.Collections.Generic;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace SessionManager
{
    internal sealed class LoggedPlayer
    {
        public string SteamId;
        public PlayerID Id;
        public string Name;
        public string Nick;
        // When you first saw them in this session (when they joined, if you were already here).
        public DateTime Joined;
        public DateTime Left;
    }

    // Who's in the session with you and who has left it, newest first. Kept for the session you're in,
    // so you can ban someone after they've gone (the game can only ban players who are here).
    internal sealed class PlayerLog
    {
        private const float Interval = 0.5f;
        private const int MaxLeft = 30;

        public readonly List<LoggedPlayer> Here = new List<LoggedPlayer>();
        public readonly List<LoggedPlayer> Left = new List<LoggedPlayer>();

        private readonly List<PlayerRow> _rows = new List<PlayerRow>();
        private readonly HashSet<string> _seen = new HashSet<string>();
        private CSteamID _lobby = CSteamID.Nil;
        private float _next;

        public void Update(CSteamID lobby)
        {
            if (lobby != _lobby)
            {
                _lobby = lobby;
                Here.Clear();
                Left.Clear();
                _next = 0f;
            }
            if (lobby == CSteamID.Nil || Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + Interval;

            Game.Players(_rows);
            DateTime now = DateTime.UtcNow;
            _seen.Clear();
            foreach (PlayerRow row in _rows)
            {
                _seen.Add(row.SteamId);
                LoggedPlayer player = Here.Find(p => p.SteamId == row.SteamId);
                if (player == null)
                {
                    // Back again: they're no longer in the "left" list.
                    Left.RemoveAll(p => p.SteamId == row.SteamId);
                    player = new LoggedPlayer { SteamId = row.SteamId, Joined = now };
                    Here.Add(player);
                }
                // Names can change while they're here, and a rejoin gets a new network ID.
                player.Id = row.Id;
                player.Name = row.Name;
                player.Nick = row.Nick;
            }

            for (int i = Here.Count - 1; i >= 0; i--)
            {
                LoggedPlayer player = Here[i];
                if (_seen.Contains(player.SteamId))
                {
                    continue;
                }
                Here.RemoveAt(i);
                player.Left = now;
                Left.Insert(0, player);
            }
            if (Left.Count > MaxLeft)
            {
                Left.RemoveRange(MaxLeft, Left.Count - MaxLeft);
            }
        }
    }
}
