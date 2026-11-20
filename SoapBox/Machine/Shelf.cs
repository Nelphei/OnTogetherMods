using System;
using System.Collections.Generic;

namespace SoapBox
{
    // The cart list: start a new cart, open one of yours or one that comes with Soap Box, rename
    // the one you're editing, or open your carts folder. Drawn into any box: over the game screen
    // while playing, or as a drop-down in the code editor.
    internal sealed class Shelf
    {
        public enum Kind
        {
            Heading,
            NewCart,
            Cart,
            Rename,
            Folder,
        }

        public sealed class Item
        {
            public Kind Kind;
            public string Label;
            public CartFiles.Entry Entry;
        }

        private const int Row = 11;

        public readonly List<Item> Items = new List<Item>();
        public readonly LineInput Name = new LineInput();
        public int Selected;
        public bool Renaming;
        public int Version;

        private int _scroll;
        // Where the list was last drawn, for the mouse.
        private int _listX, _listY, _listW, _rowsShown;

        public Item Current => Selected >= 0 && Selected < Items.Count ? Items[Selected] : null;

        public void Fill(List<CartFiles.Entry> yours, List<CartFiles.Entry> bundled, string currentPath, bool canRename)
        {
            Items.Clear();
            Add(Kind.NewCart, "+ new cart", null);
            Add(Kind.Heading, "your carts", null);
            if (yours.Count == 0)
            {
                Add(Kind.Heading, "  (none yet)", null);
            }
            foreach (CartFiles.Entry entry in yours)
            {
                Add(Kind.Cart, entry.Name, entry);
            }
            Add(Kind.Heading, "comes with soap box", null);
            foreach (CartFiles.Entry entry in bundled)
            {
                Add(Kind.Cart, entry.Name, entry);
            }
            if (canRename)
            {
                Add(Kind.Rename, "rename this cart", null);
            }
            Add(Kind.Folder, "open my carts folder", null);

            // Start on the cart that's open.
            Selected = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Entry != null && SamePath(Items[i].Entry.Path, currentPath))
                {
                    Selected = i;
                }
            }
            Renaming = false;
            _scroll = 0;
            Version++;
        }

        private void Add(Kind kind, string label, CartFiles.Entry entry)
        {
            Items.Add(new Item { Kind = kind, Label = label, Entry = entry });
        }

        // Up or down, skipping headings.
        public void Move(int by)
        {
            int i = Selected;
            for (int tries = 0; tries < Items.Count; tries++)
            {
                i = (i + by + Items.Count) % Items.Count;
                if (Items[i].Kind != Kind.Heading)
                {
                    Selected = i;
                    Version++;
                    return;
                }
            }
        }

        // The item under a pixel of the surface it was drawn on, or -1.
        public int ItemAt(int x, int y)
        {
            if (x < _listX || x >= _listX + _listW || y < _listY)
            {
                return -1;
            }
            int row = (y - _listY) / Row;
            if (row >= _rowsShown)
            {
                return -1;
            }
            int i = _scroll + row;
            return i < Items.Count && Items[i].Kind != Kind.Heading ? i : -1;
        }

        public void Draw(Surface s, int x, int y, int w, int h, string currentPath, bool caretOn)
        {
            Overlay.Box(s, x, y, w, h);
            s.Print(Renaming ? "rename this cart" : "carts", x + 7, y + 5, Palette.Pink);
            if (Renaming)
            {
                Name.Draw(s, x + 6, y + 18, w - 12, caretOn);
                s.Print("enter: save   esc: cancel", x + 7, y + 36, Palette.Grey);
                return;
            }

            _listX = x + 4;
            _listY = y + 17;
            _listW = w - 8;
            _rowsShown = Math.Max(1, (h - 21) / Row);
            if (Selected < _scroll)
            {
                _scroll = Selected;
            }
            else if (Selected >= _scroll + _rowsShown)
            {
                _scroll = Selected - _rowsShown + 1;
            }
            _scroll = Math.Max(0, Math.Min(_scroll, Items.Count - _rowsShown));

            int room = Math.Max(1, (_listW - 16) / PixelFont.Advance);
            for (int r = 0; r < _rowsShown; r++)
            {
                int i = _scroll + r;
                if (i >= Items.Count)
                {
                    break;
                }
                Item item = Items[i];
                int ry = _listY + r * Row;
                string label = item.Label.Length > room ? item.Label.Substring(0, room - 1) + "…" : item.Label;
                if (item.Kind == Kind.Heading)
                {
                    s.Print(label, _listX + 3, ry + 2, Palette.Grey);
                    continue;
                }
                bool on = i == Selected;
                if (on)
                {
                    s.RoundRectFill(_listX, ry, _listW, Row - 1, 3, Palette.DarkGrey);
                }
                bool open = item.Entry != null && SamePath(item.Entry.Path, currentPath);
                int colour = item.Kind == Kind.NewCart ? Palette.Green
                    : item.Kind == Kind.Rename || item.Kind == Kind.Folder ? Palette.LightBlue
                    : open ? Palette.Yellow : on ? Palette.White : Palette.Grey;
                int indent = item.Kind == Kind.Cart ? 9 : 3;
                if (on)
                {
                    s.Print("●", _listX + 2, ry + 2, Palette.Pink);
                    indent = Math.Max(indent, 9);
                }
                s.Print(label, _listX + indent, ry + 2, colour);
            }

            // More above or below.
            if (_scroll > 0)
            {
                s.Print("↑", x + w - 11, y + 5, Palette.Grey);
            }
            if (_scroll + _rowsShown < Items.Count)
            {
                s.Print("↓", x + w - 11, y + h - 12, Palette.Grey);
            }
        }

        public static bool SamePath(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
                   string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
    }
}
