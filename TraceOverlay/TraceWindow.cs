using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OnTogetherMods.Shared;
using UnityEngine;

namespace TraceOverlay
{
    // The image list. It appears while you draw on a chalkboard or whiteboard and goes away when
    // you step back. It sits in the bottom window stack (Shared/PanelStack.cs) and is drawn
    // with Unity's immediate-mode GUI, laid out at 1080p and scaled to the screen.
    internal sealed class TraceWindow : MonoBehaviour
    {
        private const string PanelId = "TraceOverlay";
        private const float Width = 340f;
        private const float Pad = 10f;
        private const float HeaderHeight = 26f;
        private const float RowHeight = 30f;
        private const float Gap = 4f;
        private const float StatusHeight = 22f;
        private const float ScrollbarWidth = 16f;
        private const float StatusSeconds = 5f;

        private static TraceWindow _instance;

        // Set by the patch when you start drawing.
        internal static DrawingManager Drawing;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        // Read by the painting patch. Also true while a click that started on the window is held,
        // so dragging the opacity slider off the window doesn't draw a line.
        internal static bool BlockPainting => IsMouseOver || (_instance != null && _instance._pressOnWindow);

        private static Settings S => Plugin.Settings;

        private readonly BoardGuide _guide = new BoardGuide();
        private readonly List<string> _files = new List<string>();
        private bool _active;

        private Texture2D _texture;
        private string _imagePath;
        private bool _shown = true;
        private float _opacity;
        private bool _pressOnWindow;

        private Vector2 _scroll;
        private string _status;
        private float _statusUntil;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;
        private ClickBlocker _blocker;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallButtonStyle;
        private GUIStyle _imageStyle;
        private GUIStyle _selectedStyle;
        private GUIStyle _emptyStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tipStyle;

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker("TraceOverlay ClickBlocker");
            _opacity = S.Opacity.Value;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
            _guide.Destroy();
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        private void Update()
        {
            try
            {
                bool drawing = Drawing != null && Drawing.IsActive;
                if (drawing && !_active)
                {
                    StartDrawing();
                }
                else if (!drawing && _active)
                {
                    StopDrawing();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Something went wrong. See the BepInEx log.");
            }
            UpdateMouseOver();
        }

        private void StartDrawing()
        {
            QuadPainterGPU board = Drawing.QuadPainterGPUS.FirstOrDefault(b => b != null && b.IsActive);
            if (board == null)
            {
                return;
            }
            _active = true;
            _status = null;

            // The game parks its drawing camera in front of the board (DrawingManager.ActivateDrawing).
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            Vector3 camera = scene != null && scene.DrawCamera != null
                ? scene.DrawCamera.transform.position
                : Camera.main.transform.position;
            _guide.Attach(board, camera);

            RefreshList();
            if (_texture == null && !string.IsNullOrEmpty(S.LastImage.Value))
            {
                string last = Path.Combine(S.Folder(), S.LastImage.Value);
                if (File.Exists(last))
                {
                    LoadImage(last);
                }
            }
            ApplyGuide();
        }

        private void StopDrawing()
        {
            _active = false;
            _pressOnWindow = false;
            _guide.Hide();
            if (!Mathf.Approximately(S.Opacity.Value, _opacity))
            {
                S.Opacity.Value = _opacity;
            }
        }

        private void UpdateMouseOver()
        {
            bool shown = _active && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);

            if (Input.GetMouseButtonDown(0) && IsMouseOver)
            {
                _pressOnWindow = true;
            }
            if (!Input.GetMouseButton(0))
            {
                _pressOnWindow = false;
            }
        }

        // ---- Actions ----

        private void ApplyGuide()
        {
            if (_active && _shown && _texture != null)
            {
                _guide.Show(_texture, _opacity, S.FillBoard.Value);
            }
            else
            {
                _guide.Hide();
            }
        }

        private void RefreshList()
        {
            _files.Clear();
            string folder = S.Folder();
            try
            {
                if (Directory.Exists(folder))
                {
                    _files.AddRange(Directory.GetFiles(folder)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't read the images folder. See the BepInEx log.");
            }
        }

        private void OpenFolder()
        {
            string folder = S.Folder();
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't open " + folder);
            }
        }

        private void SelectImage(string path)
        {
            if (path == _imagePath && _texture != null)
            {
                _shown = true;
                ApplyGuide();
                return;
            }
            if (LoadImage(path))
            {
                _shown = true;
                S.LastImage.Value = Path.GetFileName(path);
                ApplyGuide();
            }
        }

        private bool LoadImage(string path)
        {
            Texture2D texture;
            string error;
            try
            {
                texture = GuideImage.Load(path, out error);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                texture = null;
                error = "couldn't be opened";
            }
            if (texture == null)
            {
                Status(Path.GetFileNameWithoutExtension(path) + " " + error);
                return false;
            }
            if (_texture != null)
            {
                Destroy(_texture);
            }
            _texture = texture;
            _imagePath = path;
            return true;
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // Hidden while Photo Mode takes a photo.
            if (!_active || PanelStack.Hidden)
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
                if (S.Collapsed.Value)
                {
                    DrawCollapsed();
                }
                else
                {
                    DrawWindow();
                }
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

        private Rect PlacePanel(float height)
        {
            Rect panel = PanelStack.Place(PanelId, PanelStack.TraceOverlayOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;
            GUI.Box(panel, GUIContent.none, _boxStyle);
            return panel;
        }

        private void DrawCollapsed()
        {
            Rect panel = PlacePanel(Pad + HeaderHeight + Pad);
            var row = new Rect(panel.x + Pad, panel.y + Pad, panel.width - 2 * Pad, HeaderHeight);
            DrawHeader(row, true);
        }

        private void DrawWindow()
        {
            int rows = Mathf.Clamp(_files.Count, 2, S.VisibleRows.Value);
            float listHeight = rows * RowHeight + (rows - 1) * Gap;
            float height = Pad + HeaderHeight + Gap + listHeight + 2 * Gap + RowHeight + Gap +
                           RowHeight + Gap + RowHeight + Gap + StatusHeight + Pad;

            Rect panel = PlacePanel(height);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            DrawHeader(new Rect(x, y, w, HeaderHeight), false);
            y += HeaderHeight + Gap;

            DrawList(new Rect(x, y, w, listHeight));
            y += listHeight + 2 * Gap;

            DrawGuideRow(new Rect(x, y, w, RowHeight));
            y += RowHeight + Gap;

            DrawOpacityRow(new Rect(x, y, w, RowHeight));
            y += RowHeight + Gap;

            DrawFolderRow(new Rect(x, y, w, RowHeight));
            y += RowHeight + Gap;

            DrawStatus(new Rect(x, y, w, StatusHeight));
        }

        private void DrawHeader(Rect row, bool collapsed)
        {
            const float toggleWidth = 28f;
            const float showWidth = 100f;
            GUI.Label(row, "<b>TRACE OVERLAY</b>", _titleStyle);

            if (collapsed && _texture != null)
            {
                var show = new Rect(row.xMax - toggleWidth - Gap - showWidth, row.y, showWidth, row.height);
                if (GUI.Button(show, _shown ? "Hide guide" : "Show guide", _smallButtonStyle))
                {
                    _shown = !_shown;
                    ApplyGuide();
                }
            }

            var toggle = new Rect(row.xMax - toggleWidth, row.y, toggleWidth, row.height);
            if (GUI.Button(toggle, collapsed ? "+" : "–", _smallButtonStyle))
            {
                S.Collapsed.Value = !collapsed;
            }
        }

        private void DrawList(Rect area)
        {
            if (_files.Count == 0)
            {
                GUI.Label(area, "No images yet. Put .png or .jpg files in the images folder, then click Refresh.", _emptyStyle);
                return;
            }

            float contentHeight = _files.Count * (RowHeight + Gap) - Gap;
            bool scrolls = contentHeight > area.height + 0.5f;
            float rowWidth = scrolls ? area.width - ScrollbarWidth - Gap : area.width;

            // Clicks are acted on after the loop, so the list doesn't change while it's being drawn.
            int select = -1;
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, rowWidth, contentHeight), false, false);
            for (int i = 0; i < _files.Count; i++)
            {
                var row = new Rect(0f, i * (RowHeight + Gap), rowWidth, RowHeight);
                bool selected = _texture != null && _files[i] == _imagePath;
                string name = Path.GetFileNameWithoutExtension(_files[i]);
                if (GUI.Button(row, selected ? "► " + name : name, selected ? _selectedStyle : _imageStyle))
                {
                    select = i;
                }
            }
            GUI.EndScrollView();

            if (select >= 0)
            {
                SelectImage(_files[select]);
            }
        }

        private void DrawGuideRow(Rect row)
        {
            float half = (row.width - Gap) / 2f;
            GUI.enabled = _texture != null;
            if (GUI.Button(new Rect(row.x, row.y, half, row.height), _shown ? "Hide guide" : "Show guide", _buttonStyle))
            {
                _shown = !_shown;
                ApplyGuide();
            }
            GUI.enabled = true;
            string fit = S.FillBoard.Value ? "Fit: Fill board" : "Fit: Whole image";
            if (GUI.Button(new Rect(row.xMax - half, row.y, half, row.height), fit, _buttonStyle))
            {
                S.FillBoard.Value = !S.FillBoard.Value;
                ApplyGuide();
            }
        }

        private void DrawOpacityRow(Rect row)
        {
            const float labelWidth = 70f;
            const float valueWidth = 46f;
            GUI.Label(new Rect(row.x, row.y, labelWidth, row.height), "Opacity", _labelStyle);
            var slider = new Rect(row.x + labelWidth, row.y + row.height / 2f - 6f, row.width - labelWidth - valueWidth - Gap, 12f);
            float value = GUI.HorizontalSlider(slider, _opacity, 0.05f, 1f);
            if (!Mathf.Approximately(value, _opacity))
            {
                _opacity = value;
                ApplyGuide();
            }
            GUI.Label(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height),
                Mathf.RoundToInt(_opacity * 100f) + "%", _valueStyle);
        }

        private void DrawFolderRow(Rect row)
        {
            float half = (row.width - Gap) / 2f;
            if (GUI.Button(new Rect(row.x, row.y, half, row.height), "Open images folder", _buttonStyle))
            {
                OpenFolder();
            }
            if (GUI.Button(new Rect(row.xMax - half, row.y, half, row.height), "Refresh", _buttonStyle))
            {
                RefreshList();
                Status(_files.Count == 1 ? "Found 1 image" : "Found " + _files.Count + " images");
            }
        }

        private void DrawStatus(Rect row)
        {
            if (_status != null && Time.unscaledTime < _statusUntil)
            {
                GUI.Label(row, _status, _statusStyle);
            }
            else if (_texture == null && _files.Count > 0)
            {
                GUI.Label(row, "Click an image to lay it over the board", _tipStyle);
            }
            else if (_texture != null)
            {
                GUI.Label(row, "Only you can see the guide", _tipStyle);
            }
        }

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.65f);
            var green = (Color)new Color32(0x7C, 0xD9, 0x92, 0xFF);

            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _labelStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _valueStyle = Label(14, dim, TextAnchor.MiddleRight);
            _tipStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _emptyStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _emptyStyle.wordWrap = true;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                padding = new RectOffset(6, 6, 2, 2),
                clipping = TextClipping.Clip,
                richText = false,
            };
            _smallButtonStyle = new GUIStyle(_buttonStyle) { fontSize = 14, padding = new RectOffset(2, 2, 0, 0) };
            // File names are shown as they are, never read as formatting tags.
            _imageStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 2, 2),
                richText = false,
                clipping = TextClipping.Clip,
            };
            _selectedStyle = new GUIStyle(_imageStyle);
            _selectedStyle.normal.textColor = green;
            _selectedStyle.hover.textColor = green;
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
