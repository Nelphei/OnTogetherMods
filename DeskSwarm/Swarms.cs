using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace DeskSwarm
{
    // One swarm: its leader, its shape, and its followers in order (a follower's place in the list
    // is its spot in the formation).
    internal sealed class Swarm
    {
        public ulong Leader;
        public Formation Formation = Formation.Ring;
        public float Spread = 1f;
        // The desks fly through buildings and bridges (the leader's ThroughBuildings switch).
        public bool Through;
        public readonly List<ulong> Members = new List<ulong>();
    }

    // Who's leading and following, kept in step through Steam lobby chat. Only the leader changes a
    // swarm: followers ask to join or leave, and the leader sends the whole swarm back to everyone.
    //
    // Messages (after the tag):
    //   HELLO                      I've just arrived: leaders, tell me your swarms.
    //   SWARM <shape> <spread*10> [THRU] <follower>...   my swarm as it is now (sent by its leader).
    //                              THRU: fly through buildings. 0.2.0 skips it (it isn't a Steam ID).
    //   END                        my swarm is over.
    //   JOIN <leader>              I'd like to follow this leader.
    //   LEAVE <leader>             I've stopped following this leader.
    internal sealed class Swarms
    {
        public const int MaxFollowers = 24;
        public const float MinSpread = 0.6f;
        public const float MaxSpread = 2f;
        // A swarm list that leaves you out right after you asked to join was probably sent before
        // the leader got your message.
        private const float JoinGraceSeconds = 3f;
        private const int MaxJoinTries = 2;

        private static Settings S => Plugin.Settings;

        private readonly LobbyChannel _lobby = new LobbyChannel();
        // Other players' swarms, by leader.
        private readonly Dictionary<ulong, Swarm> _others = new Dictionary<ulong, Swarm>();
        private float _joinedAt;
        private int _joinTries;

        public Swarms()
        {
            _lobby.MessageReceived += OnMessage;
            _lobby.MemberLeft += OnMemberLeft;
            _lobby.LobbyChanged += OnLobbyChanged;
            // Your followers fly through buildings when you do (the window's switch, or the settings file).
            S.ThroughBuildings.SettingChanged += (sender, args) => Broadcast();
        }

        // Something happened that the player should hear about.
        public event Action<string> Notice;

        public bool InLobby => _lobby.InLobby;

        public ulong Me => _lobby.Me.m_SteamID;

        // Your own swarm while you lead one, or null.
        public Swarm Mine { get; private set; }

        // The leader you follow, or 0.
        public ulong Following { get; private set; }

        public IEnumerable<Swarm> Others => _others.Values;

        public Swarm Followed => Following != 0 && _others.TryGetValue(Following, out Swarm swarm) ? swarm : null;

        // Your spot in the swarm you follow, or -1 until the leader has you in their list.
        public int MySpot
        {
            get
            {
                Swarm swarm = Followed;
                return swarm != null ? swarm.Members.IndexOf(Me) : -1;
            }
        }

        // Call every frame.
        public void Update()
        {
            _lobby.Poll();

            // Asked to join but not given a spot yet: the message may have crossed with one of the
            // leader's, so ask again a couple of times before giving up.
            if (Following != 0 && MySpot < 0 && Time.unscaledTime - _joinedAt > JoinGraceSeconds)
            {
                if (_joinTries < MaxJoinTries)
                {
                    _joinTries++;
                    _joinedAt = Time.unscaledTime;
                    _lobby.Send($"JOIN {Following}");
                }
                else
                {
                    Tell($"{Game.PlayerName(Following)} didn't answer, so you're not in their swarm");
                    Leave();
                }
            }
        }

        public void Start()
        {
            if (!InLobby || Mine != null)
            {
                return;
            }
            if (Following != 0)
            {
                Leave();
            }
            Mine = new Swarm { Leader = Me, Formation = S.StartFormation.Value };
            Broadcast();
            Plugin.Log.LogInfo("Started a swarm.");
        }

        public void End()
        {
            if (Mine == null)
            {
                return;
            }
            Mine = null;
            _lobby.Send("END");
            Plugin.Log.LogInfo("Ended your swarm.");
        }

        public void Join(ulong leader)
        {
            if (!InLobby || leader == Me || leader == Following)
            {
                return;
            }
            End();
            if (Following != 0)
            {
                Leave();
            }
            Following = leader;
            _joinedAt = Time.unscaledTime;
            _joinTries = 0;
            _lobby.Send($"JOIN {leader}");
            Plugin.Log.LogInfo($"Joined {LobbyChannel.NameOf(new CSteamID(leader))}'s swarm.");
        }

        public void Leave()
        {
            if (Following == 0)
            {
                return;
            }
            _lobby.Send($"LEAVE {Following}");
            Plugin.Log.LogInfo($"Left {LobbyChannel.NameOf(new CSteamID(Following))}'s swarm.");
            Following = 0;
        }

        public void SetFormation(Formation formation)
        {
            if (Mine != null && Mine.Formation != formation)
            {
                Mine.Formation = formation;
                Broadcast();
            }
        }

        public void SetSpread(float spread)
        {
            spread = Mathf.Clamp(Mathf.Round(spread * 10f) / 10f, MinSpread, MaxSpread);
            if (Mine != null && !Mathf.Approximately(Mine.Spread, spread))
            {
                Mine.Spread = spread;
                Broadcast();
            }
        }

        private void Broadcast()
        {
            if (Mine == null)
            {
                return;
            }
            string through = S.ThroughBuildings.Value ? " THRU" : "";
            string members = string.Concat(Mine.Members.Select(id => " " + id));
            _lobby.Send($"SWARM {Mine.Formation} {Mathf.RoundToInt(Mine.Spread * 10f)}{through}{members}");
        }

        private void Tell(string text)
        {
            Notice?.Invoke(text);
        }

        private void OnLobbyChanged()
        {
            if (Mine != null || Following != 0)
            {
                Tell("You left the lobby, so your swarm is over");
            }
            Mine = null;
            Following = 0;
            _others.Clear();
            if (InLobby)
            {
                _lobby.Send("HELLO");
            }
        }

        private void OnMemberLeft(CSteamID who)
        {
            ulong id = who.m_SteamID;
            _others.Remove(id);
            if (Following == id)
            {
                Following = 0;
                Tell($"{Game.PlayerName(id)} left, so their swarm is over");
            }
            if (Mine != null && Mine.Members.Remove(id))
            {
                Tell($"{Game.PlayerName(id)} left your swarm");
                Broadcast();
            }
        }

        private void OnMessage(CSteamID sender, string[] words)
        {
            ulong from = sender.m_SteamID;
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"From {LobbyChannel.NameOf(sender)}: {string.Join(" ", words)}");
            }

            switch (words[0])
            {
                // They've just arrived (or restarted), so they aren't leading or following anyone.
                case "HELLO":
                    _others.Remove(from);
                    if (Following == from)
                    {
                        Following = 0;
                        Tell($"{Game.PlayerName(from)}'s swarm is over");
                    }
                    if (Mine != null)
                    {
                        Mine.Members.Remove(from);
                        Broadcast();
                    }
                    break;

                case "SWARM":
                    ReceiveSwarm(from, words);
                    break;

                case "END":
                    _others.Remove(from);
                    if (Following == from)
                    {
                        Following = 0;
                        Tell($"{Game.PlayerName(from)} ended their swarm");
                    }
                    break;

                case "JOIN":
                    if (words.Length >= 2 && ulong.TryParse(words[1], out ulong leader) && leader == Me)
                    {
                        ReceiveJoin(from);
                    }
                    break;

                case "LEAVE":
                    if (words.Length >= 2 && ulong.TryParse(words[1], out ulong left) && left == Me &&
                        Mine != null && Mine.Members.Remove(from))
                    {
                        Tell($"{Game.PlayerName(from)} left your swarm");
                        Broadcast();
                    }
                    break;
            }
        }

        private void ReceiveJoin(ulong from)
        {
            if (Mine == null)
            {
                // Our swarm is over: say so, so they stop waiting for a spot.
                _lobby.Send("END");
                return;
            }
            if (!Mine.Members.Contains(from))
            {
                if (Mine.Members.Count >= MaxFollowers)
                {
                    Tell($"{Game.PlayerName(from)} tried to join, but your swarm is full");
                }
                else
                {
                    Mine.Members.Add(from);
                    Tell($"{Game.PlayerName(from)} joined your swarm");
                }
            }
            // Sent even when nothing changed, so they get their spot (or see they weren't let in).
            Broadcast();
        }

        private void ReceiveSwarm(ulong from, string[] words)
        {
            // A shape from a newer version (or a bare number) isn't one we can fly.
            if (words.Length < 3 || !Enum.TryParse(words[1], out Formation formation) ||
                !Enum.IsDefined(typeof(Formation), formation) || !int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int spread))
            {
                return;
            }

            if (!_others.TryGetValue(from, out Swarm swarm))
            {
                swarm = new Swarm { Leader = from };
                _others[from] = swarm;
            }
            swarm.Formation = formation;
            swarm.Spread = Mathf.Clamp(spread / 10f, MinSpread, MaxSpread);
            // A 0.2.0 leader never sends THRU, and its own desk doesn't fly through buildings either.
            swarm.Through = false;
            swarm.Members.Clear();
            for (int i = 3; i < words.Length && swarm.Members.Count < MaxFollowers; i++)
            {
                if (words[i] == "THRU")
                {
                    swarm.Through = true;
                }
                else if (ulong.TryParse(words[i], out ulong member) && member != from && !swarm.Members.Contains(member))
                {
                    swarm.Members.Add(member);
                }
            }

            // Someone following us is leading now, so they've left us.
            if (Mine != null && Mine.Members.Remove(from))
            {
                Broadcast();
            }

            bool full = swarm.Members.Count >= MaxFollowers;
            if (Following == from && !swarm.Members.Contains(Me) && (full || Time.unscaledTime - _joinedAt > JoinGraceSeconds))
            {
                Following = 0;
                Tell(full ? $"{Game.PlayerName(from)}'s swarm is full" : $"You're not in {Game.PlayerName(from)}'s swarm any more");
            }
        }
    }
}
