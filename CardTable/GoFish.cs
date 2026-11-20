using System;
using System.Collections.Generic;
using System.Linq;

namespace CardTable
{
    internal enum FishEventKind
    {
        Deal,   // a new game: Count cards each, Seat goes first
        Ask,    // Seat asked Target for Rank and got Count cards (0 = "Go fish")
        Fish,   // after a "Go fish", Seat drew from the pond (Count 1) or found it empty (Count 0).
                // Rank is the card's rank when it was the one asked for (they go again), else -1
        Draw,   // Seat had no cards, or nobody else had any, and drew one from the pond
        Book,   // Seat put down all four Rank cards
        Out,    // Seat has no cards and the pond is empty: out until the end
        Join,   // the player Who sat down at Seat, taking a computer player's place
        Leave,  // the player Who left Seat, and a computer player took over
        Over,   // all 13 books are down
    }

    // Something that happened at the table, for the log, the animations and the computer players.
    internal struct FishEvent
    {
        public int Id;   // counts up across a table's games, so a new event is told by its number
        public FishEventKind Kind;
        public int Seat;
        public int Target;
        public int Rank;
        public int Count;
        public ulong Who;
    }

    // Go Fish: the rules and the cards, with no drawing and no players' names. Seats go round in
    // order; Hands hold the cards, Books the ranks put down.
    internal sealed class GoFish
    {
        public const int MinSeats = 2;
        public const int MaxSeats = 6;
        public const int AllBooks = 13;

        public int Seats { get; private set; }
        public List<int>[] Hands = new List<int>[0];
        public List<int>[] Books = new List<int>[0];
        public readonly List<int> Pond = new List<int>();
        public int Turn;
        public bool Over;
        // This game's events (a new deal clears them; the numbers carry on).
        public readonly List<FishEvent> Events = new List<FishEvent>();
        public int NextEventId = 1;

        private bool[] _outNoted = new bool[0];

        public static int HandSize(int seats) => seats <= 3 ? 7 : 5;

        public int BooksDown => Books.Sum(b => b.Count);

        public bool Has(int seat, int rank) => Hands[seat].Any(c => Cards.Rank(c) == rank);

        public int CountOf(int seat, int rank) => Hands[seat].Count(c => Cards.Rank(c) == rank);

        // Shuffles, deals and gives `first` the first turn.
        public void Deal(int seats, Random random, int first)
        {
            if (seats < MinSeats || seats > MaxSeats)
            {
                throw new ArgumentOutOfRangeException(nameof(seats));
            }
            Seats = seats;
            Hands = Enumerable.Range(0, seats).Select(_ => new List<int>()).ToArray();
            Books = Enumerable.Range(0, seats).Select(_ => new List<int>()).ToArray();
            _outNoted = new bool[seats];
            Over = false;
            Events.Clear();

            Pond.Clear();
            Pond.AddRange(Enumerable.Range(0, Cards.Count));
            for (int i = Pond.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Pond[i], Pond[j]) = (Pond[j], Pond[i]);
            }

            int size = HandSize(seats);
            for (int round = 0; round < size; round++)
            {
                for (int seat = 0; seat < seats; seat++)
                {
                    Hands[seat].Add(TakeFromPond());
                }
            }
            Turn = ((first % seats) + seats) % seats;
            Add(FishEventKind.Deal, Turn, count: size);
            // Four of a kind dealt straight into a hand goes down at once.
            for (int seat = 0; seat < seats; seat++)
            {
                for (int rank = Cards.Ace; rank <= Cards.King; rank++)
                {
                    CheckBook(seat, rank);
                }
            }
            Settle(again: true);
        }

        // Whose seats hold cards, apart from `seat`: the players `seat` can ask.
        public IEnumerable<int> Askable(int seat)
        {
            for (int i = 1; i < Seats; i++)
            {
                int other = (seat + i) % Seats;
                if (Hands[other].Count > 0)
                {
                    yield return other;
                }
            }
        }

        public bool CanAsk(int seat, int target, int rank)
        {
            return !Over && seat == Turn && target != seat && target >= 0 && target < Seats &&
                   Hands[target].Count > 0 && rank >= Cards.Ace && rank <= Cards.King && Has(seat, rank);
        }

        // One ask, with everything that follows from it: cards handed over or a fish from the pond,
        // books put down, and the turn moving on (or not).
        public bool Ask(int seat, int target, int rank)
        {
            if (!CanAsk(seat, target, rank))
            {
                return false;
            }
            List<int> given = Hands[target].Where(c => Cards.Rank(c) == rank).ToList();
            bool again;
            if (given.Count > 0)
            {
                Hands[target].RemoveAll(c => Cards.Rank(c) == rank);
                Hands[seat].AddRange(given);
                Add(FishEventKind.Ask, seat, target, rank, given.Count);
                CheckBook(seat, rank);
                again = true;
            }
            else
            {
                Add(FishEventKind.Ask, seat, target, rank, 0);
                if (Pond.Count > 0)
                {
                    int card = TakeFromPond();
                    Hands[seat].Add(card);
                    bool lucky = Cards.Rank(card) == rank;
                    Add(FishEventKind.Fish, seat, rank: lucky ? rank : -1, count: 1);
                    CheckBook(seat, Cards.Rank(card));
                    again = lucky;
                }
                else
                {
                    Add(FishEventKind.Fish, seat, count: 0);
                    again = false;
                }
            }
            Settle(again);
            return true;
        }

        // The seats with the most books (more than one on a tie).
        public List<int> Winners()
        {
            int most = Books.Max(b => b.Count);
            return Enumerable.Range(0, Seats).Where(s => Books[s].Count == most).ToList();
        }

        // Notes something that happened outside the rules (players joining and leaving).
        public void Note(FishEventKind kind, int seat, ulong who)
        {
            Add(kind, seat, who: who);
        }

        private int TakeFromPond()
        {
            int card = Pond[Pond.Count - 1];
            Pond.RemoveAt(Pond.Count - 1);
            return card;
        }

        private int Next(int seat) => (seat + 1) % Seats;

        private bool AnyoneElseHasCards(int seat) => Askable(seat).Any();

        private void CheckBook(int seat, int rank)
        {
            if (CountOf(seat, rank) == 4)
            {
                Hands[seat].RemoveAll(c => Cards.Rank(c) == rank);
                Books[seat].Add(rank);
                Add(FishEventKind.Book, seat, rank: rank);
            }
        }

        // After a move: ends the game once every book is down, otherwise moves the turn on (unless
        // the player goes again) to someone who can ask. A player with no cards draws one from the
        // pond on their turn, or sits out once it's empty. A player nobody can be asked by draws one
        // and the turn moves on.
        private void Settle(bool again)
        {
            if (CheckOver())
            {
                return;
            }
            if (!again)
            {
                Turn = Next(Turn);
            }
            // Each pass draws a card or skips a player who's out, so this always ends; the limit is
            // only a safety net.
            for (int guard = 0; guard < Cards.Count + 4 * MaxSeats; guard++)
            {
                List<int> hand = Hands[Turn];
                if (hand.Count == 0)
                {
                    if (Pond.Count == 0)
                    {
                        if (!_outNoted[Turn])
                        {
                            _outNoted[Turn] = true;
                            Add(FishEventKind.Out, Turn);
                        }
                        Turn = Next(Turn);
                        continue;
                    }
                    hand.Add(TakeFromPond());
                    Add(FishEventKind.Draw, Turn, count: 1);
                    // One card can't make a book.
                }
                if (AnyoneElseHasCards(Turn))
                {
                    return;
                }
                // Nobody to ask. The pond can't be empty here: this player would hold every card
                // left, which would all be in books already.
                if (Pond.Count == 0)
                {
                    break;
                }
                int card = TakeFromPond();
                hand.Add(card);
                Add(FishEventKind.Draw, Turn, count: 1);
                CheckBook(Turn, Cards.Rank(card));
                if (CheckOver())
                {
                    return;
                }
                Turn = Next(Turn);
            }
            // Shouldn't happen; end the game rather than get stuck.
            Over = true;
            Add(FishEventKind.Over, Turn);
        }

        private bool CheckOver()
        {
            if (Over)
            {
                return true;
            }
            if (BooksDown < AllBooks)
            {
                return false;
            }
            Over = true;
            Add(FishEventKind.Over, Turn);
            return true;
        }

        private void Add(FishEventKind kind, int seat, int target = -1, int rank = -1, int count = 0, ulong who = 0)
        {
            Events.Add(new FishEvent
            {
                Id = NextEventId++,
                Kind = kind,
                Seat = seat,
                Target = target,
                Rank = rank,
                Count = count,
                Who = who,
            });
        }
    }
}
