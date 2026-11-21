namespace KittyTarot
{
    // Little pictures drawn as rows of letters ('.' see-through), stamped onto the cards and the table.
    // Each comes with the letters it uses: X is the main colour, k the outline.
    internal static class Sprites
    {
        public static readonly string[] Heart5 =
        {
            "XX.XX",
            "XXXXX",
            ".XXX.",
            "..X..",
        };

        public static readonly string[] Heart7 =
        {
            ".kk.kk.",
            "kXXkXXk",
            "kXXXXXk",
            ".kXXXk.",
            "..kXk..",
            "...k...",
        };

        // Eight wide, so it sits exactly on the middle of an even-width card.
        public static readonly string[] Heart8 =
        {
            ".kk..kk.",
            "kXXkkXXk",
            "kXXXXXXk",
            ".kXXXXk.",
            "..kXXk..",
            "...kk...",
        };

        // The jewel in the middle of the back, the same turned upside down. w is its shine.
        public static readonly string[] Gem4 =
        {
            ".XX.",
            "XwXX",
            "XXwX",
            ".XX.",
        };

        // A five-pointed star: k outline, y fill, w shine.
        public static readonly string[] Star9 =
        {
            "....k....",
            "...kwk...",
            "kkkkykkkk",
            "kwyyyyyyk",
            ".kyyyyyk.",
            "..kyyyk..",
            ".kyykyyk.",
            ".kyk.kyk.",
            ".kk...kk.",
        };

        // A pomegranate for the High Priestess's curtain: k outline, r fruit, m leaf.
        public static readonly string[] Pomegranate =
        {
            "..m..",
            ".krk.",
            "krrrk",
            "krrrk",
            ".kkk.",
        };

        // The High Priestess's crown: a full moon between two crescents. k outline, y moon, w shine.
        public static readonly string[] MoonCrown =
        {
            ".kk.......kk.",
            "kyk..kkk..kyk",
            "ky..kwyyk..yk",
            "ky.kyyyyyk.yk",
            "kyk.kyyyk.kyk",
            ".kk..kkk..kk.",
        };

        // A ram's head for the Emperor's throne, facing you: w wool, f face, h horns curling down the
        // sides, e eyes, n nose.
        public static readonly string[] Ram =
        {
            "....kkkkk....",
            "..kkwwwwwkk..",
            ".khkwwwwwkhk.",
            "khhhkfffkhhhk",
            "khkhkfffkhkhk",
            "khkkfefefkkhk",
            ".khkfffffkhk.",
            "..kkkfffkkk..",
            "....kfnfk....",
            ".....kkk.....",
        };

        // A crown: y gold, Y its shade, r and b jewels.
        public static readonly string[] Crown =
        {
            "k....k....k",
            "kk..kyk..kk",
            "kyk.kyk.kyk",
            "kyykyyykyyk",
            "kyyyyyyyyyk",
            "kyrybyryyYk",
            "kkkkkkkkkkk",
        };

        public static readonly string[] Spark3 =
        {
            ".X.",
            "XXX",
            ".X.",
        };

        public static readonly string[] Spark5 =
        {
            "..X..",
            "..X..",
            "XXXXX",
            "..X..",
            "..X..",
        };

        public static readonly string[] PawSmall =
        {
            "..X.X..",
            "X.....X",
            "..XXX..",
            ".XXXXX.",
            ".XXXXX.",
            "..XXX..",
        };

        public static readonly string[] PawBig =
        {
            "..kk.kk..",
            ".kXXkXXk.",
            "kkXXkXXkk",
            "kXkkkkkXk",
            "kXkXXXkXk",
            ".kXXXXXk.",
            "kXXXXXXXk",
            "kXXXXXXXk",
            ".kkXXXkk.",
            "...kkk...",
        };

        public static readonly string[] Infinity =
        {
            ".XX...XX.",
            "X..X.X..X",
            "X...X...X",
            "X..X.X..X",
            ".XX...XX.",
        };

        public static readonly string[] Tuft =
        {
            ".X.",
            "X.X",
        };

        // P petals, y the middle.
        public static readonly string[] Flower =
        {
            ".P.",
            "PyP",
            ".P.",
        };

        // A white kitten with a flower crown, facing you: k outline, w fur, p inner ears and cheeks,
        // R nose, P m y the crown's flowers and leaves.
        public static readonly string[] Kitten =
        {
            ".k.........k.",
            "kpk.......kpk",
            "kppkPmymPkppk",
            "kwwwwwwwwwwwk",
            "kwwwwwwwwwwwk",
            "kwwkwwwwwkwwk",
            "kwkwkwwwkwkwk",
            "kpwwwwRwwwwpk",
            ".kwwwkwkwwwk.",
            "..kwwwwwwwk..",
            ".kwwwwwwwwwk.",
            "kwwwwwwwwwwwk",
            "kwwwwwwwwwwwk",
            "kwwkwwwwwkwwk",
            ".kkkkkkkkkkk.",
        };

        public static uint KittenColour(char letter)
        {
            switch (letter)
            {
                case 'k': return Palette.Ink;
                case 'w': return Palette.White;
                case 'p': return Palette.Pink2;
                case 'R': return Palette.Pink4;
                case 'P': return Palette.Pink3;
                case 'm': return Palette.Mint3;
                case 'y': return Palette.Butter3;
                default: return 0;
            }
        }

        // A cat seen from behind, looking up, with a bow on its tail: k outline, g fur, P the bow.
        public static readonly string[] CatBack =
        {
            ".k.......k....",
            "kgk.....kgk...",
            "kggkkkkkggk...",
            "kgggggggggk...",
            "kgggggggggk...",
            "kgggggggggk...",
            ".kgggggggk....",
            "..kgggggk.....",
            ".kgggggggk....",
            "kgggggggggk...",
            "kgggggggggk.kk",
            "kgggggggggkkgk",
            "kgggggggggkgk.",
            "kgggggggggkPPk",
            "kggkgggkggkPk.",
            ".kkkkkkkkkkk..",
        };

        public static uint CatBackColour(char letter)
        {
            switch (letter)
            {
                case 'k': return Palette.Ink;
                case 'g': return Palette.Lav1;
                case 'P': return Palette.Pink3;
                default: return 0;
            }
        }

        // k outline, X fill: for sprites drawn in two colours.
        public static System.Func<char, uint> Two(uint fill, uint outline = Palette.Ink)
        {
            return letter => letter == 'X' ? fill : letter == 'k' ? outline : 0u;
        }
    }
}
