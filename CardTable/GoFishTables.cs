using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CardTable
{
    // How Go Fish reaches other players: lobby chat to everyone in the session with Card Table
    // (which tables there are), and messages to one player (joining, asking, what you see).
    // CardNet is the real one; the test program has its own.
    internal interface ICardNet
    {
        // Your Steam ID (never 0).
        ulong Me { get; }
        bool InLobby { get; }
        void Broadcast(string message);
        void SendTo(ulong player, string message);
    }

    // Someone else's table, as they last announced it.
    internal sealed class TableInfo
    {
        public ulong Host;
        public int Table;
        public int Seats;
        public bool Over;
        public List<ulong> Humans = new List<ulong>();
        public float HeardAt;

        public int FreeSeats => Seats - Humans.Count;
    }

    // Your Go Fish tables: the one you started or joined (only one at a time), and the others in the
    // session that you could join. Messages come in through the On... calls.
    internal sealed class GoFishTables
    {
        private const float ForgetAfter = GoFishHost.AnnounceEvery * 2.5f + 2f;
        private const float HelloEvery = 10f;

        private readonly ICardNet _net;
        private readonly Random _random;
        private readonly List<TableInfo> _others = new List<TableInfo>();
        private float _lastHello = -1000f;

        public GoFishTables(ICardNet net, Random random)
        {
            _net = net;
            _random = random;
        }

        public GoFishHost Hosting { get; private set; }

        // A table you asked to join (it has no View until the host answers).
        public GoFishClient Joined { get; private set; }

        // The table you're at, if any.
        public IFishTable Current => Hosting != null ? Hosting : Joined != null && Joined.View != null ? (IFishTable)Joined : null;

        public bool Joining => Joined != null && Joined.View == null;

        public bool InLobby => _net.InLobby;

        public IReadOnlyList<TableInfo> Others => _others;

        // Something to tell you: the text has "{name}" where the player's name goes (0 for none).
        public event Action<string, ulong> Notice;

        public void StartTable(int seats, float now)
        {
            LeaveTable(now);
            Hosting = new GoFishHost(_net.Me, seats, _random, _net.SendTo, _net.Broadcast, now);
        }

        public void Join(TableInfo table, float now)
        {
            LeaveTable(now);
            Joined = new GoFishClient(table.Host, table.Table, _net.SendTo, now);
        }

        // Leaves the table you're at (closing it, if it's yours), or stops joining one.
        public void LeaveTable(float now)
        {
            if (Hosting != null)
            {
                Hosting.Close();
                Hosting = null;
            }
            if (Joined != null)
            {
                Joined.Leave();
                Joined = null;
            }
        }

        public void Update(float now)
        {
            Hosting?.Update(now);
            if (Joined != null)
            {
                Joined.Update(now);
                if (Joined.Gone != null)
                {
                    ulong host = Joined.HostId;
                    string why = Joined.Gone;
                    bool seated = Joined.View != null;
                    Joined = null;
                    switch (why)
                    {
                        case "full":
                            Notice?.Invoke("{name}'s table is full", host);
                            break;
                        case "unreachable":
                            Notice?.Invoke("Couldn't reach {name}'s table", host);
                            break;
                        case "lost":
                            Notice?.Invoke("Lost touch with {name}'s table", host);
                            break;
                        case "left":
                            Notice?.Invoke("{name} left, so their table closed", host);
                            break;
                        default:
                            Notice?.Invoke(seated ? "{name} closed the table" : "{name}'s table has closed", host);
                            break;
                    }
                    _others.RemoveAll(t => t.Host == host);
                }
            }
            _others.RemoveAll(t => now - t.HeardAt > ForgetAfter);
        }

        // Asks the other players' games which tables there are (they answer with TABLE). Not too often.
        public void Hello(float now)
        {
            if (_net.InLobby && now - _lastHello >= HelloEvery)
            {
                _lastHello = now;
                _net.Broadcast("HELLO");
            }
        }

        // Lobby chat: TABLE / CLOSED / HELLO.
        public void OnBroadcast(ulong from, string[] words, float now)
        {
            if (from == 0 || from == _net.Me || words.Length == 0)
            {
                return;
            }
            switch (words[0])
            {
                case "HELLO":
                    Hosting?.AnnounceNow(now);
                    break;
                case "TABLE":
                    if (words.Length >= 5 && int.TryParse(words[1], out int table) && int.TryParse(words[2], out int seats))
                    {
                        TableInfo info = _others.FirstOrDefault(t => t.Host == from);
                        if (info == null || info.Table != table)
                        {
                            _others.RemoveAll(t => t.Host == from);
                            info = new TableInfo { Host = from, Table = table };
                            _others.Add(info);
                        }
                        info.Seats = seats;
                        info.Over = words[3] == "1";
                        info.Humans = words[4].Split(',')
                            .Select(s => ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) ? id : 0)
                            .Where(id => id != 0).ToList();
                        info.HeardAt = now;
                    }
                    break;
                case "CLOSED":
                    if (words.Length >= 2 && int.TryParse(words[1], out int closed))
                    {
                        _others.RemoveAll(t => t.Host == from && t.Table == closed);
                        if (Joined != null && Joined.HostId == from && Joined.Table == closed)
                        {
                            Joined.HostLeft("closed");
                        }
                    }
                    break;
            }
        }

        // Messages to you alone.
        public void OnDirect(ulong from, string[] words, float now)
        {
            if (from == 0 || words.Length == 0)
            {
                return;
            }
            switch (words[0])
            {
                case "JOIN":
                case "LEAVE":
                case "ASK":
                    if (Hosting != null)
                    {
                        Hosting.OnDirect(from, words, now);
                    }
                    else if (words[0] == "JOIN" && words.Length >= 2)
                    {
                        // A table that's gone: tell them, so they don't wait.
                        _net.SendTo(from, $"GONE {words[1]}");
                    }
                    break;
                case "VIEW":
                case "FULL":
                case "GONE":
                    Joined?.OnDirect(from, words, now);
                    break;
            }
        }

        public void OnMemberLeft(ulong id, float now)
        {
            Hosting?.PlayerLeft(id, now);
            if (Joined != null && Joined.HostId == id)
            {
                Joined.HostLeft("left");
            }
            _others.RemoveAll(t => t.Host == id);
        }

        // You joined or left a session: tables from the old one are gone. (A table started before the
        // first session was noticed, a second after reaching the world, stays.)
        public void OnLobbyChanged(bool hadLobby, float now)
        {
            if (hadLobby)
            {
                LeaveTable(now);
            }
            _others.Clear();
            _lastHello = -1000f;
            Hello(now);
        }
    }
}
