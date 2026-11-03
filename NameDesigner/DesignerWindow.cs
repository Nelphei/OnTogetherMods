using System;
using System.Collections.Generic;
using System.Linq;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;

namespace NameDesigner
{
    // The Name Designer window. Its two tabs design your player name and the name of a session you
    // create. Type the name, pick letters, pick a colour, and the preview above the window shows it
    // the way the game will (TextMeshPro, like the name tags). Each tab has 5 preset slots.
    //
    // In a world it sits in the bottom window stack (Shared/PanelStack.cs). On the main menu, while
    // the Create Session box shows, it opens by itself beside that box. It's drawn with Unity's
    // immediate-mode GUI, laid out at 1080p and scaled to the screen.
    internal sealed class DesignerWindow : MonoBehaviour
    {
        private const string PanelId = "NameDesigner";
        private const string TextFieldName = "NameDesigner.Text";
        private const string HexFieldName = "NameDesigner.Hex";

        private const float Width = 500f;
        private const float Pad = 10f;
        private const float Gap = 6f;
        private const float HeaderHeight = 26f;
        private const float RowHeight = 30f;
        private const float LetterWidth = 28f;
        private const float LetterHeight = 38f;
        private const float LetterGap = 3f;
        private const float TipHeight = 18f;
        private const float ToolsHeight = 28f;
        private const float PickerSize = 150f;
        private const float StripWidth = 22f;
        private const float SwatchSize = 26f;
        private const float LabelHeight = 20f;
        private const float SaveHeight = 22f;
        private const float BoxGap = 20f;
        private const float StatusSeconds = 5f;
        private const float ConfirmSeconds = 3f;

        // Quick colours: bright, then pastel, then white and near-black.
        private static readonly string[] Swatches =
        {
            "FF5C5C", "FF9F43", "FFD93D", "6BCB77", "4D96FF", "9B5DE5", "F15BB5", "00BBF9",
            "FFADAD", "FFD6A5", "FDFFB6", "CAFFBF", "A0C4FF", "BDB2FF", "FFFFFF", "303030",
        };

        private enum Tab { Name, Session }

        // What one tab is editing.
        private sealed class TabState
        {
            public Design Design = new Design();
            // The letters picked for colouring, by index. None picked means the whole name.
            public readonly SortedSet<int> Picked = new SortedSet<int>();
            public int Anchor = -1;
        }

        private static DesignerWindow _instance;

        // Read by the patches. True while you type in the window, so the game ignores the keys.
        internal static bool IsTyping => _instance != null && _instance._open && _instance._typing;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly PresetStore _presets = new PresetStore();
        private readonly ColorPicker _picker = new ColorPicker();
        private readonly TabState _name = new TabState();
        private readonly TabState _session = new TabState();
        private readonly TextBox _preview = new TextBox("NameDesigner Preview", TextAlignmentOptions.Center);
        private ClickBlocker _blocker;

        private bool _open;
        private bool _iconClicked;
        private Tab _tab;
        private bool _autoOpened;
        private bool _boxWasShowing;
        private bool _nameLoaded;
        // What the Create Session box held after the window last wrote to it or read it.
        private string _sessionWritten;

        private string _hexText = "FFFFFF";
        private bool _typing;
        private bool _clearFocus;
        private int _dragFrom = -1;
        private int _confirmSlot = -1;
        private float _confirmUntil;
        private string _status;
        private float _statusUntil;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _letterStyle;
        private GUIStyle _presetStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _tipStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _statusStyle;

        private TabState Current => _tab == Tab.Name ? _name : _session;

        private void Awake()
        {
            _instance = this;
            _presets.Load();
            _blocker = new ClickBlocker("NameDesigner ClickBlocker");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
            _preview.Destroy();
            _picker.Destroy();
        }

        private void Update()
        {
            try
            {
                bool box = Game.SessionBoxShowing;
                if (box != _boxWasShowing)
                {
                    _boxWasShowing = box;
                    if (box)
                    {
                        BoxAppeared();
                    }
                    else if (_autoOpened)
                    {
                        Close();
                    }
                }

                // A click on the icon (Shared/IconBar.cs) from the last OnGUI.
                bool clicked = _iconClicked;
                _iconClicked = false;
                if ((Input.GetKeyDown(S.ToggleKey.Value) && !IsChatOpen()) || clicked)
                {
                    if (_open)
                    {
                        Close();
                    }
                    else
                    {
                        Open(_tab);
                    }
                }

                // Typing in the game's own box shows up here too.
                if (_open && _tab == Tab.Session && box)
                {
                    string text = Game.SessionField.text;
                    if (text != _sessionWritten)
                    {
                        ImportSession(text);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
            UpdateMouseOver();
        }

        private void LateUpdate()
        {
            _preview.LateUpdate();
        }

        private void BoxAppeared()
        {
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo(Game.SessionBoxBounds(out Rect bounds)
                    ? $"Create Session box at {bounds} (1080p units, screen {PanelStack.Width:0} wide)."
                    : "Create Session box is showing, but its spot on screen wasn't found.");
            }
            if (_open)
            {
                if (_tab == Tab.Session)
                {
                    StartSessionSync();
                }
            }
            else if (S.OpenWithCreateSession.Value)
            {
                Open(Tab.Session);
                _autoOpened = true;
            }
        }

        private void Open(Tab tab)
        {
            _open = true;
            _autoOpened = false;
            _status = null;
            if (!_nameLoaded)
            {
                LoadName();
                _nameLoaded = true;
            }
            SwitchTab(tab);
        }

        private void Close()
        {
            _open = false;
            _autoOpened = false;
            _typing = false;
            _dragFrom = -1;
            _confirmSlot = -1;
            _clearFocus = true;
        }

        private void SwitchTab(Tab tab)
        {
            _tab = tab;
            _dragFrom = -1;
            _confirmSlot = -1;
            // Both tabs share the name box, so stop typing in it.
            GUIUtility.keyboardControl = 0;
            if (tab == Tab.Session && Game.SessionBoxShowing)
            {
                StartSessionSync();
            }
            SyncPicker(Current);
        }

        private void UpdateMouseOver()
        {
            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        private static bool IsChatOpen()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // ---- Names ----

        // Returns false if some of the name's styling couldn't be kept (and says so).
        private bool LoadName()
        {
            _name.Design = Design.Parse(Game.CurrentName(), out bool dropped);
            ClearPicked(_name);
            if (dropped)
            {
                Status("Some styling in your name (like sizes) can't be edited here, so it's left out.");
            }
            return !dropped;
        }

        private void ApplyName()
        {
            if (_name.Design.IsBlank)
            {
                Status("Type a name first");
                return;
            }
            try
            {
                bool sent = Game.ApplyName(_name.Design.ToRichText());
                Status(sent ? "Name applied. Everyone sees it now." : "Name saved. Everyone sees it when you join a world.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't apply the name. See the BepInEx log.");
            }
        }

        // When the Create Session box shows up: an empty box gets the session name you were
        // designing, and a box with a name in it is read into the window.
        private void StartSessionSync()
        {
            string text = Game.SessionField.text;
            if (string.IsNullOrEmpty(text) && _session.Design.Count > 0)
            {
                WriteSession();
            }
            else
            {
                ImportSession(text);
            }
        }

        private void ImportSession(string text)
        {
            _session.Design = Design.Parse(text, out bool dropped);
            _sessionWritten = text;
            ClampPicked(_session);
            if (dropped)
            {
                Status("Some styling in the box (like sizes) can't be edited here, so it's left out.");
            }
        }

        private void WriteSession()
        {
            _sessionWritten = Game.WriteSession(_session.Design.ToRichText());
        }

        // Call after a tab's design changes. The session tab keeps the Create Session box up to date.
        private void Changed(TabState tab)
        {
            if (tab == _session && Game.SessionBoxShowing)
            {
                WriteSession();
            }
        }

        // ---- Letters and colours ----

        // The letters a colour goes on: the picked ones, or every letter when none are picked.
        private static List<int> Targets(TabState tab, bool skipBlank)
        {
            IEnumerable<int> all = tab.Picked.Count > 0 ? tab.Picked : Enumerable.Range(0, tab.Design.Count);
            return all.Where(i => i < tab.Design.Count && !(skipBlank && tab.Design.Letters[i].IsBlank)).ToList();
        }

        private void SetColour(TabState tab, string hex)
        {
            foreach (int i in Targets(tab, skipBlank: false))
            {
                tab.Design.Letters[i].Color = hex;
            }
            Changed(tab);
        }

        // Sets the picker to the colour of the first coloured letter it would change.
        private void SyncPicker(TabState tab)
        {
            foreach (int i in Targets(tab, skipBlank: true))
            {
                string hex = tab.Design.Letters[i].Color;
                if (hex != null)
                {
                    _picker.Set(Design.FromHex(hex));
                    _hexText = hex;
                    return;
                }
            }
        }

        // Blends from the first letter's colour to the last one's across the letters in between.
        private void Gradient(TabState tab)
        {
            List<int> targets = Targets(tab, skipBlank: true);
            if (targets.Count < 2)
            {
                Status("A gradient needs at least 2 letters");
                return;
            }
            Letter first = tab.Design.Letters[targets[0]];
            Letter last = tab.Design.Letters[targets[targets.Count - 1]];
            Color from = Design.FromHex(first.Color);
            Color to = Design.FromHex(last.Color);
            if (first.Color == last.Color)
            {
                Status("Give the first and last letters different colours, then click Gradient.");
                return;
            }
            for (int k = 0; k < targets.Count; k++)
            {
                tab.Design.Letters[targets[k]].Color = Design.ToHex(Color.Lerp(from, to, k / (targets.Count - 1f)));
            }
            Changed(tab);
        }

        // Red to violet across the letters, as strong and bright as the picker's colour (or full
        // strength when the picker is on white, grey or black).
        private void Rainbow(TabState tab)
        {
            List<int> targets = Targets(tab, skipBlank: true);
            if (targets.Count == 0)
            {
                return;
            }
            float s = _picker.S < 0.25f ? 0.75f : _picker.S;
            float v = _picker.V < 0.35f ? 1f : _picker.V;
            for (int k = 0; k < targets.Count; k++)
            {
                float hue = targets.Count == 1 ? 0f : 0.8f * k / (targets.Count - 1f);
                tab.Design.Letters[targets[k]].Color = Design.ToHex(Color.HSVToRGB(hue, s, v));
            }
            Changed(tab);
        }

        private static void ClearPicked(TabState tab)
        {
            tab.Picked.Clear();
            tab.Anchor = -1;
        }

        private static void ClampPicked(TabState tab)
        {
            tab.Picked.RemoveWhere(i => i >= tab.Design.Count);
            if (tab.Anchor >= tab.Design.Count)
            {
                tab.Anchor = -1;
            }
        }

        private static void PickRange(TabState tab, int a, int b)
        {
            tab.Picked.Clear();
            for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++)
            {
                tab.Picked.Add(i);
            }
        }

        // ---- Presets ----

        private Design[] Slots => _tab == Tab.Name ? _presets.Names : _presets.Sessions;

        private void LoadPreset(TabState tab, int slot)
        {
            Design preset = Slots[slot];
            if (preset == null)
            {
                return;
            }
            tab.Design = preset.Clone();
            tab.Design.Trim(Limit());
            ClearPicked(tab);
            SyncPicker(tab);
            Changed(tab);
            Status(_tab == Tab.Name ? $"Loaded preset {slot + 1}. Click Apply name to use it." : $"Loaded preset {slot + 1}");
        }

        private void SavePreset(TabState tab, int slot)
        {
            if (tab.Design.IsBlank)
            {
                Status("Type a name first");
                return;
            }
            // A full slot asks first: the button turns into "Replace?" for a moment.
            if (Slots[slot] != null && !(_confirmSlot == slot && Time.unscaledTime < _confirmUntil))
            {
                _confirmSlot = slot;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                return;
            }
            _confirmSlot = -1;
            Slots[slot] = tab.Design.Clone();
            Status(_presets.Save() ? $"Saved to preset {slot + 1}" : "Couldn't save the presets file. See the BepInEx log.");
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        private int Limit()
        {
            return _tab == Tab.Name ? Game.NameLimit() : Game.SessionLimit();
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value &&
                IconBar.Draw("NameDesigner", IconBar.NameDesignerOrder, Icon.Pixels,
                    _open ? IconBar.State.Open : IconBar.State.Plain, "Name Designer", S.ToggleKey.Value) == IconBar.Click.Left)
            {
                _iconClicked = true;   // acted on in Update
            }

            if (_clearFocus)
            {
                GUIUtility.keyboardControl = 0;
                _clearFocus = false;
            }
            // Hidden while Photo Mode takes a photo.
            if (!_open || PanelStack.Hidden)
            {
                return;
            }
            if (_boxStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out float screenWidth);
            try
            {
                DrawWindow(screenWidth);
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

        private void DrawWindow(float screenWidth)
        {
            TabState tab = Current;
            int limit = Limit();
            float inner = Width - 2 * Pad;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((inner + LetterGap) / (LetterWidth + LetterGap)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(tab.Design.Count / (float)perRow));
            float lettersHeight = rows * LetterHeight + (rows - 1) * LetterGap;
            float height = Pad + HeaderHeight + Gap + RowHeight + Gap + lettersHeight + 4f + TipHeight + Gap +
                ToolsHeight + 8f + PickerSize + 10f + LabelHeight + RowHeight + 3f + SaveHeight + 10f + RowHeight + Pad;

            // The preview sits just above the window. It has its own canvas, which the window's
            // background would cover, so they don't overlap.
            string styled = tab.Design.ToRichText();
            string previewText = tab.Design.Count > 0 ? styled : "<color=#FFFFFF66>(empty)</color>";
            _preview.Font = _tab == Tab.Session ? Game.SessionFont() ?? Game.NameFont() : Game.NameFont();
            Vector2 previewSize = _preview.Measure(previewText);
            previewSize.x = Mathf.Clamp(previewSize.x, 160f, Width);

            Rect area = Place(new Vector2(Width, previewSize.y + Gap + height), screenWidth);
            _preview.Draw(new Rect(area.x + (Width - previewSize.x) / 2f, area.y, previewSize.x, previewSize.y), previewText);
            var panel = new Rect(area.x, area.y + previewSize.y + Gap, Width, height);

            float scale = Screen.height / 1080f;
            _screenRect = new Rect(area.x * scale, area.y * scale, area.width * scale, area.height * scale);
            _drawnFrame = Time.frameCount;

            HandleKeys();
            HandleClickAway(area);

            GUI.Box(panel, GUIContent.none, _boxStyle);
            float x = panel.x + Pad;
            float y = panel.y + Pad;

            DrawHeader(new Rect(x, y, inner, HeaderHeight));
            y += HeaderHeight + Gap;
            DrawTextRow(new Rect(x, y, inner, RowHeight), tab, limit);
            y += RowHeight + Gap;
            DrawLetters(new Rect(x, y, inner, lettersHeight), tab, perRow);
            y += lettersHeight + 4f;
            GUI.Label(new Rect(x, y, inner, TipHeight), PickTip(tab), _tipStyle);
            y += TipHeight + Gap;
            DrawTools(new Rect(x, y, inner, ToolsHeight), tab);
            y += ToolsHeight + 8f;
            DrawPicker(new Rect(x, y, inner, PickerSize), tab);
            y += PickerSize + 10f;
            DrawPresets(new Rect(x, y, inner, LabelHeight + RowHeight + 3f + SaveHeight), tab);
            y += LabelHeight + RowHeight + 3f + SaveHeight + 10f;
            DrawAction(new Rect(x, y, inner, RowHeight), tab);

            string focused = GUI.GetNameOfFocusedControl();
            _typing = focused == TextFieldName || focused == HexFieldName;
        }

        // Beside the Create Session box while it shows (right of it, or left if there's no room),
        // otherwise in the bottom window stack.
        private static Rect Place(Vector2 size, float screenWidth)
        {
            if (Game.SessionBoxShowing && Game.SessionBoxBounds(out Rect box))
            {
                float x = box.xMax + BoxGap;
                if (x + size.x > screenWidth - 10f)
                {
                    x = box.x - BoxGap - size.x;
                }
                if (x >= 10f)
                {
                    float y = Mathf.Clamp(box.center.y - size.y / 2f, 10f, Mathf.Max(10f, 1080f - size.y - 10f));
                    return new Rect(x, y, size.x, size.y);
                }
            }
            return PanelStack.Place(PanelId, PanelStack.NameDesignerOrder, size);
        }

        // Enter or Esc stops typing. The game doesn't see either (see Patches.cs).
        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && _typing &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
            }
        }

        // Clicking outside the window stops typing in it.
        private static void HandleClickAway(Rect area)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !area.Contains(e.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
            }
        }

        private void DrawHeader(Rect row)
        {
            GUI.Label(row, "<b>NAME DESIGNER</b>", _titleStyle);
            const float tabWidth = 84f;
            var nameTab = new Rect(row.x + 150f, row.y, tabWidth, row.height);
            var sessionTab = new Rect(nameTab.xMax + 4f, row.y, tabWidth, row.height);
            if (GUI.Toggle(nameTab, _tab == Tab.Name, "Name", _tabStyle) && _tab != Tab.Name)
            {
                SwitchTab(Tab.Name);
            }
            if (GUI.Toggle(sessionTab, _tab == Tab.Session, "Session", _tabStyle) && _tab != Tab.Session)
            {
                SwitchTab(Tab.Session);
            }
            GUI.Label(row, $"{S.ToggleKey.Value} to close", _keyStyle);
        }

        private void DrawTextRow(Rect row, TabState tab, int limit)
        {
            const float buttonWidth = 90f;
            var field = new Rect(row.x, row.y, row.width - buttonWidth - Gap, row.height);
            string plain = tab.Design.Plain;
            GUI.SetNextControlName(TextFieldName);
            // The box counts characters, and a letter like an emoji takes two, so it allows double.
            // Design.SetText keeps the real letter count to the limit.
            string typed = GUI.TextField(field, plain, limit * 2, _fieldStyle);
            if (typed != plain)
            {
                tab.Design.SetText(typed, limit);
                ClampPicked(tab);
                Changed(tab);
            }

            var button = new Rect(row.xMax - buttonWidth, row.y, buttonWidth, row.height);
            if (_tab == Tab.Name)
            {
                if (GUI.Button(button, "Reset", _buttonStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    if (LoadName())
                    {
                        Status("Back to your current name");
                    }
                    SyncPicker(_name);
                }
            }
            else
            {
                GUI.enabled = tab.Design.Count > 0;
                if (GUI.Button(button, "Clear", _buttonStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    tab.Design = new Design();
                    ClearPicked(tab);
                    Changed(tab);
                }
                GUI.enabled = true;
            }
        }

        // The letters as little tiles with their colour underneath. Click a letter to pick it,
        // drag across letters to pick a run, Shift-click for a range, Ctrl-click to add or remove
        // one. Clicking the only picked letter again, or the empty space, goes back to the whole name.
        private void DrawLetters(Rect area, TabState tab, int perRow)
        {
            int id = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);
            Event e = Event.current;
            List<Letter> letters = tab.Design.Letters;

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !area.Contains(e.mousePosition))
                    {
                        break;
                    }
                    GUIUtility.keyboardControl = 0;
                    int hit = LetterAt(area, perRow, letters.Count, e.mousePosition, nearest: false);
                    if (hit < 0)
                    {
                        ClearPicked(tab);
                    }
                    else if (e.shift && tab.Anchor >= 0)
                    {
                        PickRange(tab, tab.Anchor, hit);
                    }
                    else if (e.control || e.command)
                    {
                        if (!tab.Picked.Remove(hit))
                        {
                            tab.Picked.Add(hit);
                        }
                        tab.Anchor = hit;
                    }
                    else if (tab.Picked.Count == 1 && tab.Picked.Contains(hit))
                    {
                        ClearPicked(tab);
                    }
                    else
                    {
                        PickRange(tab, hit, hit);
                        tab.Anchor = hit;
                        _dragFrom = hit;
                        GUIUtility.hotControl = id;
                    }
                    SyncPicker(tab);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && _dragFrom >= 0)
                    {
                        int over = LetterAt(area, perRow, letters.Count, e.mousePosition, nearest: true);
                        if (over >= 0)
                        {
                            PickRange(tab, _dragFrom, over);
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        _dragFrom = -1;
                        e.Use();
                    }
                    break;
            }

            if (e.type != EventType.Repaint)
            {
                return;
            }
            if (letters.Count == 0)
            {
                GUI.Label(area, "Type a name above", _hintStyle);
                return;
            }
            for (int i = 0; i < letters.Count; i++)
            {
                Rect tile = LetterRect(area, perRow, i);
                bool picked = tab.Picked.Contains(i);
                ColorPicker.Fill(tile, picked ? new Color(0.3f, 0.55f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.1f));

                Letter letter = letters[i];
                _letterStyle.normal.textColor = letter.Color != null ? Design.FromHex(letter.Color) : Color.white;
                GUI.Label(new Rect(tile.x, tile.y, tile.width, tile.height - 8f), letter.IsBlank ? "·" : letter.Text, _letterStyle);

                var bar = new Rect(tile.x + 3f, tile.yMax - 7f, tile.width - 6f, 4f);
                if (letter.Color != null)
                {
                    ColorPicker.Fill(bar, Design.FromHex(letter.Color));
                }
                else
                {
                    // No colour of its own: the game's text colour.
                    ColorPicker.Outline(bar, new Color(1f, 1f, 1f, 0.35f));
                }
            }
        }

        private static Rect LetterRect(Rect area, int perRow, int index)
        {
            int col = index % perRow;
            int row = index / perRow;
            return new Rect(area.x + col * (LetterWidth + LetterGap), area.y + row * (LetterHeight + LetterGap),
                LetterWidth, LetterHeight);
        }

        // The letter under the mouse, or -1. With `nearest`, the closest letter even between tiles
        // or past the ends (for dragging).
        private static int LetterAt(Rect area, int perRow, int count, Vector2 mouse, bool nearest)
        {
            if (count == 0)
            {
                return -1;
            }
            if (!nearest)
            {
                for (int i = 0; i < count; i++)
                {
                    if (LetterRect(area, perRow, i).Contains(mouse))
                    {
                        return i;
                    }
                }
                return -1;
            }
            int rows = (count + perRow - 1) / perRow;
            int row = Mathf.Clamp(Mathf.FloorToInt((mouse.y - area.y) / (LetterHeight + LetterGap)), 0, rows - 1);
            int col = Mathf.Clamp(Mathf.FloorToInt((mouse.x - area.x) / (LetterWidth + LetterGap)), 0, perRow - 1);
            return Mathf.Min(row * perRow + col, count - 1);
        }

        private static string PickTip(TabState tab)
        {
            if (tab.Design.Count == 0)
            {
                return "";
            }
            int n = tab.Picked.Count;
            return n == 0
                ? "Colouring the whole name. Click letters to colour just those."
                : $"Colouring {n} letter{(n == 1 ? "" : "s")}. Shift-click for a range, Ctrl-click to add one.";
        }

        private void DrawTools(Rect row, TabState tab)
        {
            float w = (row.width - 3 * Gap) / 4f;
            bool any = tab.Design.Count > 0;

            GUI.enabled = tab.Picked.Count > 0;
            if (GUI.Button(new Rect(row.x, row.y, w, row.height), "Whole name", _buttonStyle))
            {
                ClearPicked(tab);
                SyncPicker(tab);
            }
            GUI.enabled = any;
            if (GUI.Button(new Rect(row.x + (w + Gap), row.y, w, row.height), "Gradient", _buttonStyle))
            {
                Gradient(tab);
            }
            if (GUI.Button(new Rect(row.x + 2 * (w + Gap), row.y, w, row.height), "Rainbow", _buttonStyle))
            {
                Rainbow(tab);
            }
            if (GUI.Button(new Rect(row.x + 3 * (w + Gap), row.y, w, row.height), "No colour", _buttonStyle))
            {
                SetColour(tab, null);
            }
            GUI.enabled = true;
        }

        private void DrawPicker(Rect area, TabState tab)
        {
            var square = new Rect(area.x, area.y, PickerSize, PickerSize);
            var strip = new Rect(square.xMax + 8f, area.y, StripWidth, PickerSize);
            if (_picker.Draw(square, strip))
            {
                _hexText = Design.ToHex(_picker.Value);
                SetColour(tab, _hexText);
            }

            float x = strip.xMax + 14f;
            float w = area.xMax - x;
            float y = area.y;

            // The colour as a hex code, to type or copy.
            GUI.Label(new Rect(x, y, 60f, 26f), "Colour  #", _labelStyle);
            GUI.SetNextControlName(HexFieldName);
            string typed = GUI.TextField(new Rect(x + 64f, y, 86f, 26f), _hexText ?? "", 7, _fieldStyle);
            if (typed != _hexText)
            {
                _hexText = typed;
                string hex = typed.TrimStart('#').Length == 6 ? Design.NormalizeHex(typed) : null;
                if (hex != null)
                {
                    _picker.Set(Design.FromHex(hex));
                    SetColour(tab, hex);
                }
            }
            var current = new Rect(x + 158f, y, 40f, 26f);
            ColorPicker.Fill(current, _picker.Value);
            ColorPicker.Outline(current, new Color(1f, 1f, 1f, 0.5f));
            y += 26f + 8f;

            const int perRow = 8;
            for (int i = 0; i < Swatches.Length; i++)
            {
                var swatch = new Rect(x + (i % perRow) * (SwatchSize + 6f), y + (i / perRow) * (SwatchSize + 6f), SwatchSize, SwatchSize);
                Color colour = Design.FromHex(Swatches[i]);
                if (GUI.Button(swatch, GUIContent.none, GUIStyle.none))
                {
                    GUIUtility.keyboardControl = 0;
                    _picker.Set(colour);
                    _hexText = Swatches[i];
                    SetColour(tab, Swatches[i]);
                }
                ColorPicker.Fill(swatch, colour);
                ColorPicker.Outline(swatch, new Color(0f, 0f, 0f, 0.6f));
            }
            y += 2 * (SwatchSize + 6f) + 2f;

            GUI.Label(new Rect(x, y, w, area.yMax - y), "Drag in the square and the strip, or click a swatch.", _hintStyle);
        }

        private void DrawPresets(Rect area, TabState tab)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, LabelHeight), "<b>PRESETS</b>", _titleStyle);
            GUI.Label(new Rect(area.x, area.y, area.width, LabelHeight), "Click one to load it", _keyStyle);

            Design[] slots = Slots;
            float w = (area.width - (PresetStore.Slots - 1) * Gap) / PresetStore.Slots;
            float loadY = area.y + LabelHeight;
            float saveY = loadY + RowHeight + 3f;
            if (_confirmSlot >= 0 && Time.unscaledTime >= _confirmUntil)
            {
                _confirmSlot = -1;
            }
            for (int i = 0; i < PresetStore.Slots; i++)
            {
                float px = area.x + i * (w + Gap);
                Design preset = slots[i];
                GUI.enabled = preset != null;
                if (GUI.Button(new Rect(px, loadY, w, RowHeight), preset != null ? preset.ToGuiText() : "Empty", _presetStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    LoadPreset(tab, i);
                }
                GUI.enabled = tab.Design.Count > 0;
                string save = _confirmSlot == i ? "Replace?" : "Save " + (i + 1);
                if (GUI.Button(new Rect(px, saveY, w, SaveHeight), save, _buttonStyle))
                {
                    SavePreset(tab, i);
                }
                GUI.enabled = true;
            }
        }

        private void DrawAction(Rect row, TabState tab)
        {
            bool showStatus = _status != null && Time.unscaledTime < _statusUntil;
            if (_tab == Tab.Name)
            {
                const float buttonWidth = 140f;
                bool same = tab.Design.ToRichText() == Game.SavedName();
                GUI.enabled = !tab.Design.IsBlank && !same;
                if (GUI.Button(new Rect(row.x, row.y, buttonWidth, row.height), "Apply name", _buttonStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    ApplyName();
                }
                GUI.enabled = true;
                var note = new Rect(row.x + buttonWidth + 10f, row.y, row.width - buttonWidth - 10f, row.height);
                string text = showStatus ? _status
                    : tab.Design.IsBlank ? "Type a name first"
                    : same ? "This is your name now"
                    : "Not applied yet";
                GUI.Label(note, text, showStatus ? _statusStyle : _tipStyle);
            }
            else
            {
                string text = showStatus ? _status
                    : Game.SessionBoxShowing ? "The Create Session box uses this name."
                    : "Open Create Session on the main menu to use this name.";
                GUI.Label(row, text, showStatus ? _statusStyle : _tipStyle);
            }
        }

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.65f);

            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, dim, TextAnchor.MiddleRight);
            _labelStyle = Label(14, Color.white, TextAnchor.MiddleLeft);
            _tipStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _hintStyle = Label(13, dim, TextAnchor.UpperLeft);
            _hintStyle.wordWrap = true;
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _letterStyle = Label(18, Color.white, TextAnchor.MiddleCenter);
            _letterStyle.clipping = TextClipping.Overflow;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                padding = new RectOffset(4, 4, 2, 2),
            };
            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                padding = new RectOffset(4, 4, 2, 2),
            };
            // Preset names show their colours.
            _presetStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                richText = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 6, 2, 2),
            };
            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
                richText = false,
                clipping = TextClipping.Clip,
            };
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
}
