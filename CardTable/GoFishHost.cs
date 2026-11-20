using System;
using System.Collections.Generic;
using System.Linq;

namespace CardTable
{
    // Where Go Fish writes to the log. Plugin points these at BepInEx's log; outside the game
    // (the test program) they go nowhere or to the console.
    internal static class FishLog
    {
        public static Action<string> Info = _ => { };
        public static Action<string> Warn = _ => { };
    }

    // A Go Fish table you're at: yours (GoFishHost) or someone else's (GoFishClient).
    internal interface IFishTable
    {
        int Table { get; }
        ulong HostId { get; }
        bool IsHost { get; }
        // What you see. Null until a joined table has answered.
        GoFishView View { get; }
        bool Ask(int target, int rank, float now);
        void Update(float now);
    }

    // The table you started: it shuffles, deals and runs the rules, plays the computer players, and
    // sends every other player at the table what they may see after each change (only their own
    // cards). You're always seat 0. Players who join take a computer player's seat, cards and all,
    // and a computer player takes over the seat of anyone who leaves.
    internal sealed class GoFishHost : IFishTable
    {
        // How long a computer player waits before asking, so people can follow what happened.
        public const float BotThink = 1.3f;
        private const float BotThinkSpread = 0.6f;
        // A player at a table with other people who doesn't move for this long gets a computer move.
        public const float QuietTurn = 45f;
        // Views go out again now and then, in case one went missing, and tell players the table's still there.
        public const float ResendEvery = 15f;
        public const float AnnounceEvery = 10f;
        private const float AnnounceSoon = 0.5f;

        public static readonly string[] BotNames = { "Pip", "Moss", "Fern", "Clover", "Bean", "Sprout", "Pebble", "Tansy" };

        private readonly GoFish _game = new GoFish();
        private readonly Random _random;
        private readonly Action<ulong, string> _send;
        private readonly Action<string> _broadcast;
        private readonly ulong[] _who;
        private readonly int[] _bot;
        private GoFishBot[] _brains;
        private int _seq;
        private int _gameNo;
        private int _fedUpTo;
        private float _nextMoveAt;
        private float _turnStarted;
        private float _nextResend;
        private float _nextAnnounce;
        private string _announced;

        public GoFishHost(ulong me, int seats, Random random, Action<ulong, string> send, Action<string> broadcast, float now)
        {
            seats = Math.Max(GoFish.MinSeats, Math.Min(GoFish.MaxSeats, seats));
            _random = random;
            _send = send;
            _broadcast = broadcast;
            Table = random.Next(1, int.MaxValue);
            HostId = me;
            _who = new ulong[seats];
            _bot = new int[seats];
            _who[0] = me;
            _bot[0] = -1;
            for (int s = 1; s < seats; s++)
            {
                _bot[s] = FreeBotName();
            }
            _brains = Enumerable.Range(0, seats).Select(s => new GoFishBot(s, random)).ToArray();
            NewGame(now);
        }

        public int Table { get; }

        public ulong HostId { get; }

        public bool IsHost => true;

        public GoFishView View { get; private set; }

        public int SeatCount => _who.Length;

        public int Humans => _who.Count(w => w != 0);

        public bool Over => _game.Over;

        // Seats of other players (not you or computer players), for "are you sure" before closing.
        public bool HasGuests => _who.Skip(1).Any(w => w != 0);

        public IEnumerable<ulong> Guests => _who.Skip(1).Where(w => w != 0);

        public void NewGame(float now)
        {
            _gameNo++;
            // The first game starts with you, so friends can sit down before anything happens;
            // after that the first turn goes round the table.
            _game.Deal(SeatCount, _random, _gameNo - 1);
            _fedUpTo = 0;
            Changed(now);
            FishLog.Info($"Go Fish: dealt game {_gameNo} at table {Table} ({SeatCount} seats).");
        }

        public bool Ask(int target, int rank, float now) => AskFor(0, target, rank, now);

        public void Update(float now)
        {
            if (!_game.Over && now >= _nextMoveAt)
            {
                int turn = _game.Turn;
                bool bot = _who[turn] == 0 && turn != 0;
                bool quiet = !bot && Humans >= 2 && now - _turnStarted >= QuietTurn;
                if ((bot || quiet) && _brains[turn].Choose(_game, out int target, out int rank))
                {
                    if (quiet)
                    {
                        FishLog.Info($"Go Fish: seat {turn} didn't move for {QuietTurn:0} s, so a computer move was made.");
                    }
                    AskFor(turn, target, rank, now);
                }
            }
            if (now >= _nextResend)
            {
                SendViews();
                _nextResend = now + ResendEvery;
            }
            // Now and then, and soon after who's at the table (or whether the game's over) changes.
            string announce = AnnounceText();
            if (announce != _announced)
            {
                AnnounceNow(now);
            }
            if (now >= _nextAnnounce)
            {
                _broadcast(announce);
                _announced = announce;
                _nextAnnounce = now + AnnounceEvery;
            }
        }

        // Someone new in the session asked which tables there are.
        public void AnnounceNow(float now)
        {
            _nextAnnounce = Math.Min(_nextAnnounce, now + AnnounceSoon);
        }

        // "TABLE table seats over humans": announced to everyone in the session.
        public string AnnounceText()
        {
            return $"TABLE {Table} {SeatCount} {(_game.Over ? 1 : 0)} {string.Join(",", _who.Where(w => w != 0))}";
        }

        // JOIN / LEAVE / ASK from other players.
        public void OnDirect(ulong from, string[] words, float now)
        {
            if (words.Length < 2 || !int.TryParse(words[1], out int table) || table != Table || from == 0 || from == HostId)
            {
                return;
            }
            switch (words[0])
            {
                case "JOIN":
                    Join(from, now);
                    break;
                case "LEAVE":
                    PlayerLeft(from, now);
                    break;
                case "ASK":
                    int seat = Array.IndexOf(_who, from);
                    if (seat > 0 && words.Length >= 4 && int.TryParse(words[2], out int target) && int.TryParse(words[3], out int rank))
                    {
                        if (!AskFor(seat, target, rank, now))
                        {
                            // Out of step: send them the table as it is.
                            SendView(seat);
                        }
                    }
                    break;
            }
        }

        public void PlayerLeft(ulong who, float now)
        {
            int seat = Array.IndexOf(_who, who);
            if (seat <= 0)
            {
                return;
            }
            _who[seat] = 0;
            _bot[seat] = FreeBotName();
            _game.Note(FishEventKind.Leave, seat, who);
            Changed(now);
            FishLog.Info($"Go Fish: {who} left seat {seat}; a computer player took over.");
        }

        // Tells everyone at the table it's closing.
        public void Close()
        {
            foreach (ulong guest in Guests)
            {
                _send(guest, $"GONE {Table}");
            }
            _broadcast($"CLOSED {Table}");
        }

        private void Join(ulong who, float now)
        {
            int seat = Array.IndexOf(_who, who);
            if (seat > 0)
            {
                SendView(seat);   // already here: their view went missing
                return;
            }
            List<int> free = Enumerable.Range(1, SeatCount - 1).Where(s => _who[s] == 0).ToList();
            if (free.Count == 0)
            {
                _send(who, $"FULL {Table}");
                return;
            }
            // Not the computer player whose turn it is, if there's a choice, so nobody joins halfway
            // through a move.
            seat = free.FirstOrDefault(s => s != _game.Turn);
            if (seat == 0)
            {
                seat = free[0];
            }
            _who[seat] = who;
            _bot[seat] = -1;
            _game.Note(FishEventKind.Join, seat, who);
            Changed(now);
            FishLog.Info($"Go Fish: {who} sat down at seat {seat}.");
        }

        private bool AskFor(int seat, int target, int rank, float now)
        {
            if (!_game.Ask(seat, target, rank))
            {
                return false;
            }
            Changed(now);
            return true;
        }

        private void Changed(float now)
        {
            foreach (FishEvent e in _game.Events.Where(e => e.Id > _fedUpTo))
            {
                foreach (GoFishBot brain in _brains)
                {
                    brain.See(e, SeatCount);
                }
                _fedUpTo = e.Id;
            }
            _turnStarted = now;
            _nextMoveAt = now + BotThink + (float)_random.NextDouble() * BotThinkSpread;
            _seq++;
            View = ViewFor(0);
            SendViews();
            _nextResend = now + ResendEvery;
        }

        private void SendViews()
        {
            for (int s = 1; s < SeatCount; s++)
            {
                if (_who[s] != 0)
                {
                    SendView(s);
                }
            }
        }

        private void SendView(int seat)
        {
            _send(_who[seat], ViewFor(seat).Encode());
        }

        public GoFishView ViewFor(int seat)
        {
            return new GoFishView
            {
                Table = Table,
                Seq = _seq,
                GameNo = _gameNo,
                Over = _game.Over,
                Turn = _game.Turn,
                Pond = _game.Pond.Count,
                MySeat = seat,
                Seats = Enumerable.Range(0, SeatCount).Select(s => new SeatView
                {
                    Who = _who[s],
                    Bot = _bot[s],
                    Cards = _game.Hands[s].Count,
                    Books = _game.Books[s].ToList(),
                }).ToArray(),
                Hand = seat >= 0 ? _game.Hands[seat].ToList() : new List<int>(),
                Log = _game.Events.Skip(Math.Max(0, _game.Events.Count - GoFishView.LogLength)).ToList(),
            };
        }

        private int FreeBotName()
        {
            List<int> free = Enumerable.Range(0, BotNames.Length).Where(i => !_bot.Contains(i)).ToList();
            return free.Count > 0 ? free[_random.Next(free.Count)] : _random.Next(BotNames.Length);
        }

        // For the test program: the real cards.
        internal GoFish Game => _game;
    }

    // A table someone else started, which you asked to join. Their game runs the rules and sends
    // what you may see; yours sends your asks.
    internal sealed class GoFishClient : IFishTable
    {
        private const float JoinRetry = 4f;
        public const float JoinTimeout = 15f;
        // Well past the host's resend time: the table has gone quiet for good.
        public const float LostAfter = GoFishHost.ResendEvery * 3f + 5f;
        private const float AskTimeout = 5f;

        private readonly Action<ulong, string> _send;
        private readonly float _startedAt;
        private float _joinSentAt;
        private float _heardAt;
        private int _askedSeq = -1;
        private float _askedAt;

        public GoFishClient(ulong host, int table, Action<ulong, string> send, float now)
        {
            HostId = host;
            Table = table;
            _send = send;
            _startedAt = now;
            SendJoin(now);
        }

        public int Table { get; }

        public ulong HostId { get; }

        public bool IsHost => false;

        public GoFishView View { get; private set; }

        // Why the table's gone ("full", "closed", "lost", "unreachable"), or null while you're at it.
        public string Gone { get; private set; }

        public bool AskPending(float now) => View != null && _askedSeq == View.Seq && now - _askedAt < AskTimeout;

        public bool Ask(int target, int rank, float now)
        {
            if (View == null || !View.MyTurn || AskPending(now))
            {
                return false;
            }
            _send(HostId, $"ASK {Table} {target} {rank}");
            _askedSeq = View.Seq;
            _askedAt = now;
            return true;
        }

        public void Leave()
        {
            _send(HostId, $"LEAVE {Table}");
        }

        public void Update(float now)
        {
            if (Gone != null)
            {
                return;
            }
            if (View == null)
            {
                if (now - _startedAt > JoinTimeout)
                {
                    Gone = "unreachable";
                }
                else if (now - _joinSentAt > JoinRetry)
                {
                    SendJoin(now);
                }
            }
            else if (now - _heardAt > LostAfter)
            {
                Gone = "lost";
            }
        }

        public void OnDirect(ulong from, string[] words, float now)
        {
            if (from != HostId || words.Length < 2 || !int.TryParse(words[1], out int table) || table != Table)
            {
                return;
            }
            switch (words[0])
            {
                case "VIEW":
                    GoFishView view = GoFishView.Decode(words.Skip(1).ToArray());
                    if (view == null)
                    {
                        FishLog.Warn("Go Fish: couldn't read the table's view.");
                        return;
                    }
                    _heardAt = now;
                    // Messages arrive in order, but a resent view may repeat an old one.
                    if (View == null || view.Seq >= View.Seq)
                    {
                        View = view;
                    }
                    break;
                case "FULL":
                    Gone = "full";
                    break;
                case "GONE":
                    Gone = "closed";
                    break;
            }
        }

        // The host closed the table ("closed") or left the session ("left").
        public void HostLeft(string why)
        {
            if (Gone == null)
            {
                Gone = why;
            }
        }

        private void SendJoin(float now)
        {
            _send(HostId, $"JOIN {Table}");
            _joinSentAt = now;
        }
    }
}
