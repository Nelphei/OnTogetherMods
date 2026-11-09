using System.Collections.Generic;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeskSwarm
{
    // The window's background and its player names, drawn with TextMeshPro like the game's name tags
    // so names keep their colours and other styling. Unity's immediate-mode GUI (the buttons and
    // other text) always draws on top of this, so the window's box is drawn here, under the names,
    // and not with GUI.Box.
    //
    // Use it like Shared/TextBox.cs: from OnGUI, call Back and Name on every frame they should show
    // (1080p units from the top-left, inside PanelStack.Begin). LateUpdate hides what wasn't drawn,
    // and everything while Photo Mode takes a photo or the game's eye button hides the UI.
    internal sealed class WindowCanvas
    {
        private const float FontSize = 17f;

        private readonly string _name;
        private GameObject _root;
        private RectTransform _back;
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();
        private readonly List<int> _labelFrames = new List<int>();
        private int _backFrame = -100;
        private int _next;
        private Sprite _boxSprite;
        private bool _lookedForBox;

        public WindowCanvas(string name)
        {
            _name = name;
        }

        // The window's dark box. Call first in each OnGUI pass.
        public void Back(Rect rect)
        {
            Prepare();
            // OnGUI runs several times a frame, and each pass draws the same names in the same order.
            _next = 0;
            Place(_back, rect);
            _backFrame = Time.frameCount;
            if (!_lookedForBox)
            {
                _lookedForBox = true;
                Image image = _back.GetComponent<Image>();
                Sprite sprite = BoxSprite();
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Sliced;
                    image.color = Color.white;
                }
            }
        }

        // One line of text with player names in it, cut short with "..." if it's too wide.
        public void Name(Rect rect, string text, Color colour)
        {
            Prepare();
            if (_next >= _labels.Count)
            {
                _labels.Add(NewLabel());
                _labelFrames.Add(-100);
            }
            TextMeshProUGUI label = _labels[_next];
            _labelFrames[_next] = Time.frameCount;
            _next++;

            if (label.text != text)
            {
                label.text = text;
            }
            label.color = colour;
            TMP_FontAsset font = PlayerNames.NameTagFont();
            if (font != null && label.font != font)
            {
                label.font = font;
            }
            Place(label.rectTransform, rect);
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
                _root.SetActive(shown);
            }
            for (int i = 0; i < _labels.Count; i++)
            {
                bool on = shown && frame - _labelFrames[i] <= 1;
                if (_labels[i].gameObject.activeSelf != on)
                {
                    _labels[i].gameObject.SetActive(on);
                }
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        private static void Place(RectTransform t, Rect rect)
        {
            t.anchoredPosition = new Vector2(rect.x, -rect.y);
            t.sizeDelta = rect.size;
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

        private void Build()
        {
            // Named so Photo Mode leaves it alone when it hides the game's UI.
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

            var back = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            back.transform.SetParent(_root.transform, false);
            _back = TopLeft(back);
            var image = back.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(0f, 0f, 0f, 0.65f);

            _root.SetActive(false);
        }

        // Later children draw on top, so names are always over the box.
        private TextMeshProUGUI NewLabel()
        {
            var go = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(_root.transform, false);
            TopLeft(go);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform TopLeft(GameObject go)
        {
            var t = (RectTransform)go.transform;
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0f, 1f);   // placed from the top-left
            return t;
        }

        // The same dark box as PanelStack.MakeStyle, from Unity's built-in GUI skin, which can only be
        // read inside OnGUI.
        private Sprite BoxSprite()
        {
            if (_boxSprite == null)
            {
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
