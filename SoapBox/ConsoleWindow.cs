using System;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using EventType = UnityEngine.EventType;

namespace SoapBox
{
    // The Soap Box window. Playing: a pixel title strip over the pastel handheld with the cart's
    // screen in it. Editing (ConsoleWindow.Edit.cs): the code editor at twice the detail. Carts are
    // loaded, saved and listed in ConsoleWindow.Carts.cs. It sits in the stack with the other mods'
    // windows until you drag its title row. Everything is drawn in the console's own pixels, at a
    // whole number of screen pixels per pixel, and every click is worked out here from the mouse.
    //
    // Soap Box gets the keyboard like Doom Together: opening the window or clicking it hands Soap Box
    // the keys, and clicking anywhere else (or opening chat or a game menu, or typing in Bubble Shell)
    // gives them back to the game, which pauses the cart until you click it again.
    internal sealed partial class ConsoleWindow : MonoBehaviour
    {
        private const string PanelId = "SoapBox";
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;
        // Pixels between the title strip and the handheld.
        private const int Gap = 2;
        // Seconds of cart code per game frame. A cart that needs more runs slower; the game doesn't.
        private const double Budget = 0.004;
        private const float DoubleClick = 0.35f;

        private static readonly KeyCode[] LeftKeys = { KeyCode.LeftArrow, KeyCode.A };
        private static readonly KeyCode[] RightKeys = { KeyCode.RightArrow, KeyCode.D };
        private static readonly KeyCode[] UpKeys = { KeyCode.UpArrow, KeyCode.W };
        private static readonly KeyCode[] DownKeys = { KeyCode.DownArrow, KeyCode.S };
        private static readonly KeyCode[] AKeys = { KeyCode.Z, KeyCode.Space, KeyCode.C, KeyCode.N };
        private static readonly KeyCode[] BKeys = { KeyCode.X, KeyCode.V, KeyCode.M };
        private static readonly KeyCode[] MenuKeys = { KeyCode.Escape, KeyCode.Return, KeyCode.KeypadEnter };

        private static ConsoleWindow _instance;

        private static Settings S => Plugin.Settings;

        // Read by the patches. True while Soap Box has the keyboard, so the game ignores it.
        internal static bool HoldsKeys => _instance != null && _instance.KeysToConsole;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private enum Mode
        {
            Play,
            Edit,
        }

        // What covers the cart's picture while playing, if anything.
        private enum Card
        {
            None,
            Help,
            Menu,
            Shelf,
        }

        private bool _open;
        private bool _hasKeys;
        private bool _iconClicked;
        private Mode _mode;
        private Card _card;
        private int _menuIndex;

        // Keys that closed a card or picked from a menu: kept from the cart until they're let go.
        private int _ignoreKeys;

        // ---- Layout, worked out in OnGUI, in screen pixels ----
        private int _scale;            // screen pixels per console pixel (0 until first drawn)
        private Rect _panel;           // the whole window
        private int _drawnFrame = -100;

        // ---- The mouse while playing ----
        private Header.Button _hover;
        private Header.Button _pressedButton;
        private Handheld.Buttons _clickedButtons; // drawn buttons held down with the mouse
        private bool _screenPress;                // a left press on the screen, for the cart
        private int _mouseHeld;
        private int _mouseX;
        private int _mouseY;
        private float _wheel;

        // ---- Dragging the window by its title row (both modes) ----
        private bool _dragging;
        private Vector2 _dragGrab;
        private Vector2 _dragPos;
        private float _lastStripClick = -10f;

        // ---- Drawing ----
        private ClickBlocker _blocker;
        private PixelTexture _headerTexture;
        private PixelTexture _frameTexture;
        private PixelTexture _screenTexture;
        private readonly Surface _header = new Surface(Handheld.Width, Header.Height);
        private readonly Surface _frame = new Surface(Handheld.Width, Handheld.Height);
        private readonly Surface _composite = new Surface(Runner.Width, Runner.Height);
        private readonly Color32[][] _shellColours = new Color32[4][];
        private Color32[] _screenColours;
        private Handheld.Buttons _lit;
        private bool _power;
        // What each texture last showed, so it's only redrawn when something changed.
        private long _headerShows = -1;
        private string _headerTitle;
        private long _frameShows = -1;
        private long _screenShows = -1;
        private Runner _screenRunner;

        private bool KeysToConsole => _open && _hasKeys && !PanelStack.Hidden;

        private bool Shown => _open && Time.frameCount - _drawnFrame <= 2;

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "SoapBox ClickBlocker");
            _headerTexture = new PixelTexture(Handheld.Width, Header.Height);
            _frameTexture = new PixelTexture(Handheld.Width, Handheld.Height);
            _screenTexture = new PixelTexture(Runner.Width, Runner.Height);
            _editTexture = new PixelTexture(EditView.Width, EditView.Height);
            _screenColours = PixelTexture.Colours(Palette.Rgb, true);
        }

        private void OnDestroy()
        {
            SaveNow();
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
            _headerTexture?.Destroy();
            _frameTexture?.Destroy();
            _screenTexture?.Destroy();
            _editTexture?.Destroy();
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

                if (_mode == Mode.Edit)
                {
                    HandleEditMouse();
                }
                else
                {
                    HandlePlayMouse();
                }
                if (_hasKeys && (IsTyping() || GameMenuOpen() || PanelStack.Hidden || ShellLink.ConsoleTyping))
                {
                    _hasKeys = false;
                }
                if (_mode == Mode.Edit)
                {
                    HandleEditKeys();
                    RunCart();   // only keeps the handheld's lights right; the cart waits while editing
                }
                else
                {
                    HandlePlayKeys();
                    RunCart();
                }
                AutoSave();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            bool shown = Shown;
            IsMouseOver = shown && (ClickBlocker.MouseIn(_panel) || _dragging);
            _blocker?.Update(shown, _panel);
        }

        private static Vector2 MousePosition()
        {
            Vector3 mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        // ---- Playing: the mouse ----

        private void HandlePlayMouse()
        {
            Vector2 mouse = MousePosition();
            bool over = Shown && _scale > 0 && _panel.Contains(mouse) && !IconBar.MouseOver;
            // Console pixels from the window's top-left: the title strip, then the handheld.
            int ax = _scale > 0 ? Mathf.FloorToInt((mouse.x - _panel.x) / _scale) : -1;
            int ay = _scale > 0 ? Mathf.FloorToInt((mouse.y - _panel.y) / _scale) : -1;
            int fx = ax, fy = ay - Header.Height - Gap;
            bool onStrip = over && ay < Header.Height;
            bool onFrame = over && fy >= 0;
            bool onScreen = onFrame && Handheld.OnScreen(fx, fy);
            _hover = onStrip ? Header.HitTest(Handheld.Width, ax, ay) : Header.Button.None;
            _mouseX = fx - Handheld.ScreenX;
            _mouseY = fy - Handheld.ScreenY;

            UpdateDrag(mouse);
            ClickElsewhere(over);

            if (Input.GetMouseButtonDown(0) && over)
            {
                if (onStrip)
                {
                    if (_hover != Header.Button.None)
                    {
                        _pressedButton = _hover;
                    }
                    else
                    {
                        PressTitleRow(mouse);
                    }
                }
                else if (onFrame)
                {
                    PressFrame(fx, fy, onScreen);
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (_pressedButton != Header.Button.None && _hover == _pressedButton)
                {
                    Click(_pressedButton);
                }
                _pressedButton = Header.Button.None;
                _clickedButtons = Handheld.Buttons.None;
                _screenPress = false;
            }

            _mouseHeld = 0;
            if (_screenPress && Input.GetMouseButton(0))
            {
                _mouseHeld |= 1;
            }
            if (KeysToConsole && onScreen && Input.GetMouseButton(1))
            {
                _mouseHeld |= 2;
            }
            _wheel = onScreen ? Input.mouseScrollDelta.y : 0f;
        }

        // A click anywhere but the window gives the keyboard back to the game (the mod icons excepted).
        private void ClickElsewhere(bool over)
        {
            if (_open && !over && !IconBar.MouseOver &&
                (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
            {
                _hasKeys = false;
            }
        }

        // A left press on the empty part of the title row: the start of a drag, or with a double-click,
        // back into the stack with the other mods' windows.
        private void PressTitleRow(Vector2 mouse)
        {
            _hasKeys = true;
            if (Time.unscaledTime - _lastStripClick < DoubleClick)
            {
                _lastStripClick = -10f;
                _dragging = false;
                S.WindowX.Value = -1f;
                S.WindowY.Value = -1f;
                return;
            }
            _lastStripClick = Time.unscaledTime;
            _dragging = true;
            _dragGrab = mouse - _panel.position;
            _dragPos = _panel.position;
        }

        private void UpdateDrag(Vector2 mouse)
        {
            if (!_dragging)
            {
                return;
            }
            if (Input.GetMouseButton(0))
            {
                _dragPos = ClampToScreen(mouse - _dragGrab, _panel.size);
            }
            else
            {
                _dragging = false;
                Vector2 at = ClampToScreen(_dragPos, _panel.size) / Unit;
                S.WindowX.Value = Mathf.Round(at.x);
                S.WindowY.Value = Mathf.Round(at.y);
            }
        }

        // A left press on the handheld. The first click on the screen only hands the cart the keys
        // (so it doesn't also pop a bubble); the drawn buttons work straight away.
        private void PressFrame(int fx, int fy, bool onScreen)
        {
            bool had = _hasKeys;
            _hasKeys = true;
            int sx = fx - Handheld.ScreenX, sy = fy - Handheld.ScreenY;
            switch (_card)
            {
                case Card.Help:
                    _card = Card.None;
                    return;
                case Card.Menu:
                    int item = onScreen ? Overlay.MenuItemAt(sx, sy) : -1;
                    if (item >= 0)
                    {
                        _menuIndex = item;
                        Pick(item);
                    }
                    else if (Handheld.ButtonAt(fx, fy) == Handheld.Buttons.Menu)
                    {
                        _card = Card.None;
                    }
                    return;
                case Card.Shelf:
                    int index = onScreen ? _shelf.ItemAt(sx, sy) : -1;
                    if (index >= 0)
                    {
                        _shelf.Selected = index;
                        ActivateShelf();
                    }
                    else if (onScreen || Handheld.ButtonAt(fx, fy) == Handheld.Buttons.B)
                    {
                        CloseShelf();
                    }
                    return;
            }
            if (_runner == null || _runner.Error != null)
            {
                Handheld.Buttons pressed = Handheld.ButtonAt(fx, fy);
                if (pressed == Handheld.Buttons.B)
                {
                    EnterEdit();
                }
                else if (onScreen || pressed == Handheld.Buttons.A)
                {
                    Restart();
                }
                return;
            }
            Handheld.Buttons button = Handheld.ButtonAt(fx, fy);
            if (button == Handheld.Buttons.Menu)
            {
                OpenMenu();
            }
            else if (button != Handheld.Buttons.None)
            {
                _clickedButtons = button;
            }
            else if (onScreen && had)
            {
                _screenPress = true;
            }
        }

        private void Click(Header.Button button)
        {
            switch (button)
            {
                case Header.Button.Close:
                    Close();
                    break;
                case Header.Button.Size:
                    NextSize();
                    break;
                case Header.Button.Help:
                    _card = _card == Card.Help ? Card.None : Card.Help;
                    break;
                case Header.Button.Shell:
                    S.Shell.Value = (Handheld.Shell)(((int)S.Shell.Value + 1) % 4);
                    break;
                case Header.Button.Edit:
                    EnterEdit();
                    break;
                case Header.Button.Title:
                    if (_card == Card.Shelf)
                    {
                        CloseShelf();
                    }
                    else
                    {
                        OpenShelf();
                    }
                    break;
            }
        }

        private static void NextSize()
        {
            S.Size.Value = S.Size.Value == WindowSize.Small ? WindowSize.Medium
                : S.Size.Value == WindowSize.Medium ? WindowSize.Large : WindowSize.Small;
        }

        // ---- Playing: keys ----

        private void HandlePlayKeys()
        {
            int keys = ReadKeys();
            _ignoreKeys &= keys;   // forgotten once let go
            if (!KeysToConsole)
            {
                return;
            }
            bool menuKey = AnyDown(MenuKeys);
            switch (_card)
            {
                case Card.Help:
                    // Any key closes the controls card, and goes no further.
                    if (Input.anyKeyDown && !FunctionKeyDown())
                    {
                        _card = Card.None;
                        _ignoreKeys = keys;
                    }
                    return;
                case Card.Menu:
                    if (Input.GetKeyDown(KeyCode.Escape) || AnyDown(BKeys))
                    {
                        _card = Card.None;
                        _ignoreKeys = keys;
                    }
                    else if (AnyDown(UpKeys))
                    {
                        _menuIndex = (_menuIndex + Overlay.MenuItems.Length - 1) % Overlay.MenuItems.Length;
                    }
                    else if (AnyDown(DownKeys))
                    {
                        _menuIndex = (_menuIndex + 1) % Overlay.MenuItems.Length;
                    }
                    else if (AnyDown(AKeys) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        _ignoreKeys = keys;
                        Pick(_menuIndex);
                    }
                    return;
                case Card.Shelf:
                    if (Input.GetKeyDown(KeyCode.Escape) || AnyDown(BKeys))
                    {
                        CloseShelf();
                        _ignoreKeys = keys;
                    }
                    else if (AnyDown(UpKeys))
                    {
                        _shelf.Move(-1);
                    }
                    else if (AnyDown(DownKeys))
                    {
                        _shelf.Move(1);
                    }
                    else if (AnyDown(AKeys) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    {
                        _ignoreKeys = keys;
                        ActivateShelf();
                    }
                    return;
            }
            if (_runner == null || _runner.Error != null)
            {
                if (AnyDown(BKeys))
                {
                    _ignoreKeys = keys;
                    EnterEdit();
                }
                else if (menuKey || AnyDown(AKeys))
                {
                    _ignoreKeys = keys;
                    Restart();
                }
                return;
            }
            if (menuKey)
            {
                OpenMenu();
            }
        }

        // The buttons held on the keyboard, one bit each (Controls' order). Only while playing.
        private int ReadKeys()
        {
            if (!KeysToConsole || _mode != Mode.Play)
            {
                return 0;
            }
            int held = 0;
            if (AnyHeld(LeftKeys))
            {
                held |= 1 << Controls.Left;
            }
            if (AnyHeld(RightKeys))
            {
                held |= 1 << Controls.Right;
            }
            if (AnyHeld(UpKeys))
            {
                held |= 1 << Controls.Up;
            }
            if (AnyHeld(DownKeys))
            {
                held |= 1 << Controls.Down;
            }
            if (AnyHeld(AKeys))
            {
                held |= 1 << Controls.A;
            }
            if (AnyHeld(BKeys))
            {
                held |= 1 << Controls.B;
            }
            return held;
        }

        private static bool AnyHeld(KeyCode[] keys)
        {
            foreach (KeyCode key in keys)
            {
                if (Input.GetKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AnyDown(KeyCode[] keys)
        {
            foreach (KeyCode key in keys)
            {
                if (Input.GetKeyDown(key))
                {
                    return true;
                }
            }
            return false;
        }

        // Other mods use the F keys, so they don't count as "any key".
        private static bool FunctionKeyDown()
        {
            for (KeyCode key = KeyCode.F1; key <= KeyCode.F15; key++)
            {
                if (Input.GetKeyDown(key))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- The cart ----

        private void RunCart()
        {
            int held = (ReadKeys() & ~_ignoreKeys) | (int)_clickedButtons & 63;
            bool menuHeld = (_clickedButtons & Handheld.Buttons.Menu) != 0 ||
                            (KeysToConsole && _mode == Mode.Play && AnyHeld(MenuKeys));
            _lit = (Handheld.Buttons)held | (menuHeld ? Handheld.Buttons.Menu : Handheld.Buttons.None);

            bool frozen = !_open || _mode != Mode.Play || _runner == null || _runner.Error != null || _card != Card.None ||
                          (!KeysToConsole && S.PauseWithoutKeys.Value);
            _power = _runner != null && !frozen;
            if (frozen)
            {
                _runner?.Controls.Release();
                return;
            }
            _runner.Controls.SetHeld(held);
            _runner.Controls.SetMouse(_mouseX, _mouseY, _mouseHeld, _wheel);
            _runner.Tick(Time.unscaledDeltaTime, Budget);
        }

        private void Restart()
        {
            _card = Card.None;
            StartRunner();
        }

        private void OpenMenu()
        {
            _card = Card.Menu;
            _menuIndex = 0;
        }

        private void Pick(int item)
        {
            switch (item)
            {
                case 0:
                    _card = Card.None;
                    break;
                case 1:
                    Restart();
                    break;
                case 2:
                    EnterEdit();
                    break;
                case 3:
                    OpenShelf();
                    break;
                default:
                    Close();
                    break;
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
            if (!S.ControlsSeen.Value)
            {
                _card = Card.Help;
                S.ControlsSeen.Value = true;
            }
            if (_cart == null)
            {
                LoadFirstCart();
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

        // Closing keeps the cart where it is (paused) and saves your code; opening again carries on.
        private void Close()
        {
            SaveNow();
            _open = false;
            _hasKeys = false;
            _dragging = false;
            if (_card == Card.Menu || _card == Card.Shelf)
            {
                _card = Card.None;
            }
            _editShelf = false;
            _clickedButtons = Handheld.Buttons.None;
            _screenPress = false;
            _runner?.Controls.Release();
        }

        // ---- Drawing ----

        private static float Unit => Screen.height / 1080f;

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value)
            {
                IconBar.State state = _open ? IconBar.State.Open : IconBar.State.Plain;
                if (IconBar.Draw("SoapBox", IconBar.SoapBoxOrder, Icon.Pixels, state, "Soap Box", S.WindowKey.Value) ==
                    IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
            }

            // Hidden while Photo Mode takes a photo, and while the game's eye button hides the UI.
            if (!_open || PanelStack.Hidden || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                _dragging = false;
                return;
            }

            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            try
            {
                Layout();
                if (Event.current.type == EventType.Repaint)
                {
                    if (_mode == Mode.Edit)
                    {
                        RefreshEdit();
                        GUI.DrawTexture(_panel, _editTexture.Texture);
                    }
                    else
                    {
                        Refresh();
                        Draw();
                    }
                }
                // Keep clicks on the window from reaching other mods' windows underneath.
                EventType type = Event.current.type;
                if ((type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.ScrollWheel) &&
                    _panel.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                }
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        // Works out the window's size (a whole number of screen pixels per console pixel) and place.
        // The editor is drawn at twice the detail, so it uses one step smaller.
        private void Layout()
        {
            float unit = Unit;
            bool edit = _mode == Mode.Edit;
            int wanted = S.Size.Value == WindowSize.Small ? 2 : S.Size.Value == WindowSize.Large ? 4 : 3;
            if (edit)
            {
                wanted--;
            }
            int scale = Mathf.Max(1, Mathf.RoundToInt(wanted * unit));
            Vector2 size;
            while (true)
            {
                size = edit
                    ? new Vector2(EditView.Width * scale, EditView.Height * scale)
                    : new Vector2(Handheld.Width * scale, (Header.Height + Gap + Handheld.Height) * scale);
                bool fits = size.y <= Screen.height - 40f * unit && size.x <= Screen.width - 40f * unit;
                if (fits || scale <= 1)
                {
                    break;
                }
                scale--;
            }
            _scale = scale;

            Vector2 pos;
            if (_dragging)
            {
                pos = ClampToScreen(_dragPos, size);
            }
            else if (S.WindowX.Value >= 0f)
            {
                pos = ClampToScreen(new Vector2(S.WindowX.Value, S.WindowY.Value) * unit, size);
            }
            else
            {
                // In the stack with the other mods' windows, which is laid out in 1080p units.
                pos = PanelStack.Place(PanelId, PanelStack.SoapBoxOrder, size / unit).position * unit;
            }
            _panel = new Rect(new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y)), size);
            _drawnFrame = Time.frameCount;
        }

        // Kept wholly on screen, in screen pixels.
        private static Vector2 ClampToScreen(Vector2 position, Vector2 size)
        {
            return new Vector2(
                Mathf.Clamp(position.x, 0f, Mathf.Max(0f, Screen.width - size.x)),
                Mathf.Clamp(position.y, 0f, Mathf.Max(0f, Screen.height - size.y)));
        }

        // Redraws whichever pictures changed (playing).
        private void Refresh()
        {
            int shell = Mathf.Clamp((int)S.Shell.Value, 0, 3);
            if (_shellColours[shell] == null)
            {
                _shellColours[shell] = PixelTexture.Colours(Handheld.Colours((Handheld.Shell)shell), false);
            }
            Color32[] frameColours = _shellColours[shell];

            string title = CartTitle();
            long headerShows = (long)_hover | (long)S.Size.Value << 4 | (long)shell << 8 | (KeysToConsole ? 1L : 0L) << 12 |
                               (long)_card << 13;
            if (headerShows != _headerShows || _headerTitle != title)
            {
                Header.Draw(_header, title, Hint(), SizeLabel(), _hover);
                _headerTexture.Upload(_header, frameColours);
                _headerShows = headerShows;
                _headerTitle = title;
            }

            long frameShows = (long)_lit | (long)shell << 8 | (_power ? 1L : 0L) << 12;
            if (frameShows != _frameShows)
            {
                Handheld.Draw(_frame, _lit, _power);
                _frameTexture.Upload(_frame, frameColours);
                _frameShows = frameShows;
            }

            bool waiting = !KeysToConsole && S.PauseWithoutKeys.Value;
            long screenShows = (long)(_runner?.ShownVersion ?? 0) | (long)_card << 32 | (long)_menuIndex << 36 |
                               (waiting ? 1L : 0L) << 40 | (_runner?.Error != null ? 1L : 0L) << 41 |
                               (_runner != null && _runner.Busy ? 1L : 0L) << 42 | (long)(_shelf.Version & 0xFFFF) << 43 |
                               (long)(_shelf.Selected & 0xFF) << 56;
            if (screenShows != _screenShows || !ReferenceEquals(_screenRunner, _runner))
            {
                Compose(waiting);
                _screenTexture.Upload(_composite, _screenColours);
                _screenShows = screenShows;
                _screenRunner = _runner;
            }
        }

        // The cart's picture with whatever covers it.
        private void Compose(bool waiting)
        {
            _composite.ResetState();
            if (_runner != null)
            {
                Buffer.BlockCopy(_runner.Shown.Pixels, 0, _composite.Pixels, 0, _composite.Pixels.Length);
            }
            else
            {
                _composite.Clear(Palette.Ink);
            }

            if (_card == Card.Shelf)
            {
                Overlay.Dim(_composite);
                _shelf.Draw(_composite, 6, 4, Runner.Width - 12, Runner.Height - 8, _cartPath, false);
            }
            else if (_runner == null)
            {
                Overlay.Error(_composite, _loadError ?? "no cart", 0);
            }
            else if (_runner.Error != null)
            {
                Overlay.Error(_composite, _runner.Error, _runner.ErrorLine);
            }
            else if (_card == Card.Help)
            {
                Overlay.Help(_composite);
            }
            else if (_card == Card.Menu)
            {
                Overlay.Menu(_composite, _menuIndex);
            }
            else if (waiting)
            {
                Overlay.ClickToPlay(_composite);
            }
            else if (_runner.Busy)
            {
                Overlay.Busy(_composite);
            }
        }

        private void Draw()
        {
            float x = _panel.x, y = _panel.y;
            GUI.DrawTexture(new Rect(x, y, Handheld.Width * _scale, Header.Height * _scale), _headerTexture.Texture);
            float frameY = y + (Header.Height + Gap) * _scale;
            GUI.DrawTexture(new Rect(x, frameY, Handheld.Width * _scale, Handheld.Height * _scale), _frameTexture.Texture);
            GUI.DrawTexture(new Rect(x + Handheld.ScreenX * _scale, frameY + Handheld.ScreenY * _scale,
                Runner.Width * _scale, Runner.Height * _scale), _screenTexture.Texture);
        }

        private string Hint()
        {
            switch (_card)
            {
                case Card.Help:
                    return "any key: close";
                case Card.Menu:
                case Card.Shelf:
                    return "esc: back";
            }
            return KeysToConsole ? "esc: menu" : "click to play";
        }

        private static string SizeLabel()
        {
            return S.Size.Value == WindowSize.Small ? "S" : S.Size.Value == WindowSize.Large ? "L" : "M";
        }

        // ---- The game's state ----

        // The chat box. Only looked up when needed (a key press, or while Soap Box has the keys): on the
        // main menu MonoSingleton.I searches the scene.
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
