using System;

namespace SoapBox
{
    // The pastel handheld drawn around the game screen while a cart plays: rounded body, D-pad on
    // the left, A and B on the right (they light up while held), a menu button, a power light,
    // a speaker grille, a few bubbles and "soap box" under the screen. Drawn in the same chunky
    // pixels as the game. Colour numbers 0-15 are the console palette (so the game screen can be
    // copied straight in), 16 and up are the frame's own (Colours), 255 is see-through.
    internal static class Handheld
    {
        public const int Width = 260;
        public const int Height = 158;

        // Where the 192 x 128 game screen goes.
        public const int ScreenX = 34;
        public const int ScreenY = 10;

        public const byte Clear = 255;

        public enum Shell
        {
            Pink,
            Aqua,
            Lilac,
            Cream,
        }

        [Flags]
        public enum Buttons
        {
            None = 0,
            Left = 1,
            Right = 2,
            Up = 4,
            Down = 8,
            A = 16,
            B = 32,
            Menu = 64,
        }

        // The frame's own colour numbers (the header strip uses the shell ones too).
        public const byte Light = 16, Body = 17, Shade = 18, Deep = 19;
        private const byte Bezel = 20, BezelEdge = 21, Pad = 22, PadLight = 23, Lit = 24, LedOn = 25, LedGlow = 26, PadDeep = 27;

        // Centres of the controls, in frame pixels.
        public const int PadX = 15, PadY = 78;
        public const int AX = 249, AY = 70;
        public const int BX = 238, BY = 84;
        public const int MenuX = 239, MenuY = 102;
        public const int ButtonRadius = 5;

        // Light, body, shade, deep (outline and lettering), button glow.
        private static readonly uint[][] Shells =
        {
            new uint[] { 0xFFDEEC, 0xF7B8D4, 0xDE8CB6, 0x964E78, 0xFFF3F9 }, // pink
            new uint[] { 0xD6FAF4, 0x8FE3D9, 0x60B8B2, 0x2E7072, 0xF0FFFC }, // aqua (Bubble Shell's teal)
            new uint[] { 0xEAE0FC, 0xCAB4F2, 0xA488DE, 0x62489C, 0xF8F3FF }, // lilac
            new uint[] { 0xFFFDF6, 0xF1EBDE, 0xD2C8BA, 0x786C74, 0xFFFFFF }, // cream
        };

        // The frame's palette for a shell colour, 0xAARRGGBB.
        public static uint[] Colours(Shell shell)
        {
            uint[] s = Shells[(int)shell];
            var colours = new uint[256];
            for (int i = 0; i < Palette.Count; i++)
            {
                colours[i] = 0xFF000000 | Palette.Rgb[i];
            }
            colours[Light] = 0xFF000000 | s[0];
            colours[Body] = 0xFF000000 | s[1];
            colours[Shade] = 0xFF000000 | s[2];
            colours[Deep] = 0xFF000000 | s[3];
            colours[Lit] = 0xFF000000 | s[4];
            colours[Bezel] = 0xFF2C2536;
            colours[BezelEdge] = 0xFF453B52;
            colours[Pad] = 0xFF4A3D58;
            colours[PadLight] = 0xFF76658C;
            colours[PadDeep] = 0xFF2E2538;
            colours[LedOn] = 0xFFFF6A96;
            colours[LedGlow] = 0xFFFFB0CB;
            colours[Clear] = 0x00000000;
            return colours;
        }

        // The D-pad, outline included: o outline, p pad, h highlight, a arrow, s shade. Each arm's
        // pixels light up while its direction is held, and its arrow shows then.
        private static readonly string[] DPad =
        {
            "......ooooooo......",
            "......ohhhhho......",
            "......ohpapso......",
            "......ohaaaso......",
            "......ohpppso......",
            "......ohpppso......",
            "ooooooohpppsooooooo",
            "ohhhhhhppppphhhhhho",
            "ohpapppppppppppapso",
            "ohaapppppppppppaaso",
            "ohpapppppppppppapso",
            "osssssspppppsssssso",
            "ooooooohpppsooooooo",
            "......ohpppso......",
            "......ohpppso......",
            "......ohaaaso......",
            "......ohpapso......",
            "......ossssso......",
            "......ooooooo......",
        };

        public static void Draw(Surface s, Buttons held, bool power)
        {
            s.ResetState();
            s.Clear(Clear);

            // Body: outline, a 2-pixel lip of shade at the bottom, a light rim on the top and left.
            const int r = 14;
            s.RoundRectFill(0, 0, Width, Height, r, Deep);
            s.RoundRectFill(1, 1, Width - 2, Height - 2, r - 1, Shade);
            s.RoundRectFill(1, 1, Width - 2, Height - 4, r - 1, Light);
            s.RoundRectFill(2, 2, Width - 3, Height - 5, r - 2, Body);

            // Screen surround.
            s.RoundRectFill(ScreenX - 5, ScreenY - 5, 192 + 10, 128 + 10, 4, Bezel);
            s.RoundRect(ScreenX - 5, ScreenY - 5, 192 + 10, 128 + 10, 4, BezelEdge);
            s.Rect(ScreenX - 1, ScreenY - 1, 192 + 2, 128 + 2, PadDeep);

            // Power light, with a soft glow while a cart runs.
            int ledX = 13, ledY = 24;
            if (power)
            {
                s.RectFill(ledX - 1, ledY, 5, 3, LedGlow);
                s.RectFill(ledX, ledY - 1, 3, 5, LedGlow);
                s.RectFill(ledX, ledY, 3, 3, LedOn);
            }
            else
            {
                s.RectFill(ledX, ledY, 3, 3, Shade);
            }

            // D-pad, A and B, each with a soft shadow down and to the right.
            DrawPad(s, held);
            s.CircFill(BX + 1, BY + 1, ButtonRadius, Shade);
            s.CircFill(AX + 1, AY + 1, ButtonRadius, Shade);
            DrawButton(s, BX, BY, "B", (held & Buttons.B) != 0);
            DrawButton(s, AX, AY, "A", (held & Buttons.A) != 0);

            // Menu button: a small pill.
            bool menu = (held & Buttons.Menu) != 0;
            s.RoundRectFill(MenuX - 6, MenuY - 2, 13, 5, 2, Deep);
            s.RoundRectFill(MenuX - 5, MenuY - 1, 11, 3, 1, menu ? Lit : Pad);

            // Speaker grille.
            for (int i = 0; i < 4; i++)
            {
                int x = 236 + i * 5;
                s.Line(x, 132, x + 5, 127, Shade);
                s.Line(x + 1, 132, x + 5, 128, Deep);
            }

            // Bubbles drifting up the left side.
            DrawBubble(s, 9, 128, 3);
            DrawBubble(s, 19, 118, 2);
            DrawBubble(s, 11, 109, 1);

            // Name under the screen.
            const string name = "soap box";
            int nameX = ScreenX + (192 - PixelFont.Width(name)) / 2;
            int nameY = ScreenY + 128 + 7;
            s.Print(name, nameX, nameY + 1, Light);
            s.Print(name, nameX, nameY, Deep);
        }

        // The drawn button under a frame pixel (for clicking them with the mouse), or None.
        public static Buttons ButtonAt(int x, int y)
        {
            int px = x - (PadX - 9), py = y - (PadY - 9);
            if (py >= 0 && py < DPad.Length && px >= 0 && px < DPad[py].Length && DPad[py][px] != '.')
            {
                if (py < 6)
                {
                    return Buttons.Up;
                }
                if (py > 12)
                {
                    return Buttons.Down;
                }
                if (px < 6)
                {
                    return Buttons.Left;
                }
                if (px > 12)
                {
                    return Buttons.Right;
                }
                return Buttons.None;
            }
            int r = (ButtonRadius + 1) * (ButtonRadius + 1);
            if ((x - AX) * (x - AX) + (y - AY) * (y - AY) <= r)
            {
                return Buttons.A;
            }
            if ((x - BX) * (x - BX) + (y - BY) * (y - BY) <= r)
            {
                return Buttons.B;
            }
            if (x >= MenuX - 7 && x <= MenuX + 7 && y >= MenuY - 3 && y <= MenuY + 3)
            {
                return Buttons.Menu;
            }
            return Buttons.None;
        }

        // True when a frame pixel is on the game screen.
        public static bool OnScreen(int x, int y)
        {
            return x >= ScreenX && y >= ScreenY && x < ScreenX + 192 && y < ScreenY + 128;
        }

        private static void DrawPad(Surface s, Buttons held)
        {
            int ox = PadX - 9, oy = PadY - 9;
            // Shadow: the outline's shape, one pixel down and right.
            for (int y = 0; y < DPad.Length; y++)
            {
                for (int x = 0; x < DPad[y].Length; x++)
                {
                    if (DPad[y][x] != '.')
                    {
                        s.Pset(ox + x + 1, oy + y + 1, Shade);
                    }
                }
            }
            for (int y = 0; y < DPad.Length; y++)
            {
                string row = DPad[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == '.')
                    {
                        continue;
                    }
                    bool lit = IsHeld(held, x, y);
                    byte colour;
                    switch (c)
                    {
                        case 'o':
                            colour = Deep;
                            break;
                        case 'h':
                            colour = lit ? Lit : PadLight;
                            break;
                        case 'a':
                            colour = Pad; // an arrow only shows on a held (lit) arm
                            break;
                        case 's':
                            colour = lit ? Light : PadDeep;
                            break;
                        default:
                            colour = lit ? Lit : Pad;
                            break;
                    }
                    s.Pset(ox + x, oy + y, colour);
                }
            }
        }

        // Which arm a D-pad pixel belongs to.
        private static bool IsHeld(Buttons held, int x, int y)
        {
            if (y < 6)
            {
                return (held & Buttons.Up) != 0;
            }
            if (y > 12)
            {
                return (held & Buttons.Down) != 0;
            }
            if (x < 6)
            {
                return (held & Buttons.Left) != 0;
            }
            if (x > 12)
            {
                return (held & Buttons.Right) != 0;
            }
            return false;
        }

        private static void DrawButton(Surface s, int cx, int cy, string letter, bool lit)
        {
            s.CircFill(cx, cy, ButtonRadius, Deep);
            s.CircFill(cx, cy, ButtonRadius - 1, lit ? Lit : Pad);
            if (!lit)
            {
                // A highlight on the top-left of the button.
                s.Pset(cx - 2, cy - 3, PadLight);
                s.Pset(cx - 3, cy - 2, PadLight);
                s.Pset(cx - 1, cy - 3, PadLight);
            }
            s.Print(letter, cx - 2, cy - 3, lit ? Deep : Light);
        }

        private static void DrawBubble(Surface s, int cx, int cy, int radius)
        {
            s.Circ(cx, cy, radius, Light);
            s.Pset(cx - radius / 2, cy - radius / 2, Lit);
            if (radius >= 2)
            {
                s.Pset(cx + radius / 2 + (radius > 2 ? 1 : 0), cy + radius / 2, Shade);
            }
        }
    }
}
