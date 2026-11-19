using System;

namespace CardTable
{
    // A small picture in memory, top row first. Colours are 0xRRGGBBAA; 0 is see-through.
    // CardArt turns these into textures; keeping Unity out of here lets the art be checked outside the game.
    internal sealed class Picture
    {
        public readonly int Width;
        public readonly int Height;
        public readonly uint[] Pixels;

        public Picture(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        public uint this[int x, int y]
        {
            get => Inside(x, y) ? Pixels[y * Width + x] : 0u;
            set
            {
                if (Inside(x, y))
                {
                    Pixels[y * Width + x] = value;
                }
            }
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // Draws rows of letters ('.' skipped) with the colour each letter maps to (0 skips it too).
        public void Stamp(string[] rows, int left, int top, Func<char, uint> colour)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char letter = rows[y][x];
                    if (letter == '.')
                    {
                        continue;
                    }
                    uint c = colour(letter);
                    if (c != 0)
                    {
                        this[left + x, top + y] = c;
                    }
                }
            }
        }

        // Copies another picture on top, leaving its see-through pixels out.
        public void Paste(Picture other, int left, int top)
        {
            for (int y = 0; y < other.Height; y++)
            {
                for (int x = 0; x < other.Width; x++)
                {
                    uint c = other.Pixels[y * other.Width + x];
                    if (c != 0)
                    {
                        this[left + x, top + y] = c;
                    }
                }
            }
        }

        // The picture turned upside down (half a turn), for a card's bottom corner.
        public Picture Turned()
        {
            var turned = new Picture(Width, Height);
            for (int i = 0; i < Pixels.Length; i++)
            {
                turned.Pixels[Pixels.Length - 1 - i] = Pixels[i];
            }
            return turned;
        }
    }

    // The pixel art for the cards, the table and the empty spots, drawn from letter patterns like the
    // mod icons (Shared/IconBar.cs). The colours are the icon palette's.
    internal static class CardPixels
    {
        public const int Width = 32;
        public const int Height = 44;

        // The icon palette (Shared/IconBar.cs).
        public const uint Outline = 0x2A2433FF;   // K
        public const uint White = 0xF7F3EAFF;     // W
        public const uint LightGrey = 0xC9C6CFFF; // G
        public const uint DarkGrey = 0x57525FFF;  // D
        public const uint Wood = 0xA86B3CFF;      // B
        public const uint LightWood = 0xD9A066FF; // b
        public const uint DarkWood = 0x6E4325FF;  // d
        public const uint Red = 0xE05A5AFF;       // R
        public const uint DarkRed = 0xA83A46FF;   // r
        public const uint Pink = 0xF6A5A5FF;      // p
        public const uint Yellow = 0xF5D061FF;    // Y
        public const uint DarkYellow = 0xC99632FF;// y
        public const uint Blue = 0x4A8FD9FF;      // U
        public const uint DarkBlue = 0x2F5A9EFF;  // u
        public const uint Green = 0x74C069FF;     // N
        public const uint Skin = 0xF6CFAEFF;      // P
        public const uint HotPink = 0xE98BB5FF;   // S
        public const uint Hair = 0x3C3038FF;      // H

        // The table's felt, and the faint marks on it.
        public const uint Felt = 0x3A8248FF;
        public const uint FeltDark = 0x347842FF;
        public const uint FeltMark = 0x60AA6AFF;

        private static uint Palette(char letter)
        {
            switch (letter)
            {
                case 'K': return Outline;
                case 'W': return White;
                case 'G': return LightGrey;
                case 'D': return DarkGrey;
                case 'B': return Wood;
                case 'b': return LightWood;
                case 'd': return DarkWood;
                case 'R': return Red;
                case 'r': return DarkRed;
                case 'p': return Pink;
                case 'Y': return Yellow;
                case 'y': return DarkYellow;
                case 'U': return Blue;
                case 'u': return DarkBlue;
                case 'N': return Green;
                case 'P': return Skin;
                case 'S': return HotPink;
                case 'H': return Hair;
                default: return 0;
            }
        }

        // ---- Patterns ----

        // Ranks, 5 x 7 (1 and 0 are narrower, for "10").
        private static string[] Glyph(char c)
        {
            switch (c)
            {
                case 'A': return new[] { ".XXX.", "X...X", "X...X", "XXXXX", "X...X", "X...X", "X...X" };
                case '2': return new[] { ".XXX.", "X...X", "....X", "...X.", "..X..", ".X...", "XXXXX" };
                case '3': return new[] { "XXXX.", "....X", "....X", ".XXX.", "....X", "....X", "XXXX." };
                case '4': return new[] { "...X.", "..XX.", ".X.X.", "X..X.", "XXXXX", "...X.", "...X." };
                case '5': return new[] { "XXXXX", "X....", "XXXX.", "....X", "....X", "X...X", ".XXX." };
                case '6': return new[] { "..XX.", ".X...", "X....", "XXXX.", "X...X", "X...X", ".XXX." };
                case '7': return new[] { "XXXXX", "....X", "...X.", "..X..", "..X..", ".X...", ".X..." };
                case '8': return new[] { ".XXX.", "X...X", "X...X", ".XXX.", "X...X", "X...X", ".XXX." };
                case '9': return new[] { ".XXX.", "X...X", "X...X", ".XXXX", "....X", "...X.", ".XX.." };
                case '1': return new[] { ".X.", "XX.", ".X.", ".X.", ".X.", ".X.", "XXX" };
                case '0': return new[] { ".XX.", "X..X", "X..X", "X..X", "X..X", "X..X", ".XX." };
                case 'J': return new[] { "..XXX", "...X.", "...X.", "...X.", "X..X.", "X..X.", ".XX.." };
                case 'Q': return new[] { ".XXX.", "X...X", "X...X", "X...X", "X.X.X", "X..X.", ".XX.X" };
                default: return new[] { "X...X", "X..X.", "X.X..", "XX...", "X.X..", "X..X.", "X...X" };   // K
            }
        }

        // Suits for the corners, 7 x 7, by suit number (spades, hearts, clubs, diamonds).
        private static readonly string[][] SmallSuits =
        {
            new[] { "...X...", "..XXX..", ".XXXXX.", "XXXXXXX", "XXXXXXX", "...X...", "..XXX.." },
            new[] { ".XX.XX.", "XXXXXXX", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." },
            new[] { "..XXX..", "..XXX..", "XX.X.XX", "XXXXXXX", "XX.X.XX", "...X...", "..XXX.." },
            new[] { "...X...", "..XXX..", ".XXXXX.", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." },
        };

        // Suits for the middle of the number cards, 14 x 14.
        private static readonly string[][] BigSuits =
        {
            new[]
            {
                "..............",
                "......XX......",
                ".....XXXX.....",
                "....XXXXXX....",
                "...XXXXXXXX...",
                ".XXXXXXXXXXXX.",
                ".XXXXXXXXXXXX.",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                ".XXXXXXXXXXXX.",
                "...XXXXXXXX...",
                ".....XXXX.....",
                ".....XXXX.....",
            },
            new[]
            {
                "...XX....XX...",
                ".XXXXX..XXXXX.",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                ".XXXXXXXXXXXX.",
                "..XXXXXXXXXX..",
                "...XXXXXXXX...",
                "....XXXXXX....",
                "....XXXXXX....",
                ".....XXXX.....",
                "......XX......",
                "..............",
            },
            new[]
            {
                "......XX......",
                ".....XXXX.....",
                "....XXXXXX....",
                "....XXXXXX....",
                ".....XXXX.....",
                ".XXXX.XX.XXXX.",
                "XXXXX.XX.XXXXX",
                "XXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXX",
                ".XXXX.XX.XXXX.",
                "..XX..XX..XX..",
                "......XX......",
                ".....XXXX.....",
                ".....XXXX.....",
            },
            new[]
            {
                "..............",
                "......XX......",
                ".....XXXX.....",
                "....XXXXXX....",
                "....XXXXXX....",
                "...XXXXXXXX...",
                "..XXXXXXXXXX..",
                "..XXXXXXXXXX..",
                "...XXXXXXXX...",
                "....XXXXXX....",
                "....XXXXXX....",
                ".....XXXX.....",
                "......XX......",
                "..............",
            },
        };

        // Suits for the aces, 18 x 18.
        private static readonly string[][] AceSuits =
        {
            new[]
            {
                "..................",
                "........XX........",
                ".......XXXX.......",
                "......XXXXXX......",
                "......XXXXXX......",
                ".....XXXXXXXX.....",
                "...XXXXXXXXXXXX...",
                ".XXXXXXXXXXXXXXXX.",
                ".XXXXXXXXXXXXXXXX.",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                ".XXXXXXXXXXXXXXXX.",
                ".XXXXXXXXXXXXXXXX.",
                "...XXXXXXXXXXXX...",
                ".......XXXX.......",
                "......XXXXXX......",
                ".....XXXXXXXX.....",
            },
            new[]
            {
                "..................",
                "..XXXXXX..XXXXXX..",
                ".XXXXXXXXXXXXXXXX.",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                ".XXXXXXXXXXXXXXXX.",
                "..XXXXXXXXXXXXXX..",
                "...XXXXXXXXXXXX...",
                "....XXXXXXXXXX....",
                ".....XXXXXXXX.....",
                "......XXXXXX......",
                "......XXXXXX......",
                ".......XXXX.......",
                "........XX........",
                "..................",
            },
            new[]
            {
                ".......XXXX.......",
                "......XXXXXX......",
                "......XXXXXX......",
                ".....XXXXXXXX.....",
                ".....XXXXXXXX.....",
                "......XXXXXX......",
                "...XX.XXXXXX.XX...",
                ".XXXXX..XX..XXXXX.",
                "XXXXXXX.XX.XXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXXXXXXXXXXXXX",
                "XXXXXXX.XX.XXXXXXX",
                ".XXXXXX.XX.XXXXXX.",
                "..XXX..XXXX..XXX..",
                ".......XXXX.......",
                ".......XXXX.......",
                ".......XXXX.......",
                "......XXXXXX......",
            },
            new[]
            {
                "..................",
                "........XX........",
                ".......XXXX.......",
                "......XXXXXX......",
                "......XXXXXX......",
                ".....XXXXXXXX.....",
                "....XXXXXXXXXX....",
                "...XXXXXXXXXXXX...",
                "...XXXXXXXXXXXX...",
                "..XXXXXXXXXXXXXX..",
                "...XXXXXXXXXXXX...",
                "....XXXXXXXXXX....",
                ".....XXXXXXXX.....",
                "......XXXXXX......",
                "......XXXXXX......",
                ".......XXXX.......",
                "........XX........",
                "..................",
            },
        };

        // The jack, queen and king: our own little busts. X is the suit's colour, x its dark shade.
        // An outline goes round them by itself.
        private static readonly string[] Jack =
        {
            "...........NN...",
            "....XXXXXXXN....",
            "...XXXXXXXXXN...",
            "..XXXXXXXXXXXX..",
            "..xxxxxxxxxxxx..",
            ".HHPPPPPPPPPPHH.",
            ".HPPPPPPPPPPPPH.",
            ".HPPKPPPPPPKPPH.",
            ".HPPKPPPPPPKPPH.",
            ".HPpPPPPPPPPpPH.",
            "..PPPPPKKPPPPP..",
            "...PPPPPPPPPP...",
            ".....PPPPPP.....",
            "...XXWWWWWWXX...",
            "..XXXXXWWXXXXX..",
            ".XXXXXXXXXXXXXX.",
            "XxXXXXXXXXXXXXxX",
            "XxXXXXXXXXXXXXxX",
        };

        private static readonly string[] Queen =
        {
            "......Y..Y......",
            ".....YYYYYY.....",
            "...BBBBBBBBBB...",
            "..BBBBBBBBBBBB..",
            ".BBPPPPPPPPPPBB.",
            ".BPPPPPPPPPPPPB.",
            ".BPPPPPPPPPPPPB.",
            ".BPPKPPPPPPKPPB.",
            ".BPPKPPPPPPKPPB.",
            ".BPpPPPPPPPPpPB.",
            ".BPPPPPrrPPPPPB.",
            ".BBPPPPPPPPPPBB.",
            ".BBBBPPPPPPBBBB.",
            ".BBBXXPPPPXXBBB.",
            "..XXXXXYYXXXXX..",
            ".XXXXXXXXXXXXXX.",
            "XxXXXXXXXXXXXXxX",
            "XxXXXXXXXXXXXXxX",
        };

        private static readonly string[] King =
        {
            "..Y....YY....Y..",
            "..YY..YYYY..YY..",
            "..YYYYYYYYYYYY..",
            "..YyYyYNNYyYyY..",
            "..HHHHHHHHHHHH..",
            ".HPPPPPPPPPPPPH.",
            ".HPPPPPPPPPPPPH.",
            ".HPPKPPPPPPKPPH.",
            ".HPPKPPPPPPKPPH.",
            ".HPpPPPPPPPPpPH.",
            ".HPGGGGDDGGGGPH.",
            "..GGGGGGGGGGGG..",
            "...GGGGGGGGGG...",
            "..XXGGGGGGGGXX..",
            ".XXXXXGGGGXXXXX.",
            "XXXXXXXYYXXXXXXX",
            "XxXXXXXYYXXXXXxX",
            "XxXXXXXYYXXXXXxX",
        };

        // The heart on the card back's badge.
        private static readonly string[] BackHeart =
        {
            ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X...",
        };

        // The turn-the-deck-over mark on the empty deck spot: a ring with an arrow head.
        private static readonly string[] Recycle =
        {
            "....XXXXX.X.",
            "..XX.....XX.",
            ".X......XXX.",
            ".X..........",
            "X...........",
            "X...........",
            "X..........X",
            "X..........X",
            ".X........X.",
            ".X........X.",
            "..XX....XX..",
            "....XXXX....",
        };

        // ---- Cards ----

        // The card's outline: a rounded corner of one pixel.
        private static bool InCard(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return false;
            }
            int cx = Math.Min(x, Width - 1 - x);
            int cy = Math.Min(y, Height - 1 - y);
            return !((cx == 0 && cy == 0) || (cx == 1 && cy == 0) || (cx == 0 && cy == 1));
        }

        private static bool OnEdge(int x, int y)
        {
            return !InCard(x + 1, y) || !InCard(x - 1, y) || !InCard(x, y + 1) || !InCard(x, y - 1);
        }

        // A blank card: dark outline, cream face, a grey line along the bottom for a little depth.
        private static Picture Blank()
        {
            var card = new Picture(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!InCard(x, y))
                    {
                        continue;
                    }
                    card[x, y] = OnEdge(x, y) ? Outline : !InCard(x, y + 2) ? LightGrey : White;
                }
            }
            return card;
        }

        private static uint Ink(int suit) => Cards.IsRedSuit(suit) ? Red : Outline;

        // The rank and suit side by side, as in the top-left corner.
        private static Picture Corner(int rank, int suit)
        {
            string[] glyph;
            if (rank == 10)
            {
                string[] one = Glyph('1');
                string[] zero = Glyph('0');
                glyph = new string[7];
                for (int y = 0; y < 7; y++)
                {
                    glyph[y] = one[y] + "." + zero[y];
                }
            }
            else
            {
                glyph = Glyph(Cards.RankText(rank)[0]);
            }
            int width = glyph[0].Length;
            var corner = new Picture(width + 2 + 7, 7);
            uint ink = Ink(suit);
            corner.Stamp(glyph, 0, 0, c => ink);
            corner.Stamp(SmallSuits[suit], width + 2, 0, c => ink);
            return corner;
        }

        // A pattern with a dark outline drawn round it.
        private static Picture Outlined(string[] rows, Func<char, uint> colour)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var picture = new Picture(width + 2, height + 2);
            picture.Stamp(rows, 1, 1, colour);
            var solid = (uint[])picture.Pixels.Clone();
            for (int y = 0; y < picture.Height; y++)
            {
                for (int x = 0; x < picture.Width; x++)
                {
                    if (solid[y * picture.Width + x] != 0)
                    {
                        continue;
                    }
                    bool next =
                        (x + 1 < picture.Width && solid[y * picture.Width + x + 1] != 0) ||
                        (x > 0 && solid[y * picture.Width + x - 1] != 0) ||
                        (y + 1 < picture.Height && solid[(y + 1) * picture.Width + x] != 0) ||
                        (y > 0 && solid[(y - 1) * picture.Width + x] != 0);
                    if (next)
                    {
                        picture[x, y] = Outline;
                    }
                }
            }
            return picture;
        }

        public static Picture Face(int card)
        {
            int rank = Cards.Rank(card);
            int suit = Cards.Suit(card);
            Picture face = Blank();
            Picture corner = Corner(rank, suit);
            face.Paste(corner, 3, 3);
            face.Paste(corner.Turned(), Width - 3 - corner.Width, Height - 3 - corner.Height);

            if (rank >= 11)
            {
                uint main = Cards.IsRedSuit(suit) ? Red : Blue;
                uint dark = Cards.IsRedSuit(suit) ? DarkRed : DarkBlue;
                string[] rows = rank == 11 ? Jack : rank == 12 ? Queen : King;
                Picture bust = Outlined(rows, c => c == 'X' ? main : c == 'x' ? dark : Palette(c));
                face.Paste(bust, (Width - bust.Width) / 2, 12);
            }
            else
            {
                string[] rows = rank == 1 ? AceSuits[suit] : BigSuits[suit];
                uint ink = Ink(suit);
                face.Stamp(rows, (Width - rows[0].Length) / 2, (Height - rows.Length) / 2, c => ink);
            }
            return face;
        }

        // The back: a blue lattice inside a cream border, with a pink heart on a cream badge.
        public static Picture Back()
        {
            Picture back = Blank();
            for (int y = 3; y < Height - 3; y++)
            {
                for (int x = 3; x < Width - 3; x++)
                {
                    bool lattice = (x + y) % 6 == 0 || (x - y + 60) % 6 == 0;
                    back[x, y] = lattice ? Blue : DarkBlue;
                }
            }
            int cx = Width / 2;
            int cy = Height / 2;
            for (int y = cy - 6; y < cy + 6; y++)
            {
                for (int x = cx - 6; x < cx + 6; x++)
                {
                    double dx = x - cx + 0.5;
                    double dy = y - cy + 0.5;
                    double d = dx * dx + dy * dy;
                    if (d <= 30)
                    {
                        back[x, y] = White;
                    }
                    else if (d <= 42)
                    {
                        back[x, y] = Outline;
                    }
                }
            }
            Picture heart = Outlined(BackHeart, c => HotPink);
            back.Paste(heart, cx - heart.Width / 2, cy - heart.Height / 2);
            return back;
        }

        public enum SlotMark
        {
            None,
            Ace,       // a foundation: an A
            Recycle,   // the empty deck, when the waste can be turned back over
        }

        // An empty spot on the table: a faint card outline, maybe with a mark in it.
        public static Picture Slot(SlotMark mark)
        {
            var slot = new Picture(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (InCard(x, y) && OnEdge(x, y))
                    {
                        slot[x, y] = FeltMark;
                    }
                }
            }
            if (mark == SlotMark.Ace)
            {
                // The A at double size.
                string[] a = Glyph('A');
                int left = (Width - 10) / 2;
                int top = (Height - 14) / 2;
                for (int y = 0; y < 14; y++)
                {
                    for (int x = 0; x < 10; x++)
                    {
                        if (a[y / 2][x / 2] == 'X')
                        {
                            slot[left + x, top + y] = FeltMark;
                        }
                    }
                }
            }
            else if (mark == SlotMark.Recycle)
            {
                slot.Stamp(Recycle, (Width - Recycle[0].Length) / 2, (Height - Recycle.Length) / 2, c => FeltMark);
            }
            return slot;
        }

        // A yellow outline that goes over the card or spot a dragged card would land on.
        public static Picture Glow()
        {
            var glow = new Picture(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (InCard(x, y) && OnEdge(x, y))
                    {
                        glow[x, y] = Yellow;
                    }
                }
            }
            return glow;
        }

        // A corner suit on its own, for the confetti when you win.
        public static Picture Suit(int suit)
        {
            var picture = new Picture(7, 7);
            picture.Stamp(SmallSuits[suit], 0, 0, c => Cards.IsRedSuit(suit) ? Red : Outline);
            return picture;
        }

        // The table: green felt with a few darker specks, in a wooden rim with a dark outline,
        // a light top and left edge and a dark inner edge.
        public static Picture Table(int width, int height, int rim)
        {
            var table = new Picture(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int ex = Math.Min(x, width - 1 - x);
                    int ey = Math.Min(y, height - 1 - y);
                    if ((ex == 0 && ey <= 1) || (ex == 1 && ey == 0))
                    {
                        continue;   // rounded corner
                    }
                    int edge = Math.Min(ex, ey);
                    uint c;
                    if (edge == 0 || (ex == 1 && ey == 1))
                    {
                        c = Outline;
                    }
                    else if (edge == 1)
                    {
                        c = y == 1 || x == 1 ? LightWood : Wood;
                    }
                    else if (edge < rim - 1)
                    {
                        c = Wood;
                    }
                    else if (edge == rim - 1)
                    {
                        c = DarkWood;
                    }
                    else
                    {
                        c = (x * 7 + y * 13) % 11 == 0 ? FeltDark : Felt;
                    }
                    table[x, y] = c;
                }
            }
            return table;
        }
    }
}
