using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ManagedDoom;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = UnityEngine.EventType;

namespace DoomTogether
{
    // The DOOM window: a title row (game file, volume, controls, size, close) over DOOM's picture,
    // stacked with the other mods' windows. DOOM gets the keyboard while you play in it: opening the
    // window or clicking it gives DOOM the keys, and clicking anywhere else (or opening chat or a
    // game menu) gives them back to the game, which freezes DOOM until you click it again.
    internal sealed class DoomWindow : MonoBehaviour
    {
        private const string PanelId = "DoomTogether";
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // Layout, in 1080p units.
        private const float Pad = 8f;
        private const float HeaderHeight = 28f;
        private const float Gap = 6f;
        private const float ButtonGap = 4f;

        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Shade = new Color(0f, 0f, 0f, 0.55f);

        private static DoomWindow _instance;

        private static Settings S => Plugin.Settings;

        // Read by the patches. True while DOOM has the keyboard, so the game ignores it.
        internal static bool HoldsKeys => _instance != null && _instance.KeysToDoom;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private bool _open;
        private bool _hasKeys;
        private DoomHost _game;
        private string _gamePath;
        private string _error;
        private List<Wad> _wads = new List<Wad>();
        private bool _wadMenu;
        // The controls card over DOOM's picture (the ? button). DOOM waits while it shows.
        private bool _help;
        // The volume slider under the speaker button. The volume changes as you drag, and is saved
        // to the settings when the slider closes (not on every step).
        private bool _volumeMenu;
        private int _volume;

        private bool _iconClicked;
        // Clicks in the window, acted on in Update (outside the GUI pass).
        private readonly List<Action> _clicks = new List<Action>();

        private ClickBlocker _blocker;
        private Rect _screenRect;
        private int _drawnFrame = -100;

        // Dragging the window by its title row. While dragging, the position is kept here and only
        // saved to the settings when you let go.
        private int _dragControl;
        private bool _dragging;
        private Vector2 _dragOffset;
        private Vector2 _dragPos;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _centreStyle;
        private GUIStyle _helpHeadStyle;
        private GUIStyle _helpKeyStyle;
        private GUIStyle _helpTextStyle;
        private GUIStyle _helpNoteStyle;
        private GUIStyle _valueStyle;
        private Texture2D _shade;
        private Texture2D _speakerLoud;
        private Texture2D _speakerQuiet;
        private Texture2D _speakerOff;

        private bool KeysToDoom => _open && _hasKeys && _game != null && !PanelStack.Hidden;

        // An IWAD this mod can play: the bundled Freedoom ones, or the player's own.
        private sealed class Wad
        {
            public string Path;
            public string Name;
        }

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "DoomTogether ClickBlocker");
            _volume = Mathf.Clamp(S.Volume.Value, 0, 100);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            SaveVolume();
            StopGame();
            _blocker?.Destroy();
            foreach (Texture2D texture in new[] { _shade, _speakerLoud, _speakerQuiet, _speakerOff })
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
        }

        // ---- Every frame ----

        private void Update()
        {
            try
            {
                bool inWorld = SceneManager.GetActiveScene().buildIndex == WorldScene;
                bool clicked = _iconClicked;
                _iconClicked = false;
                if (inWorld && (clicked || (S.WindowKey.Value != KeyCode.None && Input.GetKeyDown(S.WindowKey.Value) && !IsTyping())))
                {
                    Toggle();
                }
                if (!inWorld && _open)
                {
                    Close();
                }

                List<Action> clicks = _clicks.ToList();
                _clicks.Clear();
                foreach (Action click in clicks)
                {
                    click();
                }

                UpdateKeyboardOwner();
                RunGame();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        // Who gets the keyboard: DOOM after a click on the window, the game after a click anywhere
        // else (except the mod icons), or when chat, a game menu or Photo Mode takes over.
        private void UpdateKeyboardOwner()
        {
            bool had = KeysToDoom;
            if (_open && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
            {
                if (ClickBlocker.MouseIn(_screenRect) && Time.frameCount - _drawnFrame <= 2)
                {
                    _hasKeys = true;
                }
                else if (!IconBar.MouseOver)
                {
                    _hasKeys = false;
                    _wadMenu = false;
                    CloseVolume();
                }
            }
            if (_hasKeys && (IsTyping() || GameMenuOpen() || PanelStack.Hidden))
            {
                _hasKeys = false;
            }
            if (had && !KeysToDoom)
            {
                _game?.ReleaseKeys();
            }
        }

        private void RunGame()
        {
            if (_game == null)
            {
                return;
            }

            if (KeysToDoom)
            {
                foreach (KeyValuePair<KeyCode, ManagedDoom.DoomKey> pair in UnityUserInput.Keys)
                {
                    if (Input.GetKeyDown(pair.Key))
                    {
                        if (_help)
                        {
                            _help = false;   // any key closes the controls card, and goes no further
                        }
                        else
                        {
                            _game.Key(pair.Value, true);
                        }
                    }
                    else if (Input.GetKeyUp(pair.Key))
                    {
                        _game.Key(pair.Value, false);
                    }
                }
            }

            _game.Freeze(!_open || _help || (!KeysToDoom && S.PauseWithoutKeys.Value));
            _game.Tick(Time.unscaledDeltaTime);

            if (_game.Finished)
            {
                // Quit from DOOM's own menu.
                StopGame();
                Close();
            }
        }

        // ---- Opening and closing ----

        private void Toggle()
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

        private void Open()
        {
            _open = true;
            _hasKeys = true;
            _wadMenu = false;
            if (!S.ControlsSeen.Value)
            {
                _help = true;
                S.ControlsSeen.Value = true;
            }
            if (_game == null)
            {
                StartGame(PickWad());
            }
            if (S.SitDown.Value)
            {
                string why = Gaming.TrySitDown();
                if (why != null)
                {
                    Plugin.Log.LogInfo($"Not sitting down to play: {why}.");
                }
            }
        }

        // Closing keeps your game where it is (frozen); opening again carries on.
        private void Close()
        {
            _open = false;
            _hasKeys = false;
            _wadMenu = false;
            _help = false;
            CloseVolume();
            _game?.ReleaseKeys();
        }

        private void CloseVolume()
        {
            _volumeMenu = false;
            SaveVolume();
        }

        private void SaveVolume()
        {
            if (S.Volume.Value != _volume)
            {
                S.Volume.Value = _volume;
            }
        }

        // ---- The game files ----

        private void FindWads()
        {
            var found = new Dictionary<string, Wad>(StringComparer.OrdinalIgnoreCase);
            // Your own folder first, so your copy wins over a bundled one with the same name.
            foreach (string folder in new[] { S.Folder(), Plugin.ModFolder })
            {
                try
                {
                    if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    {
                        continue;
                    }
                    foreach (string path in Directory.GetFiles(folder))
                    {
                        string name = Path.GetFileName(path);
                        if (string.Equals(Path.GetExtension(path), ".wad", StringComparison.OrdinalIgnoreCase) &&
                            ConfigUtilities.IsIwad(path) && !found.ContainsKey(name))
                        {
                            found[name] = new Wad { Path = path, Name = DisplayName(name) };
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Couldn't look for WADs in {folder}: {e.Message}");
                }
            }
            _wads = found.Values.OrderBy(w => Rank(Path.GetFileName(w.Path))).ToList();
        }

        // The order DOOM itself looks for game files in: the full games before Freedoom.
        private static readonly string[] Order =
        {
            "DOOM2.WAD", "PLUTONIA.WAD", "TNT.WAD", "DOOM.WAD", "DOOM1.WAD", "FREEDOOM2.WAD", "FREEDOOM1.WAD",
        };

        private static int Rank(string file)
        {
            int i = Array.IndexOf(Order, file.ToUpperInvariant());
            return i < 0 ? Order.Length : i;
        }

        private static string DisplayName(string file)
        {
            switch (file.ToUpperInvariant())
            {
                case "DOOM2.WAD": return "DOOM II";
                case "PLUTONIA.WAD": return "Final DOOM: Plutonia";
                case "TNT.WAD": return "Final DOOM: TNT";
                case "DOOM.WAD": return "DOOM";
                case "DOOM1.WAD": return "DOOM (shareware)";
                case "FREEDOOM2.WAD": return "Freedoom 2";
                case "FREEDOOM1.WAD": return "Freedoom 1";
                default: return Path.GetFileNameWithoutExtension(file);
            }
        }

        // The game file to start with: the one played last time, or the first in DOOM's order.
        private string PickWad()
        {
            FindWads();
            Wad last = _wads.FirstOrDefault(w => string.Equals(Path.GetFileName(w.Path), S.Wad.Value, StringComparison.OrdinalIgnoreCase));
            return (last ?? _wads.FirstOrDefault())?.Path;
        }

        private void StartGame(string wadPath)
        {
            StopGame();
            _error = null;
            if (wadPath == null)
            {
                _error = "No game files found. Put DOOM.WAD or DOOM2.WAD in\n" + S.Folder();
                return;
            }
            try
            {
                _game = new DoomHost(wadPath, gameObject, S.Music.Value, () => _volume / 100f);
                _gamePath = wadPath;
                _game.RenderNow();
                S.Wad.Value = Path.GetFileName(wadPath);
                Plugin.Log.LogInfo($"Started DOOM with {wadPath}.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"DOOM couldn't start with {wadPath}: {e}");
                _error = "DOOM couldn't start with " + Path.GetFileName(wadPath) + ":\n" + e.Message;
                StopGame();
            }
        }

        private void StopGame()
        {
            if (_game != null)
            {
                try
                {
                    _game.Dispose();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError(e);
                }
                _game = null;
                _gamePath = null;
            }
        }

        private void OpenFolder()
        {
            string folder = S.Folder();
            try
            {
                Directory.CreateDirectory(folder);
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    Process.Start("explorer.exe", "\"" + folder + "\"");
                }
                else
                {
                    // Opens Finder on a Mac.
                    Application.OpenURL(new Uri(folder).AbsoluteUri);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value)
            {
                IconBar.State state = _open ? IconBar.State.Open : IconBar.State.Plain;
                if (IconBar.Draw("DoomTogether", IconBar.DoomTogetherOrder, Icon.Pixels, state, "Doom Together",
                        S.WindowKey.Value) == IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
            }

            // Hidden while Photo Mode takes a photo, and while the game's eye button hides the UI.
            if (!_open || PanelStack.Hidden || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                // A drag cut short by the window closing would keep every IMGUI control busy.
                if (_dragging && GUIUtility.hotControl == _dragControl)
                {
                    GUIUtility.hotControl = 0;
                }
                _dragging = false;
                return;
            }
            if (_boxStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out float _);
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
            // First, so it keeps the same id from frame to frame.
            _dragControl = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);

            Vector2 view = ViewSize(S.Size.Value);
            var size = new Vector2(view.x + 2 * Pad, Pad + HeaderHeight + Gap + view.y + Pad);
            // In the stack with the other mods' windows, or wherever you dragged it.
            bool moved = _dragging || S.WindowX.Value >= 0f;
            Rect panel = moved
                ? new Rect(Clamp(_dragging ? _dragPos : new Vector2(S.WindowX.Value, S.WindowY.Value), size), size)
                : PanelStack.Place(PanelId, PanelStack.DoomTogetherOrder, size);
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            GUI.Box(panel, GUIContent.none, _boxStyle);

            // Title row: name and hint on the left, buttons on the right.
            float x = panel.x + Pad;
            float y = panel.y + Pad;
            float right = panel.xMax - Pad;
            right = HeaderButton(right, y, "X", () => Close());
            right = HeaderButton(right, y, SizeLabel(S.Size.Value), () => S.Size.Value = Next(S.Size.Value));
            right = HeaderButton(right, y, "?", () =>
            {
                _help = !_help;
                _wadMenu = false;
                CloseVolume();
            });
            float speakerRight = right;
            Texture2D speaker = _volume == 0 ? _speakerOff : _volume < 50 ? _speakerQuiet : _speakerLoud;
            right = IconButton(right, y, speaker, () =>
            {
                if (_volumeMenu)
                {
                    CloseVolume();
                }
                else
                {
                    _volumeMenu = true;
                    _wadMenu = false;
                    _help = false;
                }
            });
            string wadName = _wads.FirstOrDefault(w => w.Path == _gamePath)?.Name ?? "Game";
            right = HeaderButton(right, y, wadName + (_wadMenu ? " ▴" : " ▾"), () =>
            {
                _wadMenu = !_wadMenu;
                _help = false;
                CloseVolume();
                if (_wadMenu)
                {
                    FindWads();
                }
            });

            // The rest of the title row moves the window (the buttons above have taken their clicks).
            HandleDrag(new Rect(panel.x, panel.y, right - panel.x, Pad + HeaderHeight), panel, size);

            GUI.Label(new Rect(x, y, 140f, HeaderHeight), "<b>DOOM TOGETHER</b>", _titleStyle);
            string hint = KeysToDoom ? "?: controls.  Click outside to get back to the game."
                : _game != null ? "Click the screen to play.  Drag this row to move the window." : "";
            GUI.Label(new Rect(x + 140f, y, Mathf.Max(0f, right - x - 140f - ButtonGap), HeaderHeight), hint, _hintStyle);

            // DOOM's picture, at 4:3.
            var screen = new Rect(x, y + HeaderHeight + Gap, view.x, view.y);
            GUI.DrawTexture(screen, Texture2D.blackTexture);
            Texture2D picture = _game?.Texture;
            if (picture != null)
            {
                FilterMode filter = S.Smooth.Value ? FilterMode.Bilinear : FilterMode.Point;
                if (picture.filterMode != filter)
                {
                    picture.filterMode = filter;
                }
                DrawTurned(screen, picture);
            }

            if (_error != null)
            {
                GUI.Label(screen, _error, _centreStyle);
            }
            else if (_help)
            {
                DrawControls(screen);
            }
            else if (_game != null && !KeysToDoom && S.PauseWithoutKeys.Value)
            {
                GUI.DrawTexture(screen, _shade);
                GUI.Label(screen, "<b>Click to play</b>", _centreStyle);
            }

            if (_wadMenu)
            {
                DrawWadMenu(screen);
            }
            if (_volumeMenu)
            {
                DrawVolumeMenu(screen, speakerRight);
            }
        }

        // The volume slider, just under the speaker button. Drag it, click along it, or scroll over it.
        private void DrawVolumeMenu(Rect screen, float right)
        {
            const float width = 250f;
            const float rowHeight = 26f;
            const float labelWidth = 62f;
            const float valueWidth = 44f;
            float x = Mathf.Max(screen.x + Pad, Mathf.Min(right, screen.xMax - Pad) - width);
            var box = new Rect(x, screen.y + Pad, width, rowHeight + 2 * Pad);
            GUI.Box(box, GUIContent.none, _boxStyle);
            GUI.Box(box, GUIContent.none, _boxStyle);

            var row = new Rect(box.x + Pad, box.y + Pad, width - 2 * Pad, rowHeight);
            GUI.Label(new Rect(row.x, row.y, labelWidth, row.height), "Volume", _titleStyle);
            var slider = new Rect(row.x + labelWidth, row.y + row.height / 2f - 6f, row.width - labelWidth - valueWidth - Gap, 12f);
            int value = Mathf.RoundToInt(GUI.HorizontalSlider(slider, _volume, 0f, 100f));
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel && box.Contains(e.mousePosition))
            {
                // Scrolling up is louder, to the next step of 5.
                if (e.delta.y < 0f)
                {
                    value = (_volume / 5 + 1) * 5;
                }
                else if (e.delta.y > 0f)
                {
                    value = ((_volume + 4) / 5 - 1) * 5;
                }
                e.Use();
            }
            _volume = Mathf.Clamp(value, 0, 100);
            GUI.Label(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height), _volume + "%", _valueStyle);
        }

        // The texture holds DOOM's picture on its side (see UnityVideo), so it's drawn turned a
        // quarter turn: the texture's width runs down the screen and its height across it.
        private static void DrawTurned(Rect screen, Texture2D picture)
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(screen.xMax, screen.y, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            GUI.DrawTexture(new Rect(0f, 0f, screen.height, screen.width), picture, ScaleMode.StretchToFill, false);
            GUI.matrix = previous;
        }

        // The controls card: DOOM's keys in two columns over the picture. Any key or a click closes it.
        // Arrow keys are spelled out, since IMGUI's font may not have arrow symbols.
        private static readonly string[,] PlayKeys =
        {
            { "W / S", "Move" },
            { "Up / Down", "Move" },
            { "Left / Right", "Turn" },
            { "A / D", "Sidestep" },
            { "Alt + Left/Right", "Sidestep" },
            { "Ctrl", "Shoot" },
            { "Space", "Doors and switches" },
            { "Shift (hold)", "Walk" },
            { "1 - 7", "Weapons" },
            { "Tab", "Map" },
            { "-  /  =", "View size / zoom" },
        };

        private static readonly string[,] MenuKeys =
        {
            { "Esc", "Menu (save, load)" },
            { "Arrows, Enter", "Pick in the menu" },
            { "Y / N", "Yes / no" },
            { "Pause", "Pause" },
            { "F / M / C", "Map: follow/mark/clear" },
            { "Click outside", "Back to the game" },
            { "?", "This card" },
        };

        private void DrawControls(Rect screen)
        {
            // A click anywhere on the card closes it. First, so it gets the click before the labels.
            if (GUI.Button(screen, GUIContent.none, GUIStyle.none))
            {
                _clicks.Add(() => _help = false);
            }
            GUI.DrawTexture(screen, _shade);
            GUI.DrawTexture(screen, _shade);

            // Laid out for the Medium screen (480 high) and scaled to the others.
            float scale = screen.height / 480f;
            int text = Mathf.Max(10, Mathf.RoundToInt(15f * scale));
            float row = Mathf.Round(text * 1.5f);
            float pad = Mathf.Round(16f * scale);
            _helpKeyStyle.fontSize = text;
            _helpTextStyle.fontSize = text;
            _helpNoteStyle.fontSize = text;
            _helpHeadStyle.fontSize = Mathf.Max(11, Mathf.RoundToInt(17f * scale));

            float columnWidth = (screen.width - 3 * pad) / 2f;
            float top = screen.y + pad;
            DrawKeyColumn(new Rect(screen.x + pad, top, columnWidth, screen.height), row, "Playing", PlayKeys);
            DrawKeyColumn(new Rect(screen.x + 2 * pad + columnWidth, top, columnWidth, screen.height), row, "Menus and more", MenuKeys);

            GUI.Label(new Rect(screen.x + pad, screen.yMax - pad - 2.4f * row, screen.width - 2 * pad, 2.4f * row),
                "Press any key or click to close. The mouse doesn't aim, and the F keys are left to your other mods.",
                _helpNoteStyle);
        }

        private void DrawKeyColumn(Rect area, float row, string title, string[,] keys)
        {
            float y = area.y;
            GUI.Label(new Rect(area.x, y, area.width, row * 1.2f), "<b>" + title + "</b>", _helpHeadStyle);
            y += row * 1.4f;
            // As wide as the widest key, up to half the column.
            float keyWidth = 0f;
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                keyWidth = Mathf.Max(keyWidth, _helpKeyStyle.CalcSize(new GUIContent(keys[i, 0])).x);
            }
            keyWidth = Mathf.Min(Mathf.Round(keyWidth + 10f), Mathf.Round(area.width * 0.5f));
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                GUI.Label(new Rect(area.x, y, keyWidth - 4f, row), keys[i, 0], _helpKeyStyle);
                GUI.Label(new Rect(area.x + keyWidth, y, area.width - keyWidth, row), keys[i, 1], _helpTextStyle);
                y += row;
            }
        }

        // A list of game files over the top-right of the picture. Picking one starts a new game.
        private void DrawWadMenu(Rect screen)
        {
            const float rowHeight = 26f;
            const float width = 240f;
            int rows = _wads.Count + 1;
            var box = new Rect(screen.xMax - width - Pad, screen.y + Pad, width, Pad * 2 + rows * (rowHeight + ButtonGap) + 34f);
            GUI.Box(box, GUIContent.none, _boxStyle);
            float y = box.y + Pad;
            foreach (Wad wad in _wads)
            {
                bool current = wad.Path == _gamePath;
                string label = current ? "▸ " + wad.Name : wad.Name;
                if (GUI.Button(new Rect(box.x + Pad, y, width - 2 * Pad, rowHeight), label, _buttonStyle) && !current)
                {
                    string path = wad.Path;
                    _clicks.Add(() =>
                    {
                        _wadMenu = false;
                        StartGame(path);
                    });
                }
                y += rowHeight + ButtonGap;
            }
            if (GUI.Button(new Rect(box.x + Pad, y, width - 2 * Pad, rowHeight), "Open WAD folder", _buttonStyle))
            {
                _clicks.Add(OpenFolder);
            }
            y += rowHeight + ButtonGap;
            GUI.Label(new Rect(box.x + Pad, y, width - 2 * Pad, 34f),
                "Picking a game starts it over.\nSave first in DOOM's menu.", _hintStyle);
        }

        // Drag the title row to move the window anywhere on screen; double-click it to put the window
        // back with the other mods' windows. Positions are in 1080p units, like the rest of the layout.
        private void HandleDrag(Rect area, Rect panel, Vector2 size)
        {
            Event e = Event.current;
            switch (e.GetTypeForControl(_dragControl))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && area.Contains(e.mousePosition))
                    {
                        if (e.clickCount == 2)
                        {
                            _dragging = false;
                            S.WindowX.Value = -1f;
                            S.WindowY.Value = -1f;
                        }
                        else
                        {
                            GUIUtility.hotControl = _dragControl;
                            _dragging = true;
                            _dragOffset = e.mousePosition - panel.position;
                            _dragPos = panel.position;
                        }
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == _dragControl)
                    {
                        _dragPos = Clamp(e.mousePosition - _dragOffset, size);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == _dragControl)
                    {
                        GUIUtility.hotControl = 0;
                        _dragging = false;
                        Vector2 at = Clamp(_dragPos, size);
                        S.WindowX.Value = Mathf.Round(at.x);
                        S.WindowY.Value = Mathf.Round(at.y);
                        e.Use();
                    }
                    break;
            }
        }

        // Kept wholly on screen, whatever the screen's shape or the window's size.
        private static Vector2 Clamp(Vector2 position, Vector2 size)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0f, Mathf.Max(0f, PanelStack.Width - size.x)),
                Mathf.Clamp(position.y, 0f, Mathf.Max(0f, 1080f - size.y)));
        }

        private float HeaderButton(float right, float y, string label, Action action)
        {
            float width = Mathf.Max(28f, _buttonStyle.CalcSize(new GUIContent(label)).x + 12f);
            var rect = new Rect(right - width, y + 2f, width, HeaderHeight - 4f);
            if (GUI.Button(rect, label, _buttonStyle))
            {
                _clicks.Add(action);
            }
            return rect.x - ButtonGap;
        }

        // A title-row button with a pixel picture on it, drawn at 2 pixels per picture pixel.
        private float IconButton(float right, float y, Texture2D icon, Action action)
        {
            const float width = 36f;
            var rect = new Rect(right - width, y + 2f, width, HeaderHeight - 4f);
            if (GUI.Button(rect, GUIContent.none, _buttonStyle))
            {
                _clicks.Add(action);
            }
            if (icon != null)
            {
                float w = icon.width * 2f;
                float h = icon.height * 2f;
                GUI.DrawTexture(new Rect(Mathf.Round(rect.center.x - w / 2f), Mathf.Round(rect.center.y - h / 2f), w, h), icon);
            }
            return rect.x - ButtonGap;
        }

        // The speaker on the volume button: loud (two waves), quiet (one) and off (a cross).
        // One letter per pixel, top row first.
        private static readonly string[] SpeakerLoud =
        {
            "....W......",
            "...WW....W.",
            "WWWWW.W...W",
            "WWWWW..W..W",
            "WWWWW..W..W",
            "WWWWW..W..W",
            "WWWWW.W...W",
            "...WW....W.",
            "....W......",
        };

        private static readonly string[] SpeakerQuiet =
        {
            "....W......",
            "...WW......",
            "WWWWW.W....",
            "WWWWW..W...",
            "WWWWW..W...",
            "WWWWW..W...",
            "WWWWW.W....",
            "...WW......",
            "....W......",
        };

        private static readonly string[] SpeakerOff =
        {
            "....W......",
            "...WW......",
            "WWWWW.W...W",
            "WWWWW..W.W.",
            "WWWWW...W..",
            "WWWWW..W.W.",
            "WWWWW.W...W",
            "...WW......",
            "....W......",
        };

        private static Texture2D PixelPicture(string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Textures count rows from the bottom.
                    pixels[(height - 1 - y) * width + x] = rows[y][x] == '.'
                        ? new Color32(255, 255, 255, 0)
                        : new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Vector2 ViewSize(ScreenSize size)
        {
            switch (size)
            {
                case ScreenSize.Small: return new Vector2(400f, 300f);
                case ScreenSize.Large: return new Vector2(880f, 660f);
                default: return new Vector2(640f, 480f);
            }
        }

        private static string SizeLabel(ScreenSize size)
        {
            switch (size)
            {
                case ScreenSize.Small: return "S";
                case ScreenSize.Large: return "L";
                default: return "M";
            }
        }

        private static ScreenSize Next(ScreenSize size)
        {
            return size == ScreenSize.Small ? ScreenSize.Medium : size == ScreenSize.Medium ? ScreenSize.Large : ScreenSize.Small;
        }

        private void MakeStyles()
        {
            _boxStyle = new GUIStyle(GUI.skin.box);
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
            };
            _titleStyle.normal.textColor = Color.white;
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _hintStyle.normal.textColor = Dim;
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            _centreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
            };
            _centreStyle.normal.textColor = Color.white;
            _helpHeadStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };
            _helpHeadStyle.normal.textColor = new Color32(0xFF, 0x7A, 0x5C, 0xFF);
            _helpKeyStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _helpKeyStyle.normal.textColor = new Color32(0xFF, 0xD9, 0x66, 0xFF);
            _helpTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
            };
            _helpTextStyle.normal.textColor = Color.white;
            _helpNoteStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerLeft,
                wordWrap = true,
            };
            _helpNoteStyle.normal.textColor = Dim;
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
            };
            _valueStyle.normal.textColor = Color.white;
            _shade = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _shade.SetPixel(0, 0, Shade);
            _shade.Apply();
            _speakerLoud = PixelPicture(SpeakerLoud);
            _speakerQuiet = PixelPicture(SpeakerQuiet);
            _speakerOff = PixelPicture(SpeakerOff);
        }

        // ---- The game's state ----

        // The chat box. Only looked up while the window is open: on the main menu MonoSingleton.I
        // searches the scene.
        private static bool IsTyping()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // A game menu or panel (pause menu, inventory, shop, focus menu...). Free means none, and
        // Music is sitting at an instrument or drawing board.
        private static bool GameMenuOpen()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.LockState != LockState.Free && tasks.LockState != LockState.Music;
        }
    }
}
