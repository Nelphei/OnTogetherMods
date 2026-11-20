namespace KittyTarot
{
    // The deck's pastel colours, 0xRRGGBBAA. Each colour comes in shades from light (1) to deep.
    internal static class Palette
    {
        // Outlines: a soft plum instead of black.
        public const uint Ink = 0x54385CFF;
        public const uint Ink2 = 0x8C6896FF;

        public const uint Cream = 0xFFF9F2FF;
        public const uint White = 0xFFFFFFFF;

        public const uint Pink1 = 0xFFE4EEFF;
        public const uint Pink2 = 0xFFC4DAFF;
        public const uint Pink3 = 0xF496BAFF;
        public const uint Pink4 = 0xD66E9CFF;

        public const uint Lav1 = 0xF0E8FFFF;
        public const uint Lav2 = 0xD6C4F7FF;
        public const uint Lav3 = 0xB098E4FF;
        public const uint Lav4 = 0x846EC4FF;

        public const uint Blue1 = 0xE2F1FFFF;
        public const uint Blue2 = 0xBADCFAFF;
        public const uint Blue3 = 0x8CB8E8FF;
        public const uint Blue4 = 0x6896D2FF;

        public const uint Mint1 = 0xE2F8ECFF;
        public const uint Mint2 = 0xB4E6C8FF;
        public const uint Mint3 = 0x7EC4A0FF;
        public const uint Mint4 = 0x58A480FF;

        public const uint Butter1 = 0xFFF8D6FF;
        public const uint Butter2 = 0xFFE8A0FF;
        public const uint Butter3 = 0xF6C86EFF;

        public const uint Peach1 = 0xFFE2CEFF;
        public const uint Peach2 = 0xFFC8A6FF;
        public const uint Peach3 = 0xF0A078FF;
        public const uint Peach4 = 0xD67C60FF;

        // Each suit's colours: the band round the card, the ribbon and its shadow, and the sky of a
        // card still waiting for its picture. The major cards (suit -1) are pink.
        public static uint Band(int suit) => Pick(suit, Pink2, Peach2, Blue2, Lav2, Mint2);

        public static uint Ribbon(int suit) => Pick(suit, Pink3, Peach3, Blue3, Lav3, Mint3);

        public static uint RibbonShade(int suit) => Pick(suit, Pink4, Peach4, Blue4, Lav4, Mint4);

        public static uint SkyTop(int suit) => Pick(suit, Lav1, Butter1, Lav1, Lav1, Butter1);

        public static uint SkyMiddle(int suit) => Pick(suit, Pink1, Peach1, Blue1, Lav2, Mint1);

        public static uint SkyBottom(int suit) => Pick(suit, Pink2, Peach2, Blue2, Lav3, Mint2);

        // Suits in the book's order: wands, cups, swords, pentacles.
        private static uint Pick(int suit, uint major, uint wands, uint cups, uint swords, uint pentacles)
        {
            switch (suit)
            {
                case 0: return wands;
                case 1: return cups;
                case 2: return swords;
                case 3: return pentacles;
                default: return major;
            }
        }
    }
}
