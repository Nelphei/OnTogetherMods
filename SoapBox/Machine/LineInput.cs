using System;

namespace SoapBox
{
    // A one-line text box (renaming a cart): typing, backspace, delete, arrows, home, end, paste.
    internal sealed class LineInput
    {
        public string Text = "";
        public int Caret;
        public int Max = 40;
        public int Version;

        public void Set(string text)
        {
            Text = (text ?? "").Replace("\r", " ").Replace("\n", " ");
            if (Text.Length > Max)
            {
                Text = Text.Substring(0, Max);
            }
            Caret = Text.Length;
            Version++;
        }

        // Applies this frame's typing. Returns true when Enter was pressed.
        public bool Update(EditorInput input)
        {
            bool enter = false;
            string typed = input.Text.ToString();
            if (input.Paste)
            {
                typed += (input.Clipboard ?? "").Split('\n')[0];
            }
            foreach (char c in typed)
            {
                if (c >= ' ' && c != '\u007f' && Text.Length < Max)
                {
                    Text = Text.Insert(Caret, c.ToString());
                    Caret++;
                    Version++;
                }
            }
            foreach (EditKey key in input.Keys)
            {
                switch (key)
                {
                    case EditKey.Backspace:
                        if (Caret > 0)
                        {
                            Text = Text.Remove(Caret - 1, 1);
                            Caret--;
                        }
                        break;
                    case EditKey.Delete:
                        if (Caret < Text.Length)
                        {
                            Text = Text.Remove(Caret, 1);
                        }
                        break;
                    case EditKey.Left:
                        Caret = Math.Max(0, Caret - 1);
                        break;
                    case EditKey.Right:
                        Caret = Math.Min(Text.Length, Caret + 1);
                        break;
                    case EditKey.Home:
                        Caret = 0;
                        break;
                    case EditKey.End:
                        Caret = Text.Length;
                        break;
                    case EditKey.Enter:
                        enter = true;
                        break;
                }
                Version++;
            }
            return enter;
        }

        public void Draw(Surface s, int x, int y, int w, bool caretOn)
        {
            s.RoundRectFill(x, y, w, 12, 3, Palette.DarkBlue);
            int room = Math.Max(1, (w - 8) / PixelFont.Advance);
            int first = Math.Max(0, Caret - room);
            string shown = Text.Substring(first, Math.Min(room, Text.Length - first));
            s.Print(shown, x + 4, y + 3, Palette.White);
            if (caretOn)
            {
                s.RectFill(x + 4 + (Caret - first) * PixelFont.Advance - 1, y + 1, 1, 10, Palette.Pink);
            }
        }
    }
}
