using System;
using UnityEngine;

namespace OnTogetherMods.Shared
{
    // Lets Card Designer open Name Designer on its Name tab, so the name line of the ID card can be
    // designed too. The two windows sit side by side while both show. Either mod works without the
    // other.
    //
    // Both mods compile their own copy of this file, so they talk through AppDomain data, using
    // built-in types only (like PanelStack and SessionNameLink).
    internal static class NameDesignerLink
    {
        private const string OpenKey = "OnTogetherMods.NameDesignerLink.Open";
        private const string CardKey = "OnTogetherMods.NameDesignerLink.Card";
        private const string DesignerKey = "OnTogetherMods.NameDesignerLink.Designer";

        // ---- Card Designer's side ----

        // True when Name Designer is installed.
        public static bool HasDesigner => AppDomain.CurrentDomain.GetData(OpenKey) is Action;

        // Opens Name Designer on its Name tab, beside the card window.
        public static void OpenDesigner()
        {
            if (AppDomain.CurrentDomain.GetData(OpenKey) is Action open)
            {
                open();
            }
        }

        // Call on every frame the card window shows. bounds is the window, in 1080p units from the
        // top-left (like PanelStack).
        public static void ShowCard(Rect bounds)
        {
            Share(CardKey, bounds);
        }

        // Where Name Designer is, while it shows beside the card window.
        public static bool DesignerBounds(out Rect bounds)
        {
            return Read(DesignerKey, out bounds);
        }

        // ---- Name Designer's side ----

        // Call once: how to open the window on its Name tab for the card window.
        public static void SetDesigner(Action open)
        {
            AppDomain.CurrentDomain.SetData(OpenKey, open);
        }

        // True while the card window is showing.
        public static bool CardShowing => CardBounds(out _);

        public static bool CardBounds(out Rect bounds)
        {
            return Read(CardKey, out bounds);
        }

        // Call on every frame the window shows beside the card window.
        public static void ShowDesigner(Rect bounds)
        {
            Share(DesignerKey, bounds);
        }

        // ---- Both ----

        private static void Share(string key, Rect bounds)
        {
            AppDomain.CurrentDomain.SetData(key, new double[]
            {
                Time.frameCount, bounds.x, bounds.y, bounds.width, bounds.height,
            });
        }

        // A rect shared in the last couple of frames.
        private static bool Read(string key, out Rect bounds)
        {
            bounds = default;
            if (!(AppDomain.CurrentDomain.GetData(key) is double[] r) || r.Length != 5 || Time.frameCount - r[0] > 2)
            {
                return false;
            }
            bounds = new Rect((float)r[1], (float)r[2], (float)r[3], (float)r[4]);
            return true;
        }
    }
}
