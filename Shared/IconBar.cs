using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnTogetherMods.Shared
{
    // A row of small pixel-art icons in the bottom-right corner, one for each installed mod.
    // Clicking an icon does what the mod's key does, and hovering shows the mod's name and key.
    //
    // Each mod draws its own icon by calling Draw() first thing in OnGUI, every frame. Like PanelStack,
    // every mod compiles its own copy of this file, so the shared state (which icons there are, where
    // the row is) is kept in AppDomain data, using built-in types only.
    //
    // The row shows only in a world, while no game menu or panel is open (pause menu, inventory, shop,
    // player list, focus menu, emote wheel...). It hides in Photo Mode and while the game's eye button
    // hides the UI (PanelStack.Hidden). PanelStack moves the mod windows up when the row reaches under
    // them.
    internal static class IconBar
    {
        public enum State
        {
            Plain,  // a button that opens or starts something
            Open,   // its window is open, or it's running (Photo Mode)
            On,     // a switch that's on: green dot
            Off,    // a switch that's off: greyed out
        }

        public enum Click { None, Left, Right }

        // Left to right. Leave gaps for future mods.
        public const int PhotoModeOrder = 0;
        public const int EmoteSyncOrder = 10;
        public const int TeleportSpotsOrder = 20;
        public const int NameDesignerOrder = 30;
        public const int OutfitSlotsOrder = 35;
        public const int DeskSnapOrder = 40;
        public const int DeskSwarmOrder = 45;
        public const int FocusAnywhereOrder = 50;
        public const int SongPlayerOrder = 60;
        public const int TraceOverlayOrder = 70;
        public const int ChatRangeOrder = 80;
        public const int QuickHideOrder = 90;
        public const int NetworkCrashFixOrder = 100;

        // Where the row is: x, y, width, height (screen pixels from the top-left), the frame it was
        // laid out, and 1 if it's showing. PanelStack reads it under the same key.
        private const string RectKey = "OnTogetherMods.IconBar.Rect";
        private const string TableKey = "OnTogetherMods.IconBar.Icons";
        private const string PatchedKey = "OnTogetherMods.IconBar.Patched";
        private const string BlockerKey = "OnTogetherMods.IconBar.Blocker";
        private const string BlockerFrameKey = "OnTogetherMods.IconBar.BlockerFrame";
        private const string HiddenKey = "OnTogetherMods.IconBar.HiddenUntil";

        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // The hovered icon's mod draws on top of the others, so its tooltip isn't hidden under a window.
        private const int TooltipDepth = -100;

        // The colours the icons are drawn with, one letter each. '.' is see-through.
        private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
        {
            { 'K', new Color32(42, 36, 51, 255) },    // outline
            { 'W', new Color32(247, 243, 234, 255) }, // white
            { 'G', new Color32(201, 198, 207, 255) }, // light grey
            { 'g', new Color32(143, 138, 153, 255) }, // grey
            { 'D', new Color32(87, 82, 95, 255) },    // dark grey
            { 'B', new Color32(168, 107, 60, 255) },  // wood
            { 'b', new Color32(217, 160, 102, 255) }, // light wood
            { 'd', new Color32(110, 67, 37, 255) },   // dark wood
            { 'R', new Color32(224, 90, 90, 255) },   // red
            { 'r', new Color32(168, 58, 70, 255) },   // dark red
            { 'p', new Color32(246, 165, 165, 255) }, // pink highlight
            { 'Y', new Color32(245, 208, 97, 255) },  // yellow
            { 'y', new Color32(201, 150, 50, 255) },  // dark yellow
            { 'O', new Color32(232, 146, 74, 255) },  // orange
            { 'L', new Color32(159, 214, 245, 255) }, // light blue
            { 'U', new Color32(74, 143, 217, 255) },  // blue
            { 'u', new Color32(47, 90, 158, 255) },   // dark blue
            { 'N', new Color32(116, 192, 105, 255) }, // green
            { 'n', new Color32(63, 138, 74, 255) },   // dark green
            { 'P', new Color32(246, 207, 174, 255) }, // skin
            { 'V', new Color32(154, 114, 214, 255) }, // purple
            { 'v', new Color32(105, 72, 160, 255) },  // dark purple
            { 'S', new Color32(233, 139, 181, 255) }, // pink
            { 'H', new Color32(60, 48, 56, 255) },    // hair
        };

        private static readonly Color32 ButtonFill = new Color32(28, 26, 34, 200);
        private static readonly Color32 ButtonEdge = new Color32(255, 255, 255, 30);
        private static readonly Color32 HoverFill = new Color32(48, 45, 58, 225);
        private static readonly Color32 HoverEdge = new Color32(255, 255, 255, 80);
        private static readonly Color32 OpenFill = new Color32(70, 64, 88, 235);
        private static readonly Color32 OpenEdge = new Color32(255, 255, 255, 150);
        private static readonly Color32 DotFill = new Color32(140, 220, 120, 255);
        private static readonly Color32 DotEdge = new Color32(20, 40, 20, 255);

        // This copy's textures and styles. Rebuilt when the screen height changes.
        private static readonly Dictionary<string[], Texture2D[]> Icons = new Dictionary<string[], Texture2D[]>();
        private static Texture2D _buttonTexture;
        private static Texture2D _hoverTexture;
        private static Texture2D _openTexture;
        private static Texture2D _dotTexture;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _tipStyle;
        private static int _builtFor;

        private static int _cachedFrame = -1;
        private static bool _inWorld;
        private static bool _visible;
        private static int _pressedButton;

        // ---- Drawing ----

        // Draws this mod's icon. Call it first thing in OnGUI, every frame, so it can line up with
        // the other mods' icons. It draws nothing while the row is hidden. A click comes back here:
        // act on it in Update, outside the GUI pass.
        //
        // pixels: 16 rows of 16 palette letters. name and key make the tooltip ("Desk Snap (F3): on"),
        // and hint, if any, goes on a second line.
        public static Click Draw(string id, int order, string[] pixels, State state, string name, KeyCode key, string hint = null)
        {
            // Always first, so the button keeps the same control id from frame to frame.
            int control = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);
            Event e = Event.current;
            Refresh();

            if (!_inWorld)
            {
                Hide(control, e);
                UpdateBlocker(false, default);
                return Click.None;
            }

            Rect icon = Place(id, order, out Rect bar, out Metrics m);
            Share(bar);
            UpdateBlocker(_visible, bar);
            if (!_visible)
            {
                Hide(control, e);
                return Click.None;
            }

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                return Handle(control, e, icon, m, pixels, state, name, key, hint);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        // Hides the whole row for this frame and the next. Photo Mode calls it every frame while it's
        // on (before OnGUI). It runs out by itself, so the row can't get stuck hidden.
        public static void HideRow()
        {
            AppDomain.CurrentDomain.SetData(HiddenKey, Time.frameCount + 1);
        }

        private static bool RowHidden =>
            AppDomain.CurrentDomain.GetData(HiddenKey) is int until && Time.frameCount <= until;

        // " (F7)", or nothing when the key is switched off in the config.
        public static string KeyText(KeyCode key)
        {
            return key == KeyCode.None ? "" : $" ({key})";
        }

        // True while the row is showing and the mouse is on it. Any mod's copy can ask.
        public static bool MouseOver
        {
            get
            {
                if (!(AppDomain.CurrentDomain.GetData(RectKey) is double[] r) || r[5] < 0.5 || Time.frameCount - r[4] > 2)
                {
                    return false;
                }
                Vector3 mouse = Input.mousePosition;
                var rect = new Rect((float)r[0], (float)r[1], (float)r[2], (float)r[3]);
                return rect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            }
        }

        private static Click Handle(int control, Event e, Rect icon, Metrics m, string[] pixels, State state,
            string name, KeyCode key, string hint)
        {
            bool hover = icon.Contains(e.mousePosition);
            switch (e.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (hover && (e.button == 0 || e.button == 1))
                    {
                        GUIUtility.hotControl = control;
                        _pressedButton = e.button;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == control)
                    {
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == control)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                        if (hover && e.button == _pressedButton)
                        {
                            return e.button == 0 ? Click.Left : Click.Right;
                        }
                    }
                    break;

                case EventType.Repaint:
                    BuildStyles(m);
                    bool pressed = GUIUtility.hotControl == control;
                    _buttonStyle.normal.background = state == State.Open || (pressed && hover) ? _openTexture
                        : hover ? _hoverTexture : _buttonTexture;
                    _buttonStyle.Draw(icon, false, false, false, false);

                    Texture2D[] art = IconTextures(pixels);
                    var artRect = new Rect(icon.x + (icon.width - m.Art) / 2, icon.y + (icon.height - m.Art) / 2, m.Art, m.Art);
                    GUI.DrawTexture(artRect, state == State.Off ? art[1] : art[0]);

                    if (state == State.On)
                    {
                        float inset = Mathf.Max(1, Mathf.RoundToInt(2f * m.Unit));
                        float size = _dotTexture.width;
                        GUI.DrawTexture(new Rect(icon.xMax - inset - size, icon.yMax - inset - size, size, size), _dotTexture);
                    }

                    bool tip = hover && (GUIUtility.hotControl == 0 || pressed);
                    if (tip)
                    {
                        DrawTooltip(icon, m, Tooltip(state, name, key, hint));
                    }
                    GUI.depth = tip ? TooltipDepth : 0;
                    break;
            }
            return Click.None;
        }

        private static string Tooltip(State state, string name, KeyCode key, string hint)
        {
            string text = name + KeyText(key);
            if (state == State.On)
            {
                text += ": on";
            }
            else if (state == State.Off)
            {
                text += ": off";
            }
            return hint != null ? text + "\n" + hint : text;
        }

        // Above the icon, kept on screen.
        private static void DrawTooltip(Rect icon, Metrics m, string text)
        {
            var content = new GUIContent(text);
            Vector2 size = _tipStyle.CalcSize(content);
            float x = Mathf.Clamp(icon.center.x - size.x / 2f, m.Margin, Mathf.Max(m.Margin, Screen.width - m.Margin - size.x));
            float y = icon.y - m.Gap - size.y;
            _tipStyle.Draw(new Rect(Mathf.Round(x), Mathf.Round(y), size.x, size.y), content, false, false, false, false);
        }

        private static void Hide(int control, Event e)
        {
            // A button let go of while hidden (a menu opened mid-click) would keep every IMGUI control busy.
            if (GUIUtility.hotControl == control)
            {
                GUIUtility.hotControl = 0;
            }
            if (e.type == EventType.Repaint)
            {
                GUI.depth = 0;
            }
        }

        // ---- Layout ----

        private struct Metrics
        {
            public float Unit;   // screen pixels per 1080p unit
            public int Art;      // the icon's size: 16 art pixels, each a whole number of screen pixels
            public int Button;
            public int Gap;
            public int Margin;
        }

        // Laid out in whole screen pixels, so the pixel art stays crisp at any resolution.
        private static Metrics Measure()
        {
            float unit = Screen.height / 1080f;
            int scale = Mathf.Max(1, Mathf.RoundToInt(2f * unit));
            int pad = Mathf.Max(2, Mathf.RoundToInt(4f * unit));
            return new Metrics
            {
                Unit = unit,
                Art = 16 * scale,
                Button = 16 * scale + 2 * pad,
                Gap = Mathf.Max(2, Mathf.RoundToInt(6f * unit)),
                Margin = Mathf.RoundToInt(20f * unit),
            };
        }

        // Where this icon goes this frame. Icons with a lower order (or the same order and an earlier
        // id) sit to its left. An icon that stops being drawn drops out after a couple of frames.
        private static Rect Place(string id, int order, out Rect bar, out Metrics m)
        {
            Hashtable icons = Table();
            int frame = Time.frameCount;
            // Only built-in types go in the table: each mod's copy of this file is its own type.
            if (icons[id] is double[] mine)
            {
                mine[0] = order;
                mine[1] = frame;
            }
            else
            {
                icons[id] = new double[] { order, frame };
            }

            int index = 0;
            int count = 0;
            foreach (DictionaryEntry entry in icons)
            {
                var key = (string)entry.Key;
                var value = (double[])entry.Value;
                if (frame - value[1] > 2)
                {
                    continue;
                }
                count++;
                if (key != id && (value[0] < order || (value[0] == order && string.CompareOrdinal(key, id) < 0)))
                {
                    index++;
                }
            }

            m = Measure();
            int width = count * m.Button + (count - 1) * m.Gap;
            int x = Screen.width - m.Margin - width;
            int y = Screen.height - m.Margin - m.Button;
            bar = new Rect(x, y, width, m.Button);
            return new Rect(x + index * (m.Button + m.Gap), y, m.Button, m.Button);
        }

        private static void Share(Rect bar)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            int frame = Time.frameCount;
            if (domain.GetData(RectKey) is double[] old && (int)old[4] == frame && old[2] >= bar.width)
            {
                return;   // another mod already shared this frame's row
            }
            domain.SetData(RectKey, new double[] { bar.x, bar.y, bar.width, bar.height, frame, _visible ? 1 : 0 });
        }

        private static Hashtable Table()
        {
            if (!(AppDomain.CurrentDomain.GetData(TableKey) is Hashtable icons))
            {
                icons = new Hashtable();
                AppDomain.CurrentDomain.SetData(TableKey, icons);
            }
            return icons;
        }

        // Worked out once a frame. In a world MonoSingleton.I is a stored instance, so it's cheap there.
        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (_cachedFrame == frame)
            {
                return;
            }
            _cachedFrame = frame;
            _inWorld = SceneManager.GetActiveScene().buildIndex == WorldScene;
            _visible = false;
            if (_inWorld && !PanelStack.Hidden && !RowHidden)
            {
                TaskManager tasks = MonoSingleton<TaskManager>.I;
                // Free, or at an instrument or a drawing board. The other states are menus and panels.
                _visible = tasks != null && (tasks.LockState == LockState.Free || tasks.LockState == LockState.Music);
            }
        }

        // ---- Click blocker ----

        // An invisible piece of game UI under the row (see ClickBlocker.cs), shared by every mod and
        // moved once a frame. Its name starts with PanelStack.CanvasPrefix, so Photo Mode leaves it on.
        private static void UpdateBlocker(bool shown, Rect bar)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            int frame = Time.frameCount;
            if (domain.GetData(BlockerFrameKey) is int done && done == frame)
            {
                return;
            }
            domain.SetData(BlockerFrameKey, frame);

            var root = domain.GetData(BlockerKey) as GameObject;
            if (root == null)
            {
                if (!shown)
                {
                    return;
                }
                root = CreateBlocker();
                domain.SetData(BlockerKey, root);
            }
            if (shown)
            {
                var area = (RectTransform)root.transform.GetChild(0);
                // Game UI measures from the bottom-left.
                var position = new Vector2(bar.x, Screen.height - bar.yMax);
                if (area.anchoredPosition != position)
                {
                    area.anchoredPosition = position;
                }
                if (area.sizeDelta != bar.size)
                {
                    area.sizeDelta = bar.size;
                }
            }
            if (root.activeSelf != shown)
            {
                root.SetActive(shown);
            }
        }

        private static GameObject CreateBlocker()
        {
            var root = new GameObject(PanelStack.CanvasPrefix + "IconBar ClickBlocker");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();

            // A see-through image still catches clicks.
            var area = new GameObject("Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            area.transform.SetParent(root.transform, false);
            area.GetComponent<Image>().color = Color.clear;
            var rect = (RectTransform)area.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            root.SetActive(false);
            return root;
        }

        // ---- Patches ----

        // Call from Plugin.Awake. The first mod to call it patches the game for all of them:
        // - a click on the row doesn't also click the world behind it (players, pets, fishing),
        // - a right-click on the row doesn't turn the camera (which would lock the mouse mid-click),
        // - a click on the row doesn't paint the drawing board behind it (QuadPainterGPU reads the
        //   mouse itself, and chalk gets used up).
        public static void Patch()
        {
            AppDomain domain = AppDomain.CurrentDomain;
            if (domain.GetData(PatchedKey) != null)
            {
                return;
            }
            domain.SetData(PatchedKey, true);
            try
            {
                Patches.Apply(new Harmony("nelphei.ontogether.iconbar"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[OnTogetherMods] Icon bar patches failed, so clicks on it may reach the game: {e}");
            }
        }

        private static class Patches
        {
            private static Action<InputManager, bool> _setMouse0Down;
            private static Action<InputManager, bool> _setMouse1;
            private static Action<InputManager, bool> _setMouse1Up;
            private static AccessTools.FieldRef<QuadPainterGPU, Vector2> _previousUV;

            // Set when a press starts on the row, cleared once the button is let go.
            private static bool _leftOnBar;
            private static bool _rightOnBar;

            public static void Apply(Harmony harmony)
            {
                _setMouse0Down = Setter(nameof(InputManager.IsMouseButton0Down));
                _setMouse1 = Setter(nameof(InputManager.IsMouseButton1));
                _setMouse1Up = Setter(nameof(InputManager.IsMouseButton1Up));
                harmony.Patch(AccessTools.Method(typeof(InputManager), "Update"),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(InputPostfix)));

                _previousUV = AccessTools.FieldRefAccess<QuadPainterGPU, Vector2>("previousUV");
                harmony.Patch(AccessTools.Method(typeof(QuadPainterGPU), "Update"),
                    prefix: new HarmonyMethod(typeof(Patches), nameof(PaintPrefix)));
            }

            private static void InputPostfix(InputManager __instance)
            {
                bool over = MouseOver;
                if (Input.GetMouseButtonDown(0))
                {
                    _leftOnBar = over;
                }
                if (Input.GetMouseButtonDown(1))
                {
                    _rightOnBar = over;
                }

                // Only the press is hidden, so a click that started outside still ends.
                if (over)
                {
                    _setMouse0Down(__instance, false);
                }
                if (_rightOnBar)
                {
                    _setMouse1(__instance, false);
                    _setMouse1Up(__instance, false);
                }

                if (!Input.GetMouseButton(0))
                {
                    _leftOnBar = false;
                }
                if (!Input.GetMouseButton(1))
                {
                    _rightOnBar = false;
                }
            }

            // Skip painting while the mouse is on the row, and forget the last brush point so the
            // next stroke doesn't draw a line from where the mouse left the board.
            private static bool PaintPrefix(QuadPainterGPU __instance)
            {
                if (__instance.IsActive && (_leftOnBar || MouseOver))
                {
                    _previousUV(__instance).x = -1f;
                    return false;
                }
                return true;
            }

            // The properties have private setters, so bind to them once instead of reflecting every frame.
            private static Action<InputManager, bool> Setter(string property)
            {
                return (Action<InputManager, bool>)Delegate.CreateDelegate(
                    typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), property));
            }
        }

        // ---- Textures ----

        // The icon as drawn, and greyed out for "off".
        private static Texture2D[] IconTextures(string[] pixels)
        {
            if (Icons.TryGetValue(pixels, out Texture2D[] textures) && textures[0] != null && textures[1] != null)
            {
                return textures;
            }
            var on = new Color32[16 * 16];
            var off = new Color32[16 * 16];
            for (int row = 0; row < 16; row++)
            {
                string line = row < pixels.Length ? pixels[row] : "";
                for (int column = 0; column < 16; column++)
                {
                    char letter = column < line.Length ? line[column] : '.';
                    Color32 colour = Palette.TryGetValue(letter, out Color32 c) ? c : new Color32(0, 0, 0, 0);
                    // Textures count rows from the bottom.
                    int i = (15 - row) * 16 + column;
                    on[i] = colour;
                    byte grey = (byte)Mathf.RoundToInt(0.3f * colour.r + 0.59f * colour.g + 0.11f * colour.b);
                    off[i] = new Color32(grey, grey, grey, (byte)(colour.a * 0.45f));
                }
            }
            textures = new[] { NewTexture(16, 16, on, FilterMode.Point), NewTexture(16, 16, off, FilterMode.Point) };
            Icons[pixels] = textures;
            return textures;
        }

        private static void BuildStyles(Metrics m)
        {
            if (_builtFor == Screen.height && _buttonStyle != null && _buttonTexture != null && _dotTexture != null)
            {
                return;
            }
            _builtFor = Screen.height;
            int radius = Mathf.Max(2, Mathf.RoundToInt(6f * m.Unit));
            Destroy(_buttonTexture);
            Destroy(_hoverTexture);
            Destroy(_openTexture);
            Destroy(_dotTexture);
            _buttonTexture = RoundedBox(radius, ButtonFill, ButtonEdge);
            _hoverTexture = RoundedBox(radius, HoverFill, HoverEdge);
            _openTexture = RoundedBox(radius, OpenFill, OpenEdge);
            int dot = Mathf.Max(2, Mathf.RoundToInt(3f * m.Unit));
            _dotTexture = RoundedBox(dot, DotFill, DotEdge, 2 * dot);

            int border = radius + 1;
            _buttonStyle = new GUIStyle { border = new RectOffset(border, border, border, border) };
            _tipStyle = new GUIStyle
            {
                border = new RectOffset(border, border, border, border),
                padding = new RectOffset(Mathf.RoundToInt(10f * m.Unit), Mathf.RoundToInt(10f * m.Unit),
                    Mathf.RoundToInt(6f * m.Unit), Mathf.RoundToInt(6f * m.Unit)),
                fontSize = Mathf.Max(8, Mathf.RoundToInt(15f * m.Unit)),
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                richText = false,
            };
            _tipStyle.normal.background = _hoverTexture;
            _tipStyle.normal.textColor = Color.white;
        }

        // A rounded rectangle with a 1-pixel edge, smoothed. Its corners fit in a 9-slice border of
        // radius + 1. size 0 means a stretchable box; otherwise a square that size (a circle when
        // size = 2 * radius).
        private static Texture2D RoundedBox(int radius, Color32 fill, Color32 edge, int size = 0)
        {
            if (size <= 0)
            {
                size = 2 * radius + 4;
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // How far the pixel's centre is outside the rounded shape (negative inside).
                    float cx = x + 0.5f;
                    float cy = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Max(radius - cx, cx - (size - radius)), 0f);
                    float dy = Mathf.Max(Mathf.Max(radius - cy, cy - (size - radius)), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float cover = Mathf.Clamp01(0.5f - distance);
                    Color colour = Color.Lerp(fill, edge, Mathf.Clamp01(distance + 1.5f));
                    colour.a *= cover;
                    pixels[y * size + x] = colour;
                }
            }
            return NewTexture(size, size, pixels, FilterMode.Bilinear);
        }

        private static Texture2D NewTexture(int width, int height, Color32[] pixels, FilterMode filter)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp,
                // Kept across scene loads and never unloaded as unused.
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void Destroy(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
