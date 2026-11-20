using System.Collections.Generic;

namespace KittyTarot
{
    // The 78 cards by number: 0-21 are the major cards in order (The Fool is 0), then the four suits of
    // 14 cards each in the book's order (wands, cups, swords, pentacles), Ace to King.
    internal static class Deck
    {
        public const int Count = 78;
        public const int Majors = 22;
        public const int SuitSize = 14;

        // Longest line on a card's ribbon, in letters.
        public const int RibbonLetters = 10;

        private static readonly string[] MajorNames =
        {
            "The Fool", "The Magician", "The High Priestess", "The Empress", "The Emperor", "The Hierophant",
            "The Lovers", "The Chariot", "Strength", "The Hermit", "Wheel of Fortune", "Justice",
            "The Hanged Man", "Death", "Temperance", "The Devil", "The Tower", "The Star", "The Moon",
            "The Sun", "Judgement", "The World",
        };

        private static readonly string[] SuitNames = { "Wands", "Cups", "Swords", "Pentacles" };

        private static readonly string[] RankNames =
        {
            "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Page", "Knight", "Queen", "King",
        };

        private static readonly string[] Roman =
        {
            "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX", "XXI",
        };

        public static bool IsMajor(int card) => card < Majors;

        // -1 for the major cards.
        public static int Suit(int card) => IsMajor(card) ? -1 : (card - Majors) / SuitSize;

        // The major cards' own numbers; in a suit 1 is the Ace, 11-14 Page, Knight, Queen, King.
        public static int Rank(int card) => IsMajor(card) ? card : (card - Majors) % SuitSize + 1;

        public static int Card(int suit, int rank) => Majors + suit * SuitSize + rank - 1;

        public static string Name(int card)
        {
            return IsMajor(card) ? MajorNames[card] : $"{RankNames[Rank(card) - 1]} of {SuitNames[Suit(card)]}";
        }

        public static string SuitName(int suit) => SuitNames[suit];

        // What the top of the card shows: the major cards' numbers, and 2 to 10 in a suit, in Roman
        // numerals. Aces and court cards show none (null).
        public static string Numeral(int card)
        {
            int rank = Rank(card);
            if (IsMajor(card))
            {
                return Roman[rank];
            }
            return rank >= 2 && rank <= 10 ? Roman[rank] : null;
        }

        // A one-line description for the big view: "Major card VIII", "Cups, 3", "Court card of Swords".
        public static string Kind(int card)
        {
            if (IsMajor(card))
            {
                return $"Major card {Roman[card]}";
            }
            int rank = Rank(card);
            return rank > 10 ? $"Court card, {SuitNames[Suit(card)]}" : $"{SuitNames[Suit(card)]}, {rank}";
        }

        // The name in capitals, on one line or two, each at most RibbonLetters long.
        public static string[] RibbonLines(int card)
        {
            string name = Name(card).ToUpperInvariant();
            if (name.Length <= RibbonLetters)
            {
                return new[] { name };
            }
            string[] words = name.Split(' ');
            var first = new List<string>();
            int i = 0;
            while (i < words.Length && string.Join(" ", ConcatWith(first, words[i])).Length <= RibbonLetters)
            {
                first.Add(words[i]);
                i++;
            }
            if (first.Count == 0)
            {
                first.Add(words[i++]);
            }
            return new[] { string.Join(" ", first), string.Join(" ", words, i, words.Length - i) };
        }

        private static List<string> ConcatWith(List<string> list, string word)
        {
            var copy = new List<string>(list) { word };
            return copy;
        }
    }
}
