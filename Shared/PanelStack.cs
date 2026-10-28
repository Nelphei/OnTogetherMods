using System;
using System.Collections;
using UnityEngine;

namespace OnTogetherMods.Shared
{
    // Lets each mod draw its own small window in the bottom-left corner, stacked so they never
    // overlap. Every mod compiles its own copy of this file (see the <Compile Include> in each
    // .csproj), so the shared state is kept in AppDomain data, which all copies can see.
    //
    // Layout is in 1080p units: draw inside Begin()/End(), which scales to the real screen.
    internal static class PanelStack
    {
        private const string TableKey = "OnTogetherMods.PanelStack.Panels";
        private const string HiddenKey = "OnTogetherMods.PanelStack.Hidden";
        private const float Margin = 20f;
        private const float Gap = 10f;

        // Stack order, bottom first. Leave gaps for future mods.
        public const int PhotoModeOrder = 0;
        public const int EmoteSyncOrder = 10;
        public const int TeleportSpotsOrder = 20;

        // True while Photo Mode is taking a photo. Mods draw nothing then, so they stay out of it.
        public static bool Hidden =>
            AppDomain.CurrentDomain.GetData(HiddenKey) is int until && Time.frameCount <= until;

        // Hides every mod's windows for this frame and the next. It runs out by itself, so a
        // photo that fails halfway can't leave them hidden.
        public static void HideForCapture()
        {
            AppDomain.CurrentDomain.SetData(HiddenKey, Time.frameCount + 1);
        }

        // Where to draw a panel of this size this frame. Panels with a lower order (or the same
        // order and an earlier id) sit below it. A panel that stops being drawn drops out after a
        // couple of frames, and the ones above it move down.
        public static Rect Place(string id, int order, Vector2 size)
        {
            Hashtable panels = Panels();
            int frame = Time.frameCount;
            // Only built-in types go in the table: each mod's copy of this file is its own type.
            panels[id] = new double[] { order, size.y, frame };

            float y = 1080f - Margin - size.y;
            foreach (DictionaryEntry entry in panels)
            {
                var key = (string)entry.Key;
                var value = (double[])entry.Value;
                bool fresh = frame - value[2] <= 2;
                bool below = value[0] < order || (value[0] == order && string.CompareOrdinal(key, id) < 0);
                if (key != id && fresh && below)
                {
                    y -= (float)value[1] + Gap;
                }
            }
            return new Rect(Margin, y, size.x, size.y);
        }

        // Scales GUI drawing from 1080p to the real screen. Returns the old matrix for End().
        public static Matrix4x4 Begin(out float width)
        {
            float scale = Screen.height / 1080f;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            width = Screen.width / scale;
            return previous;
        }

        public static void End(Matrix4x4 previous)
        {
            GUI.matrix = previous;
        }

        // The look both mods use: a dark box with white 15px text.
        public static GUIStyle MakeStyle(TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = alignment,
                fontSize = 15,
                richText = true,
                wordWrap = false,
                padding = new RectOffset(12, 12, 10, 10),
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static Hashtable Panels()
        {
            if (!(AppDomain.CurrentDomain.GetData(TableKey) is Hashtable panels))
            {
                panels = new Hashtable();
                AppDomain.CurrentDomain.SetData(TableKey, panels);
            }
            return panels;
        }
    }
}
