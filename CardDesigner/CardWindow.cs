using System;
using System.Collections.Generic;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CardDesigner
{
    // The Card Designer window. On the left, a big typing box for one line of your ID card that shows
    // the styling as you type, with a toolbar above it (bold, italic, underline, strikethrough,
    // colour, highlight, size, alignment, emoji and symbols), presets and Apply card below it. On the
    // right, the card as other players see it: click a line there to edit it. The toolbar's popups
    // open over the card.
    //
    // In a world it sits in the bottom window stack (Shared/PanelStack.cs). While the game's own ID
    // card page shows, it sits in the middle of the screen, and with Name Designer open beside it
    // (Shared/NameDesignerLink.cs) the two share the screen. It's laid out at 1080p and scaled.
    internal sealed class CardWindow : MonoBehaviour
    {
        private const string PanelId = "CardDesigner";
        private const string HexFieldName = "CardDesigner.Hex";

        // Layout, in 1080p units.
        private const float Pad = 12f;
        private const float LeftWidth = 640f;
        private const float Gutter = 16f;
        private const float RightWidth = 440f;
        private const float Width = Pad + LeftWidth + Gutter + RightWidth + Pad;
        private const float HeaderHeight = 28f;
        private const float ToolHeight = 30f;
        private const float EditorHeight = 280f;
        private const float InfoHeight = 22f;
        private const float LabelHeight = 20f;
        private const float RowHeight = 30f;
        private const float SaveHeight = 22f;
        private const float Gap = 6f;
        private const float PresetsHeight = LabelHeight + RowHeight + 3f + SaveHeight;
        private const float Height = Pad + HeaderHeight + Gap + ToolHeight + Gap + EditorHeight + 4f + InfoHeight + 10f +
                                     PresetsHeight + 10f + RowHeight + Pad;
        private const float PairGap = 20f;
        private const float StatusSeconds = 5f;
        private const float ConfirmSeconds = 3f;

        // The typing box's lettering is as big, against its width, as the card quote's (47.93 in a box
        // 1125.7 wide), so the quote breaks into lines where it will on the card.
        private const float EditorPadding = 12f;
        private const float EditorFontSize = (LeftWidth - 2f * EditorPadding) * 47.93f / 1125.7f;

        // Quick colours: bright, then pastel, then white and near-black (the same as Name Designer's).
        private static readonly string[] Swatches =
        {
            "#FF5C5C", "#FF9F43", "#FFD93D", "#6BCB77", "#4D96FF", "#9B5DE5", "#F15BB5", "#00BBF9",
            "#FFADAD", "#FFD6A5", "#FDFFB6", "#CAFFBF", "#A0C4FF", "#BDB2FF", "#FFFFFF", "#303030",
        };

        // See-through, so the letters show through: yellow, green, blue, pink, orange, purple, white, dark.
        private static readonly string[] Highlights =
        {
            "#FFE06699", "#9CF09C99", "#7FC8FF99", "#FF9ECF99", "#FFB36699", "#C9A7FF99", "#FFFFFFAA", "#00000066",
        };

        // null is the card's normal size.
        private static readonly string[] Sizes = { "40%", "50%", "60%", "70%", "80%", "90%", null, "120%", "150%" };

        private enum Popup { None, Colour, Highlight, Size, Emoji }

        private static CardWindow _instance;

        // Read by the patches. True while you type in the window, so the game ignores the keys.
        // The typing box counts for a frame after it lets go: Esc stops it partway through a frame,
        // and the game may read the key after that.
        internal static bool IsTyping =>
            _instance != null && _instance._open && (_instance.EditorTyping || _instance._hexTyping);

        // The same, except while the window is hidden (Photo Mode, or the game's eye button).
        internal static bool HoldsKeys => IsTyping && !PanelStack.Hidden;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private readonly CardCanvas _canvas = new CardCanvas("CardDesigner");
        private readonly ColorPicker _picker = new ColorPicker();
        private readonly PresetStore _presets = new PresetStore();
        private readonly Emoji _emoji = new Emoji();

        // The card being designed, and the card as it was last read from the game or applied.
        private readonly string[] _lines = { "", "", "", "", "" };
        private readonly string[] _loaded = { "", "", "", "", "" };
        private readonly History[] _history = new History[Game.Fields];
        private int _field = Game.Quote;
        private bool _loadedOnce;

        private bool _open;
        private bool _openedFromCard;
        private bool _iconClicked;
        private bool _cardButtonClicked;
        private int _scene = -1;
        private bool _hooked;
        private bool _loadEditor;
        private bool _codeView;
        private Popup _popup;
        private bool _symbolsTab;
        private string _hexText = "#FFFFFF";

        private int _editorFrame = -100;
        private bool _hexTyping;
        private bool _clearFocus;
        private bool _refocus;
        private bool _pressFocused;
        // The line as it was at the start of this frame, for undo.
        private History.Step _known;

        // How the selection looks, for the toolbar.
        private bool _isBold;
        private bool _isItalic;
        private bool _isUnderline;
        private bool _isStrike;
        private string _align;
        private string _size;
        private string _readText;
        private int _readAnchor = -1;
        private int _readFocus = -1;
        private bool _readCode;

        private int _confirmSlot = -1;
        private float _confirmUntil;
        private float _confirmResetUntil;
        private string _status;
        private float _statusUntil;
        private bool _loggedButton;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;

        private GUIStyle _titleStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _toolStyle;
        private GUIStyle _presetStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _cardButtonStyle;

        private bool EditorTyping => Time.frameCount - _editorFrame <= 1;

        private CardInputField Editor => _canvas.Editor;

        private void Awake()
        {
            _instance = this;
            for (int i = 0; i < Game.Fields; i++)
            {
                _history[i] = new History();
            }
            _presets.Load();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _canvas.Destroy();
            _picker.Destroy();
        }

        // ---- Every frame ----

        private void Update()
        {
            try
            {
                int scene = SceneManager.GetActiveScene().buildIndex;
                if (scene != _scene)
                {
                    if (_open)
                    {
                        Close();
                    }
                    _scene = scene;
                }

                bool cardPage = Game.CardPageShowing;
                if (!cardPage)
                {
                    _loggedButton = false;
                }
                // Opened with the Design card button: closes with the game's ID card page.
                if (_open && _openedFromCard && !cardPage)
                {
                    Close();
                }

                // A click on the icon (Shared/IconBar.cs) or the Design card button from the last OnGUI.
                bool fromCard = _cardButtonClicked;
                bool clicked = _iconClicked || fromCard;
                _iconClicked = false;
                _cardButtonClicked = false;
                KeyCode key = S.WindowKey.Value;
                if ((key != KeyCode.None && Input.GetKeyDown(key) && !HoldsKeys && !IsChatOpen()) || clicked)
                {
                    if (_open)
                    {
                        Close();
                    }
                    else
                    {
                        Open(fromCard);
                    }
                }

                CardInputField editor = Editor;
                if (_open && _hooked && editor != null)
                {
                    if (editor.isFocused)
                    {
                        _editorFrame = Time.frameCount;
                        // Only one of the typing box and the hex box takes the keys.
                        if (_hexTyping)
                        {
                            _clearFocus = true;
                        }
                        Shortcuts();
                    }
                    _known = Snapshot();
                    ReadSelection();
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
            _canvas.LateUpdate();
            // Gives the typing box the keyboard back after a toolbar click took it away.
            CardInputField editor = Editor;
            if (_refocus && editor != null && editor.gameObject.activeInHierarchy)
            {
                _refocus = false;
                if (!_hexTyping && !editor.isFocused)
                {
                    editor.ActivateInputField();
                }
            }
        }

        private void UpdateMouseOver()
        {
            bool shown = _open && Time.frameCount - _drawnFrame <= 2 && !PanelStack.Hidden;
            Vector3 mouse = Input.mousePosition;
            IsMouseOver = shown && _screenRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }

        private static bool IsChatOpen()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+Z (Ctrl+Shift+Z or Ctrl+Y to redo), while typing.
        private void Shortcuts()
        {
            if (!CardInputField.CtrlHeld)
            {
                return;
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (shift)
                {
                    Redo();
                }
                else
                {
                    Undo();
                }
            }
            else if (Input.GetKeyDown(KeyCode.Y))
            {
                Redo();
            }
            else if (!_codeView && Input.GetKeyDown(KeyCode.B))
            {
                ToggleFlag(Flag.Bold);
            }
            else if (!_codeView && Input.GetKeyDown(KeyCode.I))
            {
                ToggleFlag(Flag.Italic);
            }
            else if (!_codeView && Input.GetKeyDown(KeyCode.U))
            {
                ToggleFlag(Flag.Underline);
            }
        }

        // ---- Opening and closing ----

        private void Open(bool fromCard)
        {
            _open = true;
            _openedFromCard = fromCard;
            _status = null;
            _popup = Popup.None;
            _confirmSlot = -1;
            // Unapplied changes are kept until you apply them or click Reset.
            if (!_loadedOnce || !Dirty)
            {
                LoadCard();
            }
            _loadEditor = true;
        }

        private void Close()
        {
            _open = false;
            _openedFromCard = false;
            _popup = Popup.None;
            _hexTyping = false;
            _clearFocus = true;
            _refocus = false;
            _confirmSlot = -1;
            CardInputField editor = Editor;
            EventSystem events = EventSystem.current;
            if (editor != null && events != null && events.currentSelectedGameObject == editor.gameObject)
            {
                events.SetSelectedGameObject(null);
            }
        }

        // ---- The card ----

        private bool Dirty
        {
            get
            {
                for (int i = 0; i < Game.Fields; i++)
                {
                    if (_lines[i] != _loaded[i])
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool LoadCard()
        {
            string[] card = Game.ReadCard();
            if (card == null)
            {
                return false;
            }
            for (int i = 0; i < Game.Fields; i++)
            {
                _lines[i] = card[i];
                _loaded[i] = card[i];
            }
            _loadedOnce = true;
            _loadEditor = true;
            return true;
        }

        private void ApplyCard()
        {
            for (int i = 0; i < Game.Fields; i++)
            {
                if (_lines[i].Length > Game.SendLimit)
                {
                    Status($"{Game.Label(i + 1)} is too long. Shorten it first.");
                    return;
                }
            }
            try
            {
                var lines = (string[])_lines.Clone();
                bool sent = Game.ApplyCard(lines);
                Array.Copy(lines, _loaded, Game.Fields);
                Status(sent ? "Card applied. Everyone sees it now." : "Card saved. Everyone sees it when you join a world.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                Status("Couldn't apply the card. See the BepInEx log.");
            }
        }

        private void ResetCard()
        {
            // Asks first: the button turns into "Sure?" for a moment.
            if (Time.unscaledTime >= _confirmResetUntil)
            {
                _confirmResetUntil = Time.unscaledTime + ConfirmSeconds;
                return;
            }
            _confirmResetUntil = 0f;
            string[] before = (string[])_lines.Clone();
            if (!LoadCard())
            {
                Status("Couldn't read your card.");
                return;
            }
            // Undo brings each changed line back.
            for (int i = 0; i < Game.Fields; i++)
            {
                if (before[i] != _lines[i])
                {
                    _history[i].Changed(new History.Step { Text = before[i] });
                }
            }
            Status("Back to your card as it is now");
        }

        // Clicking a line on the card: edit it, with the keyboard in the typing box.
        private void SelectField(int field)
        {
            if (field != _field)
            {
                _field = field;
                _loadEditor = true;
            }
            _refocus = true;
        }

        // Puts the line being edited into the typing box, with the caret at its end.
        private void LoadEditorNow()
        {
            CardInputField editor = Editor;
            string text = _lines[_field];
            editor.SetTextWithoutNotify(text);
            editor.stringPosition = text.Length;
            _known = Snapshot();
        }

        // ---- The typing box ----

        private void Hook()
        {
            _hooked = true;
            Editor.onValueChanged.AddListener(OnTyped);
        }

        // Typing (not the toolbar, which sets the text without telling).
        private void OnTyped(string text)
        {
            _history[_field].Typed(_known, Time.unscaledTime);
            _lines[_field] = text;
            _known = Snapshot();
        }

        private History.Step Snapshot()
        {
            CardInputField editor = Editor;
            return new History.Step
            {
                Text = editor.text,
                Anchor = editor.selectionAnchorPosition,
                Focus = editor.selectionFocusPosition,
            };
        }

        // Sets the typing box's text from the toolbar, keeping the caret and selection where given
        // (they count shown letters, which restyling doesn't change).
        private void SetEditorText(string text, int anchor, int focus)
        {
            CardInputField editor = Editor;
            editor.SetTextWithoutNotify(text);
            _lines[_field] = text;
            int last = Mathf.Max(0, editor.textComponent.textInfo.characterCount - 1);
            editor.selectionAnchorPosition = Mathf.Clamp(anchor, 0, last);
            editor.selectionFocusPosition = Mathf.Clamp(focus, 0, last);
            _known = Snapshot();
            Refocus();
        }

        private void Refocus()
        {
            if (_pressFocused || EditorTyping)
            {
                _refocus = true;
            }
        }

        // The selection: as shown letters (caret positions, low to high) and as places in the text.
        // `fresh` lays the text out again first, in case it changed since it was last drawn.
        private bool Selection(bool fresh, out int low, out int high, out int start, out int end)
        {
            low = high = start = end = 0;
            CardInputField editor = Editor;
            if (editor == null)
            {
                return false;
            }
            string text = editor.text;
            TMP_Text label = editor.textComponent;
            if (fresh && label.gameObject.activeInHierarchy)
            {
                label.ForceMeshUpdate();
            }
            TMP_TextInfo info = label.textInfo;
            int count = info.characterCount;
            if (count == 0)
            {
                return true;
            }
            int a = Mathf.Clamp(editor.selectionAnchorPosition, 0, count - 1);
            int b = Mathf.Clamp(editor.selectionFocusPosition, 0, count - 1);
            low = Mathf.Min(a, b);
            high = Mathf.Max(a, b);
            // The box ends with an invisible character at the text's end.
            start = Mathf.Min(info.characterInfo[low].index, text.Length);
            end = high > low ? Mathf.Min(info.characterInfo[high - 1].index + info.characterInfo[high - 1].stringLength, text.Length) : start;
            return true;
        }

        // How the selected text looks (all of it, or the whole line when nothing's selected). Only
        // worked out again when the text or the selection changes.
        private void ReadSelection()
        {
            CardInputField editor = Editor;
            if (editor.text == _readText && editor.selectionAnchorPosition == _readAnchor &&
                editor.selectionFocusPosition == _readFocus && _codeView == _readCode)
            {
                return;
            }
            _readText = editor.text;
            _readAnchor = editor.selectionAnchorPosition;
            _readFocus = editor.selectionFocusPosition;
            _readCode = _codeView;
            if (_codeView || !Selection(false, out int low, out int high, out int start, out int end))
            {
                return;
            }
            RichText doc = RichText.Parse(editor.text);
            Range(doc, low, high, start, end, out int from, out int to);
            _isBold = doc.AllHave(from, to, Flag.Bold);
            _isItalic = doc.AllHave(from, to, Flag.Italic);
            _isUnderline = doc.AllHave(from, to, Flag.Underline);
            _isStrike = doc.AllHave(from, to, Flag.Strike);
            _size = doc.CommonSize(from, to);
            _align = doc.Align;
        }

        private static void Range(RichText doc, int low, int high, int start, int end, out int from, out int to)
        {
            if (high > low)
            {
                doc.PiecesBetween(start, end, out from, out to);
            }
            else
            {
                from = 0;
                to = doc.Count;
            }
        }

        // Restyles the selected pieces (or the whole line). `change` returns false to leave it be.
        // A colour dragged across the picker is one undo step: only its first change starts one.
        private void Restyle(Func<RichText, int, int, bool> change, bool newStep = true)
        {
            if (Editor == null || _codeView || !Selection(true, out int low, out int high, out int start, out int end))
            {
                return;
            }
            string text = Editor.text;
            RichText doc = RichText.Parse(text);
            Range(doc, low, high, start, end, out int from, out int to);
            if (from >= to)
            {
                Status("There's nothing to style in this line yet.");
                return;
            }
            if (!change(doc, from, to))
            {
                return;
            }
            string written = doc.Write();
            if (written == text)
            {
                Refocus();
                return;
            }
            if (newStep)
            {
                _history[_field].Changed(Snapshot());
            }
            int anchor = Editor.selectionAnchorPosition;
            int focus = Editor.selectionFocusPosition;
            SetEditorText(written, anchor, focus);
        }

        private void ToggleFlag(Flag flag)
        {
            Restyle((doc, from, to) =>
            {
                doc.SetFlag(from, to, flag, !doc.AllHave(from, to, flag));
                return true;
            });
        }

        private void SetColour(string hex, bool newStep)
        {
            Restyle((doc, from, to) =>
            {
                doc.SetColour(from, to, hex);
                return true;
            }, newStep);
        }

        // Blends from the first letter's colour to the last one's across the letters in between.
        private void Gradient()
        {
            Restyle((doc, from, to) =>
            {
                List<int> letters = Letters(doc, from, to);
                if (letters.Count < 2)
                {
                    Status("A gradient needs at least 2 letters.");
                    return false;
                }
                string first = doc.Pieces[letters[0]].Style.Color;
                string last = doc.Pieces[letters[letters.Count - 1]].Style.Color;
                if (first == null || last == null || first == last)
                {
                    Status("Give the first and last letters different colours, then click Gradient.");
                    return false;
                }
                Color a = RichText.ToColour(first);
                Color b = RichText.ToColour(last);
                for (int k = 0; k < letters.Count; k++)
                {
                    doc.Pieces[letters[k]].Style.Color = RichText.ToHex(Color.Lerp(a, b, k / (letters.Count - 1f)));
                }
                FillBlanks(doc, from, to);
                return true;
            });
        }

        // Red to violet across the letters, as strong and bright as the picker's colour (or full
        // strength when the picker is on white, grey or black).
        private void Rainbow()
        {
            Restyle((doc, from, to) =>
            {
                List<int> letters = Letters(doc, from, to);
                if (letters.Count == 0)
                {
                    return false;
                }
                float s = _picker.S < 0.25f ? 0.75f : _picker.S;
                float v = _picker.V < 0.35f ? 1f : _picker.V;
                for (int k = 0; k < letters.Count; k++)
                {
                    float hue = letters.Count == 1 ? 0f : 0.8f * k / (letters.Count - 1f);
                    doc.Pieces[letters[k]].Style.Color = RichText.ToHex(Color.HSVToRGB(hue, s, v));
                }
                FillBlanks(doc, from, to);
                return true;
            });
        }

        private static List<int> Letters(RichText doc, int from, int to)
        {
            var letters = new List<int>();
            for (int k = from; k < to; k++)
            {
                if (!doc.Pieces[k].IsBlank && doc.Pieces[k].Kind != PieceKind.Sprite)
                {
                    letters.Add(k);
                }
            }
            return letters;
        }

        // Spaces and line breaks take the colour of the letter before them, which saves tags.
        private static void FillBlanks(RichText doc, int from, int to)
        {
            string previous = null;
            for (int k = from; k < to; k++)
            {
                Piece piece = doc.Pieces[k];
                if (piece.IsBlank || piece.Kind == PieceKind.Sprite)
                {
                    if (previous != null)
                    {
                        piece.Style.Color = previous;
                    }
                }
                else
                {
                    previous = piece.Style.Color;
                }
            }
        }

        private void SetAlign(string align)
        {
            Restyle((doc, from, to) =>
            {
                doc.SetAlign(align);
                return true;
            });
        }

        // An emoji or symbol at the caret, in place of the selection.
        private void Insert(string text)
        {
            if (Editor == null || !Selection(true, out int low, out int high, out int start, out int end))
            {
                return;
            }
            string current = Editor.text;
            string written = current.Remove(start, end - start).Insert(start, text);
            _history[_field].Changed(Snapshot());
            // The caret counts shown letters: an emoji or symbol is one, but in the code view each
            // character of the tag shows.
            int caret = _codeView ? low + text.Length : low + 1;
            SetEditorText(written, caret, caret);
        }

        private void Undo()
        {
            if (_history[_field].Undo(Snapshot(), out History.Step back))
            {
                SetEditorText(back.Text, back.Anchor, back.Focus);
                _refocus = true;
            }
        }

        private void Redo()
        {
            if (_history[_field].Redo(Snapshot(), out History.Step forward))
            {
                SetEditorText(forward.Text, forward.Anchor, forward.Focus);
                _refocus = true;
            }
        }

        private void ToggleCodeView()
        {
            _codeView = !_codeView;
            _popup = _codeView && _popup != Popup.Emoji ? Popup.None : _popup;
            CardInputField editor = Editor;
            if (editor != null)
            {
                editor.richText = !_codeView;
                // Laid out with or without the tags showing before the caret goes to the end.
                editor.textComponent.ForceMeshUpdate();
                editor.stringPosition = editor.text.Length;
            }
            Refocus();
        }

        // ---- Presets ----

        private void LoadPreset(int slot)
        {
            string[] preset = _presets.Cards[slot];
            if (preset == null)
            {
                return;
            }
            for (int i = 0; i < Game.Fields; i++)
            {
                if (_lines[i] != preset[i])
                {
                    _history[i].Changed(new History.Step { Text = _lines[i] });
                    _lines[i] = preset[i];
                }
            }
            _loadEditor = true;
            Status($"Loaded preset {slot + 1}. Click Apply card to use it.");
        }

        private void SavePreset(int slot)
        {
            // A full slot asks first: the button turns into "Replace?" for a moment.
            if (_presets.Cards[slot] != null && !(_confirmSlot == slot && Time.unscaledTime < _confirmUntil))
            {
                _confirmSlot = slot;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                return;
            }
            _confirmSlot = -1;
            _presets.Cards[slot] = (string[])_lines.Clone();
            Status(_presets.Save() ? $"Saved to preset {slot + 1}" : "Couldn't save the presets file. See the BepInEx log.");
        }

        // A few words from the preset, for its button.
        private static string PresetLabel(string[] card)
        {
            foreach (int i in new[] { Game.Quote, Game.Location, Game.AreaOfStudy, Game.HoroscopeSign, Game.DateOfBirth })
            {
                string plain = RichText.Parse(card[i]).Plain().Trim();
                if (plain.Length > 0)
                {
                    return plain.Length > 14 ? plain.Substring(0, 13) + "..." : plain;
                }
            }
            return "(blank)";
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons. Only shows in a world.
            if (S.ShowIcon.Value &&
                IconBar.Draw("CardDesigner", IconBar.CardDesignerOrder, Icon.Pixels,
                    _open ? IconBar.State.Open : IconBar.State.Plain, "Card Designer", S.WindowKey.Value) == IconBar.Click.Left)
            {
                _iconClicked = true;   // acted on in Update
            }

            if (_clearFocus)
            {
                GUIUtility.keyboardControl = 0;
                _clearFocus = false;
            }
            if (PanelStack.Hidden)
            {
                return;
            }
            if (_titleStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out float screenWidth);
            try
            {
                DrawCardButton();
                if (_open)
                {
                    DrawWindow(screenWidth);
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

        // The Design card button, on the game's own ID card page.
        private void DrawCardButton()
        {
            if (!S.ShowCardButton.Value || !Game.CardButtonBounds(out Rect bounds))
            {
                return;
            }
            if (S.DebugLog.Value && !_loggedButton)
            {
                _loggedButton = true;
                Plugin.Log.LogInfo($"Design card button at {bounds} (1080p units, screen {PanelStack.Width:0} wide).");
            }
            _cardButtonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(bounds.height * 0.34f, 12f, 26f));
            if (GUI.Button(bounds, _open ? "Close designer" : "Design card", _cardButtonStyle))
            {
                _cardButtonClicked = true;   // acted on in Update
            }
        }

        private void DrawWindow(float screenWidth)
        {
            Rect area = Place(new Vector2(Width, Height), screenWidth);
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(area.x * scale, area.y * scale, area.width * scale, area.height * scale);
            _drawnFrame = Time.frameCount;
            NameDesignerLink.ShowCard(area);

            Event e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                // Whether the typing box had the keyboard before this click took it away.
                _pressFocused = EditorTyping;
                if (!area.Contains(e.mousePosition))
                {
                    GUIUtility.keyboardControl = 0;
                }
            }
            if (e.type == EventType.KeyDown && _hexTyping &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
            {
                GUIUtility.keyboardControl = 0;
                e.Use();
            }

            _canvas.Back(area);
            // The game's ID card page behind is white and busy.
            if (Game.CardPageShowing)
            {
                _canvas.Dim();
            }
            if (!_hooked)
            {
                Hook();
            }
            if (_loadEditor)
            {
                _loadEditor = false;
                LoadEditorNow();
            }

            float x = area.x + Pad;
            float y = area.y + Pad;
            DrawHeader(new Rect(x, y, LeftWidth, HeaderHeight));
            y += HeaderHeight + Gap;
            DrawToolbar(new Rect(x, y, LeftWidth, ToolHeight));
            y += ToolHeight + Gap;
            TMP_FontAsset font = Game.CardFont();
            _canvas.PlaceEditor(new Rect(x, y, LeftWidth, EditorHeight), font, EditorFontSize, Game.TextColour(), Game.BoxColour());
            y += EditorHeight + 4f;
            DrawInfo(new Rect(x, y, LeftWidth, InfoHeight));
            y += InfoHeight + 10f;
            DrawPresets(new Rect(x, y, LeftWidth, PresetsHeight));
            y += PresetsHeight + 10f;
            DrawActions(new Rect(x, y, LeftWidth, RowHeight));

            float rx = x + LeftWidth + Gutter;
            float ry = area.y + Pad;
            DrawRightHeader(new Rect(rx, ry, RightWidth, HeaderHeight));
            var right = new Rect(rx, ry + HeaderHeight + Gap, RightWidth, area.yMax - Pad - (ry + HeaderHeight + Gap));
            DrawPreview(right, font);
            if (_popup != Popup.None)
            {
                DrawPopup(right, font);
            }

            _hexTyping = GUI.GetNameOfFocusedControl() == HexFieldName;
        }

        // Beside Name Designer while it's open for the name line, in the middle of the screen while
        // the game's ID card page shows, otherwise in the bottom window stack.
        private static Rect Place(Vector2 size, float screenWidth)
        {
            float middleY = Mathf.Clamp((1080f - size.y) / 2f, 10f, Mathf.Max(10f, 1080f - size.y - 10f));
            if (NameDesignerLink.DesignerBounds(out Rect designer))
            {
                float both = size.x + PairGap + designer.width;
                return new Rect(Mathf.Max(10f, (screenWidth - both) / 2f), middleY, size.x, size.y);
            }
            if (Game.CardPageShowing)
            {
                return new Rect(Mathf.Max(10f, (screenWidth - size.x) / 2f), middleY, size.x, size.y);
            }
            Rect stacked = PanelStack.Place(PanelId, PanelStack.CardDesignerOrder, size);
            stacked.y = Mathf.Max(10f, stacked.y);
            return stacked;
        }

        private void DrawHeader(Rect row)
        {
            GUI.Label(row, "<b>CARD DESIGNER</b>", _titleStyle);
            GUI.Label(new Rect(row.x + 140f, row.y, 220f, row.height), Game.Label(_field + 1), _dimStyle);

            const float w = 58f;
            float x = row.xMax - w;
            if (GUI.Toggle(new Rect(x, row.y, w, row.height), _codeView, "Code", _toolStyle) != _codeView)
            {
                ToggleCodeView();
            }
            x -= w + 4f;
            GUI.enabled = _history[_field].CanRedo;
            if (GUI.Button(new Rect(x, row.y, w, row.height), "Redo", _buttonStyle))
            {
                Redo();
            }
            x -= w + 4f;
            GUI.enabled = _history[_field].CanUndo;
            if (GUI.Button(new Rect(x, row.y, w, row.height), "Undo", _buttonStyle))
            {
                Undo();
            }
            GUI.enabled = true;
        }

        private void DrawToolbar(Rect row)
        {
            float x = row.x;
            GUI.enabled = !_codeView;
            x = FlagButton(x, row, "<b>B</b>", _isBold, Flag.Bold);
            x = FlagButton(x, row, "<i>I</i>", _isItalic, Flag.Italic);
            x = FlagButton(x, row, "U", _isUnderline, Flag.Underline);
            x = FlagButton(x, row, "S", _isStrike, Flag.Strike);
            x += 7f;

            x = PopupButton(x, row, 66f, "Colour", Popup.Colour);
            x = PopupButton(x, row, 80f, "Highlight", Popup.Highlight);
            x = PopupButton(x, row, 50f, "Size", Popup.Size);
            string align = _align == "center" ? "Centre" : _align == "right" ? "Right" : "Left";
            if (GUI.Button(new Rect(x, row.y, 100f, row.height), "Align: " + align, _buttonStyle))
            {
                SetAlign(_align == "center" ? "right" : _align == "right" ? null : "center");
            }
            x += 100f + 4f + 7f;

            // Emoji and symbols go in as they are, so they work in the code view too.
            GUI.enabled = true;
            x = PopupButton(x, row, 74f, "Emoji", Popup.Emoji);
            x += 7f;

            GUI.enabled = !_codeView;
            if (GUI.Button(new Rect(row.xMax - 60f, row.y, 60f, row.height), "Clear", _buttonStyle))
            {
                Restyle((doc, from, to) =>
                {
                    doc.ClearStyle(from, to);
                    return true;
                });
            }
            GUI.enabled = true;
        }

        private float FlagButton(float x, Rect row, string label, bool on, Flag flag)
        {
            var button = new Rect(x, row.y, 30f, row.height);
            if (GUI.Toggle(button, on, label, _toolStyle) != on)
            {
                ToggleFlag(flag);
            }
            // The GUI's font can't underline or strike through, so draw the line.
            if (Event.current.type == EventType.Repaint && (flag == Flag.Underline || flag == Flag.Strike))
            {
                float lineY = flag == Flag.Underline ? button.center.y + 8f : button.center.y + 1f;
                ColorPicker.Fill(new Rect(button.center.x - 5f, lineY, 10f, 1.5f),
                    GUI.enabled ? Color.white : new Color(1f, 1f, 1f, 0.4f));
            }
            return x + 30f + 3f;
        }

        private float PopupButton(float x, Rect row, float width, string label, Popup popup)
        {
            bool open = _popup == popup;
            if (GUI.Toggle(new Rect(x, row.y, width, row.height), open, label, _toolStyle) != open)
            {
                _popup = open ? Popup.None : popup;
                GUIUtility.keyboardControl = 0;
                Refocus();
            }
            return x + width + 4f;
        }

        private void DrawInfo(Rect row)
        {
            int length = _lines[_field].Length;
            bool tooLong = length > Game.SendLimit;
            GUI.Label(new Rect(row.x, row.y, 100f, row.height), $"{length} / {Game.SendLimit}", tooLong ? _warnStyle : _dimStyle);

            var note = new Rect(row.x + 100f, row.y, row.width - 100f, row.height);
            if (tooLong)
            {
                GUI.Label(note, "Too long: other players only get the first 1000 characters (tags count).", _warnStyle);
            }
            else if (_field == Game.Quote && _canvas.QuoteCut)
            {
                GUI.Label(note, $"The quote shows 3 lines, so the other {_canvas.QuoteLines - Game.QuoteLines} get cut off.", _warnStyle);
            }
            else if (_codeView)
            {
                GUI.Label(note, "Code view: the tags as the game gets them. Click Code again to go back.", _dimStyle);
            }
            else
            {
                GUI.Label(note, "Enter starts a new line. Ctrl+B, I or U styles, Ctrl+Z undoes.", _dimStyle);
            }
        }

        private void DrawPresets(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, LabelHeight), "<b>PRESETS</b>", _titleStyle);
            GUI.Label(new Rect(area.x, area.y, area.width, LabelHeight), "Click one to load it", _keyStyle);

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
                string[] preset = _presets.Cards[i];
                GUI.enabled = preset != null;
                if (GUI.Button(new Rect(px, loadY, w, RowHeight), preset != null ? PresetLabel(preset) : "Empty", _presetStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    LoadPreset(i);
                }
                GUI.enabled = true;
                string save = _confirmSlot == i ? "Replace?" : "Save " + (i + 1);
                if (GUI.Button(new Rect(px, saveY, w, SaveHeight), save, _buttonStyle))
                {
                    SavePreset(i);
                }
            }
        }

        private void DrawActions(Rect row)
        {
            const float applyWidth = 130f;
            const float resetWidth = 80f;
            bool dirty = Dirty;
            GUI.enabled = dirty;
            if (GUI.Button(new Rect(row.x, row.y, applyWidth, row.height), "Apply card", _buttonStyle))
            {
                GUIUtility.keyboardControl = 0;
                ApplyCard();
            }
            bool confirming = Time.unscaledTime < _confirmResetUntil;
            if (GUI.Button(new Rect(row.x + applyWidth + 6f, row.y, resetWidth, row.height), confirming ? "Sure?" : "Reset", _buttonStyle))
            {
                GUIUtility.keyboardControl = 0;
                ResetCard();
            }
            GUI.enabled = true;

            bool showStatus = _status != null && Time.unscaledTime < _statusUntil;
            float x = row.x + applyWidth + resetWidth + 18f;
            string text = showStatus ? _status : dirty ? "Not applied yet" : "This is your card now";
            GUI.Label(new Rect(x, row.y, row.xMax - x, row.height), text, showStatus ? _statusStyle : _dimStyle);
        }

        private void DrawRightHeader(Rect row)
        {
            string title;
            switch (_popup)
            {
                case Popup.Colour: title = "COLOUR"; break;
                case Popup.Highlight: title = "HIGHLIGHT"; break;
                case Popup.Size: title = "SIZE"; break;
                case Popup.Emoji: title = "EMOJI AND SYMBOLS"; break;
                default: title = "WHAT OTHERS SEE"; break;
            }
            GUI.Label(row, "<b>" + title + "</b>", _titleStyle);
            const float w = 28f;
            if (_popup != Popup.None)
            {
                if (GUI.Button(new Rect(row.xMax - w - 4f - 100f, row.y, 100f, row.height), "Show card", _buttonStyle))
                {
                    _popup = Popup.None;
                    Refocus();
                }
            }
            if (GUI.Button(new Rect(row.xMax - w, row.y, w, row.height), "×", _buttonStyle))
            {
                Close();
            }
        }

        // ---- The card preview ----

        private void DrawPreview(Rect area, TMP_FontAsset font)
        {
            Vector2 size = CardCanvas.PreviewSize(area.width);
            var card = new Rect(area.x, area.y, size.x, size.y);
            _canvas.PlacePreview(card, font, Game.CardColour(), Game.BoxColour(), Game.TextColour(), Game.PlaceholderColour());
            _canvas.PreviewLine(0, Game.Name(), Game.Label(0));
            for (int i = 0; i < Game.Fields; i++)
            {
                _canvas.PreviewLine(i + 1, _lines[i], Game.Label(i + 1));
            }
            if (_popup != Popup.None)
            {
                return;
            }

            Event e = Event.current;
            for (int r = 0; r < CardCanvas.Rows; r++)
            {
                Rect box = _canvas.PreviewBox(r);
                bool hover = box.Contains(e.mousePosition);
                if (r == 0)
                {
                    DrawNameRow(box);
                    continue;
                }
                if (GUI.Button(box, GUIContent.none, GUIStyle.none))
                {
                    GUIUtility.keyboardControl = 0;
                    SelectField(r - 1);
                }
                if (e.type == EventType.Repaint && (r - 1 == _field || hover))
                {
                    Color edge = r - 1 == _field ? new Color(0.3f, 0.55f, 1f, 1f) : new Color(1f, 1f, 1f, 0.6f);
                    ColorPicker.Outline(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), edge);
                    ColorPicker.Outline(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f), edge);
                }
            }
            float hintY = card.yMax + 6f;
            GUI.Label(new Rect(area.x, hintY, area.width, area.yMax - hintY),
                "Click a line to edit it. This is the card other players see when they click you.", _hintStyle);
        }

        // Your name is designed in Name Designer, when it's installed.
        private void DrawNameRow(Rect box)
        {
            if (NameDesignerLink.HasDesigner)
            {
                const float w = 110f;
                var button = new Rect(box.xMax - w - 6f, box.y + (box.height - 24f) / 2f, w, 24f);
                if (GUI.Button(button, "Design name", _buttonStyle))
                {
                    GUIUtility.keyboardControl = 0;
                    NameDesignerLink.OpenDesigner();
                }
            }
            else if (GUI.Button(box, GUIContent.none, GUIStyle.none))
            {
                Status("Change your name on the game's ID card, or colour it with Name Designer.");
            }
        }

        // ---- Popups, over the card preview ----

        private void DrawPopup(Rect area, TMP_FontAsset font)
        {
            _canvas.Popup(new Rect(area.x - 4f, area.y - 2f, area.width + 8f, area.height + 4f));
            var inner = new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f);
            switch (_popup)
            {
                case Popup.Colour:
                    DrawColourPopup(inner);
                    break;
                case Popup.Highlight:
                    DrawHighlightPopup(inner);
                    break;
                case Popup.Size:
                    DrawSizePopup(inner);
                    break;
                case Popup.Emoji:
                    DrawEmojiPopup(inner, font);
                    break;
            }
        }

        private void DrawColourPopup(Rect area)
        {
            const float pickerSize = 150f;
            var square = new Rect(area.x, area.y, pickerSize, pickerSize);
            var strip = new Rect(square.xMax + 8f, area.y, 22f, pickerSize);
            // Read before the picker uses the event: the press starts a new undo step, the drag doesn't.
            bool press = Event.current.type == EventType.MouseDown;
            if (_picker.Draw(square, strip))
            {
                _hexText = RichText.ToHex(_picker.Value);
                SetColour(_hexText, newStep: press);
            }

            float x = strip.xMax + 14f;
            float y = area.y;
            GUI.Label(new Rect(x, y, 64f, 26f), "Colour  #", _labelStyle);
            GUI.SetNextControlName(HexFieldName);
            string shown = (_hexText ?? "").TrimStart('#');
            string typed = GUI.TextField(new Rect(x + 66f, y, 80f, 26f), shown, 6, _fieldStyle);
            if (typed != shown)
            {
                _hexText = "#" + typed;
                string hex = typed.Length == 6 ? RichText.Hex("#" + typed) : null;
                if (hex != null)
                {
                    _picker.Set(RichText.ToColour(hex));
                    SetColour(hex, newStep: true);
                }
            }
            var current = new Rect(x + 152f, y, 36f, 26f);
            ColorPicker.Fill(current, _picker.Value);
            ColorPicker.Outline(current, new Color(1f, 1f, 1f, 0.5f));
            y += 26f + 8f;

            const int perRow = 8;
            const float swatch = 24f;
            for (int i = 0; i < Swatches.Length; i++)
            {
                var cell = new Rect(x + (i % perRow) * (swatch + 6f), y + (i / perRow) * (swatch + 6f), swatch, swatch);
                Color colour = RichText.ToColour(Swatches[i]);
                if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
                {
                    GUIUtility.keyboardControl = 0;
                    _picker.Set(colour);
                    _hexText = Swatches[i];
                    SetColour(Swatches[i], newStep: true);
                }
                ColorPicker.Fill(cell, colour);
                ColorPicker.Outline(cell, new Color(0f, 0f, 0f, 0.6f));
            }
            y = area.y + pickerSize + 12f;

            float w = (area.width - 2f * Gap) / 3f;
            if (GUI.Button(new Rect(area.x, y, w, RowHeight), "No colour", _buttonStyle))
            {
                GUIUtility.keyboardControl = 0;
                SetColour(null, newStep: true);
            }
            if (GUI.Button(new Rect(area.x + w + Gap, y, w, RowHeight), "Gradient", _buttonStyle))
            {
                GUIUtility.keyboardControl = 0;
                Gradient();
            }
            if (GUI.Button(new Rect(area.x + 2f * (w + Gap), y, w, RowHeight), "Rainbow", _buttonStyle))
            {
                GUIUtility.keyboardControl = 0;
                Rainbow();
            }
            y += RowHeight + 10f;
            GUI.Label(new Rect(area.x, y, area.width, area.yMax - y),
                "Colours the selected text, or the whole line when nothing is selected.\n\n" +
                "Gradient blends from the first letter's colour to the last one's. Rainbow runs red to violet " +
                "across the letters, as strong and bright as the picker.\n\n" +
                "Each change of colour uses about 17 of the line's 1000 characters, so a rainbow on a long line " +
                "adds up fast.", _hintStyle);
        }

        private void DrawHighlightPopup(Rect area)
        {
            const int perRow = 4;
            float cell = (area.width - (perRow - 1) * Gap) / perRow;
            const float cellHeight = 40f;
            for (int i = 0; i < Highlights.Length; i++)
            {
                var r = new Rect(area.x + (i % perRow) * (cell + Gap), area.y + (i / perRow) * (cellHeight + Gap), cell, cellHeight);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    string mark = Highlights[i];
                    Restyle((doc, from, to) =>
                    {
                        doc.SetMark(from, to, mark);
                        return true;
                    });
                }
                if (Event.current.type == EventType.Repaint)
                {
                    // On the card's own box colour, the way it'll look.
                    ColorPicker.Fill(r, Game.BoxColour());
                    Color c = RichText.ToColour(Highlights[i]);
                    c.a = Alpha(Highlights[i]);
                    ColorPicker.Fill(new Rect(r.x + 8f, r.y + 10f, r.width - 16f, r.height - 20f), c);
                    ColorPicker.Outline(r, r.Contains(Event.current.mousePosition) ? Color.white : new Color(0f, 0f, 0f, 0.6f));
                }
            }
            float y = area.y + 2f * (cellHeight + Gap) + 6f;
            if (GUI.Button(new Rect(area.x, y, 140f, RowHeight), "No highlight", _buttonStyle))
            {
                Restyle((doc, from, to) =>
                {
                    doc.SetMark(from, to, null);
                    return true;
                });
            }
            y += RowHeight + 10f;
            GUI.Label(new Rect(area.x, y, area.width, area.yMax - y),
                "Puts a coloured band behind the selected text, or the whole line when nothing is selected.", _hintStyle);
        }

        private static float Alpha(string hex)
        {
            string h = RichText.Hex(hex);
            return h != null && h.Length == 9 ? Convert.ToInt32(h.Substring(7, 2), 16) / 255f : 1f;
        }

        private void DrawSizePopup(Rect area)
        {
            const int perRow = 3;
            float w = (area.width - (perRow - 1) * Gap) / perRow;
            for (int i = 0; i < Sizes.Length; i++)
            {
                var r = new Rect(area.x + (i % perRow) * (w + Gap), area.y + (i / perRow) * (34f + Gap), w, 34f);
                string size = Sizes[i];
                bool on = _size == size;
                if (GUI.Toggle(r, on, size ?? "Normal", _toolStyle) != on)
                {
                    Restyle((doc, from, to) =>
                    {
                        doc.SetSize(from, to, size);
                        return true;
                    });
                }
            }
            float y = area.y + 3f * (34f + Gap) + 10f;
            GUI.Label(new Rect(area.x, y, area.width, area.yMax - y),
                "Sizes the selected text, or the whole line when nothing is selected.\n\n" +
                "Smaller text fits more on the card. The short lines show one line of normal-size text; at 70% or " +
                "smaller, two lines fit (press Enter between them). The quote shows 3 lines at any size, but smaller " +
                "text fits more on each. The card on the right shows exactly what fits.", _hintStyle);
        }

        private void DrawEmojiPopup(Rect area, TMP_FontAsset font)
        {
            float w = (area.width - Gap) / 2f;
            if (GUI.Toggle(new Rect(area.x, area.y, w, 26f), !_symbolsTab, "Emoji", _toolStyle) == _symbolsTab)
            {
                _symbolsTab = false;
            }
            if (GUI.Toggle(new Rect(area.x + w + Gap, area.y, w, 26f), _symbolsTab, "Symbols", _toolStyle) != _symbolsTab)
            {
                _symbolsTab = true;
            }
            var body = new Rect(area.x, area.y + 34f, area.width, area.height - 34f);
            if (_symbolsTab)
            {
                DrawSymbols(body, font);
            }
            else
            {
                DrawEmoji(body);
            }
        }

        private void DrawEmoji(Rect area)
        {
            Texture sheet = _emoji.SheetTexture;
            IReadOnlyList<Emoji.Picture> pictures = _emoji.Pictures;
            if (sheet == null || pictures.Count == 0)
            {
                GUI.Label(area, "The game's emoji sheet wasn't found, so there are no emoji to add. Symbols still work.", _hintStyle);
                return;
            }
            const int perRow = 8;
            float cell = (area.width - (perRow - 1) * 4f) / perRow;
            Event e = Event.current;
            for (int i = 0; i < pictures.Count; i++)
            {
                var r = new Rect(area.x + (i % perRow) * (cell + 4f), area.y + (i / perRow) * (cell + 4f), cell, cell);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Insert(Emoji.Tag(pictures[i].Index));
                }
                if (e.type == EventType.Repaint)
                {
                    bool hover = r.Contains(e.mousePosition);
                    ColorPicker.Fill(r, new Color(1f, 1f, 1f, hover ? 0.18f : 0.06f));
                    GUI.DrawTextureWithTexCoords(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), sheet, pictures[i].Uv);
                }
            }
            int rows = (pictures.Count + perRow - 1) / perRow;
            float y = area.y + rows * (cell + 4f) + 10f;
            GUI.Label(new Rect(area.x, y, area.width, area.yMax - y),
                "Click one to put it at the caret. These are the game's own emoji, so everyone sees them. " +
                "Each one uses 27 of the line's 1000 characters.\n\n" +
                "Emoji typed from the keyboard don't show in the game: use these, or the Symbols tab.", _hintStyle);
        }

        private void DrawSymbols(Rect area, TMP_FontAsset font)
        {
            IReadOnlyList<string> symbols = _emoji.Symbols(font);
            const int perRow = 16;
            float cell = area.width / perRow;
            Event e = Event.current;
            for (int i = 0; i < symbols.Count; i++)
            {
                var r = new Rect(area.x + (i % perRow) * cell, area.y + (i / perRow) * cell, cell, cell);
                _canvas.Cell(r, symbols[i], font, cell * 0.68f);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Insert(symbols[i]);
                }
                if (e.type == EventType.Repaint && r.Contains(e.mousePosition))
                {
                    ColorPicker.Outline(r, Color.white);
                }
            }
            int rows = (symbols.Count + perRow - 1) / perRow;
            float y = area.y + rows * cell + 8f;
            GUI.Label(new Rect(area.x, y, area.width, area.yMax - y),
                "Every symbol here is in the card's lettering, so everyone sees it. Each uses 1 character.", _hintStyle);
        }

        // ---- Styles ----

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.65f);

            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _dimStyle = Label(14, dim, TextAnchor.MiddleLeft);
            _keyStyle = Label(13, dim, TextAnchor.MiddleRight);
            _labelStyle = Label(14, Color.white, TextAnchor.MiddleLeft);
            _hintStyle = Label(13, dim, TextAnchor.UpperLeft);
            _hintStyle.wordWrap = true;
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _warnStyle = Label(14, new Color32(0xFF, 0x8A, 0x7A, 0xFF), TextAnchor.MiddleLeft);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                padding = new RectOffset(4, 4, 2, 2),
            };
            _toolStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                richText = true,
                padding = new RectOffset(4, 4, 2, 2),
            };
            _presetStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
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
            _cardButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
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
