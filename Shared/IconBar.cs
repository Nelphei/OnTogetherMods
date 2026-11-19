using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnTogetherMods.Shared
{
    // A row of small pixel-art icons in the bottom-right corner, one for each installed mod, with the
    // tray button at its right end. Clicking an icon does what the mod's key does, and hovering shows
    // the mod's name and key. Icons the player tucks away go in the tray, which pops up above the tray
    // button. Right-clicking the tray button (or Arrange icons in the tray) starts arranging: drag icons
    // to reorder them or move them between the row and the tray, or click one to tuck it away or put it
    // back. The arrangement is saved in BepInEx\config\IconRow.json.
    //
    // Like PanelStack, every mod compiles its own copy of this file, so whatever the copies share is
    // kept in AppDomain data, using built-in types only. Each mod calls Draw() first thing in OnGUI,
    // every frame: that notes its icon (picture, state, tooltip) in a shared table and hands back its
    // clicks. One host object, made by the copy with the newest HostVersion, lays out and draws the
    // whole row from its own OnGUI, so the tray and the arrangement work the same for every mod.
    //
    // Copies from before the tray draw their own icon, placed from the shared table under TableKey. The
    // host keeps that table up to date (where each icon goes, and the tray button last), so their icons
    // still line up. Those icons stay in the row and can't be moved until their mod is updated.
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

        // Left to right, until the player arranges them. Leave gaps for future mods.
        public const int PhotoModeOrder = 0;
        public const int EmoteSyncOrder = 10;
        public const int TeleportSpotsOrder = 20;
        public const int NameDesignerOrder = 30;
        public const int CardDesignerOrder = 32;
        public const int OutfitSlotsOrder = 35;
        public const int DeskSnapOrder = 40;
        public const int DeskSwarmOrder = 45;
        public const int BuddyUpOrder = 47;
        public const int FocusAnywhereOrder = 50;
        public const int SongPlayerOrder = 60;
        public const int TraceOverlayOrder = 70;
        public const int ChatRangeOrder = 80;
        public const int DoomTogetherOrder = 85;
        public const int CardTableOrder = 87;
        public const int QuickHideOrder = 90;
        public const int SessionManagerOrder = 95;

        // The newest copy's host draws the row for every mod, so bump this whenever this file changes how
        // the row looks or behaves: the change then reaches players as soon as any one mod is updated.
        private const int HostVersion = 2;

        // ---- Shared data ----

        // The row (its icons and the tray button): x, y, width, height (screen pixels from the top-left),
        // the frame, and 1 if it's showing. PanelStack reads it under the same key, and so do copies from
        // before the tray.
        private const string RectKey = "OnTogetherMods.IconBar.Rect";
        // Copies from before the tray place their icon from this: id -> double[] { order, frame }. The
        // host's own entries have a third number, so it can tell the two apart.
        private const string TableKey = "OnTogetherMods.IconBar.Icons";
        private const string HiddenKey = "OnTogetherMods.IconBar.HiddenUntil";
        // Each mod's icon: id -> object[] { double[] numbers, string[] pixels, string name, string hint }.
        private const string EntriesKey = "OnTogetherMods.IconBar.Entries";
        // The host: object[] { int version, GameObject }.
        private const string HostKey = "OnTogetherMods.IconBar.Host";
        // The other places that take clicks while showing (the tray, the box around the row while
        // arranging): double[] { frame, then x, y, width, height for each }.
        private const string AreasKey = "OnTogetherMods.IconBar.Areas";
        // Copies from before the tray patch under "OnTogetherMods.IconBar.Patched".
        private const string PatchedKey = "OnTogetherMods.IconBar.Patched2";

        private const int EntryNumbers = 0;
        private const int EntryPixels = 1;
        private const int EntryName = 2;
        private const int EntryHint = 3;
        private const int EntrySize = 4;

        private const int NumOrder = 0;
        private const int NumState = 1;
        private const int NumKey = 2;
        private const int NumFrame = 3;   // the frame its mod last drew it
        private const int NumClick = 4;   // a click waiting for its mod: 0 none, 1 left, 2 right
        private const int NumSize = 5;

        // The tray button's place in TableKey: after every icon.
        private const string TrayId = "~IconTray";
        private const double TrayOrder = 1000;

        private const string SaveFile = "IconRow.json";

        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // On top of every mod's own GUI: they draw at 0, and copies from before the tray at -100 while
        // their tooltip shows.
        private const int HostDepth = -150;

        // The most icons side by side in the tray. More start another line above.
        private const int TrayColumns = 5;

        // The tray button's picture: a grey gear.
        private static readonly string[] TrayPixels =
        {
            "......KKKK......",
            "..KK.KWWWGK.KK..",
            ".KWGKKWGGGKKWgK.",
            ".KGGGGGGGGGGGgK.",
            "..KWGGGGGGGGgK..",
            ".KKWGGGGGGGGgKK.",
            "KWWGGGGKKGGGGGgK",
            "KWGGGGK..KGGGGgK",
            "KWGGGGK..KGGGGgK",
            "KGGGGGGKKGGGGggK",
            ".KKGGGGGGGGGgKK.",
            "..KGGGGGGGGGgK..",
            ".KGGGGGGGGGGggK.",
            ".KGgKKGGGgKKggK.",
            "..KK.KggggK.KK..",
            "......KKKK......",
        };

        private static readonly GUIContent ArrangeText = new GUIContent("Arrange icons");
        private static readonly GUIContent ResetText = new GUIContent("Reset");
        private static readonly GUIContent DoneText = new GUIContent("Done");
        private static readonly GUIContent EmptyText = new GUIContent("Nothing tucked away yet");
        private static readonly GUIContent DropText = new GUIContent("Drop icons here to tuck them away");
        private static readonly GUIContent HintText = new GUIContent("Drag icons to move them.\nClick one to tuck it away or put it back.");

        // ---- For the mods ----

        // Notes this mod's icon for this frame. Call it first thing in OnGUI, every frame; the host draws
        // it. A click on it comes back here: act on it in Update, outside the GUI pass.
        //
        // pixels: 16 rows of 16 palette letters. name and key make the tooltip ("Desk Snap (F3): on"),
        // and hint, if any, goes on a second line.
        public static Click Draw(string id, int order, string[] pixels, State state, string name, KeyCode key, string hint = null)
        {
            EnsureHost();
            object[] entry = Entry(id);
            var numbers = (double[])entry[EntryNumbers];
            numbers[NumOrder] = order;
            numbers[NumState] = (int)state;
            numbers[NumKey] = (int)key;
            numbers[NumFrame] = Time.frameCount;
            entry[EntryPixels] = pixels;
            entry[EntryName] = name;
            entry[EntryHint] = hint;

            double click = numbers[NumClick];
            if (click == 0)
            {
                return Click.None;
            }
            numbers[NumClick] = 0;
            return click == 1 ? Click.Left : Click.Right;
        }

        // Hides the whole row for this frame and the next. Photo Mode calls it every frame while it's on
        // (before OnGUI). It runs out by itself, so the row can't get stuck hidden.
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

        // True while the row is showing and the mouse is on it or on the open tray. Any mod's copy can ask.
        public static bool MouseOver
        {
            get
            {
                Vector3 mouse = Input.mousePosition;
                var point = new Vector2(mouse.x, Screen.height - mouse.y);
                AppDomain domain = AppDomain.CurrentDomain;
                int frame = Time.frameCount;
                if (domain.GetData(RectKey) is double[] row && row.Length >= 6 && row[5] >= 0.5 && frame - row[4] <= 2 &&
                    Inside(row, 0, point))
                {
                    return true;
                }
                if (domain.GetData(AreasKey) is double[] areas && areas.Length >= 1 && frame - areas[0] <= 2)
                {
                    for (int i = 1; i + 3 < areas.Length; i += 4)
                    {
                        if (Inside(areas, i, point))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        private static bool Inside(double[] r, int i, Vector2 point)
        {
            return point.x >= r[i] && point.x < r[i] + r[i + 2] && point.y >= r[i + 1] && point.y < r[i + 1] + r[i + 3];
        }

        private static object[] Entry(string id)
        {
            Hashtable entries = Table(EntriesKey);
            if (entries[id] is object[] entry && entry.Length >= EntrySize &&
                entry[EntryNumbers] is double[] numbers && numbers.Length >= NumSize)
            {
                return entry;
            }
            entry = new object[EntrySize];
            entry[EntryNumbers] = new double[NumSize];
            entries[id] = entry;
            return entry;
        }

        private static Hashtable Table(string key)
        {
            if (!(AppDomain.CurrentDomain.GetData(key) is Hashtable table))
            {
                table = new Hashtable();
                AppDomain.CurrentDomain.SetData(key, table);
            }
            return table;
        }

        // ---- The host ----

        private static int _hostChecked = -1;
        private static bool _hostFailed;

        // Makes the host if there isn't one yet, or takes over from an older copy's.
        private static void EnsureHost()
        {
            int frame = Time.frameCount;
            if (_hostChecked == frame || _hostFailed)
            {
                return;
            }
            _hostChecked = frame;

            AppDomain domain = AppDomain.CurrentDomain;
            if (domain.GetData(HostKey) is object[] host && host.Length >= 2 && host[1] is GameObject current && current != null)
            {
                if (host[0] is int version && version >= HostVersion)
                {
                    return;
                }
                UnityEngine.Object.Destroy(current);
            }
            GameObject created = null;
            try
            {
                created = new GameObject(PanelStack.CanvasPrefix + "IconBar");
                UnityEngine.Object.DontDestroyOnLoad(created);
                created.AddComponent<IconBarHost>();
                domain.SetData(HostKey, new object[] { HostVersion, created });
            }
            catch (Exception e)
            {
                // Not tried again, so a failure can't leave a new object behind every frame.
                _hostFailed = true;
                if (created != null)
                {
                    UnityEngine.Object.Destroy(created);
                }
                Debug.LogError($"[OnTogetherMods] Couldn't set up the icon row: {e}");
            }
        }

        // An icon in the row or the tray, as the host sees it this frame.
        private sealed class Item
        {
            public string Id;
            public object[] Entry;     // null for an icon a copy from before the tray draws itself
            public double[] Numbers;
            public double Order;       // its mod's order constant
            public double Slot;        // where it goes in TableKey
            public bool Tucked;
            public Rect Rect;
        }

        private struct TrayLayout
        {
            public Rect Box;
            public Vector2 Grid;   // the first icon's top-left
            public int Columns;
            public float GridHeight;
        }

        private enum Target { None, Icon, TrayButton, Arrange, Reset, Done, Background }

        private static readonly Dictionary<string, Item> Items = new Dictionary<string, Item>();
        private static readonly List<Item> Live = new List<Item>();      // icons drawn by mods with this kind of copy
        private static readonly List<Item> Older = new List<Item>();     // icons copies from before the tray draw
        private static readonly List<Item> Unlisted = new List<Item>();
        private static readonly List<Item> Row = new List<Item>();       // left to right, both kinds
        private static readonly List<Item> Tray = new List<Item>();
        private static readonly List<string> RowIds = new List<string>();    // the arrangement, without a drag
        private static readonly List<string> TrayIds = new List<string>();
        private static readonly List<string> ShownRow = new List<string>();  // ... and with one
        private static readonly List<string> ShownTray = new List<string>();
        private static readonly List<double> Slots = new List<double>();
        private static readonly List<object> Stale = new List<object>();
        private static readonly List<double> Areas = new List<double>();

        private static readonly Comparison<Item> ByOrder = (a, b) =>
            a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id);
        private static readonly Comparison<Item> BySlot = (a, b) =>
            a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.Id, b.Id);

        // The saved arrangement. It keeps the places of mods that aren't drawing an icon right now.
        private static List<string> _savedRow = new List<string>();
        private static List<string> _savedTray = new List<string>();
        private static bool _loaded;

        private static int _frame = -1;
        private static bool _dirty;
        private static bool _inWorld;
        private static bool _shown;
        private static Metrics _m;
        private static Rect _rowRect;       // the icons and the tray button
        private static Rect _rowBox;        // around them, while arranging
        private static Rect _trayButton;
        private static bool _trayShown;
        private static TrayLayout _tray;
        private static Rect _emptyRect;
        private static Rect _hintRect;
        private static Rect _arrangeButton;
        private static Rect _resetButton;
        private static Rect _doneButton;

        private static bool _trayOpen;
        private static bool _arranging;

        private static Target _pressed;
        private static Item _pressedItem;
        private static int _pressedButton;

        // Arranging: the icon pressed, and whether it has moved far enough to be dragged.
        private static Item _held;
        private static bool _dragging;
        private static Vector2 _pressedAt;
        private static Vector2 _grab;          // the mouse's place inside the held icon
        private static bool _dropInTray;
        private static int _dropIndex = -1;    // -1: back where it was
        // The row and the tray laid out with the held icon in them, for finding where it would go.
        private static float _rowLeftWith;
        private static int _rowCountWith;
        private static TrayLayout _trayWith;
        private static int _trayCountWithout;

        // Called by the host's OnGUI.
        internal static void HostGUI(GameObject self)
        {
            if (!(AppDomain.CurrentDomain.GetData(HostKey) is object[] host) || host.Length < 2 || !ReferenceEquals(host[1], self))
            {
                UnityEngine.Object.Destroy(self);   // a newer copy has taken over
                return;
            }
            GUI.depth = HostDepth;
            // Always first, so the row keeps the same control id from frame to frame.
            int control = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);
            Event e = Event.current;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                Prepare();
                if (!_shown)
                {
                    // A button let go of while hidden (a menu opened mid-click) would keep every IMGUI control busy.
                    if (GUIUtility.hotControl == control)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    return;
                }
                switch (e.GetTypeForControl(control))
                {
                    case EventType.MouseDown:
                        MouseDown(control, e);
                        break;

                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == control)
                        {
                            Drag(e.mousePosition);
                            e.Use();
                        }
                        break;

                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == control)
                        {
                            GUIUtility.hotControl = 0;
                            e.Use();
                            MouseUp(e);
                        }
                        break;

                    case EventType.Repaint:
                        Paint(control, e.mousePosition);
                        break;
                }
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        internal static void HostDestroyed()
        {
            Close();
        }

        // Works out what shows, once a frame and again after each change.
        private static void Prepare()
        {
            int frame = Time.frameCount;
            if (_frame != frame)
            {
                _frame = frame;
                _dirty = true;
                _inWorld = SceneManager.GetActiveScene().buildIndex == WorldScene;
                _shown = false;
                if (_inWorld && !PanelStack.Hidden && !RowHidden)
                {
                    TaskManager tasks = MonoSingleton<TaskManager>.I;
                    // Free, or at an instrument or a drawing board. The other states are menus and panels.
                    _shown = tasks != null && (tasks.LockState == LockState.Free || tasks.LockState == LockState.Music);
                }
            }
            if (!_dirty)
            {
                return;
            }
            _dirty = false;
            if (!_inWorld)
            {
                Close();
                UpdateBlocker(false);
                return;
            }
            if (!_loaded)
            {
                Load();
            }
            _m = Measure();
            BuildStyles(_m);
            Gather(frame);
            if (Live.Count == 0)
            {
                // No mod is drawing an icon (they're all switched off with ShowIcon), so no tray button either.
                _shown = false;
            }
            if (!_shown)
            {
                Close();
            }
            Arrange();
            Layout();
            if (Live.Count > 0)
            {
                Publish(frame);
            }
            UpdateBlocker(_shown);
        }

        private static void Close()
        {
            _trayOpen = false;
            _arranging = false;
            _held = null;
            _dragging = false;
            _dropIndex = -1;
            _pressed = Target.None;
            _pressedItem = null;
        }

        // The icons drawn in the last couple of frames.
        private static void Gather(int frame)
        {
            Live.Clear();
            Older.Clear();
            foreach (DictionaryEntry pair in Table(EntriesKey))
            {
                if (!(pair.Key is string id) || !(pair.Value is object[] entry) || entry.Length < EntrySize ||
                    !(entry[EntryNumbers] is double[] numbers) || numbers.Length < NumSize)
                {
                    continue;
                }
                if (frame - numbers[NumFrame] > 2)
                {
                    numbers[NumClick] = 0;   // its mod stopped drawing it, so a click waiting for it is dropped
                    continue;
                }
                Item item = ItemFor(id);
                item.Entry = entry;
                item.Numbers = numbers;
                item.Order = numbers[NumOrder];
                Live.Add(item);
            }
            foreach (DictionaryEntry pair in Table(TableKey))
            {
                // Copies from before the tray write two numbers; the host writes three.
                if (pair.Key is string id && pair.Value is double[] value && value.Length == 2 && frame - value[1] <= 2 &&
                    Find(Live, id) == null)
                {
                    Item item = ItemFor(id);
                    item.Entry = null;
                    item.Numbers = null;
                    item.Order = value[0];
                    item.Slot = value[0];
                    item.Tucked = false;
                    Older.Add(item);
                }
            }
        }

        private static Item ItemFor(string id)
        {
            if (!Items.TryGetValue(id, out Item item))
            {
                item = new Item { Id = id };
                Items[id] = item;
            }
            return item;
        }

        private static Item Find(List<Item> items, string id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                {
                    return items[i];
                }
            }
            return null;
        }

        // Puts the icons in order: the tray as saved, then the row as saved, where icons that were never
        // arranged take their usual place. A held icon shows where it would be dropped.
        private static void Arrange()
        {
            TrayIds.Clear();
            RowIds.Clear();
            foreach (string id in _savedTray)
            {
                if (Find(Live, id) != null && !TrayIds.Contains(id))
                {
                    TrayIds.Add(id);
                }
            }
            foreach (string id in _savedRow)
            {
                if (Find(Live, id) != null && !TrayIds.Contains(id) && !RowIds.Contains(id))
                {
                    RowIds.Add(id);
                }
            }
            Unlisted.Clear();
            foreach (Item item in Live)
            {
                if (!RowIds.Contains(item.Id) && !TrayIds.Contains(item.Id))
                {
                    Unlisted.Add(item);
                }
            }
            Unlisted.Sort(ByOrder);
            foreach (Item item in Unlisted)
            {
                InsertInPlace(RowIds, item);
            }

            ShownRow.Clear();
            ShownRow.AddRange(RowIds);
            ShownTray.Clear();
            ShownTray.AddRange(TrayIds);
            if (_held != null && _dragging && _dropIndex >= 0 && Find(Live, _held.Id) != null)
            {
                ShownRow.Remove(_held.Id);
                ShownTray.Remove(_held.Id);
                List<string> into = _dropInTray ? ShownTray : ShownRow;
                into.Insert(Mathf.Clamp(_dropIndex, 0, into.Count), _held.Id);
            }

            // The row's icons take their mods' usual orders between them, in the order shown, so the
            // icons of copies from before the tray keep their usual place among them.
            Slots.Clear();
            foreach (string id in ShownRow)
            {
                Slots.Add(Find(Live, id).Order);
            }
            Slots.Sort();
            Row.Clear();
            for (int i = 0; i < ShownRow.Count; i++)
            {
                Item item = Find(Live, ShownRow[i]);
                double slot = Slots[i];
                if (i > 0 && slot <= Row[i - 1].Slot)
                {
                    slot = Row[i - 1].Slot + 0.001;
                }
                item.Slot = slot;
                item.Tucked = false;
                Row.Add(item);
            }
            Row.AddRange(Older);
            Row.Sort(BySlot);

            Tray.Clear();
            foreach (string id in ShownTray)
            {
                Item item = Find(Live, id);
                item.Tucked = true;
                Tray.Add(item);
            }
        }

        // Before the first icon that comes after it in the usual order, so a newly installed mod's icon
        // lands about where it would have been.
        private static void InsertInPlace(List<string> ids, Item item)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                Item other = Find(Live, ids[i]);
                if (other != null && ByOrder(item, other) < 0)
                {
                    ids.Insert(i, item.Id);
                    return;
                }
            }
            ids.Add(item.Id);
        }

        private static void Layout()
        {
            Metrics m = _m;
            int count = Row.Count + 1;
            int width = count * m.Button + (count - 1) * m.Gap;
            float x = Screen.width - m.Margin - width;
            float y = Screen.height - m.Margin - m.Button;
            _rowRect = new Rect(x, y, width, m.Button);
            for (int i = 0; i < Row.Count; i++)
            {
                Row[i].Rect = new Rect(x + i * (m.Button + m.Gap), y, m.Button, m.Button);
            }
            _trayButton = new Rect(x + Row.Count * (m.Button + m.Gap), y, m.Button, m.Button);
            float pad = Mathf.Max(2, Mathf.RoundToInt(m.Gap / 2f));
            _rowBox = new Rect(_rowRect.x - pad, _rowRect.y - pad, _rowRect.width + 2 * pad, _rowRect.height + 2 * pad);

            _trayShown = _shown && (_trayOpen || _arranging);
            if (_trayShown)
            {
                _tray = LayoutTray(Tray.Count);
                for (int i = 0; i < Tray.Count; i++)
                {
                    Tray[i].Rect = new Rect(_tray.Grid.x + i % _tray.Columns * (m.Button + m.Gap),
                        _tray.Grid.y + i / _tray.Columns * (m.Button + m.Gap), m.Button, m.Button);
                }
                LayoutTrayText();
            }

            if (_held != null)
            {
                bool inRow = !_held.Tucked;
                _rowCountWith = Row.Count - (inRow ? 1 : 0) + 1;
                int withTray = _rowCountWith + 1;
                _rowLeftWith = Screen.width - m.Margin - (withTray * m.Button + (withTray - 1) * m.Gap);
                _trayCountWithout = Tray.Count - (inRow ? 0 : 1);
                _trayWith = LayoutTray(_trayCountWithout + 1);
            }
        }

        // The tray box for this many icons, right above the tray button: the icons, then Arrange icons
        // (or, while arranging, how to and Reset / Done).
        private static TrayLayout LayoutTray(int icons)
        {
            Metrics m = _m;
            int columns = _arranging ? TrayColumns : Mathf.Clamp(icons, 1, TrayColumns);
            int lines = (icons + columns - 1) / columns;
            if (_arranging)
            {
                lines = Mathf.Max(1, lines);   // room to drop the first one
            }
            float gridWidth = columns * m.Button + (columns - 1) * m.Gap;
            float gridHeight = lines > 0 ? lines * m.Button + (lines - 1) * m.Gap : 0;
            Vector2 text = TrayTextSize(icons);
            float inner = Mathf.Ceil(Mathf.Max(gridWidth, text.x));
            float width = inner + 2 * m.Gap;
            float height = Mathf.Ceil(gridHeight + (gridHeight > 0 ? m.Gap : 0) + text.y + 2 * m.Gap);
            var box = new Rect(_trayButton.xMax - width, _rowRect.y - m.Gap - height, width, height);
            return new TrayLayout
            {
                Box = box,
                // Whole pixels, so the icons stay crisp.
                Grid = new Vector2(box.x + m.Gap + Mathf.Floor((inner - gridWidth) / 2f), box.y + m.Gap),
                Columns = columns,
                GridHeight = gridHeight,
            };
        }

        // What goes under the icons. With nothing tucked away, a line saying so comes first.
        private static Vector2 TrayTextSize(int icons)
        {
            Metrics m = _m;
            if (_arranging)
            {
                Vector2 hint = _labelStyle.CalcSize(HintText);
                Vector2 reset = _textButtonStyle.CalcSize(ResetText);
                Vector2 done = _textButtonStyle.CalcSize(DoneText);
                float buttons = Mathf.Max(reset.y, done.y);
                float drop = icons == 0 ? _labelStyle.CalcSize(DropText).x : 0;
                return new Vector2(Mathf.Max(hint.x, reset.x + 3 * m.Gap + done.x, drop), hint.y + m.Gap + buttons);
            }
            Vector2 arrange = _textButtonStyle.CalcSize(ArrangeText);
            if (icons == 0)
            {
                Vector2 empty = _labelStyle.CalcSize(EmptyText);
                return new Vector2(Mathf.Max(arrange.x, empty.x), empty.y + m.Gap + arrange.y);
            }
            return arrange;
        }

        private static void LayoutTrayText()
        {
            Metrics m = _m;
            Rect box = _tray.Box;
            float left = box.x + m.Gap;
            float inner = box.width - 2 * m.Gap;
            float y = _tray.Grid.y + (_tray.GridHeight > 0 ? _tray.GridHeight + m.Gap : 0);
            _emptyRect = default;
            _hintRect = default;
            _arrangeButton = default;
            _resetButton = default;
            _doneButton = default;
            if (_arranging)
            {
                if (Tray.Count == 0)
                {
                    _emptyRect = new Rect(left, _tray.Grid.y, inner, _tray.GridHeight);
                }
                Vector2 hint = _labelStyle.CalcSize(HintText);
                _hintRect = new Rect(left, y, inner, hint.y);
                y += hint.y + m.Gap;
                Vector2 reset = _textButtonStyle.CalcSize(ResetText);
                Vector2 done = _textButtonStyle.CalcSize(DoneText);
                // Apart, so Reset isn't hit by mistake.
                _resetButton = new Rect(left, y, reset.x, reset.y);
                _doneButton = new Rect(box.xMax - m.Gap - done.x, y, done.x, done.y);
                return;
            }
            if (Tray.Count == 0)
            {
                Vector2 empty = _labelStyle.CalcSize(EmptyText);
                _emptyRect = new Rect(left, y, inner, empty.y);
                y += empty.y + m.Gap;
            }
            Vector2 arrange = _textButtonStyle.CalcSize(ArrangeText);
            _arrangeButton = new Rect(box.xMax - m.Gap - arrange.x, y, arrange.x, arrange.y);
        }

        // Shares where everything is: the old table for copies from before the tray, the row for
        // PanelStack, and the clickable places for MouseOver.
        private static void Publish(int frame)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            Hashtable table = Table(TableKey);
            foreach (Item item in Row)
            {
                if (item.Entry != null)
                {
                    Put(table, item.Id, item.Slot, frame);
                }
            }
            Put(table, TrayId, TrayOrder, frame);
            Stale.Clear();
            foreach (DictionaryEntry pair in table)
            {
                if (pair.Value is double[] value && value.Length >= 3 && pair.Key is string id && id != TrayId &&
                    (Find(Row, id) == null || Find(Row, id).Entry == null))
                {
                    Stale.Add(pair.Key);   // tucked away, or its mod stopped drawing it
                }
            }
            foreach (object key in Stale)
            {
                table.Remove(key);
            }

            domain.SetData(RectKey, new double[] { _rowRect.x, _rowRect.y, _rowRect.width, _rowRect.height, frame, _shown ? 1 : 0 });

            Areas.Clear();
            Areas.Add(frame);
            if (_trayShown)
            {
                AddArea(_tray.Box);
            }
            if (_shown && _arranging)
            {
                AddArea(_rowBox);
            }
            domain.SetData(AreasKey, Areas.ToArray());
        }

        private static void Put(Hashtable table, string id, double slot, int frame)
        {
            if (table[id] is double[] value && value.Length >= 3)
            {
                value[0] = slot;
                value[1] = frame;
            }
            else
            {
                table[id] = new double[] { slot, frame, 1 };
            }
        }

        private static void AddArea(Rect rect)
        {
            Areas.Add(rect.x);
            Areas.Add(rect.y);
            Areas.Add(rect.width);
            Areas.Add(rect.height);
        }

        // ---- Mouse ----

        private static void MouseDown(int control, Event e)
        {
            if (e.button != 0 && e.button != 1)
            {
                return;
            }
            Vector2 mouse = e.mousePosition;
            Target target = HitTest(mouse, out Item item);
            if (target == Target.None)
            {
                // A click anywhere else closes the tray, and still does whatever it does there.
                if (_trayOpen || _arranging)
                {
                    _trayOpen = false;
                    _arranging = false;
                    _dirty = true;
                }
                return;
            }
            GUIUtility.hotControl = control;
            e.Use();
            _pressed = Target.None;
            _pressedItem = null;
            if (_arranging && target == Target.Icon)
            {
                if (e.button == 0)
                {
                    _held = item;
                    _dragging = false;
                    _dropIndex = -1;
                    _pressedAt = mouse;
                    _grab = mouse - item.Rect.position;
                }
                return;
            }
            _pressed = target;
            _pressedItem = item;
            _pressedButton = e.button;
        }

        private static void Drag(Vector2 mouse)
        {
            if (_held == null)
            {
                return;
            }
            if (!_dragging)
            {
                float start = Mathf.Max(3f, 4f * _m.Unit);
                if ((mouse - _pressedAt).sqrMagnitude < start * start)
                {
                    return;
                }
                _dragging = true;
                _dirty = true;
            }
            FindDrop(mouse, out bool inTray, out int index);
            if (inTray != _dropInTray || index != _dropIndex)
            {
                _dropInTray = inTray;
                _dropIndex = index;
                _dirty = true;
            }
        }

        // Where the held icon would go: a place in the tray, a place in the row, or back (-1).
        private static void FindDrop(Vector2 mouse, out bool inTray, out int index)
        {
            Metrics m = _m;
            float step = m.Button + m.Gap;
            Rect tray = _trayWith.Box;
            tray = new Rect(tray.x - m.Gap, tray.y - m.Gap, tray.width + 2 * m.Gap, tray.height + 2 * m.Gap);
            if (tray.Contains(mouse))
            {
                inTray = true;
                int column = Mathf.Clamp(Mathf.FloorToInt((mouse.x - _trayWith.Grid.x + m.Gap / 2f) / step), 0, _trayWith.Columns - 1);
                int line = Mathf.Max(0, Mathf.FloorToInt((mouse.y - _trayWith.Grid.y + m.Gap / 2f) / step));
                index = Mathf.Clamp(line * _trayWith.Columns + column, 0, _trayCountWithout);
                return;
            }
            inTray = false;
            if (mouse.y >= _rowRect.y - m.Button / 2f && mouse.x >= _rowLeftWith - m.Button)
            {
                int slot = Mathf.Clamp(Mathf.FloorToInt((mouse.x - _rowLeftWith + m.Gap / 2f) / step), 0, _rowCountWith - 1);
                index = MovableBefore(slot);
                return;
            }
            index = -1;
        }

        // How many of the icons before this place in the row can move (the held one left out). Icons of
        // copies from before the tray stay where they are, so only the others count.
        private static int MovableBefore(int place)
        {
            int index = 0;
            int seen = 0;
            foreach (Item item in Row)
            {
                if (item == _held)
                {
                    continue;
                }
                if (seen++ >= place)
                {
                    break;
                }
                if (item.Entry != null)
                {
                    index++;
                }
            }
            return index;
        }

        private static void MouseUp(Event e)
        {
            Vector2 mouse = e.mousePosition;
            if (_held != null)
            {
                Item held = _held;
                bool dragged = _dragging;
                bool inTray = _dropInTray;
                int index = _dropIndex;
                _held = null;
                _dragging = false;
                _dropIndex = -1;
                _dirty = true;
                if (dragged)
                {
                    if (index >= 0)
                    {
                        Move(held, inTray, index);
                    }
                }
                else if (e.button == 0 && held.Rect.Contains(mouse))
                {
                    Toggle(held);
                }
                return;
            }

            Target pressed = _pressed;
            Item item = _pressedItem;
            int button = _pressedButton;
            _pressed = Target.None;
            _pressedItem = null;
            if (pressed == Target.None || e.button != button || HitTest(mouse, out Item over) != pressed || over != item)
            {
                return;
            }
            switch (pressed)
            {
                case Target.Icon:
                    item.Numbers[NumClick] = button == 0 ? 1 : 2;
                    if (item.Tucked)
                    {
                        _trayOpen = false;
                    }
                    break;
                case Target.TrayButton:
                    if (_arranging)
                    {
                        _arranging = false;
                        _trayOpen = false;
                    }
                    else if (button == 1)
                    {
                        _arranging = true;
                        _trayOpen = true;
                    }
                    else
                    {
                        _trayOpen = !_trayOpen;
                    }
                    break;
                case Target.Arrange:
                    _arranging = true;
                    _trayOpen = true;
                    break;
                case Target.Done:
                    _arranging = false;
                    break;
                case Target.Reset:
                    Reset();
                    break;
            }
            _dirty = true;
        }

        private static Target HitTest(Vector2 mouse, out Item item)
        {
            item = null;
            if (_trayShown && _tray.Box.Contains(mouse))
            {
                if (_arranging)
                {
                    if (_resetButton.Contains(mouse))
                    {
                        return Target.Reset;
                    }
                    if (_doneButton.Contains(mouse))
                    {
                        return Target.Done;
                    }
                }
                else if (_arrangeButton.Contains(mouse))
                {
                    return Target.Arrange;
                }
                foreach (Item tucked in Tray)
                {
                    if (tucked != _held && tucked.Rect.Contains(mouse))
                    {
                        item = tucked;
                        return Target.Icon;
                    }
                }
                return Target.Background;
            }
            if (_trayButton.Contains(mouse))
            {
                return Target.TrayButton;
            }
            foreach (Item shown in Row)
            {
                if (shown == _held || !shown.Rect.Contains(mouse))
                {
                    continue;
                }
                if (shown.Entry == null)
                {
                    // A copy from before the tray handles its own icon's clicks, except while arranging.
                    return _arranging ? Target.Background : Target.None;
                }
                item = shown;
                return Target.Icon;
            }
            if (_arranging && _rowBox.Contains(mouse))
            {
                return Target.Background;
            }
            return Target.None;
        }

        // ---- Arranging ----

        private static void Move(Item item, bool intoTray, int index)
        {
            var row = new List<string>(RowIds);
            var tray = new List<string>(TrayIds);
            row.Remove(item.Id);
            tray.Remove(item.Id);
            List<string> into = intoTray ? tray : row;
            into.Insert(Mathf.Clamp(index, 0, into.Count), item.Id);
            Commit(row, tray);
        }

        // Tucks an icon away (at the end of the tray), or puts it back in its usual place in the row.
        private static void Toggle(Item item)
        {
            var row = new List<string>(RowIds);
            var tray = new List<string>(TrayIds);
            if (tray.Remove(item.Id))
            {
                InsertInPlace(row, item);
            }
            else
            {
                row.Remove(item.Id);
                tray.Add(item.Id);
            }
            Commit(row, tray);
        }

        private static void Commit(List<string> row, List<string> tray)
        {
            _savedRow = KeepMissing(row, _savedRow, tray);
            _savedTray = KeepMissing(tray, _savedTray, row);
            Save();
            _dirty = true;
        }

        // shown, plus the saved ids of mods that aren't drawing an icon right now (not installed, or
        // ShowIcon off), each kept right after the id it came after.
        private static List<string> KeepMissing(List<string> shown, List<string> saved, List<string> other)
        {
            var result = new List<string>(shown);
            for (int i = 0; i < saved.Count; i++)
            {
                string id = saved[i];
                if (result.Contains(id) || other.Contains(id) || Find(Live, id) != null)
                {
                    continue;
                }
                int at = 0;
                for (int j = i - 1; j >= 0; j--)
                {
                    int k = result.IndexOf(saved[j]);
                    if (k >= 0)
                    {
                        at = k + 1;
                        break;
                    }
                }
                result.Insert(at, id);
            }
            return result;
        }

        // Every icon back in the row, in the usual order.
        private static void Reset()
        {
            _savedRow = new List<string>();
            _savedTray = new List<string>();
            string path = SavePath;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OnTogetherMods] Couldn't delete {path}: {e.Message}");
            }
            _dirty = true;
        }

        // ---- Saving ----

        private static string SavePath => Path.Combine(Paths.ConfigPath, SaveFile);

        private static void Load()
        {
            _loaded = true;
            _savedRow = new List<string>();
            _savedTray = new List<string>();
            string path = SavePath;
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }
                JObject file = JObject.Parse(File.ReadAllText(path));
                ReadIds(file["Row"], _savedRow);
                ReadIds(file["Tray"], _savedTray);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OnTogetherMods] Couldn't read {path}, so the icons are in their usual order: {e.Message}");
            }
        }

        private static void ReadIds(JToken token, List<string> into)
        {
            if (!(token is JArray ids))
            {
                return;
            }
            foreach (JToken id in ids)
            {
                if (id.Type == JTokenType.String && !into.Contains((string)id))
                {
                    into.Add((string)id);
                }
            }
        }

        private static void Save()
        {
            string path = SavePath;
            try
            {
                var file = new JObject
                {
                    ["Version"] = 1,
                    ["Row"] = new JArray(_savedRow.ToArray()),
                    ["Tray"] = new JArray(_savedTray.ToArray()),
                };
                // Written beside the old file and swapped in, so quitting halfway can't leave half a file.
                string temp = path + ".tmp";
                File.WriteAllText(temp, file.ToString(Formatting.Indented));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OnTogetherMods] Couldn't save {path}: {e.Message}");
            }
        }

        // ---- Painting ----

        private static void Paint(int control, Vector2 mouse)
        {
            Metrics m = _m;
            bool idle = GUIUtility.hotControl == 0;
            bool mine = GUIUtility.hotControl == control;

            if (_arranging)
            {
                // See-through, since the icons of copies from before the tray are under it.
                PaintBox(_rowBox, _slotTexture);
            }
            foreach (Item item in Row)
            {
                if (item.Entry == null)
                {
                    continue;   // its own copy draws it
                }
                PaintItem(item, mouse, idle, mine, m);
            }
            PaintTrayButton(mouse, idle, mine, m);

            if (_trayShown)
            {
                PaintBox(_tray.Box, _panelTexture);
                foreach (Item item in Tray)
                {
                    PaintItem(item, mouse, idle, mine, m);
                }
                if (_emptyRect.width > 0)
                {
                    _labelStyle.Draw(_emptyRect, _arranging ? DropText : EmptyText, false, false, false, false);
                }
                if (_arranging)
                {
                    _labelStyle.Draw(_hintRect, HintText, false, false, false, false);
                    PaintTextButton(_resetButton, ResetText, Target.Reset, mouse, idle, mine);
                    PaintTextButton(_doneButton, DoneText, Target.Done, mouse, idle, mine);
                }
                else
                {
                    PaintTextButton(_arrangeButton, ArrangeText, Target.Arrange, mouse, idle, mine);
                }
            }

            if (_held != null && _dragging)
            {
                var lifted = new Rect(mouse.x - _grab.x, mouse.y - _grab.y, m.Button, m.Button);
                PaintIcon(lifted, Pixels(_held), StateOf(_held), _openTexture, m);
                return;   // no tooltip while dragging
            }

            Target over = HitTest(mouse, out Item hovered);
            if (over == Target.Icon && (idle || _pressedItem == hovered || _held == hovered))
            {
                DrawTooltip(hovered.Rect, m, ItemTooltip(hovered));
            }
            else if (over == Target.TrayButton && (idle || _pressed == Target.TrayButton))
            {
                DrawTooltip(_trayButton, m, TrayTooltip());
            }
        }

        private static void PaintItem(Item item, Vector2 mouse, bool idle, bool mine, Metrics m)
        {
            if (item == _held && _dragging)
            {
                PaintBox(item.Rect, _slotTexture);   // where it will land
                return;
            }
            State state = StateOf(item);
            bool pressed = mine && (_pressedItem == item || _held == item);
            bool hover = item.Rect.Contains(mouse) && (idle || pressed);
            Texture2D back = state == State.Open || (pressed && hover) ? _openTexture : hover ? _hoverTexture : _buttonTexture;
            PaintIcon(item.Rect, Pixels(item), state, back, m);
        }

        private static void PaintTrayButton(Vector2 mouse, bool idle, bool mine, Metrics m)
        {
            bool pressed = mine && _pressed == Target.TrayButton;
            bool hover = _trayButton.Contains(mouse) && (idle || pressed);
            Texture2D back = _trayShown || (pressed && hover) ? _openTexture : hover ? _hoverTexture : _buttonTexture;
            // The green dot when a tucked-away mod is switched on.
            State state = State.Plain;
            foreach (Item item in Tray)
            {
                if (StateOf(item) == State.On)
                {
                    state = State.On;
                }
            }
            PaintIcon(_trayButton, TrayPixels, state, back, m);
        }

        // A rounded box: every box texture has the same corners, so they share one 9-slice style.
        private static void PaintBox(Rect rect, Texture2D texture)
        {
            _buttonStyle.normal.background = texture;
            _buttonStyle.Draw(rect, false, false, false, false);
        }

        private static void PaintIcon(Rect rect, string[] pixels, State state, Texture2D back, Metrics m)
        {
            PaintBox(rect, back);
            if (pixels != null)
            {
                Texture2D[] art = IconTextures(pixels);
                var artRect = new Rect(rect.x + (rect.width - m.Art) / 2, rect.y + (rect.height - m.Art) / 2, m.Art, m.Art);
                GUI.DrawTexture(artRect, state == State.Off ? art[1] : art[0]);
            }
            if (state == State.On)
            {
                float inset = Mathf.Max(1, Mathf.RoundToInt(2f * m.Unit));
                float size = _dotTexture.width;
                GUI.DrawTexture(new Rect(rect.xMax - inset - size, rect.yMax - inset - size, size, size), _dotTexture);
            }
        }

        private static void PaintTextButton(Rect rect, GUIContent text, Target target, Vector2 mouse, bool idle, bool mine)
        {
            bool pressed = mine && _pressed == target;
            bool hover = rect.Contains(mouse) && (idle || pressed);
            _textButtonStyle.normal.background = pressed && hover ? _openTexture : hover ? _hoverTexture : _buttonTexture;
            _textButtonStyle.Draw(rect, text, false, false, false, false);
        }

        private static string[] Pixels(Item item)
        {
            return item.Entry?[EntryPixels] as string[];
        }

        private static State StateOf(Item item)
        {
            return item.Numbers != null ? (State)(int)item.Numbers[NumState] : State.Plain;
        }

        private static string ItemTooltip(Item item)
        {
            string name = item.Entry[EntryName] as string ?? item.Id;
            if (_arranging)
            {
                return name + (item.Tucked ? "\nClick to put it back in the row, or drag it" : "\nClick to tuck it away, or drag it");
            }
            return Tooltip(StateOf(item), name, (KeyCode)(int)item.Numbers[NumKey], item.Entry[EntryHint] as string);
        }

        private static string TrayTooltip()
        {
            if (_arranging)
            {
                return "Icon tray\nClick: done arranging";
            }
            string tucked = Tray.Count > 0 ? $": {Tray.Count} tucked away" : "";
            return $"Icon tray{tucked}\nRight-click: arrange the icons";
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
            float y = Mathf.Max(0f, icon.y - m.Gap - size.y);
            _tipStyle.Draw(new Rect(Mathf.Round(x), Mathf.Round(y), size.x, size.y), content, false, false, false, false);
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

        // ---- Click blocker ----

        // Invisible game UI under the row and the tray (see ClickBlocker.cs). A child of the host, so it
        // goes with it. Its name starts with PanelStack.CanvasPrefix, so Photo Mode leaves it on.
        private static GameObject _blocker;
        private static RectTransform _rowArea;
        private static RectTransform _trayArea;

        private static void UpdateBlocker(bool shown)
        {
            if (_blocker == null)
            {
                if (!shown || !(AppDomain.CurrentDomain.GetData(HostKey) is object[] host) || host.Length < 2 ||
                    !(host[1] is GameObject parent) || parent == null)
                {
                    return;
                }
                _blocker = CreateBlocker(parent.transform);
            }
            if (shown)
            {
                PlaceArea(_rowArea, _arranging ? _rowBox : _rowRect, true);
                PlaceArea(_trayArea, _tray.Box, _trayShown);
            }
            if (_blocker.activeSelf != shown)
            {
                _blocker.SetActive(shown);
            }
        }

        private static void PlaceArea(RectTransform area, Rect rect, bool on)
        {
            if (area.gameObject.activeSelf != on)
            {
                area.gameObject.SetActive(on);
            }
            if (!on)
            {
                return;
            }
            // Game UI measures from the bottom-left.
            var position = new Vector2(rect.x, Screen.height - rect.yMax);
            if (area.anchoredPosition != position)
            {
                area.anchoredPosition = position;
            }
            if (area.sizeDelta != rect.size)
            {
                area.sizeDelta = rect.size;
            }
        }

        private static GameObject CreateBlocker(Transform parent)
        {
            var root = new GameObject(PanelStack.CanvasPrefix + "IconBar ClickBlocker");
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();
            _rowArea = AddArea(root.transform, "Row");
            _trayArea = AddArea(root.transform, "Tray");
            root.SetActive(false);
            return root;
        }

        private static RectTransform AddArea(Transform parent, string name)
        {
            // A see-through image still catches clicks.
            var area = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            area.transform.SetParent(parent, false);
            area.GetComponent<Image>().color = Color.clear;
            var rect = (RectTransform)area.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return rect;
        }

        // ---- Patches ----

        // Call from Plugin.Awake. The first mod to call it patches the game for all of them:
        // - a click on the row or the tray doesn't also click the world behind it (players, pets, fishing),
        // - a right-click there doesn't turn the camera (which would lock the mouse mid-click),
        // - a click there doesn't paint the drawing board behind it (QuadPainterGPU reads the mouse
        //   itself, and chalk gets used up).
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
                Patches.Apply(new Harmony("nelphei.ontogether.iconbar.v2"));
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
        private static readonly Color32 PanelFill = new Color32(36, 34, 45, 235);
        private static readonly Color32 PanelEdge = new Color32(255, 255, 255, 60);
        private static readonly Color32 SlotFill = new Color32(255, 255, 255, 16);
        private static readonly Color32 SlotEdge = new Color32(255, 255, 255, 110);
        private static readonly Color32 DotFill = new Color32(140, 220, 120, 255);
        private static readonly Color32 DotEdge = new Color32(20, 40, 20, 255);

        // The host's textures and styles. Rebuilt when the screen height changes.
        private static readonly Dictionary<string[], Texture2D[]> Icons = new Dictionary<string[], Texture2D[]>();
        private static Texture2D _buttonTexture;
        private static Texture2D _hoverTexture;
        private static Texture2D _openTexture;
        private static Texture2D _panelTexture;
        private static Texture2D _slotTexture;
        private static Texture2D _dotTexture;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _textButtonStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _tipStyle;
        private static int _builtFor;

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
                    char letter = line != null && column < line.Length ? line[column] : '.';
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
            if (_builtFor == Screen.height && _buttonStyle != null && _buttonTexture != null && _dotTexture != null &&
                _panelTexture != null && _slotTexture != null)
            {
                return;
            }
            _builtFor = Screen.height;
            int radius = Mathf.Max(2, Mathf.RoundToInt(6f * m.Unit));
            Destroy(_buttonTexture);
            Destroy(_hoverTexture);
            Destroy(_openTexture);
            Destroy(_panelTexture);
            Destroy(_slotTexture);
            Destroy(_dotTexture);
            _buttonTexture = RoundedBox(radius, ButtonFill, ButtonEdge);
            _hoverTexture = RoundedBox(radius, HoverFill, HoverEdge);
            _openTexture = RoundedBox(radius, OpenFill, OpenEdge);
            _panelTexture = RoundedBox(radius, PanelFill, PanelEdge);
            _slotTexture = RoundedBox(radius, SlotFill, SlotEdge);
            int dot = Mathf.Max(2, Mathf.RoundToInt(3f * m.Unit));
            _dotTexture = RoundedBox(dot, DotFill, DotEdge, 2 * dot);

            int border = radius + 1;
            int fontSize = Mathf.Max(8, Mathf.RoundToInt(15f * m.Unit));
            int smallFont = Mathf.Max(8, Mathf.RoundToInt(14f * m.Unit));
            _buttonStyle = new GUIStyle { border = new RectOffset(border, border, border, border) };
            _textButtonStyle = new GUIStyle
            {
                border = new RectOffset(border, border, border, border),
                padding = new RectOffset(Mathf.RoundToInt(12f * m.Unit), Mathf.RoundToInt(12f * m.Unit),
                    Mathf.RoundToInt(5f * m.Unit), Mathf.RoundToInt(5f * m.Unit)),
                fontSize = smallFont,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                richText = false,
            };
            _textButtonStyle.normal.textColor = Color.white;
            _labelStyle = new GUIStyle
            {
                fontSize = smallFont,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                richText = false,
            };
            _labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            _tipStyle = new GUIStyle
            {
                border = new RectOffset(border, border, border, border),
                padding = new RectOffset(Mathf.RoundToInt(10f * m.Unit), Mathf.RoundToInt(10f * m.Unit),
                    Mathf.RoundToInt(6f * m.Unit), Mathf.RoundToInt(6f * m.Unit)),
                fontSize = fontSize,
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

    // Draws the icon row for every mod (see IconBar). There's one at a time, made by the copy of
    // IconBar.cs with the newest HostVersion.
    internal sealed class IconBarHost : MonoBehaviour
    {
        private void OnGUI()
        {
            IconBar.HostGUI(gameObject);
        }

        private void OnDestroy()
        {
            IconBar.HostDestroyed();
        }
    }
}
