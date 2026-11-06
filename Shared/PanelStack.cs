using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OnTogetherMods.Shared
{
    // Lets each mod draw its own small window along the bottom of the screen, stacked so they
    // never overlap. The windows' right edges line up 15% of the screen in from the right, which
    // keeps them clear of the chat box in the bottom-left corner.
    //
    // Every mod compiles its own copy of this file (see the <Compile Include> in each .csproj),
    // so the shared state is kept in AppDomain data, which all copies can see.
    //
    // Layout is in 1080p units: draw inside Begin()/End(), which scales to the real screen.
    internal static class PanelStack
    {
        private const string TableKey = "OnTogetherMods.PanelStack.Panels";
        private const string HiddenKey = "OnTogetherMods.PanelStack.Hidden";
        // Where the icon row is, written by IconBar.cs: x, y, width, height (screen pixels from the
        // top-left), the frame, and whether it's showing.
        private const string IconBarKey = "OnTogetherMods.IconBar.Rect";
        private const float Margin = 20f;
        // How far in from the right edge the windows' right edges sit, as a share of screen width.
        private const float FromRight = 0.15f;
        private const float Gap = 10f;
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // Stack order, bottom first. Leave gaps for future mods.
        public const int PhotoModeOrder = 0;
        public const int EmoteSyncOrder = 10;
        public const int TeleportSpotsOrder = 20;
        public const int SongPlayerOrder = 30;
        public const int TraceOverlayOrder = 40;
        public const int NameDesignerOrder = 50;
        public const int OutfitSlotsOrder = 60;

        // Screen width in the same 1080p units the windows are laid out in.
        public static float Width => Screen.width * 1080f / Screen.height;

        // Mods' own screen canvases (TextBox) are named with this. Photo Mode leaves them alone when
        // it hides the game's UI, so they keep showing alongside the mods' other windows. Like those,
        // they hide themselves while a photo is taken.
        public const string CanvasPrefix = "OnTogetherMods ";

        public static bool IsModCanvas(Canvas canvas)
        {
            Canvas root = canvas;
            for (Transform t = canvas.transform.parent; t != null; t = t.parent)
            {
                Canvas above = t.GetComponent<Canvas>();
                root = above != null ? above : root;
            }
            return root.gameObject.name.StartsWith(CanvasPrefix, StringComparison.Ordinal);
        }

        // True while mods should draw nothing: Photo Mode is taking a photo, or the player has hidden
        // the game's UI with its eye button.
        public static bool Hidden =>
            (AppDomain.CurrentDomain.GetData(HiddenKey) is int until && Time.frameCount <= until) || GameUIHidden;

        // True while the game's own UI is hidden by its eye button (UIManager.ButtonHideUI). The button
        // only shows in the split-screen and Desk window modes (keys 2-4). Switching modes doesn't reset
        // it, so in Normal mode (key 1), which has no button to bring the UI back, mod UI always shows.
        // Worked out once a frame, and only in the world: on the main menu MonoSingleton.I searches
        // the scene.
        public static bool GameUIHidden
        {
            get
            {
                int frame = Time.frameCount;
                if (_hiddenFrame != frame)
                {
                    _hiddenFrame = frame;
                    _gameUIHidden = false;
                    if (SceneManager.GetActiveScene().buildIndex == WorldScene)
                    {
                        OverlayManager overlay = MonoSingleton<OverlayManager>.I;
                        UIManager ui = MonoSingleton<UIManager>.I;
                        _gameUIHidden = overlay != null && overlay.ResolutionMode != ResolutionMode.Normal
                            && ui != null && EyeState(ui) != HideUIState.Open;
                    }
                }
                return _gameUIHidden;
            }
        }

        private static int _hiddenFrame = -1;
        private static bool _gameUIHidden;

        // UIManager._hideUIState is private. Looked up on first use, so a game update that renames it
        // leaves mod UI showing instead of breaking this class.
        private static AccessTools.FieldRef<UIManager, HideUIState> _eyeState;
        private static bool _eyeStateMissing;

        private static HideUIState EyeState(UIManager ui)
        {
            if (_eyeState == null && !_eyeStateMissing)
            {
                try
                {
                    _eyeState = AccessTools.FieldRefAccess<UIManager, HideUIState>("_hideUIState");
                }
                catch (Exception)
                {
                    _eyeStateMissing = true;
                }
            }
            return _eyeState != null ? _eyeState(ui) : HideUIState.Open;
        }

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

            float width = Width;
            // Never pushed off the left edge, even on a narrow screen.
            float x = Mathf.Max(Margin, width * (1f - FromRight) - size.x);
            float y = Bottom(x + size.x, frame) - size.y;
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
            return new Rect(x, y, size.x, size.y);
        }

        // Where the stack starts. The mods' icon row (IconBar.cs) sits in the bottom-right corner;
        // when it reaches under the windows, they start just above it.
        private static float Bottom(float windowRight, int frame)
        {
            float bottom = 1080f - Margin;
            if (AppDomain.CurrentDomain.GetData(IconBarKey) is double[] bar && frame - bar[4] <= 2)
            {
                float scale = 1080f / Screen.height;
                if (bar[0] * scale < windowRight + Gap)
                {
                    bottom = Mathf.Min(bottom, (float)bar[1] * scale - Gap);
                }
            }
            return bottom;
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
