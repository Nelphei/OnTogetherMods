using System;
using System.Collections.Generic;
using System.Text;

namespace SoapBox
{
    // The code editor: a cart's Lua code as lines of text, with a caret, a selection, scrolling,
    // copy and paste, undo and redo, auto-indent and coloured code. No Unity: EditorInput says what
    // the player did, and Draw paints it onto the editor's canvas.
    internal sealed class CodeEditor
    {
        public const int TabSize = 2;
        private const int MaxLength = 200000;   // letters of code a cart can have
        private const int MaxUndo = 300;
        private const double MergeSeconds = 1.5; // typing this close together is one undo step

        private readonly List<string> _lines = new List<string> { "" };
        // The LuaColours state each line starts in, worked out again after every change.
        private readonly List<int> _states = new List<int>();
        private bool _statesDirty = true;
        private byte[] _colours = new byte[256];

        // The caret, and the other end of the selection (the same place when nothing's selected).
        public int Line;
        public int Col;
        private int _anchorLine;
        private int _anchorCol;
        // The column Up and Down try to keep.
        private int _wantCol;

        // The first line and column showing.
        public int Top;
        public int Left;

        // True once the code differs from what was loaded or last saved.
        public bool Changed;
        // Goes up with every change the picture should show (text, caret, selection, scrolling).
        public int Version;
        // A line to mark in red (1-based; 0 for none): where the cart last stopped with an error.
        public int ErrorLine;
        // Set when Copy or Cut ran this frame: the window puts it on the clipboard.
        public string CopyText;

        // Where the text was last drawn, for the mouse: its left edge, top, rows and columns.
        private int _areaX, _areaY, _areaW, _areaH;
        private int _textX = 26, _textY = 16, _rows = 20, _cols = 58;
        private bool _dragging;

        private struct Snapshot
        {
            public string Text;
            public int Line, Col, AnchorLine, AnchorCol;
        }

        private enum Kind
        {
            None,
            Typing,
            Deleting,
            Other,
        }

        private readonly List<Snapshot> _undo = new List<Snapshot>();
        private readonly List<Snapshot> _redo = new List<Snapshot>();
        private Kind _lastKind;
        private double _lastEdit;

        public int LineCount => _lines.Count;
        public int Rows => _rows;

        public string Text => string.Join("\n", _lines);

        public bool HasSelection => Line != _anchorLine || Col != _anchorCol;

        public void Load(string text)
        {
            SetText(text ?? "");
            Line = Col = _anchorLine = _anchorCol = _wantCol = 0;
            Top = Left = 0;
            _undo.Clear();
            _redo.Clear();
            _lastKind = Kind.None;
            Changed = false;
            Version++;
        }

        public void MarkSaved()
        {
            Changed = false;
        }

        // Puts the caret at the start of a line (1-based) and scrolls it into the upper part of the view.
        public void GoTo(int line)
        {
            Line = Clamp(line - 1, 0, _lines.Count - 1);
            Col = Indent(_lines[Line]);
            _wantCol = Col;
            Deselect();
            Top = Math.Max(0, Line - _rows / 3);
            Left = 0;
            ScrollToCaret();
            Version++;
        }

        // ---- Input ----

        public void Update(EditorInput input)
        {
            CopyText = null;
            double now = input.Now;

            if (input.Wheel != 0f)
            {
                int max = Math.Max(0, _lines.Count - _rows);
                Top = Clamp(Top - Math.Sign(input.Wheel) * 3, 0, max);
                Version++;
            }
            if (input.MouseDown && InArea(input.MouseX, input.MouseY))
            {
                PositionAt(input.MouseX, input.MouseY, out int line, out int col);
                if (input.Clicks >= 2)
                {
                    SelectWord(line, col);
                }
                else
                {
                    Line = line;
                    Col = col;
                    if (!input.Shift)
                    {
                        Deselect();
                    }
                    _dragging = true;
                }
                _wantCol = Col;
                _lastKind = Kind.None;
                Version++;
            }
            if (_dragging && input.MouseHeld && !input.MouseDown)
            {
                PositionAt(input.MouseX, input.MouseY, out int line, out int col);
                if (line != Line || col != Col)
                {
                    Line = line;
                    Col = col;
                    _wantCol = Col;
                    ScrollToCaret();
                    Version++;
                }
            }
            if (input.MouseUp || !input.MouseHeld)
            {
                _dragging = false;
            }

            if (input.SelectAll)
            {
                _anchorLine = 0;
                _anchorCol = 0;
                Line = _lines.Count - 1;
                Col = _lines[Line].Length;
                Version++;
            }
            if (input.Copy)
            {
                CopyText = SelectedOrLine();
            }
            if (input.Cut)
            {
                CopyText = SelectedOrLine();
                Checkpoint(Kind.Other, now);
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else
                {
                    RemoveLine();
                }
                Edited();
            }
            if (input.Paste && !string.IsNullOrEmpty(input.Clipboard))
            {
                Checkpoint(Kind.Other, now);
                Insert(Clean(input.Clipboard));
            }
            if (input.Undo)
            {
                Step(_undo, _redo);
            }
            if (input.Redo)
            {
                Step(_redo, _undo);
            }
            if (input.Text.Length > 0)
            {
                Checkpoint(HasSelection ? Kind.Other : Kind.Typing, now);
                Insert(Clean(input.Text.ToString()));
            }
            foreach (EditKey key in input.Keys)
            {
                Key(key, input.Shift, input.Ctrl, now);
            }
        }

        private void Key(EditKey key, bool shift, bool ctrl, double now)
        {
            switch (key)
            {
                case EditKey.Left:
                    if (HasSelection && !shift)
                    {
                        Order(out int sl, out int sc, out _, out _);
                        Line = sl;
                        Col = sc;
                    }
                    else if (ctrl)
                    {
                        WordLeft();
                    }
                    else if (Col > 0)
                    {
                        Col--;
                    }
                    else if (Line > 0)
                    {
                        Line--;
                        Col = _lines[Line].Length;
                    }
                    _wantCol = Col;
                    Moved(shift);
                    break;
                case EditKey.Right:
                    if (HasSelection && !shift)
                    {
                        Order(out _, out _, out int el, out int ec);
                        Line = el;
                        Col = ec;
                    }
                    else if (ctrl)
                    {
                        WordRight();
                    }
                    else if (Col < _lines[Line].Length)
                    {
                        Col++;
                    }
                    else if (Line < _lines.Count - 1)
                    {
                        Line++;
                        Col = 0;
                    }
                    _wantCol = Col;
                    Moved(shift);
                    break;
                case EditKey.Up:
                    MoveLines(-1, shift);
                    break;
                case EditKey.Down:
                    MoveLines(1, shift);
                    break;
                case EditKey.PageUp:
                    MoveLines(-Math.Max(1, _rows - 1), shift);
                    break;
                case EditKey.PageDown:
                    MoveLines(Math.Max(1, _rows - 1), shift);
                    break;
                case EditKey.Home:
                    if (ctrl)
                    {
                        Line = 0;
                        Col = 0;
                    }
                    else
                    {
                        // To the first letter, or the very start if already there.
                        int indent = Indent(_lines[Line]);
                        Col = Col == indent ? 0 : indent;
                    }
                    _wantCol = Col;
                    Moved(shift);
                    break;
                case EditKey.End:
                    if (ctrl)
                    {
                        Line = _lines.Count - 1;
                    }
                    Col = _lines[Line].Length;
                    _wantCol = Col;
                    Moved(shift);
                    break;
                case EditKey.Backspace:
                    Checkpoint(Kind.Deleting, now);
                    if (HasSelection)
                    {
                        DeleteSelection();
                    }
                    else if (Col > 0)
                    {
                        // In the indentation, back to the previous tab stop.
                        string line = _lines[Line];
                        int remove = 1;
                        if (Col <= Indent(line) && Col >= TabSize)
                        {
                            remove = (Col - 1) % TabSize + 1;
                        }
                        _lines[Line] = line.Remove(Col - remove, remove);
                        Col -= remove;
                    }
                    else if (Line > 0)
                    {
                        Col = _lines[Line - 1].Length;
                        _lines[Line - 1] += _lines[Line];
                        _lines.RemoveAt(Line);
                        Line--;
                    }
                    Deselect();
                    Edited();
                    break;
                case EditKey.Delete:
                    Checkpoint(Kind.Deleting, now);
                    if (HasSelection)
                    {
                        DeleteSelection();
                    }
                    else if (Col < _lines[Line].Length)
                    {
                        _lines[Line] = _lines[Line].Remove(Col, 1);
                    }
                    else if (Line < _lines.Count - 1)
                    {
                        _lines[Line] += _lines[Line + 1];
                        _lines.RemoveAt(Line + 1);
                    }
                    Deselect();
                    Edited();
                    break;
                case EditKey.Enter:
                    Checkpoint(Kind.Other, now);
                    Insert("\n" + new string(' ', NewLineIndent()));
                    break;
                case EditKey.Tab:
                    Checkpoint(Kind.Other, now);
                    Order(out int fromLine, out _, out int toLine, out _);
                    if (shift || fromLine != toLine)
                    {
                        IndentLines(fromLine, toLine, !shift);
                    }
                    else
                    {
                        Insert(new string(' ', TabSize - Col % TabSize));
                    }
                    break;
            }
        }

        private void MoveLines(int by, bool shift)
        {
            Line = Clamp(Line + by, 0, _lines.Count - 1);
            Col = Math.Min(_wantCol, _lines[Line].Length);
            Moved(shift);
        }

        private void Moved(bool shift)
        {
            if (!shift)
            {
                Deselect();
            }
            _lastKind = Kind.None;
            ScrollToCaret();
            Version++;
        }

        private void WordLeft()
        {
            if (Col == 0)
            {
                if (Line > 0)
                {
                    Line--;
                    Col = _lines[Line].Length;
                }
                return;
            }
            string line = _lines[Line];
            int i = Col;
            while (i > 0 && char.IsWhiteSpace(line[i - 1]))
            {
                i--;
            }
            bool word = i > 0 && IsWord(line[i - 1]);
            while (i > 0 && !char.IsWhiteSpace(line[i - 1]) && IsWord(line[i - 1]) == word)
            {
                i--;
            }
            Col = i;
        }

        private void WordRight()
        {
            string line = _lines[Line];
            if (Col >= line.Length)
            {
                if (Line < _lines.Count - 1)
                {
                    Line++;
                    Col = 0;
                }
                return;
            }
            int i = Col;
            bool word = IsWord(line[i]);
            while (i < line.Length && !char.IsWhiteSpace(line[i]) && IsWord(line[i]) == word)
            {
                i++;
            }
            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }
            Col = i;
        }

        private static bool IsWord(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private void SelectWord(int line, int col)
        {
            string text = _lines[line];
            int start = Math.Min(col, text.Length), end = start;
            if (start < text.Length && IsWord(text[start]) || start > 0 && IsWord(text[start - 1]))
            {
                while (start > 0 && IsWord(text[start - 1]))
                {
                    start--;
                }
                while (end < text.Length && IsWord(text[end]))
                {
                    end++;
                }
            }
            else if (end < text.Length)
            {
                end++;
            }
            Line = line;
            _anchorLine = line;
            _anchorCol = start;
            Col = end;
        }

        // ---- Changing the text ----

        // Puts text (which may have line breaks) at the caret, over the selection if there is one.
        private void Insert(string text)
        {
            if (HasSelection)
            {
                DeleteSelection();
            }
            int room = MaxLength - Length();
            if (room <= 0)
            {
                return;
            }
            if (text.Length > room)
            {
                text = text.Substring(0, room);
            }
            string line = _lines[Line];
            string before = line.Substring(0, Col);
            string after = line.Substring(Col);
            string[] parts = text.Split('\n');
            if (parts.Length == 1)
            {
                _lines[Line] = before + text + after;
                Col += text.Length;
            }
            else
            {
                _lines[Line] = before + parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    _lines.Insert(Line + i, parts[i]);
                }
                Line += parts.Length - 1;
                Col = parts[parts.Length - 1].Length;
                _lines[Line] += after;
            }
            _wantCol = Col;
            Deselect();
            Edited();
        }

        private void DeleteSelection()
        {
            Order(out int sl, out int sc, out int el, out int ec);
            if (sl == el)
            {
                _lines[sl] = _lines[sl].Remove(sc, ec - sc);
            }
            else
            {
                _lines[sl] = _lines[sl].Substring(0, sc) + _lines[el].Substring(ec);
                _lines.RemoveRange(sl + 1, el - sl);
            }
            Line = sl;
            Col = sc;
            _wantCol = Col;
            Deselect();
        }

        private void RemoveLine()
        {
            if (_lines.Count == 1)
            {
                _lines[0] = "";
            }
            else
            {
                _lines.RemoveAt(Line);
            }
            Line = Math.Min(Line, _lines.Count - 1);
            Col = Math.Min(Col, _lines[Line].Length);
            Deselect();
        }

        // Tab / Shift+Tab on whole lines. The caret and the selection stay on the same letters.
        private void IndentLines(int from, int to, bool more)
        {
            int caretShift = 0, anchorShift = 0;
            for (int i = from; i <= to; i++)
            {
                string line = _lines[i];
                int change = 0;
                if (more && line.Length > 0)
                {
                    _lines[i] = new string(' ', TabSize) + line;
                    change = TabSize;
                }
                else if (!more)
                {
                    int remove = Math.Min(TabSize, Indent(line));
                    _lines[i] = line.Substring(remove);
                    change = -remove;
                }
                if (i == Line)
                {
                    caretShift = change;
                }
                if (i == _anchorLine)
                {
                    anchorShift = change;
                }
            }
            Col = Clamp(Col + caretShift, 0, _lines[Line].Length);
            _anchorCol = Clamp(_anchorCol + anchorShift, 0, _lines[_anchorLine].Length);
            _wantCol = Col;
            Edited();
        }

        // How far a new line after the caret is indented: like this line, one step more after
        // something that opens a block (then, do, else, repeat, function(...), {).
        private int NewLineIndent()
        {
            string line = _lines[Line];
            int indent = Math.Min(Indent(line), Col);
            string before = line.Substring(0, Col).Trim();
            int comment = before.IndexOf("--", StringComparison.Ordinal);
            if (comment >= 0)
            {
                before = before.Substring(0, comment).TrimEnd();
            }
            bool opens = EndsWithWord(before, "then") || EndsWithWord(before, "do") || EndsWithWord(before, "else") ||
                         EndsWithWord(before, "repeat") || before.EndsWith("{", StringComparison.Ordinal) ||
                         (before.EndsWith(")", StringComparison.Ordinal) && ContainsWord(before, "function"));
            return indent + (opens ? TabSize : 0);
        }

        private static bool EndsWithWord(string text, string word)
        {
            return text.EndsWith(word, StringComparison.Ordinal) &&
                   (text.Length == word.Length || !IsWord(text[text.Length - word.Length - 1]));
        }

        private static bool ContainsWord(string text, string word)
        {
            int i = text.IndexOf(word, StringComparison.Ordinal);
            while (i >= 0)
            {
                bool before = i == 0 || !IsWord(text[i - 1]);
                bool after = i + word.Length >= text.Length || !IsWord(text[i + word.Length]);
                if (before && after)
                {
                    return true;
                }
                i = text.IndexOf(word, i + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private void Edited()
        {
            Changed = true;
            _statesDirty = true;
            ScrollToCaret();
            Version++;
        }

        // ---- Undo ----

        // Saves the text before a change, unless it continues the last one (typing in a burst).
        private void Checkpoint(Kind kind, double now)
        {
            bool merge = kind != Kind.Other && kind == _lastKind && now - _lastEdit < MergeSeconds;
            if (!merge)
            {
                _undo.Add(Take());
                if (_undo.Count > MaxUndo)
                {
                    _undo.RemoveAt(0);
                }
            }
            _redo.Clear();
            _lastKind = kind;
            _lastEdit = now;
        }

        private Snapshot Take()
        {
            return new Snapshot { Text = Text, Line = Line, Col = Col, AnchorLine = _anchorLine, AnchorCol = _anchorCol };
        }

        private void Step(List<Snapshot> from, List<Snapshot> to)
        {
            if (from.Count == 0)
            {
                return;
            }
            to.Add(Take());
            Snapshot s = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            SetText(s.Text);
            Line = Clamp(s.Line, 0, _lines.Count - 1);
            Col = Clamp(s.Col, 0, _lines[Line].Length);
            _anchorLine = Clamp(s.AnchorLine, 0, _lines.Count - 1);
            _anchorCol = Clamp(s.AnchorCol, 0, _lines[_anchorLine].Length);
            _wantCol = Col;
            _lastKind = Kind.None;
            Edited();
        }

        // ---- Helpers ----

        private void SetText(string text)
        {
            _lines.Clear();
            _lines.AddRange(text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", new string(' ', TabSize)).Split('\n'));
            if (_lines.Count == 0)
            {
                _lines.Add("");
            }
            _statesDirty = true;
        }

        // Pasted or typed text: Windows line ends and tabs made plain, other control letters dropped.
        private static string Clean(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text.Replace("\r\n", "\n").Replace('\r', '\n'))
            {
                if (c == '\t')
                {
                    sb.Append(' ', TabSize);
                }
                else if (c == '\n' || c >= ' ' && c != '\u007f')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private int Length()
        {
            int length = _lines.Count - 1;
            foreach (string line in _lines)
            {
                length += line.Length;
            }
            return length;
        }

        private string SelectedOrLine()
        {
            if (!HasSelection)
            {
                return _lines[Line] + "\n";
            }
            Order(out int sl, out int sc, out int el, out int ec);
            if (sl == el)
            {
                return _lines[sl].Substring(sc, ec - sc);
            }
            var sb = new StringBuilder();
            sb.Append(_lines[sl].Substring(sc)).Append('\n');
            for (int i = sl + 1; i < el; i++)
            {
                sb.Append(_lines[i]).Append('\n');
            }
            sb.Append(_lines[el].Substring(0, ec));
            return sb.ToString();
        }

        // The selection's start and end, in text order.
        private void Order(out int sl, out int sc, out int el, out int ec)
        {
            bool caretFirst = Line < _anchorLine || (Line == _anchorLine && Col < _anchorCol);
            sl = caretFirst ? Line : _anchorLine;
            sc = caretFirst ? Col : _anchorCol;
            el = caretFirst ? _anchorLine : Line;
            ec = caretFirst ? _anchorCol : Col;
        }

        private void Deselect()
        {
            _anchorLine = Line;
            _anchorCol = Col;
        }

        private static int Indent(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ')
            {
                i++;
            }
            return i;
        }

        private void ScrollToCaret()
        {
            if (Line < Top)
            {
                Top = Line;
            }
            else if (Line >= Top + _rows)
            {
                Top = Line - _rows + 1;
            }
            Top = Clamp(Top, 0, Math.Max(0, _lines.Count - 1));
            if (Col < Left)
            {
                Left = Math.Max(0, Col - 4);
            }
            else if (Col >= Left + _cols)
            {
                Left = Col - _cols + 5;
            }
        }

        private bool InArea(int x, int y)
        {
            return x >= _areaX && y >= _areaY && x < _areaX + _areaW && y < _areaY + _areaH;
        }

        // The line and column nearest a canvas pixel (above or below the text: the first or last row).
        private void PositionAt(int x, int y, out int line, out int col)
        {
            int row = (int)Math.Floor((y - _textY + 2) / (double)PixelFont.LineHeight);
            line = Clamp(Top + row, 0, _lines.Count - 1);
            col = Clamp((int)Math.Floor((x - _textX + 3) / (double)PixelFont.Advance) + Left, 0, _lines[line].Length);
        }

        private static int Clamp(int v, int min, int max)
        {
            return v < min ? min : v > max ? max : v;
        }

        // ---- Drawing ----

        // Paints the code into the box (x, y, w, h) of the canvas: line numbers, coloured code, the
        // selection and (if caretOn) the caret.
        public void Draw(Surface s, int x, int y, int w, int h, bool caretOn)
        {
            int digits = Math.Max(3, _lines.Count.ToString().Length);
            int gutter = digits * PixelFont.Advance + 4;
            _areaX = x;
            _areaY = y;
            _areaW = w;
            _areaH = h;
            _textX = x + gutter + 4;
            _textY = y + 3;
            _rows = Math.Max(1, (h - 3) / PixelFont.LineHeight);
            _cols = Math.Max(1, (w - gutter - 8) / PixelFont.Advance);
            if (Top > Math.Max(0, _lines.Count - 1))
            {
                Top = Math.Max(0, _lines.Count - 1);
            }

            s.RectFill(x, y, w, h, Palette.Ink);
            ColourStates();
            Order(out int sl, out int sc, out int el, out int ec);
            bool selection = HasSelection;

            for (int row = 0; row < _rows; row++)
            {
                int li = Top + row;
                if (li >= _lines.Count)
                {
                    break;
                }
                int ly = _textY + row * PixelFont.LineHeight;
                string number = (li + 1).ToString();
                bool error = li == ErrorLine - 1;
                int numberColour = error ? Palette.Red : li == Line ? Palette.White : Palette.DarkGrey;
                if (error)
                {
                    s.RectFill(x, ly - 2, 2, PixelFont.LineHeight, Palette.Red);
                }
                s.Print(number, x + gutter - 2 - PixelFont.Width(number), ly, numberColour);

                string text = _lines[li];
                if (selection && li >= sl && li <= el)
                {
                    int from = li == sl ? sc : 0;
                    int to = li == el ? ec : text.Length + 1;   // past the end: the line break is selected too
                    from = Math.Max(from, Left);
                    to = Math.Min(to, Left + _cols);
                    if (to > from)
                    {
                        s.RectFill(_textX + (from - Left) * PixelFont.Advance - 1, ly - 2, (to - from) * PixelFont.Advance,
                            PixelFont.LineHeight, Palette.DarkBlue);
                    }
                }
                if (error && text.Length > Left)
                {
                    s.Line(_textX, ly + 8, _textX + Math.Min(text.Length - Left, _cols) * PixelFont.Advance - 2, ly + 8, Palette.Red);
                }

                if (_colours.Length < text.Length)
                {
                    _colours = new byte[text.Length * 2];
                }
                LuaColours.Colour(text, _states[li], _colours);
                int end = Math.Min(text.Length, Left + _cols);
                for (int i = Left; i < end; i++)
                {
                    PixelFont.DrawChar(s, text[i], _textX + (i - Left) * PixelFont.Advance, ly, _colours[i]);
                }
                if (text.Length > Left + _cols)
                {
                    s.Pset(x + w - 6, ly + 3, Palette.Grey);   // more of this line to the right
                }
            }

            if (caretOn && Line >= Top && Line < Top + _rows && Col >= Left && Col <= Left + _cols)
            {
                s.RectFill(_textX + (Col - Left) * PixelFont.Advance - 1, _textY + (Line - Top) * PixelFont.LineHeight - 2,
                    1, PixelFont.LineHeight, Palette.Pink);
            }

            // Scroll bar.
            if (_lines.Count > _rows)
            {
                int track = h - 4;
                int thumb = Math.Max(6, track * _rows / _lines.Count);
                int at = (track - thumb) * Top / Math.Max(1, _lines.Count - _rows);
                s.RectFill(x + w - 3, y + 2, 2, track, Palette.DarkGrey);
                s.RectFill(x + w - 3, y + 2 + Math.Min(at, track - thumb), 2, thumb, Palette.Grey);
            }
        }

        private void ColourStates()
        {
            if (!_statesDirty && _states.Count == _lines.Count)
            {
                return;
            }
            _states.Clear();
            int state = 0;
            foreach (string line in _lines)
            {
                _states.Add(state);
                if (_colours.Length < line.Length)
                {
                    _colours = new byte[line.Length * 2];
                }
                state = LuaColours.Colour(line, state, _colours);
            }
            _statesDirty = false;
        }
    }
}
