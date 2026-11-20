namespace SoapBox
{
    // The console's 16 colours, all taken from the icon row's palette (Shared/IconBar.cs), so carts
    // look at home next to the icons. Grouped in ramps, dark to light: greys, warm colours, then
    // cool ones. Colour 0 is the "ink" every screen starts with, and sprites treat it as see-through
    // unless a cart says otherwise.
    internal static class Palette
    {
        public const int Count = 16;

        // 0xRRGGBB, with the IconBar letter each one comes from.
        public static readonly uint[] Rgb =
        {
            0x2A2433, //  0 ink         K
            0x57525F, //  1 dark grey   D
            0x8F8A99, //  2 grey        g
            0xF7F3EA, //  3 white       W
            0xA86B3C, //  4 brown       B
            0xE05A5A, //  5 red         R
            0xE8924A, //  6 orange      O
            0xF5D061, //  7 yellow      Y
            0xF6CFAE, //  8 peach       P
            0xE98BB5, //  9 pink        S
            0x9A72D6, // 10 purple      V
            0x2F5A9E, // 11 dark blue   u
            0x4A8FD9, // 12 blue        U
            0x9FD6F5, // 13 light blue  L
            0x3F8A4A, // 14 dark green  n
            0x74C069, // 15 green       N
        };

        public static readonly string[] Names =
        {
            "ink", "dark grey", "grey", "white", "brown", "red", "orange", "yellow",
            "peach", "pink", "purple", "dark blue", "blue", "light blue", "dark green", "green",
        };

        // Named numbers for code that draws with the palette (editors, the frame, built-in screens).
        public const byte Ink = 0, DarkGrey = 1, Grey = 2, White = 3, Brown = 4, Red = 5, Orange = 6, Yellow = 7,
            Peach = 8, Pink = 9, Purple = 10, DarkBlue = 11, Blue = 12, LightBlue = 13, DarkGreen = 14, Green = 15;
    }
}
