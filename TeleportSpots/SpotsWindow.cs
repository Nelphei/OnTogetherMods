using System;
using System.Collections.Generic;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace TeleportSpots
{
    // The teleport window: save where you're standing, then click a name to go back there.
    // It sits in the bottom-left window stack (Shared/PanelStack.cs) and is drawn with Unity's
    // immediate-mode GUI, laid out at 1080p and scaled to the screen.
    internal sealed class SpotsWindow : MonoBehaviour
    {
        private const string PanelId = "TeleportSpots";
        private const string FieldName = "TeleportSpots.Name";
        private const float Width = 330f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 30f;
        private const float Gap = 4f;
        private const float StatusHeight = 22f;
        private const float ScrollbarWidth = 16f;
        private const float StatusSeconds = 4f;

        private enum EditMode { None, Add, Rename, Delete }

        private static SpotsWindow _instance;

        // Read by the patches. True while the name box is open, so the game ignores the keys.
        internal static bool IsTyping =>
            _instance != null && _instance._open &&
            (_instance._edit == EditMode.Add || _instance._edit == EditMode.Rename);

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly SpotStore _store = new SpotStore();
        private TextChannelManager _channel;
        private bool _open;

        private EditMode _edit;
        private int _editIndex = -1;
        private string _editText = "";
        private Spot _newSpot;
        private bool _focusField;
        private bool _selectAll;
        private bool _clearFocus;

        private Vector2 _scroll;
        private string _status;
        private float _statusUntil;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;

        // An invisible piece of game UI under the window. It stops clicks reaching the game's own
        // buttons (like the chat box) and stops the scroll wheel zooming the camera.
        private GameObject _blocker;
        private RectTransform _blockerArea;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _spotStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _promptStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tipStyle;

        private void Awake()
        {
            _instance = this;
            _store.Load();
            CreateBlocker();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            if (_blocker != null)
            {
                Destroy(_blocker);
            }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(S.ToggleKey.Value) && !IsChatOpen())
                {
                    if (_open)
                    {
                        Close();
                    }
                    else
                    {
                        Open();
                    }
                }
                UpdateMouseOver();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        private void Open()
        {
            _open = true;
            _status = null;
        }

        private void Close()
        {
            // Closing while naming a spot keeps it, the same as clicking away. Esc is the way to cancel.
            if (IsTyping)
            {
                ConfirmEdit();
            }
            else
            {
                EndEdit();
            }
            _open = false;
        }

        private void UpdateMouseOver()
        {
            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            Vector3 mouse = Input.mousePosition;
            IsMouseOver = shown && _screenRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));

            if (_blocker == null)
            {
                return;
            }
            if (shown)
            {
                // Game UI measures from the bottom-left.
                _blockerArea.anchoredPosition = new Vector2(_screenRect.x, Screen.height - _screenRect.yMax);
                _blockerArea.sizeDelta = _screenRect.size;
            }
            if (_blocker.activeSelf != shown)
            {
                _blocker.SetActive(shown);
            }
        }

        private void CreateBlocker()
        {
            _blocker = new GameObject("TeleportSpots ClickBlocker");
            DontDestroyOnLoad(_blocker);
            var canvas = _blocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            _blocker.AddComponent<GraphicRaycaster>();

            // A see-through image still catches clicks.
            var area = new GameObject("Area", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            area.transform.SetParent(_blocker.transform, false);
            area.GetComponent<Image>().color = Color.clear;
            _blockerArea = (RectTransform)area.transform;
            _blockerArea.anchorMin = Vector2.zero;
            _blockerArea.anchorMax = Vector2.zero;
            _blockerArea.pivot = Vector2.zero;
            _blocker.SetActive(false);
        }

        private static bool IsChatOpen()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        private TextChannelManager Channel()
        {
            // Looked up only when you click, because on the main menu this searches the scene.
            if (_channel == null)
            {
                _channel = NetworkSingleton<TextChannelManager>.I;
            }
            return _channel;
        }

        // ---- Actions ----

        private void StartAdd()
        {
            TextChannelManager channel = Channel();
            string busy = Game.WhyBusy(channel);
            if (busy != null)
            {
                Status(busy);
                return;
            }
            // Remember the spot now, so it doesn't matter if you drift while typing the name.
            _newSpot = Game.Capture(channel.MainMovementController, _store.NextDefaultName());
            StartEdit(EditMode.Add, -1, _newSpot.Name);
        }

        private void TeleportTo(int index)
        {
            Spot spot = _store.Spots[index];
            TextChannelManager channel = Channel();
            string busy = Game.WhyBusy(channel);
            if (busy != null)
            {
                Status(busy);
                return;
            }

            try
            {
                Game.Teleport(channel.MainMovementController, spot, S.RestoreView.Value);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Teleport failed. See the BepInEx log.");
                return;
            }

            Status("Teleported to " + spot.Name);
            if (S.CloseAfterTeleport.Value)
            {
                Close();
            }
        }

        private void StartEdit(EditMode mode, int index, string text)
        {
            _edit = mode;
            _editIndex = index;
            _editText = text;
            _focusField = mode == EditMode.Add || mode == EditMode.Rename;
        }

        private void ConfirmEdit()
        {
            string name = SpotStore.CleanName(_editText);
            List<Spot> spots = _store.Spots;

            if (_edit == EditMode.Add && _newSpot != null)
            {
                if (name.Length > 0)
                {
                    _newSpot.Name = name;
                }
                spots.Add(_newSpot);
                SaveAndReport("Saved " + _newSpot.Name);
                // Scroll to the bottom, where the new spot is. The scroll view clamps it.
                _scroll.y = float.MaxValue;
            }
            else if (_edit == EditMode.Rename && _editIndex >= 0 && _editIndex < spots.Count &&
                     name.Length > 0 && name != spots[_editIndex].Name)
            {
                spots[_editIndex].Name = name;
                SaveAndReport("Renamed to " + name);
            }
            EndEdit();
        }

        private void CancelEdit()
        {
            EndEdit();
        }

        private void EndEdit()
        {
            if (_edit == EditMode.Add || _edit == EditMode.Rename)
            {
                _clearFocus = true;
            }
            _edit = EditMode.None;
            _editIndex = -1;
            _editText = "";
            _newSpot = null;
            _focusField = false;
            _selectAll = false;
        }

        private void DeleteSpot(int index)
        {
            List<Spot> spots = _store.Spots;
            if (index < 0 || index >= spots.Count)
            {
                EndEdit();
                return;
            }
            string name = spots[index].Name;
            spots.RemoveAt(index);
            EndEdit();
            SaveAndReport("Deleted " + name);
        }

        private void SaveAndReport(string message)
        {
            Status(_store.Save() ? message : "Couldn't save the spots file. See the BepInEx log.");
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
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

            Matrix4x4 previous = PanelStack.Begin(out _);
            try
            {
                DrawWindow();
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
                EndEdit();
            }
            finally
            {
                PanelStack.End(previous);
            }
        }

        private void DrawWindow()
        {
            List<Spot> spots = _store.Spots;
            int rows = Mathf.Clamp(spots.Count, 1, S.VisibleRows.Value);
            float listHeight = rows * RowHeight + (rows - 1) * Gap;
            float height = Pad + HeaderHeight + Gap + RowHeight + 2 * Gap + listHeight + Gap + StatusHeight + Pad;

            Rect panel = PanelStack.Place(PanelId, PanelStack.TeleportSpotsOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            HandleKeys();
            HandleClickAway(panel);

            GUI.Box(panel, GUIContent.none, _boxStyle);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>TELEPORT SPOTS</b>", _titleStyle);
            GUI.Label(new Rect(x, y, w, HeaderHeight), $"{S.ToggleKey.Value} to close", _keyStyle);
            y += HeaderHeight + Gap;

            DrawSaveRow(new Rect(x, y, w, RowHeight));
            y += RowHeight + 2 * Gap;

            DrawList(new Rect(x, y, w, listHeight));
            y += listHeight + Gap;

            DrawStatus(new Rect(x, y, w, StatusHeight));
        }

        // Enter keeps the name, Esc cancels. The game doesn't see either (see Patches.cs).
        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || !IsTyping)
            {
                return;
            }
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                ConfirmEdit();
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                CancelEdit();
                e.Use();
            }
        }

        // Clicking outside the window keeps a name you were typing, and says no to a delete.
        private void HandleClickAway(Rect panel)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || _edit == EditMode.None || panel.Contains(e.mousePosition))
            {
                return;
            }
            if (IsTyping)
            {
                ConfirmEdit();
            }
            else
            {
                CancelEdit();
            }
        }

        private void DrawSaveRow(Rect row)
        {
            if (_edit == EditMode.Add)
            {
                DrawNameEditor(row, "Save");
                return;
            }
            GUI.enabled = _edit == EditMode.None;
            if (GUI.Button(row, "+  Save this spot", _buttonStyle))
            {
                StartAdd();
            }
            GUI.enabled = true;
        }

        private void DrawNameEditor(Rect row, string confirmText)
        {
            const float button = 62f;
            var field = new Rect(row.x, row.y, row.width - 2 * (button + Gap), row.height);

            GUI.SetNextControlName(FieldName);
            _editText = GUI.TextField(field, _editText ?? "", SpotStore.MaxNameLength, _fieldStyle);
            if (_focusField || GUI.GetNameOfFocusedControl() != FieldName)
            {
                GUI.FocusControl(FieldName);
                _selectAll |= _focusField;
                _focusField = false;
            }
            else if (_selectAll && Event.current.type == EventType.Repaint)
            {
                // Select the starting name, so typing replaces it.
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor.SelectAll();
                _selectAll = false;
            }

            if (GUI.Button(new Rect(field.xMax + Gap, row.y, button, row.height), confirmText, _buttonStyle))
            {
                ConfirmEdit();
            }
            if (GUI.Button(new Rect(row.xMax - button, row.y, button, row.height), "Cancel", _buttonStyle))
            {
                CancelEdit();
            }
        }

        private void DrawList(Rect area)
        {
            List<Spot> spots = _store.Spots;
            if (spots.Count == 0)
            {
                GUI.Label(area, "No spots yet. Stand somewhere you like and click Save.", _emptyStyle);
                return;
            }

            float contentHeight = spots.Count * (RowHeight + Gap) - Gap;
            bool scrolls = contentHeight > area.height + 0.5f;
            float rowWidth = scrolls ? area.width - ScrollbarWidth - Gap : area.width;

            // Clicks are acted on after the loop, so the list doesn't change while it's being drawn.
            int teleport = -1;
            int rename = -1;
            int remove = -1;
            bool confirmDelete = false;

            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, rowWidth, contentHeight), false, false);
            for (int i = 0; i < spots.Count; i++)
            {
                var row = new Rect(0f, i * (RowHeight + Gap), rowWidth, RowHeight);

                if (_edit == EditMode.Rename && _editIndex == i)
                {
                    DrawNameEditor(row, "OK");
                    continue;
                }

                if (_edit == EditMode.Delete && _editIndex == i)
                {
                    const float button = 50f;
                    GUI.Label(new Rect(row.x + 4f, row.y, row.width - 2 * (button + Gap) - 4f, row.height),
                        "Delete " + spots[i].Name + "?", _promptStyle);
                    if (GUI.Button(new Rect(row.xMax - 2 * button - Gap, row.y, button, row.height), "Yes", _buttonStyle))
                    {
                        confirmDelete = true;
                    }
                    if (GUI.Button(new Rect(row.xMax - button, row.y, button, row.height), "No", _buttonStyle))
                    {
                        CancelEdit();
                    }
                    continue;
                }

                const float editWidth = 46f;
                const float deleteWidth = 30f;
                GUI.enabled = _edit == EditMode.None;
                if (GUI.Button(new Rect(row.x, row.y, row.width - editWidth - deleteWidth - 2 * Gap, row.height), spots[i].Name, _spotStyle))
                {
                    teleport = i;
                }
                if (GUI.Button(new Rect(row.xMax - deleteWidth - Gap - editWidth, row.y, editWidth, row.height), "Edit", _buttonStyle))
                {
                    rename = i;
                }
                if (GUI.Button(new Rect(row.xMax - deleteWidth, row.y, deleteWidth, row.height), "X", _buttonStyle))
                {
                    remove = i;
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();

            if (confirmDelete)
            {
                DeleteSpot(_editIndex);
            }
            else if (teleport >= 0)
            {
                TeleportTo(teleport);
            }
            else if (rename >= 0)
            {
                StartEdit(EditMode.Rename, rename, spots[rename].Name);
            }
            else if (remove >= 0)
            {
                StartEdit(EditMode.Delete, remove, "");
            }
        }

        private void DrawStatus(Rect row)
        {
            if (_status != null && Time.unscaledTime < _statusUntil)
            {
                GUI.Label(row, _status, _statusStyle);
            }
            else if (IsTyping)
            {
                GUI.Label(row, "Enter to save, Esc to cancel", _tipStyle);
            }
            else if (_store.Spots.Count > 0)
            {
                GUI.Label(row, "Click a name to teleport there", _tipStyle);
            }
        }

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.65f);

            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, dim, TextAnchor.MiddleRight);
            _promptStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _tipStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _emptyStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _emptyStyle.wordWrap = true;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                padding = new RectOffset(4, 4, 2, 2),
            };
            // Spot names are shown as typed, never read as formatting tags.
            _spotStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 2, 2),
                richText = false,
                clipping = TextClipping.Clip,
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
