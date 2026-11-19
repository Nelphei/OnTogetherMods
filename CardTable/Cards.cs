namespace CardTable
{
    // A card is a number from 0 to 51: suit * 13 + rank - 1. Suits go spades, hearts, clubs,
    // diamonds, so black and red take turns. Ranks go from 1 (ace) to 13 (king).
    internal static class Cards
    {
        public const int Count = 52;
        public const int Ace = 1;
        public const int King = 13;

        public const int Spades = 0;
        public const int Hearts = 1;
        public const int Clubs = 2;
        public const int Diamonds = 3;

        public static int Suit(int card) => card / 13;
        public static int Rank(int card) => card % 13 + 1;
        public static bool IsRed(int card) => Suit(card) % 2 == 1;
        public static bool IsRedSuit(int suit) => suit % 2 == 1;
        public static int Make(int suit, int rank) => suit * 13 + rank - 1;

        public static string RankText(int rank)
        {
            switch (rank)
            {
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return rank.ToString();
            }
        }

        // For the log: "Q of hearts".
        public static string Name(int card)
        {
            string[] suits = { "spades", "hearts", "clubs", "diamonds" };
            return RankText(Rank(card)) + " of " + suits[Suit(card)];
        }
    }
}
