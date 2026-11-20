using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CardTable
{
    internal sealed class SeatView
    {
        public ulong Who;   // the player's Steam ID, or 0 for a computer player
        public int Bot;     // which computer player (their name), when Who is 0
        public int Cards;
        public List<int> Books = new List<int>();

        public bool IsBot => Who == 0;
    }

    // A Go Fish table as one player sees it: everyone's card counts and books, but only their own
    // cards. The table's host makes one for each player after every change; the window draws it.
    // Sent to other players as one line of text (Encode / Decode).
    internal sealed class GoFishView
    {
        public const int LogLength = 8;

        public int Table;
        public int Seq;        // counts up with every change to the table
        public int GameNo;     // counts up with every deal
        public bool Over;
        public int Turn;
        public int Pond;
        public int MySeat;
        public SeatView[] Seats = new SeatView[0];
        public List<int> Hand = new List<int>();
        public List<FishEvent> Log = new List<FishEvent>();

        public int SeatCount => Seats.Length;

        public bool MyTurn => !Over && Turn == MySeat;

        public int Humans => Seats.Count(s => !s.IsBot);

        public List<int> Winners()
        {
            int most = Seats.Max(s => s.Books.Count);
            return Enumerable.Range(0, Seats.Length).Where(s => Seats[s].Books.Count == most).ToList();
        }

        // "VIEW table seq game over turn pond seat seats hand log", where seats are
        // "who,bot,cards,books" joined by '/', books are ranks as hex digits (1-D), the hand is card
        // numbers joined by ',', log entries are "id,kind,seat,target,rank,count,who" joined by '/',
        // and an empty list is '-'.
        public string Encode()
        {
            var text = new StringBuilder("VIEW");
            text.Append(' ').Append(Table)
                .Append(' ').Append(Seq)
                .Append(' ').Append(GameNo)
                .Append(' ').Append(Over ? 1 : 0)
                .Append(' ').Append(Turn)
                .Append(' ').Append(Pond)
                .Append(' ').Append(MySeat)
                .Append(' ');
            text.Append(string.Join("/", Seats.Select(s =>
                $"{s.Who},{s.Bot},{s.Cards},{(s.Books.Count == 0 ? "-" : string.Concat(s.Books.Select(r => r.ToString("X"))))}")));
            text.Append(' ').Append(Hand.Count == 0 ? "-" : string.Join(",", Hand));
            text.Append(' ').Append(Log.Count == 0 ? "-" : string.Join("/", Log.Select(e =>
                $"{e.Id},{(int)e.Kind},{e.Seat},{e.Target},{e.Rank},{e.Count},{e.Who}")));
            return text.ToString();
        }

        // The words after "VIEW". Null if they don't make sense.
        public static GoFishView Decode(IReadOnlyList<string> w)
        {
            try
            {
                if (w.Count < 10)
                {
                    return null;
                }
                var view = new GoFishView
                {
                    Table = Int(w[0]),
                    Seq = Int(w[1]),
                    GameNo = Int(w[2]),
                    Over = w[3] == "1",
                    Turn = Int(w[4]),
                    Pond = Int(w[5]),
                    MySeat = Int(w[6]),
                };
                view.Seats = w[7].Split('/').Select(part =>
                {
                    string[] f = part.Split(',');
                    var seat = new SeatView
                    {
                        Who = ulong.Parse(f[0], CultureInfo.InvariantCulture),
                        Bot = Int(f[1]),
                        Cards = Int(f[2]),
                    };
                    if (f[3] != "-")
                    {
                        seat.Books = f[3].Select(c => int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToList();
                    }
                    return seat;
                }).ToArray();
                if (w[8] != "-")
                {
                    view.Hand = w[8].Split(',').Select(Int).ToList();
                }
                if (w[9] != "-")
                {
                    view.Log = w[9].Split('/').Select(part =>
                    {
                        string[] f = part.Split(',');
                        return new FishEvent
                        {
                            Id = Int(f[0]),
                            Kind = (FishEventKind)Int(f[1]),
                            Seat = Int(f[2]),
                            Target = Int(f[3]),
                            Rank = Int(f[4]),
                            Count = Int(f[5]),
                            Who = ulong.Parse(f[6], CultureInfo.InvariantCulture),
                        };
                    }).ToList();
                }
                return view.Valid() ? view : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool Valid()
        {
            int n = Seats.Length;
            return n >= GoFish.MinSeats && n <= GoFish.MaxSeats && Turn >= 0 && Turn < n && MySeat >= -1 && MySeat < n &&
                   Pond >= 0 && Pond <= Cards.Count && Hand.All(c => c >= 0 && c < Cards.Count) &&
                   Seats.All(s => s.Cards >= 0 && s.Cards <= Cards.Count && s.Books.All(r => r >= Cards.Ace && r <= Cards.King)) &&
                   Log.All(e => Enum.IsDefined(typeof(FishEventKind), e.Kind));
        }

        private static int Int(string s) => int.Parse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
}
