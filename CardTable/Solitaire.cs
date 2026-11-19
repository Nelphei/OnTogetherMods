using System;
using System.Collections.Generic;
using System.Linq;

namespace CardTable
{
    internal enum PileKind
    {
        Stock,       // the deck, face down, top left
        Waste,       // cards turned over from the deck
        Foundation,  // the four piles at the top right, ace up to king
        Tableau,     // the seven piles below
    }

    internal readonly struct PileId : IEquatable<PileId>
    {
        public readonly PileKind Kind;
        public readonly int Index;

        private PileId(PileKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static readonly PileId Stock = new PileId(PileKind.Stock, 0);
        public static readonly PileId Waste = new PileId(PileKind.Waste, 0);
        public static PileId Foundation(int index) => new PileId(PileKind.Foundation, index);
        public static PileId Tableau(int index) => new PileId(PileKind.Tableau, index);

        public bool Equals(PileId other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is PileId other && Equals(other);
        public override int GetHashCode() => (int)Kind * 16 + Index;
        public static bool operator ==(PileId a, PileId b) => a.Equals(b);
        public static bool operator !=(PileId a, PileId b) => !a.Equals(b);
        public override string ToString() => Kind + (Kind == PileKind.Foundation || Kind == PileKind.Tableau ? " " + Index : "");
    }

    // Klondike solitaire: the rules and the cards, with no drawing. Lists hold cards bottom first,
    // so the last card is the top one.
    internal sealed class Solitaire
    {
        public const int Piles = 7;
        public const int FoundationCount = 4;
        // How far back Undo goes.
        private const int MaxHistory = 1000;

        public readonly List<int> Stock = new List<int>();
        public readonly List<int> Waste = new List<int>();
        public readonly List<int>[] Foundations = NewLists(FoundationCount);
        public readonly List<int>[] Tableau = NewLists(Piles);
        // How many cards at the bottom of each tableau pile are face down. The rest are face up.
        public readonly int[] FaceDown = new int[Piles];
        // How many of the waste's top cards are spread out to the right: the ones the last draw turned
        // over (up to three when drawing three), less the ones played since.
        public int WasteFanned;

        public bool DrawThree;
        // Everything you've done this game, Undo included.
        public int Moves;
        public double Seconds;
        // Whether this game is in the played count yet (it goes in at its first move).
        public bool Counted;

        private readonly List<State> _history = new List<State>();
        // Draws in a row by Auto-finish that put nothing up top. It gives up after a whole trip
        // through the deck.
        private int _idleDraws;

        public bool CanUndo => _history.Count > 0;
        public bool Started => Moves > 0;
        public bool Won => Foundations.All(f => f.Count == 13);

        // ---- Dealing ----

        public void Deal(Random random)
        {
            var deck = Enumerable.Range(0, Cards.Count).ToList();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = deck[i];
                deck[i] = deck[j];
                deck[j] = swap;
            }

            Stock.Clear();
            Waste.Clear();
            foreach (List<int> pile in Foundations.Concat(Tableau))
            {
                pile.Clear();
            }
            // Row by row, like dealing by hand: pile i gets i + 1 cards, the top one face up.
            int next = 0;
            for (int row = 0; row < Piles; row++)
            {
                for (int pile = row; pile < Piles; pile++)
                {
                    Tableau[pile].Add(deck[next++]);
                }
            }
            for (int pile = 0; pile < Piles; pile++)
            {
                FaceDown[pile] = pile;
            }
            // The rest is the deck. Its last card is the top one.
            Stock.AddRange(deck.Skip(next));
            WasteFanned = 0;
            Moves = 0;
            Seconds = 0;
            Counted = false;
            _history.Clear();
            _idleDraws = 0;
        }

        // The tableau cards in the order they were dealt, for the dealing animation.
        public IEnumerable<int> DealOrder()
        {
            for (int row = 0; row < Piles; row++)
            {
                for (int pile = row; pile < Piles; pile++)
                {
                    if (row < Tableau[pile].Count)
                    {
                        yield return Tableau[pile][row];
                    }
                }
            }
        }

        // ---- Reading the table ----

        public List<int> Pile(PileId pile)
        {
            switch (pile.Kind)
            {
                case PileKind.Stock: return Stock;
                case PileKind.Waste: return Waste;
                case PileKind.Foundation: return Foundations[pile.Index];
                default: return Tableau[pile.Index];
            }
        }

        public bool IsFaceUp(PileId pile, int index)
        {
            switch (pile.Kind)
            {
                case PileKind.Stock: return false;
                case PileKind.Tableau: return index >= FaceDown[pile.Index];
                default: return true;
            }
        }

        // Whether the card at this spot (with every card on top of it) can be picked up.
        public bool CanPickUp(PileId pile, int index)
        {
            List<int> cards = Pile(pile);
            if (index < 0 || index >= cards.Count)
            {
                return false;
            }
            switch (pile.Kind)
            {
                case PileKind.Stock: return false;
                case PileKind.Tableau: return index >= FaceDown[pile.Index];
                default: return index == cards.Count - 1;   // only the top card of the waste or a foundation
            }
        }

        // Whether this card can go on that foundation now.
        public bool FitsFoundation(int card, int foundation)
        {
            List<int> pile = Foundations[foundation];
            if (pile.Count == 0)
            {
                return Cards.Rank(card) == Cards.Ace;
            }
            int top = pile[pile.Count - 1];
            return Cards.Suit(top) == Cards.Suit(card) && Cards.Rank(card) == Cards.Rank(top) + 1;
        }

        // The foundation this card can go on now, or -1. Its own suit's pile if one is started,
        // otherwise (for an ace) the first empty one.
        public int FoundationFor(int card)
        {
            for (int f = 0; f < FoundationCount; f++)
            {
                if (Foundations[f].Count > 0 && FitsFoundation(card, f))
                {
                    return f;
                }
            }
            if (Cards.Rank(card) == Cards.Ace)
            {
                for (int f = 0; f < FoundationCount; f++)
                {
                    if (Foundations[f].Count == 0)
                    {
                        return f;
                    }
                }
            }
            return -1;
        }

        // Whether this card can go on that tableau pile now: a king on an empty pile, or one rank
        // lower and the other colour.
        public bool FitsTableau(int card, int pile)
        {
            List<int> cards = Tableau[pile];
            if (cards.Count == 0)
            {
                return Cards.Rank(card) == Cards.King;
            }
            int top = cards[cards.Count - 1];
            return Cards.IsRed(top) != Cards.IsRed(card) && Cards.Rank(card) == Cards.Rank(top) - 1;
        }

        public bool CanMove(PileId from, int index, PileId to)
        {
            if (from == to || !CanPickUp(from, index))
            {
                return false;
            }
            List<int> cards = Pile(from);
            int card = cards[index];
            switch (to.Kind)
            {
                case PileKind.Foundation:
                    return index == cards.Count - 1 && FitsFoundation(card, to.Index);
                case PileKind.Tableau:
                    return FitsTableau(card, to.Index);
                default:
                    return false;
            }
        }

        // Where a click sends this card (and the ones on it): up top if it's a single card that fits
        // there, otherwise the first tableau pile it fits on, trying piles with cards before empty ones.
        // A king that's already at the bottom of its pile isn't moved to another empty pile.
        public PileId? BestMove(PileId from, int index)
        {
            if (!CanPickUp(from, index))
            {
                return null;
            }
            List<int> cards = Pile(from);
            int card = cards[index];
            if (index == cards.Count - 1 && from.Kind != PileKind.Foundation)
            {
                int f = FoundationFor(card);
                if (f >= 0)
                {
                    return PileId.Foundation(f);
                }
            }
            for (int pile = 0; pile < Piles; pile++)
            {
                if (Tableau[pile].Count > 0 && CanMove(from, index, PileId.Tableau(pile)))
                {
                    return PileId.Tableau(pile);
                }
            }
            bool kingAtBottom = from.Kind == PileKind.Tableau && index == 0;
            if (!kingAtBottom)
            {
                for (int pile = 0; pile < Piles; pile++)
                {
                    if (Tableau[pile].Count == 0 && CanMove(from, index, PileId.Tableau(pile)))
                    {
                        return PileId.Tableau(pile);
                    }
                }
            }
            return null;
        }

        // ---- Moving ----

        // Moves the card at index (and every card on top of it) onto another pile.
        public bool Move(PileId from, int index, PileId to)
        {
            if (!CanMove(from, index, to))
            {
                return false;
            }
            Remember();
            List<int> source = Pile(from);
            List<int> moving = source.GetRange(index, source.Count - index);
            source.RemoveRange(index, moving.Count);
            Pile(to).AddRange(moving);
            if (from.Kind == PileKind.Tableau)
            {
                TurnOverTop(from.Index);
            }
            if (from.Kind == PileKind.Waste)
            {
                WasteFanned = Math.Max(0, WasteFanned - 1);
            }
            Moves++;
            return true;
        }

        // Turns over one card from the deck (three when drawing three), or, when the deck is empty,
        // turns the waste back over into a new deck.
        public bool Draw()
        {
            if (Stock.Count == 0 && Waste.Count == 0)
            {
                return false;
            }
            Remember();
            if (Stock.Count == 0)
            {
                // The waste's bottom card becomes the deck's top card.
                for (int i = Waste.Count - 1; i >= 0; i--)
                {
                    Stock.Add(Waste[i]);
                }
                Waste.Clear();
                WasteFanned = 0;
            }
            else
            {
                int count = Math.Min(DrawThree ? 3 : 1, Stock.Count);
                for (int i = 0; i < count; i++)
                {
                    Waste.Add(Stock[Stock.Count - 1]);
                    Stock.RemoveAt(Stock.Count - 1);
                }
                WasteFanned = count;
            }
            Moves++;
            return true;
        }

        public bool Undo()
        {
            if (_history.Count == 0)
            {
                return false;
            }
            State state = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Restore(state);
            Moves++;
            _idleDraws = 0;
            return true;
        }

        // A tableau pile's top card is always face up.
        private void TurnOverTop(int pile)
        {
            int count = Tableau[pile].Count;
            if (count > 0 && FaceDown[pile] >= count)
            {
                FaceDown[pile] = count - 1;
            }
            if (count == 0)
            {
                FaceDown[pile] = 0;
            }
        }

        // ---- Auto-finish ----

        // Once no card is face down, the rest of the game is just putting cards up top.
        public bool CanAutoFinish => !Won && FaceDown.All(n => n == 0);

        public void StartAutoFinish()
        {
            _idleDraws = 0;
        }

        // Whether Auto-finish would win from here, tried out on a copy. Drawing one it always does;
        // drawing three it can get stuck, with deck cards it can't reach without your help.
        public bool AutoFinishWins()
        {
            if (!CanAutoFinish)
            {
                return false;
            }
            var copy = new Solitaire();
            if (!copy.Load(Save()))
            {
                return false;
            }
            copy.StartAutoFinish();
            for (int i = 0; i < 5000 && copy.AutoFinishStep(); i++)
            {
            }
            return copy.Won;
        }

        // One step: puts the lowest card that can go up top there, or turns over the deck. False when
        // nothing more can happen: won, or (drawing three) the deck went round without helping.
        public bool AutoFinishStep()
        {
            PileId? from = null;
            int fromIndex = -1;
            int lowest = int.MaxValue;
            for (int pile = 0; pile < Piles; pile++)
            {
                Consider(PileId.Tableau(pile), ref from, ref fromIndex, ref lowest);
            }
            Consider(PileId.Waste, ref from, ref fromIndex, ref lowest);
            if (from.HasValue)
            {
                int card = Pile(from.Value)[fromIndex];
                _idleDraws = 0;
                return Move(from.Value, fromIndex, PileId.Foundation(FoundationFor(card)));
            }
            int left = Stock.Count + Waste.Count;
            if (left > 0 && _idleDraws <= left + 1)
            {
                _idleDraws++;
                return Draw();
            }
            return false;
        }

        private void Consider(PileId pile, ref PileId? from, ref int fromIndex, ref int lowest)
        {
            List<int> cards = Pile(pile);
            if (cards.Count == 0)
            {
                return;
            }
            int card = cards[cards.Count - 1];
            int rank = Cards.Rank(card);
            if (rank < lowest && FoundationFor(card) >= 0)
            {
                lowest = rank;
                from = pile;
                fromIndex = cards.Count - 1;
            }
        }

        // ---- Undo history ----

        private sealed class State
        {
            public int[] Stock;
            public int[] Waste;
            public int[][] Foundations;
            public int[][] Tableau;
            public int[] FaceDown;
            public int WasteFanned;
        }

        private void Remember()
        {
            _history.Add(new State
            {
                Stock = Stock.ToArray(),
                Waste = Waste.ToArray(),
                Foundations = Foundations.Select(f => f.ToArray()).ToArray(),
                Tableau = Tableau.Select(t => t.ToArray()).ToArray(),
                FaceDown = (int[])FaceDown.Clone(),
                WasteFanned = WasteFanned,
            });
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        private void Restore(State state)
        {
            Refill(Stock, state.Stock);
            Refill(Waste, state.Waste);
            for (int i = 0; i < FoundationCount; i++)
            {
                Refill(Foundations[i], state.Foundations[i]);
            }
            for (int i = 0; i < Piles; i++)
            {
                Refill(Tableau[i], state.Tableau[i]);
            }
            Array.Copy(state.FaceDown, FaceDown, Piles);
            WasteFanned = state.WasteFanned;
        }

        private static void Refill(List<int> list, int[] cards)
        {
            list.Clear();
            list.AddRange(cards);
        }

        private static List<int>[] NewLists(int count)
        {
            var lists = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                lists[i] = new List<int>();
            }
            return lists;
        }

        // ---- Saving ----

        // The game as it's saved in CardTable.json. Undo history isn't kept.
        internal sealed class Saved
        {
            public List<int> Stock;
            public List<int> Waste;
            public List<List<int>> Foundations;
            public List<List<int>> Tableau;
            public List<int> FaceDown;
            public int WasteFanned;
            public bool DrawThree;
            public int Moves;
            public double Seconds;
            public bool Counted;
        }

        public Saved Save()
        {
            return new Saved
            {
                Stock = Stock.ToList(),
                Waste = Waste.ToList(),
                Foundations = Foundations.Select(f => f.ToList()).ToList(),
                Tableau = Tableau.Select(t => t.ToList()).ToList(),
                FaceDown = FaceDown.ToList(),
                WasteFanned = WasteFanned,
                DrawThree = DrawThree,
                Moves = Moves,
                Seconds = Seconds,
                Counted = Counted,
            };
        }

        // Loads a saved game. Returns false (and changes nothing) if it isn't a whole, legal table:
        // every card exactly once, foundations in order, a face-up card on top of every pile.
        public bool Load(Saved saved)
        {
            if (saved?.Stock == null || saved.Waste == null || saved.Foundations?.Count != FoundationCount ||
                saved.Tableau?.Count != Piles || saved.FaceDown?.Count != Piles ||
                saved.Foundations.Any(f => f == null) || saved.Tableau.Any(t => t == null))
            {
                return false;
            }
            var all = saved.Stock.Concat(saved.Waste).Concat(saved.Foundations.SelectMany(f => f))
                .Concat(saved.Tableau.SelectMany(t => t)).ToList();
            if (all.Count != Cards.Count || all.Any(c => c < 0 || c >= Cards.Count) || all.Distinct().Count() != Cards.Count)
            {
                return false;
            }
            foreach (List<int> pile in saved.Foundations)
            {
                for (int i = 0; i < pile.Count; i++)
                {
                    if (Cards.Rank(pile[i]) != i + 1 || Cards.Suit(pile[i]) != Cards.Suit(pile[0]))
                    {
                        return false;
                    }
                }
            }
            for (int i = 0; i < Piles; i++)
            {
                int down = saved.FaceDown[i];
                int count = saved.Tableau[i].Count;
                if (down < 0 || (count > 0 && down >= count) || (count == 0 && down != 0))
                {
                    return false;
                }
            }

            Refill(Stock, saved.Stock.ToArray());
            Refill(Waste, saved.Waste.ToArray());
            for (int i = 0; i < FoundationCount; i++)
            {
                Refill(Foundations[i], saved.Foundations[i].ToArray());
            }
            for (int i = 0; i < Piles; i++)
            {
                Refill(Tableau[i], saved.Tableau[i].ToArray());
                FaceDown[i] = saved.FaceDown[i];
            }
            WasteFanned = Math.Max(0, Math.Min(Math.Min(3, saved.WasteFanned), Waste.Count));
            DrawThree = saved.DrawThree;
            Moves = Math.Max(0, saved.Moves);
            Seconds = Math.Max(0, saved.Seconds);
            Counted = saved.Counted;
            _history.Clear();
            _idleDraws = 0;
            return true;
        }
    }
}
