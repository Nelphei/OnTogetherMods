using System;
using System.Collections.Generic;
using System.Linq;

namespace KittyTarot
{
    internal enum Phase
    {
        Ready,     // the whole deck in one pile, nothing on the spread
        Cutting,   // the deck cut into three piles, waiting to be stacked back up
        Picking,   // the deck fanned out face down, cards being picked
        Laid,      // the reading's cards are on the spread
    }

    // A deck of cards on a table and the reading done with it, with no Unity: shuffling, washing,
    // cutting, dealing, picking and turning cards over. Like real cards, each card keeps the way up it
    // lies: only a wash turns cards round, and cards put back go under the deck as they were.
    internal sealed class Reading
    {
        public readonly List<int> Deck = new List<int>();      // top first
        public readonly List<int> SetAside = new List<int>();  // the suits, while reading with the major cards only
        public readonly bool[] Upside = new bool[KittyTarot.Deck.Count];

        public Spread Spread { get; private set; } = Spreads.Three;
        public Phase Phase { get; private set; } = Phase.Ready;
        public bool MajorsOnly { get; private set; }

        // The cards on the spread by position (-1 for none), and whether each has been turned over.
        public int[] Slots { get; private set; }
        public bool[] FaceUp { get; private set; }

        // While cutting: the three piles, top first, and the ones stacked back so far, in order.
        public readonly List<int>[] Piles = { new List<int>(), new List<int>(), new List<int>() };
        public readonly List<int> Taken = new List<int>();

        // While picking: the cards picked from the fan, in order.
        public readonly List<int> Picked = new List<int>();

        // Since the last reading, for the status line.
        public int Shuffles { get; private set; }
        public int Washes { get; private set; }
        public bool WasCut { get; private set; }

        public Reading()
        {
            NewDeck();
        }

        // A deck straight out of the box: every card in order, all the right way up.
        public void NewDeck()
        {
            Deck.Clear();
            SetAside.Clear();
            for (int card = 0; card < KittyTarot.Deck.Count; card++)
            {
                Deck.Add(card);
                Upside[card] = false;
            }
            foreach (List<int> pile in Piles)
            {
                pile.Clear();
            }
            Taken.Clear();
            Picked.Clear();
            MajorsOnly = false;
            Phase = Phase.Ready;
            ClearSpread();
            Shuffles = 0;
            Washes = 0;
            WasCut = false;
        }

        private void ClearSpread()
        {
            Slots = Enumerable.Repeat(-1, Spread.Count).ToArray();
            FaceUp = new bool[Spread.Count];
        }

        // ---- Before a reading ----

        // One riffle shuffle (the Gilbert-Shannon-Reeds model of a real one): the deck is cut about in half
        // and the halves fall together, a card more likely to come from the bigger half. A few of them mix
        // the deck well. It never turns cards round.
        public bool Riffle(Random random)
        {
            if (Phase != Phase.Ready || Deck.Count < 2)
            {
                return false;
            }
            int cut = 0;
            for (int i = 0; i < Deck.Count; i++)
            {
                cut += random.Next(2);
            }
            var left = new Queue<int>(Deck.Take(cut));
            var right = new Queue<int>(Deck.Skip(cut));
            Deck.Clear();
            while (left.Count + right.Count > 0)
            {
                bool fromLeft = random.Next(left.Count + right.Count) < left.Count;
                Deck.Add(fromLeft ? left.Dequeue() : right.Dequeue());
            }
            Shuffles++;
            return true;
        }

        // The cards spread out face down, swirled round and gathered up: any order, and each card as likely
        // to end up one way up as the other.
        public bool Wash(Random random)
        {
            if (Phase != Phase.Ready)
            {
                return false;
            }
            for (int i = Deck.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Deck[i], Deck[j]) = (Deck[j], Deck[i]);
            }
            foreach (int card in Deck)
            {
                Upside[card] = random.Next(2) == 1;
            }
            Washes++;
            return true;
        }

        // Cuts the deck into three piles, roughly in thirds.
        public bool StartCut(Random random)
        {
            if (Phase != Phase.Ready || Deck.Count < 3)
            {
                return false;
            }
            int n = Deck.Count;
            int jitter = Math.Max(1, n / 12);
            int a = Clamp(n / 3 + random.Next(-jitter, jitter + 1), 1, n - 2);
            int b = Clamp(2 * n / 3 + random.Next(-jitter, jitter + 1), a + 1, n - 1);
            for (int i = 0; i < 3; i++)
            {
                Piles[i].Clear();
            }
            Piles[0].AddRange(Deck.Take(a));
            Piles[1].AddRange(Deck.Skip(a).Take(b - a));
            Piles[2].AddRange(Deck.Skip(b));
            Deck.Clear();
            Taken.Clear();
            Phase = Phase.Cutting;
            return true;
        }

        // Stacks a pile back up: the first one picked up ends at the bottom, the last on top.
        public bool TakePile(int pile)
        {
            if (Phase != Phase.Cutting || pile < 0 || pile > 2 || Taken.Contains(pile))
            {
                return false;
            }
            Deck.InsertRange(0, Piles[pile]);
            Piles[pile].Clear();
            Taken.Add(pile);
            if (Taken.Count == 3)
            {
                Phase = Phase.Ready;
                WasCut = true;
            }
            return true;
        }

        // ---- Laying out the cards ----

        // The top cards of the deck onto the spread, face down, in order.
        public bool Deal()
        {
            if (Phase != Phase.Ready || Deck.Count < Spread.Count)
            {
                return false;
            }
            for (int i = 0; i < Spread.Count; i++)
            {
                Slots[i] = Deck[0];
                Deck.RemoveAt(0);
                FaceUp[i] = false;
            }
            Phase = Phase.Laid;
            return true;
        }

        public bool StartPick()
        {
            if (Phase != Phase.Ready || Deck.Count < Spread.Count)
            {
                return false;
            }
            Picked.Clear();
            Phase = Phase.Picking;
            return true;
        }

        // Picks a card from the fan, or puts a picked one back.
        public bool TogglePick(int card)
        {
            if (Phase != Phase.Picking || !Deck.Contains(card))
            {
                return false;
            }
            if (Picked.Remove(card))
            {
                return true;
            }
            if (Picked.Count >= Spread.Count)
            {
                return false;
            }
            Picked.Add(card);
            return true;
        }

        public bool PickedAll => Phase == Phase.Picking && Picked.Count == Spread.Count;

        // The picked cards onto the spread, in the order they were picked; the rest gather back up.
        public bool FinishPick()
        {
            if (!PickedAll)
            {
                return false;
            }
            for (int i = 0; i < Spread.Count; i++)
            {
                Slots[i] = Picked[i];
                FaceUp[i] = false;
                Deck.Remove(Picked[i]);
            }
            Picked.Clear();
            Phase = Phase.Laid;
            return true;
        }

        public bool CancelPick()
        {
            if (Phase != Phase.Picking)
            {
                return false;
            }
            Picked.Clear();
            Phase = Phase.Ready;
            return true;
        }

        public bool Flip(int position)
        {
            if (Phase != Phase.Laid || position < 0 || position >= Slots.Length || Slots[position] < 0 || FaceUp[position])
            {
                return false;
            }
            FaceUp[position] = true;
            return true;
        }

        public bool AllFaceUp => FaceUp.All(up => up);

        // Puts the reading's cards back under the deck, the way up they lay, ready for the next one.
        // majorsOnly: whether the next reading uses the major cards only.
        public bool Gather(bool majorsOnly)
        {
            if (Phase != Phase.Laid)
            {
                return false;
            }
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] >= 0)
                {
                    Deck.Add(Slots[i]);
                }
            }
            ClearSpread();
            Phase = Phase.Ready;
            Shuffles = 0;
            Washes = 0;
            WasCut = false;
            SetMajorsOnly(majorsOnly);
            return true;
        }

        // With the major cards only, the suits wait to one side, in their order, until you want them back;
        // then they go under the deck. Only between readings.
        public bool SetMajorsOnly(bool on)
        {
            if (Phase != Phase.Ready || on == MajorsOnly)
            {
                return false;
            }
            if (on)
            {
                SetAside.AddRange(Deck.Where(card => !KittyTarot.Deck.IsMajor(card)));
                Deck.RemoveAll(card => !KittyTarot.Deck.IsMajor(card));
            }
            else
            {
                Deck.AddRange(SetAside);
                SetAside.Clear();
            }
            MajorsOnly = on;
            return true;
        }

        // Finishes a half-done cut (the piles left, in order) or calls off a pick, so the table can be saved.
        public void Settle()
        {
            if (Phase == Phase.Cutting)
            {
                for (int pile = 0; pile < 3; pile++)
                {
                    TakePile(pile);
                }
            }
            if (Phase == Phase.Picking)
            {
                CancelPick();
            }
        }

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        // ---- Saving ----

        // What goes in the save file (Json.NET), after Settle().
        public sealed class Saved
        {
            public int[] Deck;
            public int[] SetAside;
            public int[] Upside;     // the cards lying upside down
            public string Spread;
            public int[] Slots;
            public bool[] FaceUp;
            public bool MajorsOnly;
            public int Shuffles;
            public int Washes;
            public bool WasCut;
        }

        // A half-done cut is saved as finished (the piles left stacked on in order) and a half-done pick as
        // called off, without changing the table you're looking at.
        public Saved Save()
        {
            var deck = new List<int>(Deck);
            if (Phase == Phase.Cutting)
            {
                for (int pile = 0; pile < 3; pile++)
                {
                    if (!Taken.Contains(pile))
                    {
                        deck.InsertRange(0, Piles[pile]);
                    }
                }
            }
            bool laid = Phase == Phase.Laid;
            return new Saved
            {
                Deck = deck.ToArray(),
                SetAside = SetAside.ToArray(),
                Upside = Enumerable.Range(0, KittyTarot.Deck.Count).Where(card => Upside[card]).ToArray(),
                Spread = Spread.Id,
                Slots = laid ? (int[])Slots.Clone() : Enumerable.Repeat(-1, Spread.Count).ToArray(),
                FaceUp = laid ? (bool[])FaceUp.Clone() : new bool[Spread.Count],
                MajorsOnly = MajorsOnly,
                Shuffles = Shuffles,
                Washes = Washes,
                WasCut = WasCut || Phase == Phase.Cutting,
            };
        }

        // Takes a saved table back, if it makes sense (every card exactly once, and so on). Returns false,
        // changing nothing, if it doesn't.
        public bool Load(Saved saved)
        {
            Spread spread = saved == null ? null : Spreads.Find(saved.Spread);
            if (spread == null || saved.Deck == null || saved.SetAside == null || saved.Slots == null || saved.FaceUp == null
                || saved.Slots.Length != spread.Count || saved.FaceUp.Length != spread.Count)
            {
                return false;
            }
            bool laid = saved.Slots.All(card => card >= 0);
            if (!laid && saved.Slots.Any(card => card >= 0))
            {
                return false;
            }
            var seen = new bool[KittyTarot.Deck.Count];
            foreach (int card in saved.Deck.Concat(saved.SetAside).Concat(saved.Slots.Where(c => c >= 0)))
            {
                if (card < 0 || card >= KittyTarot.Deck.Count || seen[card])
                {
                    return false;
                }
                seen[card] = true;
            }
            if (seen.Any(s => !s))
            {
                return false;
            }
            if (saved.MajorsOnly
                ? saved.SetAside.Any(KittyTarot.Deck.IsMajor) || saved.Deck.Any(card => !KittyTarot.Deck.IsMajor(card))
                : saved.SetAside.Length > 0)
            {
                return false;
            }

            NewDeck();
            Spread = spread;
            Deck.Clear();
            Deck.AddRange(saved.Deck);
            SetAside.AddRange(saved.SetAside);
            foreach (int card in saved.Upside ?? new int[0])
            {
                if (card >= 0 && card < KittyTarot.Deck.Count)
                {
                    Upside[card] = true;
                }
            }
            Slots = (int[])saved.Slots.Clone();
            FaceUp = laid ? (bool[])saved.FaceUp.Clone() : new bool[spread.Count];
            MajorsOnly = saved.MajorsOnly;
            Phase = laid ? Phase.Laid : Phase.Ready;
            Shuffles = Math.Max(0, saved.Shuffles);
            Washes = Math.Max(0, saved.Washes);
            WasCut = saved.WasCut;
            return true;
        }
    }
}
