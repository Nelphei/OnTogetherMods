using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace QuickHide
{
    // The Windows calls Quick Hide needs: reading a key while another app is focused, and handing the
    // keyboard to the window behind the game. Only call these on Windows.
    internal static class Win32
    {
        // ---- Keys ----

        // The Windows key code for a Unity key, or 0 if this list doesn't have it.
        public static int VirtualKey(KeyCode key)
        {
            if (key >= KeyCode.F1 && key <= KeyCode.F15)
            {
                return 0x70 + (key - KeyCode.F1);
            }
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                return 0x41 + (key - KeyCode.A);
            }
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                return 0x30 + (key - KeyCode.Alpha0);
            }
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                return 0x60 + (key - KeyCode.Keypad0);
            }
            switch (key)
            {
                case KeyCode.Pause: return 0x13;
                case KeyCode.ScrollLock: return 0x91;
                case KeyCode.Numlock: return 0x90;
                case KeyCode.Insert: return 0x2D;
                case KeyCode.Delete: return 0x2E;
                case KeyCode.Home: return 0x24;
                case KeyCode.End: return 0x23;
                case KeyCode.PageUp: return 0x21;
                case KeyCode.PageDown: return 0x22;
                case KeyCode.BackQuote: return 0xC0;
                case KeyCode.Escape: return 0x1B;
                default: return 0;
            }
        }

        // True while the key is held, whichever app is focused.
        public static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        // ---- Keyboard focus ----

        // If the game window has the keyboard, gives it to the next app window under the game (usually
        // the one you were in before, like Alt+Tab), or failing that to the desktop. Returns what happened,
        // for the log, or null if the game didn't have the keyboard.
        public static string FocusWindowBehind()
        {
            IntPtr game = GetForegroundWindow();
            uint me = GetCurrentProcessId();
            if (game == IntPtr.Zero || ProcessOf(game) != me)
            {
                return null;
            }

            // BepInEx's console window, which may not count as the game's own.
            IntPtr console = GetConsoleWindow();
            IntPtr window = GetWindow(game, GW_HWNDNEXT);
            // Capped, in case windows reorder while this walks down the list.
            for (int i = 0; i < 1000 && window != IntPtr.Zero; i++)
            {
                if (window != console && IsAppWindow(window, me))
                {
                    return Focus(window);
                }
                window = GetWindow(window, GW_HWNDNEXT);
            }

            IntPtr desktop = GetShellWindow();
            return desktop != IntPtr.Zero ? Focus(desktop) : "nowhere: no window to give it to";
        }

        private static string Focus(IntPtr window)
        {
            bool done = SetForegroundWindow(window);
            return $"a {ClassName(window)} window" + (done ? "" : ", but Windows refused");
        }

        // Roughly the windows Alt+Tab lists: shown, not minimised, not a tool window, popup or overlay,
        // and from another program.
        private static bool IsAppWindow(IntPtr window, uint me)
        {
            if (!IsWindowVisible(window) || IsIconic(window) || ProcessOf(window) == me)
            {
                return false;
            }
            int style = GetWindowLong(window, GWL_EXSTYLE);
            bool appWindow = (style & WS_EX_APPWINDOW) != 0;
            if (!appWindow && ((style & WS_EX_TOOLWINDOW) != 0 || GetWindow(window, GW_OWNER) != IntPtr.Zero))
            {
                return false;
            }
            if ((style & (WS_EX_NOACTIVATE | WS_EX_TRANSPARENT)) != 0 || IsCloaked(window))
            {
                return false;
            }
            return GetWindowRect(window, out Rect rect) && rect.Right > rect.Left && rect.Bottom > rect.Top;
        }

        // Windows 10 keeps some windows "shown" but invisible, like other desktops' apps and suspended
        // store apps.
        private static bool IsCloaked(IntPtr window)
        {
            try
            {
                return DwmGetWindowAttribute(window, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static uint ProcessOf(IntPtr window)
        {
            GetWindowThreadProcessId(window, out uint process);
            return process;
        }

        private static string ClassName(IntPtr window)
        {
            var name = new StringBuilder(256);
            return GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : "?";
        }

        // ---- Imports ----

        private const int GWL_EXSTYLE = -20;
        private const uint GW_HWNDNEXT = 2;
        private const uint GW_OWNER = 4;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_APPWINDOW = 0x40000;
        private const int WS_EX_NOACTIVATE = 0x8000000;
        private const int DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
    }
}
