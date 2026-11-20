using System;
using System.Collections.Generic;

namespace SoapBox
{
    // The editor's screen: a top bar (editor tabs, the cart's name, run, help, size, close), the
    // code, and a status bar. Drawn at twice the game screen's detail (384 x 256 for 192 x 128), so
    // code is easy to read. The top bar is also the window's title row while editing.
    internal sealed class EditView
    {
        public const int Width = 384;
        public const int Height = 256;
        public const int BarHeight = 14;
        public const int StatusHeight = 11;
        public const int CodeTop = BarHeight;
        public const int CodeHeight = Height - BarHeight - StatusHeight;

        public enum Button
        {
            None,
            CodeTab,
            SpritesTab,
            MapTab,
            SoundsTab,
            Cart,
            Run,
            Help,
            Size,
            Close,
        }

        public readonly Surface Canvas = new Surface(Width, Height);
        public readonly CodeEditor Code = new CodeEditor();

        // The API sheet (the ? button), over the code.
        public bool ShowHelp;

        // A note in the status bar (saved, an error...) until a time.
        private string _message;
        private bool _messageError;
        private double _messageUntil;

        // The buttons' boxes as last drawn: x, y, width, height.
        private readonly Dictionary<Button, int[]> _boxes = new Dictionary<Button, int[]>();

        // Goes up with every new status note, so the window knows to redraw.
        public int Version;

        public void Say(string message, bool error, double now, double seconds = 4.0)
        {
            _message = message;
            _messageError = error;
            _messageUntil = now + seconds;
            Version++;
        }

        public bool Saying(double now) => _message != null && now < _messageUntil;

        public Button HitTest(int x, int y)
        {
            foreach (KeyValuePair<Button, int[]> pair in _boxes)
            {
                int[] b = pair.Value;
                if (x >= b[0] && y >= b[1] && x < b[0] + b[2] && y < b[1] + b[3])
                {
                    return pair.Key;
                }
            }
            return Button.None;
        }

        public void Draw(string cartName, bool readOnly, string sizeLabel, Button hover, bool caretOn, double now,
            Shelf shelf, string currentPath)
        {
            Surface s = Canvas;
            s.ResetState();
            _boxes.Clear();

            // ---- Top bar ----
            s.RectFill(0, 0, Width, BarHeight, Palette.DarkGrey);
            Tab(s, Button.CodeTab, 3, true, hover);
            Tab(s, Button.SpritesTab, 21, false, hover);
            Tab(s, Button.MapTab, 39, false, hover);
            Tab(s, Button.SoundsTab, 57, false, hover);

            string name = string.IsNullOrEmpty(cartName) ? "no cart" : cartName;
            if (name.Length > 26)
            {
                name = name.Substring(0, 25) + "…";
            }
            int nameW = PixelFont.Width(name) + 14;
            Box(Button.Cart, 77, 2, nameW, 11);
            if (hover == Button.Cart || shelf != null)
            {
                s.RoundRectFill(77, 2, nameW, 11, 3, Palette.Ink);
            }
            s.Print(name, 81, 4, Palette.White);
            DrawDownArrow(s, 81 + PixelFont.Width(name) + 4, 7, Palette.Grey);
            if (readOnly)
            {
                s.Print("(comes with soap box)", 77 + nameW + 4, 4, Palette.Grey);
            }

            int x = Width - 12;
            SmallButton(s, Button.Close, x, hover);
            s.Line(x + 3, 4, x + 7, 8, hover == Button.Close ? Palette.White : Palette.Grey);
            s.Line(x + 7, 4, x + 3, 8, hover == Button.Close ? Palette.White : Palette.Grey);
            x -= 12;
            SmallButton(s, Button.Size, x, hover);
            s.Print(sizeLabel, x + 3, 4, hover == Button.Size ? Palette.White : Palette.Grey);
            x -= 12;
            SmallButton(s, Button.Help, x, hover);
            s.Print("?", x + 3, 4, hover == Button.Help || ShowHelp ? Palette.White : Palette.Grey);
            x -= 32;
            Box(Button.Run, x, 2, 29, 11);
            s.RoundRectFill(x, 2, 29, 11, 3, hover == Button.Run ? Palette.Green : Palette.DarkGreen);
            DrawPlay(s, x + 5, 4, Palette.White);
            s.Print("run", x + 12, 4, Palette.White);

            // ---- Code ----
            Code.Draw(s, 0, CodeTop, Width, CodeHeight, caretOn && !ShowHelp && shelf == null);

            // ---- Status bar ----
            int sy = Height - StatusHeight;
            s.RectFill(0, sy, Width, StatusHeight, Palette.DarkGrey);
            string hint = "ctrl+r: run   esc: back to the game";
            if (Saying(now))
            {
                string message = _message.Length > 58 ? _message.Substring(0, 57) + "…" : _message;
                s.Print(message, 4, sy + 2, _messageError ? Palette.Pink : Palette.Yellow);
            }
            else
            {
                s.Print("line " + (Code.Line + 1) + "/" + Code.LineCount, 4, sy + 2, Palette.Grey);
                s.Print(hint, Width - 4 - PixelFont.Width(hint), sy + 2, Palette.Grey);
            }

            if (ShowHelp)
            {
                DrawHelp(s);
            }
            if (shelf != null)
            {
                int listHeight = Math.Min(CodeHeight + 1, 23 + shelf.Items.Count * 11);
                shelf.Draw(s, 77, BarHeight - 1, 190, shelf.Renaming ? 52 : listHeight, currentPath, caretOn);
            }
        }

        private void Box(Button button, int x, int y, int w, int h)
        {
            _boxes[button] = new[] { x, y, w, h };
        }

        private void SmallButton(Surface s, Button button, int x, Button hover)
        {
            Box(button, x, 2, 11, 11);
            if (hover == button)
            {
                s.RoundRectFill(x, 2, 11, 11, 3, Palette.Ink);
            }
        }

        // The editor tabs. Only the code tab works so far; the others come in later steps.
        private void Tab(Surface s, Button button, int x, bool open, Button hover)
        {
            Box(button, x, 2, 15, 11);
            if (open)
            {
                s.RoundRectFill(x, 2, 15, 11, 3, Palette.Pink);
            }
            else if (hover == button)
            {
                s.RoundRectFill(x, 2, 15, 11, 3, Palette.Ink);
            }
            int c = open ? Palette.Ink : Palette.Grey;
            switch (button)
            {
                case Button.CodeTab:
                    s.Print("<>", x + 2, 4, c);
                    break;
                case Button.SpritesTab:
                    Art(s, Brush, x + 4, 4, c);
                    break;
                case Button.MapTab:
                    Art(s, Grid, x + 4, 4, c);
                    break;
                case Button.SoundsTab:
                    s.Print("♪", x + 5, 4, c);
                    break;
            }
        }

        private static readonly string[] Brush =
        {
            ".....##",
            "....###",
            "...###.",
            "..###..",
            ".###...",
            "##.....",
            "#......",
        };

        private static readonly string[] Grid =
        {
            "#######",
            "#..#..#",
            "#..#..#",
            "#######",
            "#..#..#",
            "#..#..#",
            "#######",
        };

        private static void Art(Surface s, string[] art, int x, int y, int colour)
        {
            for (int j = 0; j < art.Length; j++)
            {
                for (int i = 0; i < art[j].Length; i++)
                {
                    if (art[j][i] == '#')
                    {
                        s.Pset(x + i, y + j, colour);
                    }
                }
            }
        }

        private static void DrawPlay(Surface s, int x, int y, int colour)
        {
            for (int j = 0; j < 7; j++)
            {
                int w = j < 4 ? j + 1 : 7 - j;
                s.RectFill(x, y + j, w, 1, colour);
            }
        }

        private static void DrawDownArrow(Surface s, int x, int y, int colour)
        {
            s.RectFill(x, y - 1, 5, 1, colour);
            s.RectFill(x + 1, y, 3, 1, colour);
            s.Pset(x + 2, y + 1, colour);
        }

        // ---- The API sheet ----

        private static readonly string[] HelpLines =
        {
            "draw    cls(c) pset(x,y,c) pget(x,y) line(x0,y0,x1,y1,c)",
            "        rect(x,y,w,h,c) rectf(x,y,w,h,c) color(c)",
            "        rrect(x,y,w,h,r,c) rrectf(x,y,w,h,r,c)",
            "        circ(x,y,r,c) circf(x,y,r,c) print(s,x,y,c,scale)",
            "        camera(x,y) clip(x,y,w,h) pal(c,new) palt(c,on)",
            "sprites spr(n,x,y,w,h,flipx,flipy) map(cx,cy,x,y,w,h)",
            "        sspr(sx,sy,sw,sh,x,y,w,h) mget(x,y) mset(x,y,n)",
            "        sget(x,y) sset(x,y,c) fget(n,f) fset(n,f,on)",
            "input   btn(b) btnp(b), b = \"left\" \"right\" \"up\" \"down\"",
            "        \"a\" (z, space) \"b\" (x)   mouse() mousep()",
            "maths   rnd(n) flr ceil abs sgn min max mid sqrt",
            "        sin cos atan2(y,x) srand(n) time() log(...)",
            "yours   _init() once, _update() 60 times a second,",
            "        _draw() after it.  the screen is 192 x 128.",
        };

        private static void DrawHelp(Surface s)
        {
            int x = 2, y = CodeTop + 2, w = Width - 4, h = CodeHeight - 4;
            Overlay.Box(s, x, y, w, h);
            s.Print("soap box functions", x + 8, y + 6, Palette.Pink);
            string close = "any key or click: close";
            s.Print(close, x + w - 8 - PixelFont.Width(close), y + 6, Palette.Grey);
            int ly = y + 20;
            foreach (string line in HelpLines)
            {
                // The label in yellow, the functions in white.
                string label = line.Substring(0, 8).TrimEnd();
                s.Print(label, x + 8, ly, Palette.Yellow);
                s.Print(line.Substring(8), x + 8 + 8 * PixelFont.Advance, ly, Palette.White);
                ly += PixelFont.LineHeight + 1;
            }
            s.Print("colours", x + 8, ly + 3, Palette.Yellow);
            for (int c = 0; c < 16; c++)
            {
                int cx = x + 8 + 8 * PixelFont.Advance + c * 18;
                s.RectFill(cx, ly, 16, 14, c);
                s.Rect(cx, ly, 16, 14, Palette.DarkGrey);
                string number = c.ToString();
                int text = c == 0 || c == 1 || c == 4 || c == 10 || c == 11 || c == 14 ? Palette.White : Palette.Ink;
                s.Print(number, cx + (16 - PixelFont.Width(number)) / 2, ly + 4, text);
            }
        }
    }
}
