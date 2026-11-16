using System;
using UnityEngine;

namespace OnTogetherMods.Shared
{
    // Lets Name Designer design the session name in Session Manager's window, the way it designs the
    // name in the main menu's Create Session box. Either mod works without the other.
    //
    // Both mods compile their own copy of this file, so they talk through AppDomain data, using
    // built-in types only (like PanelStack and IconBar).
    internal static class SessionNameLink
    {
        private const string BoxKey = "OnTogetherMods.SessionNameLink.Box";
        private const string DesignerKey = "OnTogetherMods.SessionNameLink.OpenDesigner";

        // ---- Session Manager's side ----

        // Call on every frame Session Manager's name box shows. bounds is its window, in 1080p units
        // from the top-left (like PanelStack). read and write get and set the text in the box (it
        // only renames the session when the host clicks Rename). letterLimit caps the letters.
        public static void ShowBox(Rect bounds, Func<string> read, Action<string> write, int letterLimit)
        {
            AppDomain.CurrentDomain.SetData(BoxKey, new object[]
            {
                Time.frameCount,
                new double[] { bounds.x, bounds.y, bounds.width, bounds.height },
                read,
                write,
                letterLimit,
            });
        }

        // True when Name Designer is installed.
        public static bool HasDesigner => AppDomain.CurrentDomain.GetData(DesignerKey) is Action;

        // Opens Name Designer on its Session tab, beside the name box.
        public static void OpenDesigner()
        {
            if (AppDomain.CurrentDomain.GetData(DesignerKey) is Action open)
            {
                open();
            }
        }

        // ---- Name Designer's side ----

        // Call once: how to open the window on its Session tab.
        public static void SetDesigner(Action open)
        {
            AppDomain.CurrentDomain.SetData(DesignerKey, open);
        }

        // True while Session Manager's name box is showing.
        public static bool BoxShowing => Box() != null;

        public static bool BoxBounds(out Rect bounds)
        {
            bounds = default;
            if (!(Box() is object[] box) || !(box[1] is double[] r) || r.Length != 4)
            {
                return false;
            }
            bounds = new Rect((float)r[0], (float)r[1], (float)r[2], (float)r[3]);
            return true;
        }

        public static string ReadBox()
        {
            return Box() is object[] box && box[2] is Func<string> read ? read() : null;
        }

        // Puts a name in the box. False if it isn't showing.
        public static bool WriteBox(string text)
        {
            if (Box() is object[] box && box[3] is Action<string> write)
            {
                write(text);
                return true;
            }
            return false;
        }

        public static int BoxLimit => Box() is object[] box && box[4] is int limit ? limit : 0;

        // The box, if it was drawn in the last couple of frames.
        private static object[] Box()
        {
            return AppDomain.CurrentDomain.GetData(BoxKey) is object[] box && box.Length == 5 &&
                   box[0] is int frame && Time.frameCount - frame <= 2
                ? box
                : null;
        }
    }
}
