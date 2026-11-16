using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FocusAnywhere
{
    // The objects window (right-click the icon): which of an activity's objects show while you focus.
    // Three quick setups, a switch per group (Choices), exceptions for the objects of the activity
    // you're doing right now, other activities' objects to add to it ("Add item"), and two of the
    // game's own settings that only change your screen.
    // It sits in the bottom window stack (Shared/PanelStack.cs), drawn with Unity's immediate-mode
    // GUI at 1080p and scaled to the screen, and it shows only while the icon row does (in the world,
    // no game menu open), so it never sits over the game's Settings menu.
    internal sealed class ObjectsWindow : MonoBehaviour
    {
        private const string PanelId = "FocusAnywhere";
        private const float Width = 340f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 22f;
        private const float ButtonHeight = 26f;
        private const float SectionHeight = 20f;
        private const float Gap = 2f;
        private const float SectionGap = 8f;
        private const float ButtonGap = 6f;
        private const float ToggleHeight = 18f;
        private const float ResetWidth = 46f;
        private const float AddButtonWidth = 104f;
        private const float AddedTagWidth = 60f;

        // The "Add item" list: two columns in a box that scrolls once it's taller than this.
        private const float PickRowHeight = 20f;
        private const float MaxPickerHeight = 250f;
        private const float ScrollbarWidth = 16f;
        private const float ColumnGap = 6f;

        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        private static ObjectsWindow _instance;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private bool _open;
        private bool _canShow;

        // The "Add item" list is open, and how far it's scrolled.
        private bool _picking;
        private Vector2 _pickScroll;

        // Clicks from OnGUI, acted on in Update.
        private readonly List<Action> _clicks = new List<Action>();

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;

        // An invisible piece of game UI under the window (see Shared/ClickBlocker.cs).
        private ClickBlocker _blocker;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _noteStyle;
        private GUIStyle _offStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _dimRightStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _pickStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _pickedStyle;
        private GUIStyle _resetStyle;

        // Opens or closes the window. Called from the icon's right-click.
        public static void Toggle()
        {
            if (_instance != null)
            {
                _instance._open = !_instance._open;
            }
        }

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "FocusAnywhere ClickBlocker");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
        }

        private void Update()
        {
            try
            {
                foreach (Action click in _clicks)
                {
                    click();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                _clicks.Clear();
            }

            // The list belongs to the activity you're doing, so it closes when you stand up.
            if (!NoObjects.Focusing)
            {
                _picking = false;
            }

            _canShow = false;
            if (_open && SceneManager.GetActiveScene().buildIndex == WorldScene)
            {
                // The same rule as the icon row: free, or at an instrument or a drawing board.
                TaskManager tasks = MonoSingleton<TaskManager>.I;
                _canShow = tasks != null && (tasks.LockState == LockState.Free || tasks.LockState == LockState.Music);
            }

            bool shown = _canShow && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // Hidden while Photo Mode takes a photo and while the game's eye button hides the UI.
            if (!_canShow || PanelStack.Hidden)
            {
                return;
            }
            if (_boxStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out _);
            try
            {
                DrawWindow();
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                PanelStack.End(previous);
            }
        }

        private void DrawWindow()
        {
            // The activity's own objects, for the "Right now" list. The desk, floaties and Zzz have
            // their own switch above it. Then the objects added to it.
            List<NoObjects.Item> items = NoObjects.Current
                .Where(item => !item.Added && (item.Part == Part.Placed || item.Part == Part.Hands || item.Part == Part.Headphones))
                .ToList();
            List<NoObjects.Item> added = NoObjects.Current.Where(item => item.Added).ToList();
            int nowRows = Mathf.Max(1, items.Count + added.Count);
            string activity = NoObjects.Activity;
            bool canAdd = NoObjects.Focusing && activity != null && NoObjects.Addable.Count > 0;
            bool picking = _picking && canAdd;
            List<KeyValuePair<Spot, List<NoObjects.Item>>> groups = picking ? PickGroups() : null;
            float pickContent = picking ? PickContentHeight(groups) : 0f;
            float pickHeight = Mathf.Min(pickContent, MaxPickerHeight);
            bool? spin = Game.FloatieSpin;
            bool? turning = Game.CameraTurning;
            int screenRows = (spin.HasValue ? 1 : 0) + (turning.HasValue ? 1 : 0);

            float height = Pad + HeaderHeight + Gap + RowHeight + Gap + ButtonHeight
                + SectionGap + Rows(Parts.All.Length)
                + SectionGap + SectionHeight + Rows(nowRows)
                + (canAdd ? ButtonGap + ButtonHeight : 0f)
                + (picking ? Gap + pickHeight : 0f)
                + (screenRows > 0 ? SectionGap + SectionHeight + Rows(screenRows) : 0f)
                + Pad;

            Rect panel = PanelStack.Place(PanelId, PanelStack.FocusAnywhereOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            GUI.Box(panel, GUIContent.none, _boxStyle);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>FOCUS OBJECTS</b>", _titleStyle);
            GUI.Label(new Rect(x, y, w, HeaderHeight), "Right-click the icon to close", _keyStyle);
            y += HeaderHeight + Gap;

            if (S.Enabled.Value)
            {
                GUI.Label(new Rect(x, y, w, RowHeight), "Ticked = shown. Everyone sees your changes.", _noteStyle);
            }
            else
            {
                GUI.Label(new Rect(x, y, w, RowHeight), "Focus Anywhere is off, so the game's own objects show.", _offStyle);
            }
            y += RowHeight + Gap;

            DrawPresets(new Rect(x, y, w, ButtonHeight));
            y += ButtonHeight + SectionGap;

            foreach (Part part in Parts.All)
            {
                Part p = part;
                ToggleRow(new Rect(x, y, w, RowHeight), Choices.GroupShown(p), Parts.Name(p), Parts.Help(p),
                    now => Choices.SetGroup(p, now));
                y += RowHeight + Gap;
            }

            y += SectionGap - Gap;
            GUI.Label(new Rect(x, y, w, SectionHeight), "RIGHT NOW", _sectionStyle);
            y += SectionHeight;
            if (items.Count + added.Count == 0)
            {
                string message = NoObjects.Focusing
                    ? "This activity has no objects of its own."
                    : "Start an activity to list its objects here.";
                GUI.Label(new Rect(x, y, w, RowHeight), message, _dimStyle);
                y += RowHeight + Gap;
            }
            foreach (NoObjects.Item item in items)
            {
                ObjectRow(new Rect(x, y, w, RowHeight), item);
                y += RowHeight + Gap;
            }
            foreach (NoObjects.Item item in added)
            {
                AddedRow(new Rect(x, y, w, RowHeight), item, activity);
                y += RowHeight + Gap;
            }

            if (canAdd)
            {
                y += ButtonGap - Gap;
                if (GUI.Button(new Rect(x, y, AddButtonWidth, ButtonHeight), picking ? "Done" : "Add item...", _buttonStyle))
                {
                    bool open = !picking;
                    _clicks.Add(() =>
                    {
                        _picking = open;
                        _pickScroll = Vector2.zero;
                    });
                }
                string hint = picking ? "Ticked = added to this activity" : "from other activities";
                GUI.Label(new Rect(x + AddButtonWidth + 8f, y, w - AddButtonWidth - 8f, ButtonHeight), hint, _dimStyle);
                y += ButtonHeight + Gap;
            }
            if (picking)
            {
                DrawPicker(new Rect(x, y, w, pickHeight), groups, pickContent, activity);
                y += pickHeight + Gap;
            }

            if (screenRows > 0)
            {
                y += SectionGap - Gap;
                GUI.Label(new Rect(x, y, w, SectionHeight), "ONLY ON YOUR SCREEN (GAME SETTINGS)", _sectionStyle);
                y += SectionHeight;
                if (spin.HasValue)
                {
                    ToggleRow(new Rect(x, y, w, RowHeight), spin.Value, "Floatie spin", "everyone's floaties",
                        Game.SetFloatieSpin);
                    y += RowHeight + Gap;
                }
                if (turning.HasValue)
                {
                    ToggleRow(new Rect(x, y, w, RowHeight), turning.Value, "Camera turning", "while you focus",
                        Game.SetCameraTurning);
                }
            }
        }

        private static float Rows(int count) => count * RowHeight + Mathf.Max(0, count - 1) * Gap;

        private void DrawPresets(Rect r)
        {
            Choices.Preset? current = Choices.Current();
            float width = (r.width - 2 * ButtonGap) / 3f;
            PresetButton(new Rect(r.x, r.y, width, r.height), "Show all", Choices.Preset.All, current);
            PresetButton(new Rect(r.x + width + ButtonGap, r.y, width, r.height), "Hands only", Choices.Preset.HandsOnly, current);
            PresetButton(new Rect(r.x + 2 * (width + ButtonGap), r.y, width, r.height), "Hide all", Choices.Preset.None, current);
        }

        private void PresetButton(Rect r, string text, Choices.Preset preset, Choices.Preset? current)
        {
            // The setup the switches match looks pressed in.
            if (GUI.Button(r, text, current == preset ? _pickedStyle : _buttonStyle))
            {
                _clicks.Add(() => Choices.Apply(preset));
            }
        }

        // A switch with its name, and a hint after it.
        private float ToggleRow(Rect r, bool on, string text, string help, Action<bool> set)
        {
            string label = " " + text;
            float width = Mathf.Min(_toggleStyle.CalcSize(new GUIContent(label)).x, r.width);
            bool now = GUI.Toggle(new Rect(r.x, r.y + (r.height - ToggleHeight) / 2f, width, ToggleHeight), on, label, _toggleStyle);
            if (now != on)
            {
                _clicks.Add(() => set(now));
            }
            if (!string.IsNullOrEmpty(help))
            {
                GUI.Label(new Rect(r.x + width + 8f, r.y, Mathf.Max(0f, r.width - width - 8f), r.height), help, _dimStyle);
            }
            return width;
        }

        // One of the activity's objects: ticked if it shows. On the right, its group, or, when it has an
        // exception, what the exception says and a button to go back to the group.
        private void ObjectRow(Rect r, NoObjects.Item item)
        {
            Part part = item.Part;
            string key = item.Name;
            bool? exception = Choices.Exception(part, key);
            ToggleRow(new Rect(r.x, r.y, r.width - ResetWidth - 100f, r.height), Choices.Shown(part, key), item.Label, null,
                now => Choices.SetObject(part, key, now));

            if (exception.HasValue)
            {
                var reset = new Rect(r.xMax - ResetWidth, r.y + 2f, ResetWidth, r.height - 4f);
                if (GUI.Button(reset, "reset", _resetStyle))
                {
                    _clicks.Add(() => Choices.ClearException(key));
                }
                GUI.Label(new Rect(r.x, r.y, r.width - ResetWidth - 6f, r.height),
                    exception.Value ? "always shown" : "always hidden", _tagStyle);
            }
            else
            {
                GUI.Label(new Rect(r.x, r.y, r.width, r.height), Parts.Short(part), _dimRightStyle);
            }
        }

        // An object added to this activity: ticked, and unticking takes it off again.
        private void AddedRow(Rect r, NoObjects.Item item, string activity)
        {
            string key = item.Name;
            ToggleRow(new Rect(r.x, r.y, r.width - AddedTagWidth, r.height), true, item.Label, null,
                now => Choices.SetAdded(activity, key, now));
            GUI.Label(new Rect(r.x, r.y, r.width, r.height), "added", _tagStyle);
        }

        // The objects that can be added, under a heading for each hand, the head, set down and the rest.
        private static List<KeyValuePair<Spot, List<NoObjects.Item>>> PickGroups()
        {
            var groups = new List<KeyValuePair<Spot, List<NoObjects.Item>>>();
            foreach (Spot spot in Parts.Spots)
            {
                List<NoObjects.Item> list = NoObjects.Addable.Where(item => item.Spot == spot).ToList();
                if (list.Count > 0)
                {
                    groups.Add(new KeyValuePair<Spot, List<NoObjects.Item>>(spot, list));
                }
            }
            return groups;
        }

        private static float PickRows(int count) => count * PickRowHeight + Mathf.Max(0, count - 1) * Gap;

        private static float PickContentHeight(List<KeyValuePair<Spot, List<NoObjects.Item>>> groups)
        {
            float height = 0f;
            foreach (KeyValuePair<Spot, List<NoObjects.Item>> group in groups)
            {
                height += SectionHeight + PickRows((group.Value.Count + 1) / 2) + SectionGap;
            }
            return Mathf.Max(0f, height - SectionGap);
        }

        private void DrawPicker(Rect r, List<KeyValuePair<Spot, List<NoObjects.Item>>> groups, float contentHeight, string activity)
        {
            bool scrolls = contentHeight > r.height;
            var content = new Rect(0f, 0f, r.width - (scrolls ? ScrollbarWidth : 0f), contentHeight);
            _pickScroll = GUI.BeginScrollView(r, _pickScroll, content);
            try
            {
                float column = (content.width - ColumnGap) / 2f;
                float y = 0f;
                foreach (KeyValuePair<Spot, List<NoObjects.Item>> group in groups)
                {
                    GUI.Label(new Rect(0f, y, content.width, SectionHeight), Parts.SpotName(group.Key), _sectionStyle);
                    y += SectionHeight;
                    bool hand = group.Key == Spot.LeftHand || group.Key == Spot.RightHand;
                    for (int i = 0; i < group.Value.Count; i++)
                    {
                        NoObjects.Item item = group.Value[i];
                        var cell = new Rect((i % 2) * (column + ColumnGap), y + (i / 2) * (PickRowHeight + Gap), column, PickRowHeight);
                        string label = " " + (hand ? Parts.ShortLabel(item.Label) : item.Label);
                        bool on = Choices.IsAdded(activity, item.Name);
                        bool now = GUI.Toggle(cell, on, label, _pickStyle);
                        if (now != on)
                        {
                            string key = item.Name;
                            _clicks.Add(() => Choices.SetAdded(activity, key, now));
                        }
                    }
                    y += PickRows((group.Value.Count + 1) / 2) + SectionGap;
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private void MakeStyles()
        {
            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleRight);
            _noteStyle = Label(14, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleLeft);
            _offStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _sectionStyle = Label(12, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerLeft);
            _dimStyle = Label(13, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
            _dimRightStyle = Label(13, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);
            _tagStyle = Label(13, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleRight);

            _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 14, wordWrap = false, clipping = TextClipping.Clip };
            _pickStyle = new GUIStyle(_toggleStyle) { fontSize = 13 };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            // The preset the switches match looks pressed in.
            _pickedStyle = new GUIStyle(_buttonStyle);
            _pickedStyle.normal = GUI.skin.button.onNormal;
            _pickedStyle.hover = GUI.skin.button.onHover;
            _pickedStyle.active = GUI.skin.button.onActive;
            _resetStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(2, 2, 1, 1) };
        }

        private static GUIStyle Label(int size, Color color, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = alignment,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = color;
            return style;
        }
    }

    // A click on the window shouldn't also click the world behind it (pets, players, the fishing rod).
    // Only the press is hidden, so a click that started outside still ends.
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class WindowInputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down = (Action<InputManager, bool>)Delegate.CreateDelegate(
            typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Down)));

        private static void Postfix(InputManager __instance)
        {
            if (ObjectsWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }
    }

    // Nor paint the chalkboard or whiteboard behind it (QuadPainterGPU reads the mouse itself, and
    // chalk gets used up). The last brush point is forgotten, so the next stroke doesn't draw a line
    // from where the mouse left the board.
    [HarmonyPatch(typeof(QuadPainterGPU), "Update")]
    internal static class WindowPaintPatch
    {
        private static readonly AccessTools.FieldRef<QuadPainterGPU, Vector2> PreviousUV =
            AccessTools.FieldRefAccess<QuadPainterGPU, Vector2>("previousUV");

        private static bool Prefix(QuadPainterGPU __instance)
        {
            if (__instance.IsActive && ObjectsWindow.IsMouseOver)
            {
                PreviousUV(__instance).x = -1f;
                return false;
            }
            return true;
        }
    }
}
