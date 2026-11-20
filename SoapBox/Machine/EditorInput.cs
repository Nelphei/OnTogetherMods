using System.Collections.Generic;
using System.Text;

namespace SoapBox
{
    internal enum EditKey
    {
        Left,
        Right,
        Up,
        Down,
        Home,
        End,
        PageUp,
        PageDown,
        Backspace,
        Delete,
        Enter,
        Tab,
    }

    // What the player did this frame, for the editors. The window fills it in from Unity (typed text,
    // the editing keys with key repeat, shortcuts, the mouse in canvas pixels); tests fill it in by hand.
    internal sealed class EditorInput
    {
        public readonly StringBuilder Text = new StringBuilder();
        public readonly List<EditKey> Keys = new List<EditKey>();
        public bool Shift;
        public bool Ctrl;
        public bool Copy;
        public bool Cut;
        public bool Paste;
        public bool Undo;
        public bool Redo;
        public bool SelectAll;
        public string Clipboard = "";   // what Paste pastes

        public int MouseX;
        public int MouseY;
        public bool MouseDown;
        public bool MouseHeld;
        public bool MouseUp;
        public int Clicks;               // on MouseDown: 1, or 2 for a double-click
        public float Wheel;

        public double Now;               // seconds, for grouping undo steps

        public bool Typed => Text.Length > 0 || Keys.Count > 0 || Copy || Cut || Paste || Undo || Redo || SelectAll;

        public void Clear()
        {
            Text.Clear();
            Keys.Clear();
            Shift = false;
            Ctrl = false;
            Copy = false;
            Cut = false;
            Paste = false;
            Undo = false;
            Redo = false;
            SelectAll = false;
            Clipboard = "";
            MouseDown = false;
            MouseHeld = false;
            MouseUp = false;
            Clicks = 0;
            Wheel = 0f;
        }
    }
}
