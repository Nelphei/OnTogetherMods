namespace SoapBox
{
    // The window's title strip while playing, drawn in the console's pixels above the handheld (same
    // width): the cart's name (click it for the cart list) and a hint on the left, buttons on the
    // right. Uses the frame's palette (Handheld.Colours), so the shell button shows the shell's colour.
    internal static class Header
    {
        public const int Height = 13;
        private const int ButtonSize = 11;
        private const int TitleX = 13;

        public enum Button
        {
            None,
            Close,
            Size,
            Help,
            Shell,
            Edit,
            Title,
        }

        // Right to left.
        private static readonly Button[] Buttons = { Button.Close, Button.Size, Button.Help, Button.Shell, Button.Edit };

        // Where the cart's name ends, as last drawn.
        private static int _titleEnd = 60;

        // The left edge of a button, in strip pixels.
        private static int ButtonX(int width, int index)
        {
            return width - 2 - (index + 1) * (ButtonSize + 1);
        }

        public static Button HitTest(int width, int x, int y)
        {
            if (y < 1 || y >= 1 + ButtonSize)
            {
                return Button.None;
            }
            if (x >= 2 && x < _titleEnd + 3)
            {
                return Button.Title;
            }
            for (int i = 0; i < Buttons.Length; i++)
            {
                int bx = ButtonX(width, i);
                if (x >= bx && x < bx + ButtonSize)
                {
                    return Buttons[i];
                }
            }
            return Button.None;
        }

        public static void Draw(Surface s, string title, string hint, string sizeLabel, Button hover)
        {
            s.ResetState();
            s.Clear(Handheld.Clear);
            s.RoundRectFill(0, 0, s.Width, Height, 4, Palette.Ink);
            if (title.Length > 22)
            {
                title = title.Substring(0, 21) + "…";
            }
            _titleEnd = TitleX + PixelFont.Width(title) + 7;
            if (hover == Button.Title)
            {
                s.RoundRectFill(2, 1, _titleEnd + 1, ButtonSize, 3, Palette.DarkGrey);
            }
            s.Print("○", 5, 3, Palette.LightBlue);
            int x = s.Print(title, TitleX, 3, Palette.White);
            // A small down arrow: the name opens the cart list.
            s.RectFill(x + 1, 6, 5, 1, Palette.Grey);
            s.RectFill(x + 2, 7, 3, 1, Palette.Grey);
            s.Pset(x + 3, 8, Palette.Grey);

            // The hint fits between the title and the buttons, or is left out.
            int start = _titleEnd + 6;
            int room = ButtonX(s.Width, Buttons.Length - 1) - 4 - start;
            if (!string.IsNullOrEmpty(hint) && PixelFont.Width(hint) <= room)
            {
                s.Print(hint, start, 3, Palette.Grey);
            }

            for (int i = 0; i < Buttons.Length; i++)
            {
                Button b = Buttons[i];
                int bx = ButtonX(s.Width, i);
                bool lit = b == hover;
                if (lit)
                {
                    s.RoundRectFill(bx, 1, ButtonSize, ButtonSize, 3, Palette.DarkGrey);
                }
                int c = lit ? Palette.White : Palette.Grey;
                switch (b)
                {
                    case Button.Close:
                        s.Line(bx + 3, 3, bx + 7, 7, c);
                        s.Line(bx + 7, 3, bx + 3, 7, c);
                        break;
                    case Button.Size:
                        s.Print(sizeLabel, bx + 3, 3, c);
                        break;
                    case Button.Help:
                        s.Print("?", bx + 3, 3, c);
                        break;
                    case Button.Shell:
                        s.CircFill(bx + 5, 6, 3, Handheld.Deep);
                        s.CircFill(bx + 5, 6, 2, Handheld.Body);
                        s.Pset(bx + 4, 5, Handheld.Light);
                        break;
                    case Button.Edit:
                        DrawPencil(s, bx + 2, 2, lit ? Palette.White : Palette.Pink);
                        break;
                }
            }
        }

        private static readonly string[] Pencil =
        {
            ".....##",
            "....###",
            "...###.",
            "..###..",
            ".###...",
            "#.#....",
            "##.....",
        };

        private static void DrawPencil(Surface s, int x, int y, int colour)
        {
            for (int j = 0; j < Pencil.Length; j++)
            {
                for (int i = 0; i < Pencil[j].Length; i++)
                {
                    if (Pencil[j][i] == '#')
                    {
                        s.Pset(x + i, y + j, colour);
                    }
                }
            }
        }
    }
}
