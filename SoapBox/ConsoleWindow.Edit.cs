using System.Collections.Generic;
using OnTogetherMods.Shared;
using UnityEngine;

namespace SoapBox
{
    // The window while editing a cart's code: the editor's canvas (EditView) fills the window, its top
    // bar is the title row (drag it to move the window). Typing goes to the code while Soap Box has the
    // keys. Esc goes back to the game as it was, Ctrl+R runs the new code, Ctrl+S saves (carts also
    // save by themselves).
    internal sealed partial class ConsoleWindow
    {
        private const float RepeatDelay = 0.4f;
        private const float RepeatRate = 0.035f;

        // Editing keys that repeat while held (Backspace and Enter come with the typed letters).
        private static readonly KeyValuePair<KeyCode, EditKey>[] EditKeys =
        {
            new KeyValuePair<KeyCode, EditKey>(KeyCode.LeftArrow, EditKey.Left),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.RightArrow, EditKey.Right),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.UpArrow, EditKey.Up),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.DownArrow, EditKey.Down),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.Home, EditKey.Home),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.End, EditKey.End),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.PageUp, EditKey.PageUp),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.PageDown, EditKey.PageDown),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.Delete, EditKey.Delete),
            new KeyValuePair<KeyCode, EditKey>(KeyCode.Tab, EditKey.Tab),
        };

        private EditView _edit;
        private PixelTexture _editTexture;
        private readonly EditorInput _input = new EditorInput();
        private readonly Dictionary<KeyCode, float> _repeatAt = new Dictionary<KeyCode, float>();
        // The cart list as a drop-down under the cart's name.
        private bool _editShelf;
        private EditView.Button _editHover;
        private EditView.Button _editPressed;
        private bool _codePress;          // a left press that started on the code (dragging selects)
        private float _lastClickTime = -10f;
        private Vector2 _lastClickAt;
        private float _lastTyped;
        private long _editShows = -1;
        private string _editTitle;

        private EditView Edit => _edit ?? (_edit = new EditView());

        private void EnterEdit()
        {
            _mode = Mode.Edit;
            _card = Card.None;
            _editShelf = false;
            _hasKeys = true;
            _dragging = false;
            if (!ReferenceEquals(_editCart, _cart))
            {
                Edit.Code.Load(_cart?.Code ?? "");
                _editCart = _cart;
            }
            double now = Time.unscaledTimeAsDouble;
            if (_runner != null && _runner.Error != null)
            {
                // Straight to the line that went wrong.
                Edit.Code.ErrorLine = _runner.ErrorLine;
                if (_runner.ErrorLine > 0)
                {
                    Edit.Code.GoTo(_runner.ErrorLine);
                }
                Edit.Say((_runner.ErrorLine > 0 ? "line " + _runner.ErrorLine + ": " : "") + _runner.Error, true, now, 30.0);
            }
            else if (_cartBundled)
            {
                Edit.Say("this cart comes with soap box: changing it saves a copy in your carts", false, now, 6.0);
            }
            _lastTyped = Time.unscaledTime;
        }

        // Back to the game as it was (Esc). The new code runs with Ctrl+R.
        private void LeaveEdit()
        {
            SaveNow();
            _mode = Mode.Play;
            _editShelf = false;
            Edit.ShowHelp = false;
            _dragging = false;
        }

        private void RunFromEditor()
        {
            SaveNow();
            Edit.Code.ErrorLine = 0;
            _mode = Mode.Play;
            _card = Card.None;
            _editShelf = false;
            Edit.ShowHelp = false;
            _hasKeys = true;
            _dragging = false;
            StartRunner();
        }

        // ---- The mouse ----

        private void HandleEditMouse()
        {
            Vector2 mouse = MousePosition();
            EditView edit = Edit;
            bool over = Shown && _scale > 0 && _panel.Contains(mouse) && !IconBar.MouseOver;
            int cx = _scale > 0 ? Mathf.FloorToInt((mouse.x - _panel.x) / _scale) : -1;
            int cy = _scale > 0 ? Mathf.FloorToInt((mouse.y - _panel.y) / _scale) : -1;
            _editHover = over ? edit.HitTest(cx, cy) : EditView.Button.None;

            UpdateDrag(mouse);
            ClickElsewhere(over);

            _input.Clear();
            _input.Now = Time.unscaledTimeAsDouble;
            _input.MouseX = cx;
            _input.MouseY = cy;

            if (Input.GetMouseButtonDown(0) && over)
            {
                _hasKeys = true;
                _lastTyped = Time.unscaledTime;
                if (edit.ShowHelp)
                {
                    edit.ShowHelp = false;
                }
                else if (_editShelf && _editHover != EditView.Button.Cart)
                {
                    int i = _shelf.Renaming ? -1 : _shelf.ItemAt(cx, cy);
                    if (i >= 0)
                    {
                        _shelf.Selected = i;
                        ActivateShelf();
                    }
                    else if (!_shelf.Renaming)
                    {
                        CloseShelf();   // a click outside the list closes it
                    }
                }
                else if (cy < EditView.BarHeight)
                {
                    if (_editHover != EditView.Button.None)
                    {
                        _editPressed = _editHover;
                    }
                    else
                    {
                        PressTitleRow(mouse);
                    }
                }
                else if (cy < EditView.Height - EditView.StatusHeight)
                {
                    _codePress = true;
                    _input.MouseDown = true;
                    bool twice = Time.unscaledTime - _lastClickTime < DoubleClick && (mouse - _lastClickAt).sqrMagnitude < 25f;
                    _input.Clicks = twice ? 2 : 1;
                    _lastClickTime = twice ? -10f : Time.unscaledTime;
                    _lastClickAt = mouse;
                }
            }
            _input.MouseHeld = _codePress && Input.GetMouseButton(0);
            if (Input.GetMouseButtonUp(0))
            {
                if (_editPressed != EditView.Button.None && _editHover == _editPressed)
                {
                    ClickEdit(_editPressed);
                }
                _editPressed = EditView.Button.None;
                _input.MouseUp = _codePress;
                _codePress = false;
            }
            if (over && !edit.ShowHelp && !_editShelf && cy >= EditView.CodeTop)
            {
                _input.Wheel = Input.mouseScrollDelta.y;
            }
        }

        private void ClickEdit(EditView.Button button)
        {
            double now = Time.unscaledTimeAsDouble;
            switch (button)
            {
                case EditView.Button.SpritesTab:
                    Edit.Say("drawing sprites comes in the next step", false, now);
                    break;
                case EditView.Button.MapTab:
                    Edit.Say("making maps comes in the next step", false, now);
                    break;
                case EditView.Button.SoundsTab:
                    Edit.Say("making sounds comes in a later step", false, now);
                    break;
                case EditView.Button.Cart:
                    if (_editShelf)
                    {
                        CloseShelf();
                    }
                    else
                    {
                        OpenShelf();
                    }
                    break;
                case EditView.Button.Run:
                    RunFromEditor();
                    break;
                case EditView.Button.Help:
                    Edit.ShowHelp = !Edit.ShowHelp;
                    break;
                case EditView.Button.Size:
                    NextSize();
                    break;
                case EditView.Button.Close:
                    Close();
                    break;
            }
        }

        // ---- Keys ----

        private void HandleEditKeys()
        {
            EditView edit = Edit;
            if (!KeysToConsole)
            {
                ApplyCode();   // the mouse still scrolls and selects
                return;
            }
            ReadTyping(_input);
            bool esc = Input.GetKeyDown(KeyCode.Escape);

            if (_input.Ctrl && Input.GetKeyDown(KeyCode.R))
            {
                RunFromEditor();
                return;
            }
            if (_input.Ctrl && Input.GetKeyDown(KeyCode.S))
            {
                if (_cartBundled)
                {
                    MakeYours(true);
                }
                else
                {
                    SaveNow(true);
                }
            }

            if (edit.ShowHelp)
            {
                // Any key closes the sheet, and goes no further.
                if (esc || (Input.anyKeyDown && !FunctionKeyDown()))
                {
                    edit.ShowHelp = false;
                }
                return;
            }
            if (_editShelf)
            {
                if (_shelf.Renaming)
                {
                    if (esc)
                    {
                        _shelf.Renaming = false;
                        _shelf.Version++;
                    }
                    else if (_shelf.Name.Update(_input))
                    {
                        FinishRename();
                    }
                    return;
                }
                if (esc)
                {
                    CloseShelf();
                    return;
                }
                foreach (EditKey key in _input.Keys)
                {
                    if (key == EditKey.Up)
                    {
                        _shelf.Move(-1);
                    }
                    else if (key == EditKey.Down)
                    {
                        _shelf.Move(1);
                    }
                    else if (key == EditKey.Enter)
                    {
                        ActivateShelf();
                        return;
                    }
                }
                return;
            }
            if (esc)
            {
                LeaveEdit();
                return;
            }
            ApplyCode();
        }

        private void ApplyCode()
        {
            CodeEditor code = Edit.Code;
            int before = code.Version;
            code.Update(_input);
            if (code.CopyText != null)
            {
                GUIUtility.systemCopyBuffer = code.CopyText;
            }
            if (code.Changed)
            {
                if (_cartBundled)
                {
                    MakeYours(false);
                }
                ScheduleSave();
            }
            if (code.Version != before)
            {
                _lastTyped = Time.unscaledTime;
            }
        }

        // This frame's typing: letters (with the keyboard's own repeat), the editing keys (with ours),
        // and Ctrl shortcuts. AltGr is Ctrl+Alt on Windows and types letters ({ } [ ] \ @ on many
        // keyboards), so it doesn't count as Ctrl.
        private void ReadTyping(EditorInput input)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
            bool shortcut = ctrl && !alt;
            input.Ctrl = shortcut;
            input.Shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    input.Keys.Add(EditKey.Backspace);
                }
                else if (c == '\n' || c == '\r')
                {
                    input.Keys.Add(EditKey.Enter);
                }
                else if (c >= ' ' && c != '\u007f' && !shortcut)
                {
                    input.Text.Append(c);
                }
            }
            foreach (KeyValuePair<KeyCode, EditKey> pair in EditKeys)
            {
                if (Repeat(pair.Key))
                {
                    input.Keys.Add(pair.Value);
                }
            }
            if (shortcut)
            {
                input.Copy = Input.GetKeyDown(KeyCode.C);
                input.Cut = Input.GetKeyDown(KeyCode.X);
                if (Input.GetKeyDown(KeyCode.V))
                {
                    input.Paste = true;
                    input.Clipboard = GUIUtility.systemCopyBuffer ?? "";
                }
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    if (input.Shift)
                    {
                        input.Redo = true;
                    }
                    else
                    {
                        input.Undo = true;
                    }
                }
                input.Redo |= Input.GetKeyDown(KeyCode.Y);
                input.SelectAll = Input.GetKeyDown(KeyCode.A);
            }
        }

        // True on the press, then again and again while it's held.
        private bool Repeat(KeyCode key)
        {
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(key))
            {
                _repeatAt[key] = now + RepeatDelay;
                return true;
            }
            if (Input.GetKey(key) && _repeatAt.TryGetValue(key, out float at) && now >= at)
            {
                _repeatAt[key] = now + RepeatRate;
                return true;
            }
            if (!Input.GetKey(key))
            {
                _repeatAt.Remove(key);
            }
            return false;
        }

        // ---- Drawing ----

        private void RefreshEdit()
        {
            EditView edit = Edit;
            double now = Time.unscaledTimeAsDouble;
            bool caretOn = KeysToConsole && (Time.unscaledTime - _lastTyped) % 1f < 0.6f;
            string title = CartTitle();
            unchecked
            {
                long shows = edit.Code.Version;
                shows = shows * 31 + (int)_editHover;
                shows = shows * 2 + (caretOn ? 1 : 0);
                shows = shows * 2 + (edit.ShowHelp ? 1 : 0);
                shows = shows * 2 + (_editShelf ? 1 : 0);
                shows = shows * 131 + _shelf.Version;
                shows = shows * 131 + _shelf.Selected;
                shows = shows * 131 + _shelf.Name.Version;
                shows = shows * 2 + (_shelf.Renaming ? 1 : 0);
                shows = shows * 2 + (edit.Saying(now) ? 1 : 0);
                shows = shows * 131 + edit.Version;
                shows = shows * 4 + (int)S.Size.Value;
                shows = shows * 2 + (_cartBundled ? 1 : 0);
                if (shows == _editShows && _editTitle == title)
                {
                    return;
                }
                _editShows = shows;
            }
            _editTitle = title;
            edit.Draw(title, _cartBundled, SizeLabel(), _editHover, caretOn, now, _editShelf ? _shelf : null, _cartPath);
            _editTexture.Upload(edit.Canvas, _screenColours);
        }
    }
}
