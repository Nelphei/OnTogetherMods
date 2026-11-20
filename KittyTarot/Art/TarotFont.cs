using System;
using System.Collections.Generic;

namespace KittyTarot
{
    // Monogram (TarotFontData.cs) for the card names and the table. Every letter is 6 pixels wide, gap
    // included. Capitals are 7 pixels tall and sit on the y you draw at; descenders reach 2 below them.
    internal static class TarotFont
    {
        public const int Advance = 6;
        public const int CapHeight = 7;
        public const int LineHeight = 10;

        private const int Rows = 12;
        private const int TopRow = 3; // rows above the capitals

        // A heart of our own (Soap Box's), from the capital top down.
        private static readonly string[] Heart =
        {
            "......",
            ".#.#..",
            "#####.",
            "#####.",
            ".###..",
            "..#...",
            "......",
        };

        private static Dictionary<int, byte[]> _glyphs;

        private static Dictionary<int, byte[]> Glyphs => _glyphs ?? (_glyphs = Load());

        private static Dictionary<int, byte[]> Load()
        {
            byte[] data = Convert.FromBase64String(TarotFontData.Glyphs);
            var glyphs = new Dictionary<int, byte[]>(TarotFontData.GlyphCount + 1);
            for (int i = 0; i + 2 + Rows <= data.Length; i += 2 + Rows)
            {
                int character = data[i] | (data[i + 1] << 8);
                var rows = new byte[Rows];
                Array.Copy(data, i + 2, rows, 0, Rows);
                glyphs[character] = rows;
            }
            var heart = new byte[Rows];
            for (int r = 0; r < Heart.Length; r++)
            {
                for (int x = 0; x < Heart[r].Length; x++)
                {
                    if (Heart[r][x] == '#')
                    {
                        heart[TopRow + r] |= (byte)(1 << (x + 1));
                    }
                }
            }
            glyphs[0x2665] = heart;
            return glyphs;
        }

        // Width of one line of text, without the gap after its last letter.
        public static int Width(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : text.Length * Advance - 1;
        }

        // Draws one line with its first capital's top-left at (x, y).
        public static void Draw(Picture picture, string text, int x, int y, uint colour)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Dictionary<int, byte[]> glyphs = Glyphs;
            foreach (char c in text)
            {
                if (c != ' ' && (glyphs.TryGetValue(c, out byte[] rows) || glyphs.TryGetValue('?', out rows)))
                {
                    for (int r = 0; r < Rows; r++)
                    {
                        int bits = rows[r];
                        for (int b = 0; bits != 0 && b < 8; b++)
                        {
                            if ((bits & (1 << b)) != 0)
                            {
                                picture[x + b - 1, y + r - TopRow] = colour;
                            }
                        }
                    }
                }
                x += Advance;
            }
        }

        // A line of text on its own picture, with room for descenders below.
        public static Picture Render(string text, uint colour)
        {
            var picture = new Picture(Math.Max(1, Width(text)), CapHeight + 2);
            Draw(picture, text, 0, 0, colour);
            return picture;
        }
    }
}
