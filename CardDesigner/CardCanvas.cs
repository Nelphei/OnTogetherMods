using System.Collections.Generic;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardDesigner
{
    // The parts of the window drawn with TextMeshPro on a screen canvas: the window's box, the typing
    // box, the card preview and the popup's box with its symbols. Unity's immediate-mode GUI (the
    // buttons and labels) always draws on top of this, which is why the window's box is drawn here
    // and not with GUI.Box.
    //
    // Use it like Shared/TextBox.cs: from OnGUI, call Back first and then the rest on every frame
    // they should show (1080p units from the top-left, inside PanelStack.Begin). LateUpdate hides
    // what wasn't drawn, and everything while Photo Mode takes a photo or the game's eye button
    // hides the UI. The canvas also stops clicks on the window from reaching the game's UI.
    internal sealed class CardCanvas
    {
        // The preview is built in the game's ID card units and scaled down, so the text breaks into
        // lines (and gets cut off) exactly as on the card other players see. The sizes are the card's
        // (reference/Assembly-CSharp/TemporaryPhoto.cs boxes, read from the game's scene).
        private const float CardPad = 28f;
        private const float LabelHeight = 46f;
        private const float LabelSize = 30f;
        private const float RowGap = 12f;
        private const float BoxWidth = 1211.7f;
        private const float ShortHeight = 99.9f;
        private const float QuoteHeight = 245.2f;
        private const float InsetX = 43f;
        private const float ShortInsetY = 6.5f;
        private const float QuoteInsetY = 41.5f;
        private const float CardFontSize = 47.93f;
        public const int Rows = 6;
        public const int QuoteRow = 5;

        // The game's input boxes add an invisible character after the text (TMP_InputField.UpdateLabel).
        private const string Invisible = "​";

        private sealed class Row
        {
            public TextMeshProUGUI Label;
            public Image Box;
            public TextMeshProUGUI Text;
            public string Shown;
            public float Y;
            public float Height;
        }

        private readonly string _name;
        private GameObject _root;
        private RectTransform _back;
        private GameObject _dim;
        private int _dimFrame = -100;
        private RectTransform _editorRect;
        private Image _editorBack;
        private RectTransform _preview;
        private Image _cardImage;
        private readonly Row[] _rows = new Row[Rows];
        private RectTransform _popup;
        private readonly List<TextMeshProUGUI> _cells = new List<TextMeshProUGUI>();
        private readonly List<int> _cellFrames = new List<int>();
        private int _nextCell;
        private int _backFrame = -100;
        private int _popupFrame = -100;
        private int _previewFrame = -100;
        private Sprite _cardSprite;

        // The window's solid background: a dark grey with a hint of purple, like the icon row's buttons.
        private static readonly Color WindowColour = new Color32(30, 28, 36, 255);
        private float _previewScale = 1f;
        private Vector2 _previewOrigin;
        private float _cardHeight;
        private Color _textColour = Color.black;
        private Color _placeholderColour = Color.grey;

        public CardCanvas(string name)
        {
            _name = name;
        }

        public CardInputField Editor { get; private set; }

        // How many lines the quote has, and whether the card cuts some off (it shows 3).
        public int QuoteLines { get; private set; }
        public bool QuoteCut => QuoteLines > Game.QuoteLines;

        // The preview's size for a given width, in 1080p units.
        public static Vector2 PreviewSize(float width)
        {
            float scale = width / CardWidth;
            return new Vector2(width, CardHeight * scale);
        }

        private static float CardWidth => BoxWidth + 2f * CardPad;

        private static float CardHeight
        {
            get
            {
                float y = CardPad;
                for (int r = 0; r < Rows; r++)
                {
                    y += LabelHeight + (r == QuoteRow ? QuoteHeight : ShortHeight) + RowGap;
                }
                return y - RowGap + CardPad;
            }
        }

        // ---- Drawing (from OnGUI) ----

        // The window's dark box. Call first in each OnGUI pass.
        public void Back(Rect rect)
        {
            Prepare();
            _nextCell = 0;
            Place(_back, rect);
            _backFrame = Time.frameCount;
        }

        // Dims the rest of the screen this frame, like a dialog does. Used over the game's ID card
        // page, which is white and busy. It doesn't catch clicks, so the page's buttons still work.
        public void Dim()
        {
            _dimFrame = Time.frameCount;
            SetActive(_dim, true);
        }

        // The typing box, in the card's lettering and colours, so styled text looks as it will.
        public void PlaceEditor(Rect rect, TMP_FontAsset font, float fontSize, Color text, Color back)
        {
            Place(_editorRect, rect);
            CardInputField editor = Editor;
            if (font != null && editor.fontAsset != font)
            {
                editor.fontAsset = font;
            }
            if (!Mathf.Approximately(editor.pointSize, fontSize))
            {
                editor.pointSize = fontSize;
            }
            if (editor.textComponent.color != text)
            {
                editor.textComponent.color = text;
                editor.caretColor = text;
                editor.placeholder.color = new Color(text.r, text.g, text.b, 0.45f);
            }
            _editorBack.color = back;
        }

        // The card preview, scaled to fit `rect`'s width, at its top.
        public void PlacePreview(Rect rect, TMP_FontAsset font, Color card, Color box, Color text, Color placeholder)
        {
            _previewScale = rect.width / CardWidth;
            _previewOrigin = rect.position;
            _cardHeight = CardHeight;
            _preview.anchoredPosition = new Vector2(rect.x, -rect.y);
            _preview.localScale = new Vector3(_previewScale, _previewScale, 1f);
            _previewFrame = Time.frameCount;
            // On straight away, so the quote can be measured this frame.
            SetActive(_preview.gameObject, true);
            _textColour = text;
            _placeholderColour = placeholder;

            _cardImage.color = card;
            Sprite boxSprite = Game.BoxSprite();
            if (boxSprite != _cardSprite)
            {
                _cardSprite = boxSprite;
                foreach (Row row in _rows)
                {
                    row.Box.sprite = boxSprite;
                    row.Box.type = boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                }
                _cardImage.sprite = boxSprite;
                _cardImage.type = boxSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }

            // Labels sit on the card's colour: dark on a light card, light on a dark one.
            float light = 0.299f * card.r + 0.587f * card.g + 0.114f * card.b;
            Color labelColour = light > 0.6f ? new Color(0.15f, 0.12f, 0.16f, 0.85f) : new Color(1f, 1f, 1f, 0.9f);
            for (int r = 0; r < Rows; r++)
            {
                Row row = _rows[r];
                row.Box.color = box;
                row.Label.color = labelColour;
                if (font != null && row.Text.font != font)
                {
                    row.Text.font = font;
                    row.Label.font = font;
                    // Measured again in the new lettering.
                    row.Shown = null;
                }
            }
        }

        // One line of the preview. An empty line shows its label in the box, as the card does.
        public void PreviewLine(int r, string text, string label)
        {
            Row row = _rows[r];
            if (row.Label.text != label)
            {
                row.Label.text = label;
            }
            bool empty = string.IsNullOrEmpty(text);
            Color colour = empty ? _placeholderColour : _textColour;
            if (row.Text.color != colour)
            {
                row.Text.color = colour;
            }
            string shown = empty ? label : text + Invisible;
            if (row.Shown == shown)
            {
                return;
            }
            row.Shown = shown;
            row.Text.text = shown;
            if (r == QuoteRow)
            {
                CutQuote(row, empty ? "" : text);
            }
        }

        // The quote box shows 3 lines: setting its text cuts off the rest, the way the game's box does
        // (TMP_InputField.UpdateLabel with lineLimit 3).
        private void CutQuote(Row row, string text)
        {
            QuoteLines = 0;
            if (text.Length == 0)
            {
                return;
            }
            row.Text.ForceMeshUpdate();
            TMP_TextInfo info = row.Text.textInfo;
            QuoteLines = info.lineCount;
            if (info.lineCount <= Game.QuoteLines)
            {
                return;
            }
            int last = info.lineInfo[Game.QuoteLines - 1].lastCharacterIndex;
            int keep = info.characterInfo[last].index + info.characterInfo[last].stringLength;
            if (keep >= 0 && keep < text.Length)
            {
                row.Text.text = text.Remove(keep) + Invisible;
            }
        }

        // Where a preview line's box is, in 1080p units, for clicking.
        public Rect PreviewBox(int r)
        {
            Row row = _rows[r];
            return new Rect(_previewOrigin.x + CardPad * _previewScale, _previewOrigin.y + row.Y * _previewScale,
                BoxWidth * _previewScale, row.Height * _previewScale);
        }

        // Where a preview line's label is, in 1080p units.
        public Rect PreviewLabel(int r)
        {
            Row row = _rows[r];
            return new Rect(_previewOrigin.x + CardPad * _previewScale, _previewOrigin.y + (row.Y - LabelHeight) * _previewScale,
                BoxWidth * _previewScale, LabelHeight * _previewScale);
        }

        public float PreviewHeight => _cardHeight * _previewScale;

        // The popup's box, over the preview.
        public void Popup(Rect rect)
        {
            Place(_popup, rect);
            _popupFrame = Time.frameCount;
        }

        // One symbol in the popup, centred in `rect`.
        public void Cell(Rect rect, string text, TMP_FontAsset font, float size)
        {
            if (_nextCell >= _cells.Count)
            {
                _cells.Add(NewText("Cell", _root.transform, size, TextAlignmentOptions.Center));
                _cellFrames.Add(-100);
            }
            TextMeshProUGUI cell = _cells[_nextCell];
            _cellFrames[_nextCell] = Time.frameCount;
            _nextCell++;
            if (font != null && cell.font != font)
            {
                cell.font = font;
            }
            if (cell.text != text)
            {
                cell.text = text;
            }
            cell.fontSize = size;
            Place(cell.rectTransform, rect);
        }

        public void LateUpdate()
        {
            if (_root == null)
            {
                return;
            }
            int frame = Time.frameCount;
            bool shown = frame - _backFrame <= 1 && !PanelStack.Hidden;
            if (_root.activeSelf != shown)
            {
                // Switching it off also stops typing in the box.
                _root.SetActive(shown);
            }
            if (!shown)
            {
                return;
            }
            SetActive(_dim, frame - _dimFrame <= 1);
            SetActive(_popup.gameObject, frame - _popupFrame <= 1);
            SetActive(_preview.gameObject, frame - _previewFrame <= 1);
            for (int i = 0; i < _cells.Count; i++)
            {
                SetActive(_cells[i].gameObject, frame - _cellFrames[i] <= 1);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go.activeSelf != on)
            {
                go.SetActive(on);
            }
        }

        private static void Place(RectTransform t, Rect rect)
        {
            var position = new Vector2(rect.x, -rect.y);
            if (t.anchoredPosition != position)
            {
                t.anchoredPosition = position;
            }
            if (t.sizeDelta != rect.size)
            {
                t.sizeDelta = rect.size;
            }
        }

        private void Prepare()
        {
            if (_root == null)
            {
                Build();
            }
            if (!_root.activeSelf && !PanelStack.Hidden)
            {
                _root.SetActive(true);
            }
        }

        // ---- Building ----

        private void Build()
        {
            // Named so Photo Mode leaves it alone when it hides the game's UI.
            _root = new GameObject(PanelStack.CanvasPrefix + _name);
            // Off while it's built, so nothing starts up half set up.
            _root.SetActive(false);
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            // Laid out in 1080p units, scaled to the real screen height, like PanelStack.Begin.
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            // Lets the typing box take clicks, and keeps clicks on the window off the game's UI.
            _root.AddComponent<GraphicRaycaster>();

            // The whole screen, behind everything else, for Dim.
            Image dim = NewImage("Dim", _root.transform, new Color(0f, 0f, 0f, 0.55f), raycast: false);
            Stretch(dim.rectTransform);
            _dim = dim.gameObject;
            _dim.SetActive(false);

            // Solid, unlike the other mods' see-through boxes: the window often sits over the game's
            // white ID card page.
            Image back = NewImage("Box", _root.transform, WindowColour, raycast: true);
            back.sprite = RoundedSprite();
            back.type = Image.Type.Sliced;
            _back = TopLeft(back.gameObject);

            BuildEditor();
            BuildPreview();

            Image popup = NewImage("Popup", _root.transform, new Color(0.08f, 0.07f, 0.1f, 0.96f), raycast: true);
            _popup = TopLeft(popup.gameObject);
            popup.gameObject.SetActive(false);
        }

        private void BuildEditor()
        {
            _editorBack = NewImage("Editor", _root.transform, Color.white, raycast: true);
            GameObject go = _editorBack.gameObject;
            _editorRect = TopLeft(go);

            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(12f, 10f);
            areaRect.offsetMax = new Vector2(-12f, -10f);

            TextMeshProUGUI placeholder = NewText("Placeholder", area.transform, 24f, TextAlignmentOptions.TopLeft);
            Stretch(placeholder.rectTransform);
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.text = "Type here...";
            TextMeshProUGUI text = NewText("Text", area.transform, 24f, TextAlignmentOptions.TopLeft);
            Stretch(text.rectTransform);

            CardInputField editor = go.AddComponent<CardInputField>();
            editor.textViewport = areaRect;
            editor.textComponent = text;
            editor.placeholder = placeholder;
            editor.transition = Selectable.Transition.None;
            editor.navigation = new Navigation { mode = Navigation.Mode.None };
            editor.lineType = TMP_InputField.LineType.MultiLineNewline;
            // Shows the styling while you type, and moves the caret over the hidden tags.
            editor.richText = true;
            editor.isRichTextEditingAllowed = false;
            // Esc stops typing but keeps what you typed. Clicking a toolbar button keeps the selection.
            editor.restoreOriginalTextOnEscape = false;
            editor.resetOnDeActivation = false;
            editor.onFocusSelectAll = false;
            editor.keepTextSelectionVisible = true;
            editor.customCaretColor = true;
            editor.caretWidth = 2;
            editor.selectionColor = new Color(0.35f, 0.55f, 1f, 0.45f);
            editor.scrollSensitivity = 2f;
            editor.characterLimit = 0;
            Editor = editor;
        }

        private void BuildPreview()
        {
            var go = new GameObject("Preview", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            _preview = TopLeft(go);
            _preview.sizeDelta = new Vector2(CardWidth, CardHeight);

            _cardImage = NewImage("Card", go.transform, Color.white, raycast: false);
            RectTransform card = TopLeft(_cardImage.gameObject);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);

            float y = CardPad;
            for (int r = 0; r < Rows; r++)
            {
                bool quote = r == QuoteRow;
                var row = new Row { Height = quote ? QuoteHeight : ShortHeight };

                row.Label = NewText("Label", go.transform, LabelSize, TextAlignmentOptions.BottomLeft);
                row.Label.overflowMode = TextOverflowModes.Ellipsis;
                Place(TopLeft(row.Label.gameObject), new Rect(CardPad + 10f, y, BoxWidth - 20f, LabelHeight - 6f));
                y += LabelHeight;
                row.Y = y;

                row.Box = NewImage("Box", go.transform, Color.white, raycast: false);
                Place(TopLeft(row.Box.gameObject), new Rect(CardPad, y, BoxWidth, row.Height));

                var viewport = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
                viewport.transform.SetParent(row.Box.transform, false);
                var viewRect = (RectTransform)viewport.transform;
                viewRect.anchorMin = Vector2.zero;
                viewRect.anchorMax = Vector2.one;
                float insetY = quote ? QuoteInsetY : ShortInsetY;
                viewRect.offsetMin = new Vector2(InsetX, insetY);
                viewRect.offsetMax = new Vector2(-InsetX, -insetY);

                // As on the card other players see (TemporaryPhoto): the short lines don't wrap and end
                // in "..." when too long; the quote wraps from the top.
                row.Text = NewText("Text", viewport.transform, CardFontSize,
                    quote ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
                Stretch(row.Text.rectTransform);
                row.Text.textWrappingMode = quote ? TextWrappingModes.Normal : TextWrappingModes.PreserveWhitespaceNoWrap;
                row.Text.overflowMode = quote ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;

                _rows[r] = row;
                y += row.Height + RowGap;
            }
        }

        private static Image NewImage(string name, Transform parent, Color colour, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            return text;
        }

        private static RectTransform TopLeft(GameObject go)
        {
            var t = (RectTransform)go.transform;
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0f, 1f);   // placed from the top-left
            return t;
        }

        private static void Stretch(RectTransform t)
        {
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero;
            t.offsetMax = Vector2.zero;
        }

        // A white box with rounded corners, sliced so the corners keep their size; tinted by the image.
        private static Sprite RoundedSprite()
        {
            const int size = 32;
            const float radius = 8f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // How far the pixel's centre is outside the rounded corner, smoothed over one pixel.
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var inner = new Vector2(Mathf.Clamp(p.x, radius, size - radius), Mathf.Clamp(p.y, radius, size - radius));
                    float alpha = Mathf.Clamp01(radius - Vector2.Distance(p, inner) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
