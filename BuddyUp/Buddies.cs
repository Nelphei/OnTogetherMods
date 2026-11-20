using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace BuddyUp
{
    // Someone riding on someone's head, as they told everyone.
    internal sealed class Ride
    {
        public ulong Carrier;
        public float Height;
    }

    // What someone sat down to do, as they told everyone.
    internal sealed class Sit
    {
        public FocusType Type;
        public int Anim;
        public bool Desk;   // their personal desk came out
        public bool Seat;   // at one of the game's seat spots (a bench...), not free focus
        public float At;    // when we heard
    }

    // Who has the mod, who's whose buddy, and what everyone's doing, kept in step through Steam lobby
    // chat. Being buddies is two-way: one asks, the other says yes, and either can end it.
    //
    // Messages (after the tag):
    //   HELLO                   I've just arrived (or restarted): tell me who you are and what you're doing.
    //   HERE                    I have Buddy Up (the answer to HELLO).
    //   ASK <to>                Will you be my buddy?
    //   YES <to>                Yes: we're buddies now.
    //   NO <to>                 No thanks.
    //   BYE <to>                We're not buddies any more.
    //   RIDE <carrier> <cm>     I'm riding on this player's head, this many centimetres above it.
    //                           RIDE 0 0: I got off.
    //   SHAKE <rider>           Please get off my head.
    //   FOLLOW <target>         I'm walking with this player. FOLLOW 0: I stopped.
    //   SIT <type> <anim> <desk>  I sat down in this free-focus activity (desk 1: my desk came out).
    //   SIT SEAT                I sat down at one of the game's seat spots.
    //   UP                      I stood up.
    //
    // One leader per group: everyone goes along with someone (riding on them or walking with them)
    // or with nobody, and nobody goes along with someone who goes along with them. If that happens
    // anyway (someone rides on or walks with a player who was going along with them), the newest
    // one wins and the player it's aimed at stops theirs (GiveWayTo).
    internal sealed class Buddies
    {
        private const float RequestSeconds = 60f;
        // Two rides or walks heard within this long of each other count as started together.
        private const float TieSeconds = 1f;

        private static Settings S => Plugin.Settings;

        private readonly LobbyChannel _lobby = new LobbyChannel();
        private readonly HashSet<ulong> _modded = new HashSet<ulong>();
        private readonly HashSet<ulong> _buddies = new HashSet<ulong>();
        // Requests we sent and got, with when.
        private readonly Dictionary<ulong, float> _asked = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> _requests = new Dictionary<ulong, float>();
        // What everyone else is doing.
        private readonly Dictionary<ulong, Ride> _rides = new Dictionary<ulong, Ride>();
        private readonly Dictionary<ulong, ulong> _follows = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, Sit> _sits = new Dictionary<ulong, Sit>();
        // When we heard each player start riding on or walking with someone, and when you did.
        private readonly Dictionary<ulong, float> _linkedAt = new Dictionary<ulong, float>();
        private float _myLinkAt;
        private readonly List<ulong> _circle = new List<ulong>();
        // What we last told everyone we're doing, sent again to players who arrive later.
        private string _myRide;
        private string _myFollow;
        private string _mySit;

        public Buddies()
        {
            _lobby.MessageReceived += OnMessage;
            _lobby.MemberLeft += OnMemberLeft;
            _lobby.LobbyChanged += OnLobbyChanged;
        }

        // Something happened that the player should hear about.
        public event Action<string> Notice;

        // Someone asked to be your buddy.
        public event Action<ulong> Asked;

        // You and this player aren't buddies any more (either of you ended it, or they left).
        public event Action<ulong> Ended;

        // The player you're riding on asked you to get off.
        public event Action<ulong> ShakenOff;

        public bool InLobby => _lobby.InLobby;

        public ulong Me => _lobby.Me.m_SteamID;

        public IReadOnlyCollection<ulong> List => _buddies;

        // Everyone else in the session with Buddy Up who isn't your buddy yet.
        public IEnumerable<ulong> Others => _modded.Where(id => !_buddies.Contains(id));

        // Players waiting for your answer.
        public IEnumerable<ulong> Requests => _requests.Keys;

        public IReadOnlyDictionary<ulong, Ride> Rides => _rides;

        public bool IsBuddy(ulong id) => _buddies.Contains(id);

        public bool HaveAsked(ulong id) => _asked.ContainsKey(id);

        public Sit SitOf(ulong id) => _sits.TryGetValue(id, out Sit sit) ? sit : null;

        public ulong Carrying(ulong rider) => _rides.TryGetValue(rider, out Ride ride) ? ride.Carrier : 0;

        // Everyone riding directly on this player's head.
        public IEnumerable<ulong> RidersOf(ulong carrier) => _rides.Where(r => r.Value.Carrier == carrier).Select(r => r.Key);

        // The head you're on and the buddy you walk with, as you told everyone (0 for none).
        public ulong MyCarrier { get; private set; }
        public ulong MyFollow { get; private set; }

        // Who this player goes along with: the one whose head they're on, or else the one they walk
        // with. 0 for nobody.
        public ulong DirectLeader(ulong id)
        {
            if (id == Me)
            {
                return MyCarrier != 0 ? MyCarrier : MyFollow;
            }
            if (_rides.TryGetValue(id, out Ride ride))
            {
                return ride.Carrier;
            }
            return _follows.TryGetValue(id, out ulong target) ? target : 0;
        }

        // The front of this player's group: the one everyone in it goes along with, directly or
        // through others. The player themselves if they go along with nobody, and 0 if it goes round
        // in a circle (being sorted out: see GiveWayTo).
        public ulong GroupLeader(ulong id)
        {
            ulong at = id;
            for (int i = 0; i < 32; i++)
            {
                ulong next = DirectLeader(at);
                if (next == 0)
                {
                    return at;
                }
                if (next == id)
                {
                    return 0;
                }
                at = next;
            }
            return 0;
        }

        // Everyone walking (not riding) in this leader's group, you included if you are.
        public IEnumerable<ulong> WalkersWith(ulong leader)
        {
            foreach (ulong id in _follows.Keys)
            {
                if (!_rides.ContainsKey(id) && GroupLeader(id) == leader)
                {
                    yield return id;
                }
            }
            if (MyFollow != 0 && MyCarrier == 0 && GroupLeader(Me) == leader)
            {
                yield return Me;
            }
        }

        // Nobody goes along with someone who goes along with them (maybe through others). When that
        // happens, the newest ride or walk in the circle wins (a tie goes to the higher Steam ID),
        // and the player it's aimed at stops their own. Every game in the circle works this out the
        // same way. Returns the player you should make way for, or 0.
        public ulong GiveWayTo()
        {
            ulong me = Me;
            ulong next = DirectLeader(me);
            if (next == 0)
            {
                return 0;
            }
            _circle.Clear();
            _circle.Add(me);
            while (next != me)
            {
                if (next == 0 || _circle.Count > 32 || _circle.Contains(next))
                {
                    return 0;   // no circle through you
                }
                _circle.Add(next);
                next = DirectLeader(next);
            }
            float latest = float.MinValue;
            foreach (ulong id in _circle)
            {
                latest = Mathf.Max(latest, LinkedAt(id));
            }
            ulong newest = 0;
            foreach (ulong id in _circle)
            {
                if (LinkedAt(id) >= latest - TieSeconds && id > newest)
                {
                    newest = id;
                }
            }
            return DirectLeader(newest) == me ? newest : 0;
        }

        private float LinkedAt(ulong id)
        {
            if (id == Me)
            {
                return _myLinkAt;
            }
            return _linkedAt.TryGetValue(id, out float at) ? at : 0f;
        }

        // Call every frame.
        public void Update()
        {
            _lobby.Poll();

            float now = Time.unscaledTime;
            foreach (ulong id in _asked.Where(a => now - a.Value > RequestSeconds).Select(a => a.Key).ToList())
            {
                _asked.Remove(id);
                Tell($"{Game.PlayerName(id)} didn't answer");
            }
            foreach (ulong id in _requests.Where(r => now - r.Value > RequestSeconds).Select(r => r.Key).ToList())
            {
                _requests.Remove(id);
            }
        }

        public void Ask(ulong id)
        {
            if (!InLobby || id == Me || _buddies.Contains(id))
            {
                return;
            }
            if (_requests.ContainsKey(id))
            {
                // They asked first: that's a yes.
                Answer(id, true);
                return;
            }
            _asked[id] = Time.unscaledTime;
            _lobby.Send($"ASK {id}");
            Tell($"Asked {Game.PlayerName(id)} to be your buddy");
        }

        public void Answer(ulong id, bool yes)
        {
            if (!_requests.Remove(id))
            {
                return;
            }
            if (yes)
            {
                AddBuddy(id);
                _lobby.Send($"YES {id}");
            }
            else
            {
                _lobby.Send($"NO {id}");
            }
        }

        public void End(ulong id)
        {
            if (!_buddies.Remove(id))
            {
                return;
            }
            _lobby.Send($"BYE {id}");
            Plugin.Log.LogInfo($"No longer buddies with {LobbyChannel.NameOf(new CSteamID(id))}.");
            Ended?.Invoke(id);
        }

        public void SendRide(ulong carrier, float height)
        {
            if (carrier != 0 && carrier != MyCarrier)
            {
                _myLinkAt = Time.unscaledTime;
            }
            MyCarrier = carrier;
            _myRide = carrier != 0
                ? $"RIDE {carrier} {Mathf.RoundToInt(height * 100f).ToString(CultureInfo.InvariantCulture)}"
                : null;
            _lobby.Send(_myRide ?? "RIDE 0 0");
        }

        public void SendShake(ulong rider)
        {
            _lobby.Send($"SHAKE {rider}");
        }

        public void SendFollow(ulong target)
        {
            if (target != 0 && target != MyFollow)
            {
                _myLinkAt = Time.unscaledTime;
            }
            MyFollow = target;
            _myFollow = target != 0 ? $"FOLLOW {target}" : null;
            _lobby.Send(_myFollow ?? "FOLLOW 0");
        }

        public void SendSit(PlayerFocusController me)
        {
            _mySit = Game.SitsFree(me)
                ? $"SIT {(int)me.FocusType} {Game.MyAnim()} {(me.IsDesk ? 1 : 0)}"
                : "SIT SEAT";
            _lobby.Send(_mySit);
        }

        public void SendUp()
        {
            if (_mySit != null)
            {
                _mySit = null;
                _lobby.Send("UP");
            }
        }

        private void AddBuddy(ulong id)
        {
            _asked.Remove(id);
            _requests.Remove(id);
            _modded.Add(id);
            if (_buddies.Add(id))
            {
                Tell($"You and {Game.PlayerName(id)} are buddies now!");
                Plugin.Log.LogInfo($"Buddies with {LobbyChannel.NameOf(new CSteamID(id))}.");
            }
        }

        private void Tell(string text)
        {
            Notice?.Invoke(text);
        }

        private void Forget(ulong id)
        {
            _asked.Remove(id);
            _requests.Remove(id);
            _rides.Remove(id);
            _follows.Remove(id);
            _sits.Remove(id);
            _linkedAt.Remove(id);
            if (_buddies.Remove(id))
            {
                Ended?.Invoke(id);
            }
        }

        private void OnLobbyChanged()
        {
            foreach (ulong id in _buddies.ToList())
            {
                _buddies.Remove(id);
                Ended?.Invoke(id);
            }
            _modded.Clear();
            _asked.Clear();
            _requests.Clear();
            _rides.Clear();
            _follows.Clear();
            _sits.Clear();
            _linkedAt.Clear();
            _myRide = _myFollow = _mySit = null;
            MyCarrier = MyFollow = 0;
            if (InLobby)
            {
                _lobby.Send("HELLO");
            }
        }

        private void OnMemberLeft(CSteamID who)
        {
            ulong id = who.m_SteamID;
            bool buddy = _buddies.Contains(id);
            _modded.Remove(id);
            Forget(id);
            if (buddy)
            {
                Tell($"{Game.PlayerName(id)} left the session");
            }
        }

        private void OnMessage(CSteamID sender, string[] words)
        {
            ulong from = sender.m_SteamID;
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"From {LobbyChannel.NameOf(sender)}: {string.Join(" ", words)}");
            }
            _modded.Add(from);

            switch (words[0])
            {
                // They've just arrived (or restarted), so they have no buddies and aren't doing anything.
                case "HELLO":
                    if (_buddies.Contains(from))
                    {
                        Tell($"{Game.PlayerName(from)} rejoined, so you're not buddies any more");
                    }
                    Forget(from);
                    _lobby.Send("HERE");
                    foreach (string state in new[] { _myRide, _myFollow, _mySit })
                    {
                        if (state != null)
                        {
                            _lobby.Send(state);
                        }
                    }
                    break;

                case "HERE":
                    break;

                case "ASK":
                    if (ToMe(words))
                    {
                        ReceiveAsk(from);
                    }
                    break;

                case "YES":
                    if (ToMe(words) && (_asked.ContainsKey(from) || _buddies.Contains(from)))
                    {
                        AddBuddy(from);
                    }
                    break;

                case "NO":
                    if (ToMe(words) && _asked.Remove(from))
                    {
                        Tell($"{Game.PlayerName(from)} said no thanks");
                    }
                    break;

                case "BYE":
                    if (ToMe(words) && _buddies.Remove(from))
                    {
                        Tell($"{Game.PlayerName(from)} isn't your buddy any more");
                        Ended?.Invoke(from);
                    }
                    break;

                case "RIDE":
                    ReceiveRide(from, words);
                    break;

                case "SHAKE":
                    if (ToMe(words))
                    {
                        ShakenOff?.Invoke(from);
                    }
                    break;

                case "FOLLOW":
                    if (words.Length >= 2 && ulong.TryParse(words[1], out ulong target) && target != 0 && target != from)
                    {
                        if (!_follows.TryGetValue(from, out ulong old) || old != target)
                        {
                            _linkedAt[from] = Time.unscaledTime;
                        }
                        _follows[from] = target;
                    }
                    else
                    {
                        _follows.Remove(from);
                    }
                    break;

                case "SIT":
                    ReceiveSit(from, words);
                    break;

                case "UP":
                    _sits.Remove(from);
                    break;
            }
        }

        private bool ToMe(string[] words)
        {
            return words.Length >= 2 && ulong.TryParse(words[1], out ulong to) && to == Me;
        }

        private void ReceiveAsk(ulong from)
        {
            if (_buddies.Contains(from) || _asked.ContainsKey(from))
            {
                // Already buddies (they lost track), or we both asked: that's a yes either way.
                AddBuddy(from);
                _lobby.Send($"YES {from}");
                return;
            }
            bool fresh = !_requests.ContainsKey(from);
            _requests[from] = Time.unscaledTime;
            if (fresh)
            {
                Asked?.Invoke(from);
            }
        }

        private void ReceiveRide(ulong from, string[] words)
        {
            if (words.Length >= 3 && ulong.TryParse(words[1], out ulong carrier) && carrier != 0 && carrier != from &&
                int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cm))
            {
                bool fresh = !_rides.TryGetValue(from, out Ride ride) || ride.Carrier != carrier;
                _rides[from] = new Ride { Carrier = carrier, Height = Mathf.Clamp(cm / 100f, 0f, 3f) };
                if (fresh)
                {
                    _linkedAt[from] = Time.unscaledTime;
                }
                if (fresh && carrier == Me)
                {
                    Tell($"{Game.PlayerName(from)} climbed onto your head!");
                }
            }
            else if (_rides.TryGetValue(from, out Ride old))
            {
                _rides.Remove(from);
                if (old.Carrier == Me)
                {
                    Tell($"{Game.PlayerName(from)} hopped off your head");
                }
            }
        }

        private void ReceiveSit(ulong from, string[] words)
        {
            if (words.Length >= 2 && words[1] == "SEAT")
            {
                _sits[from] = new Sit { Seat = true, At = Time.unscaledTime };
                return;
            }
            if (words.Length >= 4 &&
                int.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type) &&
                Enum.IsDefined(typeof(FocusType), type) &&
                int.TryParse(words[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int anim))
            {
                _sits[from] = new Sit { Type = (FocusType)type, Anim = anim, Desk = words[3] == "1", At = Time.unscaledTime };
            }
        }
    }
}
