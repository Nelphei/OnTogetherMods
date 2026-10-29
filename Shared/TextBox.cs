using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnTogetherMods.Shared
{
    // A dark box of text drawn with TextMeshPro, like the game's name tags, so player names keep
    // their colours and other styling (see PlayerNames). It looks like the boxes drawn with
    // PanelStack.MakeStyle, and it's placed in the same 1080p units.
    //
    // Use it like the immediate-mode GUI: from OnGUI, call Measure and Draw on every frame it
    // should show. Call LateUpdate from your LateUpdate: it hides the box once you stop drawing
    // it, and while Photo Mode takes a photo. The box doesn't catch clicks.
    //
    // Each box has its own screen canvas, named with PanelStack.CanvasPrefix so Photo Mode leaves
    // it alone when it hides the game's UI.
    internal sealed class TextBox
    {
        public const float FontSize = 18f;
        private static readonly RectOffset Padding = new RectOffset(12, 12, 10, 10);
        private static Sprite _boxSprite;
        private static bool _lookedForBox;

        private readonly string _name;
        private readonly TextAlignmentOptions _alignment;
        private GameObject _root;
        private RectTransform _box;
        private TextMeshProUGUI _text;
        private int _drawnFrame = -100;
        private string _measured;
        private TMP_FontAsset _measuredFont;
        private Vector2 _measuredSize;

        public TextBox(string name, TextAlignmentOptions alignment)
        {
            _name = name;
            _alignment = alignment;
        }

        // How big the box is for this text, padding included, in 1080p units. Call from OnGUI.
        public Vector2 Measure(string text)
        {
            Prepare();
            if (text != _measured || _text.font != _measuredFont)
            {
                Vector2 size = _text.GetPreferredValues(text);
                _measuredSize = new Vector2(Mathf.Ceil(size.x) + Padding.horizontal, Mathf.Ceil(size.y) + Padding.vertical);
                _measured = text;
                _measuredFont = _text.font;
            }
            return _measuredSize;
        }

        // Shows the box at `rect` (1080p units from the top-left, like inside PanelStack.Begin)
        // this frame. Call from OnGUI.
        public void Draw(Rect rect, string text)
        {
            Prepare();
            if (_text.text != text)
            {
                _text.text = text;
            }
            _box.anchoredPosition = new Vector2(rect.x, -rect.y);
            _box.sizeDelta = rect.size;
            _drawnFrame = Time.frameCount;
        }

        public void LateUpdate()
        {
            if (_root == null)
            {
                return;
            }
            bool shown = Time.frameCount - _drawnFrame <= 1 && !PanelStack.Hidden;
            if (_root.activeSelf != shown)
            {
                _root.SetActive(shown);
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        private void Prepare()
        {
            if (_root == null)
            {
                Build();
            }
            TMP_FontAsset font = PlayerNames.NameTagFont();
            if (font != null && _text.font != font)
            {
                _text.font = font;
            }
            // Turned on while being measured and drawn, so TextMeshPro measures it as it'll show.
            // LateUpdate turns it off again if Draw stops being called.
            if (!_root.activeSelf && !PanelStack.Hidden)
            {
                _root.SetActive(true);
            }
        }

        private void Build()
        {
            _root = new GameObject(PanelStack.CanvasPrefix + _name);
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

            var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(_root.transform, false);
            _box = (RectTransform)box.transform;
            _box.anchorMin = _box.anchorMax = _box.pivot = new Vector2(0f, 1f);   // placed from the top-left
            var background = box.GetComponent<Image>();
            background.raycastTarget = false;
            Sprite sprite = BoxSprite();
            if (sprite != null)
            {
                background.sprite = sprite;
                background.type = Image.Type.Sliced;
            }
            else
            {
                background.color = new Color(0f, 0f, 0f, 0.65f);
            }

            var label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            label.transform.SetParent(box.transform, false);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(Padding.left, Padding.bottom);
            rect.offsetMax = new Vector2(-Padding.right, -Padding.top);
            _text = label.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false;
            _text.richText = true;
            _text.fontSize = FontSize;
            _text.color = Color.white;
            _text.alignment = _alignment;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;

            _root.SetActive(false);
        }

        // The dark box behind PanelStack.MakeStyle text comes from Unity's built-in GUI skin, which
        // can only be read inside OnGUI (where Measure and Draw are called).
        private static Sprite BoxSprite()
        {
            if (!_lookedForBox)
            {
                _lookedForBox = true;
                GUIStyle box = GUI.skin.box;
                Texture2D texture = box.normal.background;
                if (texture != null)
                {
                    RectOffset border = box.border;
                    _boxSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                        new Vector4(border.left, border.bottom, border.right, border.top));
                }
            }
            return _boxSprite;
        }
    }
}
