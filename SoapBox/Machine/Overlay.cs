using System;
using System.Collections.Generic;

namespace SoapBox
{
    // What the console draws over a cart's picture: "click to play", the pause menu, the controls
    // card and the error screen. Drawn onto a copy of the picture, so the cart's own screen is never
    // touched.
    internal static class Overlay
    {
        public const int Width = Runner.Width;
        public const int Height = Runner.Height;

        public static readonly string[] MenuItems = { "continue", "restart", "edit code", "carts", "close" };

        // Each colour's darker stand-in, for the dimmed picture behind a box.
        private static readonly byte[] DimColours = { 0, 0, 1, 1, 0, 4, 4, 4, 1, 1, 11, 0, 11, 11, 0, 14 };

        public static void Dim(Surface s)
        {
            byte[] pixels = s.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = DimColours[pixels[i] & 15];
            }
        }

        public static void ClickToPlay(Surface s)
        {
            Dim(s);
            Box(s, 24, 48, 144, 30);
            Centre(s, "click to play", 54, Palette.White);
            Centre(s, "the cart gets the keys", 65, Palette.Grey);
        }

        public static void Menu(Surface s, int selected)
        {
            Dim(s);
            int h = 22 + MenuItems.Length * 11;
            int y = (Height - h) / 2;
            Box(s, 56, y, 80, h);
            Centre(s, "paused", y + 5, Palette.Pink);
            for (int i = 0; i < MenuItems.Length; i++)
            {
                int iy = y + 19 + i * 11;
                bool on = i == selected;
                if (on)
                {
                    s.RoundRectFill(62, iy - 2, 68, 11, 3, Palette.DarkGrey);
                    s.Print("●", 66, iy, Palette.Pink);
                }
                s.Print(MenuItems[i], 76, iy, on ? Palette.White : Palette.Grey);
            }
        }

        // Which menu item a screen pixel is on, or -1.
        public static int MenuItemAt(int x, int y)
        {
            int h = 22 + MenuItems.Length * 11;
            int top = (Height - h) / 2 + 19 - 2;
            if (x < 62 || x >= 130 || y < top)
            {
                return -1;
            }
            int i = (y - top) / 11;
            return i < MenuItems.Length ? i : -1;
        }

        public static void Help(Surface s)
        {
            Dim(s);
            Box(s, 8, 6, 176, 116);
            Centre(s, "soap box controls", 11, Palette.Pink);
            string[,] rows =
            {
                { "←↑→↓ or wasd", "move" },
                { "z or space", "a button" },
                { "x", "b button" },
                { "esc or enter", "menu" },
                { "mouse", "point, click" },
                { "drawn buttons", "click them" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                int y = 27 + i * 11;
                s.Print(rows[i, 0], 16, y, Palette.Yellow);
                s.Print(rows[i, 1], 104, y, Palette.White);
            }
            s.Print("click outside the window", 16, 96, Palette.Grey);
            s.Print("to give the keys back.", 16, 106, Palette.Grey);
        }

        public static void Error(Surface s, string message, int line)
        {
            Dim(s);
            Box(s, 8, 14, 176, 100);
            Centre(s, "oops! the cart stopped", 20, Palette.Pink);
            int y = 36;
            if (line > 0)
            {
                s.Print("line " + line + ":", 16, y, Palette.Yellow);
                y += 11;
            }
            foreach (string part in Wrap(message ?? "", 27))
            {
                if (y > 88)
                {
                    break;
                }
                s.Print(part, 16, y, Palette.White);
                y += 10;
            }
            Centre(s, "z: start again  x: fix it", 100, Palette.LightBlue);
        }

        // A small note in the corner while the cart is slow to finish a frame.
        public static void Busy(Surface s)
        {
            s.RoundRectFill(Width - 38, Height - 13, 35, 11, 3, Palette.Ink);
            s.Print("busy…", Width - 35, Height - 11, Palette.Yellow);
        }

        public static void Box(Surface s, int x, int y, int w, int h)
        {
            s.RoundRectFill(x, y, w, h, 5, Palette.Ink);
            s.RoundRect(x, y, w, h, 5, Palette.DarkGrey);
        }

        public static void Centre(Surface s, string text, int y, int colour)
        {
            s.Print(text, (Width - PixelFont.Width(text)) / 2, y, colour);
        }

        // Splits text into lines of at most max letters, at spaces where it can.
        public static List<string> Wrap(string text, int max)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                string rest = paragraph;
                while (rest.Length > max)
                {
                    int cut = rest.LastIndexOf(' ', max);
                    if (cut <= 0)
                    {
                        cut = max;
                    }
                    lines.Add(rest.Substring(0, cut));
                    rest = rest.Substring(cut).TrimStart();
                }
                lines.Add(rest);
            }
            return lines;
        }
    }
}
