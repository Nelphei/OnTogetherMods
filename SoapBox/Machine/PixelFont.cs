using System;
using System.Collections.Generic;

namespace SoapBox
{
    // The console's one font: Monogram (FontData.cs) plus a few symbols of our own below. Every
    // letter is 6 pixels wide, gap included. Capitals are 7 pixels tall and sit on the y you draw
    // at; descenders reach 2 below them, accents 3 above.
    internal static class PixelFont
    {
        public const int Advance = 6;
        public const int CapHeight = 7;

        // Capitals + descenders + a 1-pixel gap. Accents on capitals reach into the gap above.
        public const int LineHeight = 10;

        private const int Rows = 12;
        private const int TopRow = 3; // rows above the capitals

        // Extra symbols, from the capital top (row 3) down, '#' = on. Up to 6 columns; drawn like a letter.
        // The arrows replace Monogram's, which are too small to read at the console's size.
        private static readonly Dictionary<int, string[]> Extras = new Dictionary<int, string[]>
        {
            {
                0x2190, new[] // ←
                {
                    "......",
                    "..#...",
                    ".##...",
                    "#####.",
                    ".##...",
                    "..#...",
                    "......",
                }
            },
            {
                0x2191, new[] // ↑
                {
                    "..#...",
                    ".###..",
                    "#####.",
                    "..#...",
                    "..#...",
                    "..#...",
                    "......",
                }
            },
            {
                0x2192, new[] // →
                {
                    "......",
                    "..#...",
                    "..##..",
                    "#####.",
                    "..##..",
                    "..#...",
                    "......",
                }
            },
            {
                0x2193, new[] // ↓
                {
                    "..#...",
                    "..#...",
                    "..#...",
                    "#####.",
                    ".###..",
                    "..#...",
                    "......",
                }
            },
            {
                0x2665, new[] // ♥
                {
                    "......",
                    ".#.#..",
                    "#####.",
                    "#####.",
                    ".###..",
                    "..#...",
                    "......",
                }
            },
            {
                0x266A, new[] // ♪
                {
                    "..##..",
                    "..#.#.",
                    "..#...",
                    "..#...",
                    "###...",
                    "###...",
                    "......",
                }
            },
            {
                0x2605, new[] // ★
                {
                    "..#...",
                    "..#...",
                    "#####.",
                    ".###..",
                    ".###..",
                    "#...#.",
                    "......",
                }
            },
            {
                0x25CB, new[] // ○ (a bubble)
                {
                    "......",
                    ".###..",
                    "#...#.",
                    "#...#.",
                    "#...#.",
                    ".###..",
                    "......",
                }
            },
            {
                0x25CF, new[] // ●
                {
                    "......",
                    ".###..",
                    "#####.",
                    "#####.",
                    "#####.",
                    ".###..",
                    "......",
                }
            },
        };

        private static Dictionary<int, byte[]> _glyphs;

        private static Dictionary<int, byte[]> Glyphs
        {
            get
            {
                if (_glyphs == null)
                {
                    _glyphs = Load();
                }
                return _glyphs;
            }
        }

        private static Dictionary<int, byte[]> Load()
        {
            byte[] data = Convert.FromBase64String(FontData.Glyphs);
            var glyphs = new Dictionary<int, byte[]>(FontData.GlyphCount + Extras.Count);
            for (int i = 0; i + 2 + Rows <= data.Length; i += 2 + Rows)
            {
                int character = data[i] | (data[i + 1] << 8);
                var rows = new byte[Rows];
                Array.Copy(data, i + 2, rows, 0, Rows);
                glyphs[character] = rows;
            }
            foreach (KeyValuePair<int, string[]> extra in Extras)
            {
                var rows = new byte[Rows];
                for (int r = 0; r < extra.Value.Length && TopRow + r < Rows; r++)
                {
                    string line = extra.Value[r];
                    for (int x = 0; x < line.Length && x < 7; x++)
                    {
                        if (line[x] == '#')
                        {
                            rows[TopRow + r] |= (byte)(1 << (x + 1));
                        }
                    }
                }
                glyphs[extra.Key] = rows;
            }
            return glyphs;
        }

        public static bool Has(char c)
        {
            return Glyphs.ContainsKey(c);
        }

        // Draws text with its first capital's top-left at (x, y); "\n" starts a new line. Scale makes
        // every pixel a scale x scale square. Goes through the surface's camera, clip and colour swaps.
        // Returns the x just past the last letter.
        public static int Draw(Surface surface, string text, int x, int y, int colour, int scale = 1)
        {
            if (string.IsNullOrEmpty(text))
            {
                return x;
            }
            scale = Math.Max(1, scale);
            int startX = x;
            Dictionary<int, byte[]> glyphs = Glyphs;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    x = startX;
                    y += LineHeight * scale;
                    continue;
                }
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++; // one letter outside the font, like an emoji
                    c = '?';
                }
                if (c != ' ')
                {
                    if (!glyphs.TryGetValue(c, out byte[] rows) && !glyphs.TryGetValue('?', out rows))
                    {
                        rows = null;
                    }
                    if (rows != null)
                    {
                        for (int r = 0; r < Rows; r++)
                        {
                            int bits = rows[r];
                            if (bits == 0)
                            {
                                continue;
                            }
                            int py = y + (r - TopRow) * scale;
                            for (int b = 0; b < 8; b++)
                            {
                                if ((bits & (1 << b)) == 0)
                                {
                                    continue;
                                }
                                if (scale == 1)
                                {
                                    surface.Pset(x + b - 1, py, colour);
                                }
                                else
                                {
                                    surface.RectFill(x + (b - 1) * scale, py, scale, scale, colour);
                                }
                            }
                        }
                    }
                }
                x += Advance * scale;
            }
            return x;
        }

        // One letter at (x, y), like Draw (no scaling, no new lines). For the code editor, which
        // colours every letter on its own.
        public static void DrawChar(Surface surface, char c, int x, int y, int colour)
        {
            if (c == ' ')
            {
                return;
            }
            Dictionary<int, byte[]> glyphs = Glyphs;
            if (!glyphs.TryGetValue(c, out byte[] rows) && !glyphs.TryGetValue('?', out rows))
            {
                return;
            }
            for (int r = 0; r < Rows; r++)
            {
                int bits = rows[r];
                if (bits == 0)
                {
                    continue;
                }
                int py = y + r - TopRow;
                for (int b = 0; b < 8; b++)
                {
                    if ((bits & (1 << b)) != 0)
                    {
                        surface.Pset(x + b - 1, py, colour);
                    }
                }
            }
        }

        // Width of the widest line, without the gap after its last letter.
        public static int Width(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int widest = 0, count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    widest = Math.Max(widest, count);
                    count = 0;
                }
                else if (!char.IsLowSurrogate(text[i]))
                {
                    count++;
                }
            }
            widest = Math.Max(widest, count);
            return widest == 0 ? 0 : widest * Advance - 1;
        }
    }
}
