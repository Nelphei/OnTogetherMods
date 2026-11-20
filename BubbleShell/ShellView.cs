using System.Collections.Generic;
using HarmonyLib;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace BubbleShell
{
    // What the console looks like: a dark see-through panel hanging from the top of the screen, soft
    // teal text, the prompt with its blinking block cursor, a teal edge at the bottom, and little
    // bubbles (aqua, pink, lilac) drifting up behind it all. Drawn with TextMeshPro on its own canvas
    // (player names in the output keep their colours), laid out in screen pixels so the pixel font
    // lands on whole pixels.
    //
    // The canvas is named with PanelStack.CanvasPrefix, so Photo Mode leaves it alone; the window hides
    // it while PanelStack.Hidden is set.
    internal sealed class ShellView
    {
        // TextMeshPro paints letters from a colour-glyph font white whatever their colour tags say
        // (it's meant for emoji; see TMP_Text.SaveGlyphVertexInfo), and the crisp pixel font is one.
        // It still notes each letter's colour (TMP_CharacterInfo.color), so after it lays the text out,
        // and before the mesh goes to the screen (OnPreRenderText), the letters get their colours back.
        private static readonly AccessTools.FieldRef<TMP_Text, bool> LinearColors =
            AccessTools.FieldRefAccess<TMP_Text, bool>("m_ConvertToLinearSpace");

        private const GlyphRenderMode ColorGlyphs = (GlyphRenderMode)65536;
        private const int BubbleCount = 14;

        // The bubble picture: L = ring, W = shine.
        private static readonly string[] BubblePixels =
        {
            "..LLLL..",
            ".L....L.",
            "L.W....L",
            "L.W....L",
            "L......L",
            "L......L",
            ".L....L.",
            "..LLLL..",
        };

        private static readonly Color PanelColor = new Color(0.035f, 0.075f, 0.085f);
        private static readonly Color EdgeColor = new Color(0.56f, 0.89f, 0.85f, 0.8f);
        private static readonly Color CaretColor = new Color(0.56f, 0.89f, 0.85f, 0.9f);
        private static readonly Color FpsBack = new Color(0.035f, 0.075f, 0.085f, 0.7f);

        // Each bubble gets one of these, like the shimmer on the icon's bubble.
        private static readonly Color[] BubbleColors =
        {
            new Color(0.72f, 0.95f, 0.93f),  // aqua
            new Color(1f, 0.78f, 0.87f),     // pink
            new Color(0.85f, 0.79f, 1f),     // lilac
        };

        private readonly GameObject _root;
        private readonly RectTransform _panel;
        private readonly Image _panelImage;
        private readonly RectTransform _edge;
        private readonly RectTransform _bubbleLayer;
        private readonly RectTransform _outputArea;
        private readonly TextMeshProUGUI _output;
        private readonly TextMeshProUGUI _input;
        private readonly TextMeshProUGUI _title;
        private readonly RectTransform _fpsBox;
        private readonly TextMeshProUGUI _fps;
        private readonly RectTransform _caret;
        private readonly Image _caretImage;
        private readonly List<Bubble> _bubbles = new List<Bubble>();
        private readonly Texture2D _bubbleTexture;
        private readonly Sprite _bubbleSprite;

        private TMP_FontAsset _font;
        private int _scale = -1;
        private float _panelHeight = -1f;
        private string _outputText;
        private string _inputText;
        private string _titleText;
        private string _fpsText;

        private sealed class Bubble
        {
            public RectTransform Rect;
            public Image Image;
            public float X;
            public float Y;
            public float Speed;
            public float Phase;
            public int Size;
        }

        public ShellView()
        {
            _root = new GameObject(PanelStack.CanvasPrefix + "BubbleShell");
            Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            // No CanvasScaler: one canvas unit is one screen pixel.

            _panel = NewRect("Panel", _root.transform);
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panelImage = _panel.gameObject.AddComponent<Image>();
            _panelImage.raycastTarget = false;
            _panel.gameObject.AddComponent<RectMask2D>();

            _bubbleTexture = MakeBubbleTexture();
            _bubbleSprite = Sprite.Create(_bubbleTexture, new Rect(0f, 0f, _bubbleTexture.width, _bubbleTexture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _bubbleLayer = NewRect("Bubbles", _panel);
            Stretch(_bubbleLayer);
            for (int i = 0; i < BubbleCount; i++)
            {
                RectTransform rect = NewRect("Bubble", _bubbleLayer);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = _bubbleSprite;
                image.raycastTarget = false;
                _bubbles.Add(new Bubble { Rect = rect, Image = image, Y = -1f });
            }

            // The output has its own clipping area: older lines run off its top, not under the title.
            _outputArea = NewRect("OutputArea", _panel);
            _outputArea.gameObject.AddComponent<RectMask2D>();
            _output = NewText("Output", _outputArea);
            _output.alignment = TextAlignmentOptions.BottomLeft;
            _output.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_output.rectTransform);

            _title = NewText("Title", _panel);
            _title.alignment = TextAlignmentOptions.TopRight;
            _title.textWrappingMode = TextWrappingModes.NoWrap;

            _input = NewText("Input", _panel);
            _input.alignment = TextAlignmentOptions.Left;
            _input.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform inputRect = _input.rectTransform;
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0f, 0.5f);

            _caret = NewRect("Caret", inputRect);
            _caret.anchorMin = _caret.anchorMax = new Vector2(0f, 0.5f);
            _caret.pivot = new Vector2(0f, 0.5f);
            _caretImage = _caret.gameObject.AddComponent<Image>();
            _caretImage.color = CaretColor;
            _caretImage.raycastTarget = false;

            _edge = NewRect("Edge", _panel);
            _edge.anchorMin = new Vector2(0f, 0f);
            _edge.anchorMax = new Vector2(1f, 0f);
            _edge.pivot = new Vector2(0.5f, 0f);
            var edge = _edge.gameObject.AddComponent<Image>();
            edge.color = EdgeColor;
            edge.raycastTarget = false;

            _fpsBox = NewRect("FpsBox", _root.transform);
            _fpsBox.anchorMin = _fpsBox.anchorMax = _fpsBox.pivot = new Vector2(0f, 1f);
            var fpsBack = _fpsBox.gameObject.AddComponent<Image>();
            fpsBack.color = FpsBack;
            fpsBack.raycastTarget = false;
            _fps = NewText("Fps", _fpsBox);
            _fps.alignment = TextAlignmentOptions.Center;
            _fps.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(_fps.rectTransform);

            _root.SetActive(false);
        }

        // Screen pixels per font pixel.
        public int Scale => Mathf.Max(1, _scale);
        public float CharWidth => ShellFont.IsPixelFont ? ShellFont.Advance * Scale : FontSize * 0.55f;
        public float LineHeight => ShellFont.IsPixelFont ? 14f * Scale : FontSize * 1.25f;
        private float FontSize => ShellFont.IsPixelFont ? ShellFont.PixelsPerEm * Scale : 9f * Scale;
        private float Margin => 6f * Scale;

        // How many letters fit across the output.
        public int Columns => Mathf.Max(10, Mathf.FloorToInt((Screen.width - 2f * Margin) / CharWidth));

        // How many lines fit in the output when fully open.
        public int Rows => Mathf.Max(1, Mathf.FloorToInt((_panelHeight - LineHeight * 2f - 4f * Margin) / LineHeight));

        // The part of the screen the console covers now (pixels from the top-left, like OnGUI).
        public Rect ScreenRect { get; private set; }

        public void Hide()
        {
            if (_root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        // Lays everything out for this frame. slide: 0 = tucked away above the screen, 1 = all the way down.
        public void Show(float slide, float heightPart, float opacity, int scale, bool bubbles, float fps)
        {
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }

            TMP_FontAsset font = ShellFont.Get();
            bool restyle = font != _font || scale != _scale;
            _font = font;
            _scale = scale;
            float height = Mathf.Round(Screen.height * heightPart);
            if (restyle || !Mathf.Approximately(height, _panelHeight))
            {
                _panelHeight = height;
                Restyle();
            }

            bool shown = slide > 0.001f;
            _panel.gameObject.SetActive(shown);
            float offset = Mathf.Round((1f - slide) * height);
            _panel.anchoredPosition = new Vector2(0f, offset);
            _panel.sizeDelta = new Vector2(0f, height);
            _panelImage.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, opacity);
            ScreenRect = shown ? new Rect(0f, 0f, Screen.width, height - offset) : Rect.zero;

            _bubbleLayer.gameObject.SetActive(bubbles && shown);
            if (bubbles && shown)
            {
                MoveBubbles(height);
            }

            string fpsText = fps > 0f ? Shell.Colored(Shell.TextColor, $"{fps:0} fps") : null;
            if (fpsText != _fpsText)
            {
                _fpsText = fpsText;
                _fpsBox.gameObject.SetActive(fpsText != null);
                if (fpsText != null)
                {
                    _fps.text = fpsText;
                }
            }
        }

        // Only the fps counter (console closed).
        public void ShowFpsOnly(int scale, float fps)
        {
            Show(0f, Plugin.Settings.Height.Value, 0f, scale, false, fps);
        }

        public void SetOutput(string text)
        {
            if (text != _outputText)
            {
                _outputText = text;
                _output.text = text;
            }
        }

        public void SetTitle(string text)
        {
            if (text != _titleText)
            {
                _titleText = text;
                _title.text = text;
            }
        }

        // The input line (rich text) and where the cursor is in it: `caret` letters from the start,
        // counting the prompt. A hidden cursor still takes its place.
        public void SetInput(string text, int caret, bool caretOn)
        {
            if (text != _inputText)
            {
                _inputText = text;
                _input.text = text;
                _input.ForceMeshUpdate();
            }
            TMP_TextInfo info = _input.textInfo;
            float x = 0f;
            if (info != null && info.characterCount > 0)
            {
                x = caret < info.characterCount ? info.characterInfo[caret].origin : info.characterInfo[info.characterCount - 1].xAdvance;
            }
            _caret.anchoredPosition = new Vector2(Mathf.Round(x), 0f);
            _caretImage.enabled = caretOn;
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
            if (_bubbleSprite != null)
            {
                Object.Destroy(_bubbleSprite);
            }
            if (_bubbleTexture != null)
            {
                Object.Destroy(_bubbleTexture);
            }
        }

        // ---- Layout ----

        private void Restyle()
        {
            float u = Scale;
            float line = LineHeight;
            foreach (TextMeshProUGUI text in new[] { _output, _input, _title, _fps })
            {
                if (_font != null)
                {
                    text.font = _font;
                }
                text.fontSize = FontSize;
                // Monogram's lines are 12.66 font pixels; make them 14 so rows land on whole pixels.
                text.lineSpacing = ShellFont.IsPixelFont && _font != null
                    ? (14f - _font.faceInfo.lineHeight * ShellFont.PixelsPerEm / Mathf.Max(1f, _font.faceInfo.pointSize)) * 100f / ShellFont.PixelsPerEm
                    : 0f;
            }

            Stretch(_outputArea);
            _outputArea.offsetMin = new Vector2(Margin, line + 3f * Margin);
            _outputArea.offsetMax = new Vector2(-Margin, -(line + Margin));

            RectTransform title = _title.rectTransform;
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -Margin * 0.5f);
            title.sizeDelta = new Vector2(-2f * Margin, line);

            RectTransform input = _input.rectTransform;
            input.anchoredPosition = new Vector2(Margin, Margin + line * 0.5f + u);
            input.sizeDelta = new Vector2(-2f * Margin, line);
            _caret.sizeDelta = new Vector2(CharWidth, line - 2f * u);

            _edge.sizeDelta = new Vector2(0f, u);

            _fpsBox.anchoredPosition = new Vector2(Margin, -Margin * 0.5f);
            _fpsBox.sizeDelta = new Vector2(9f * CharWidth, line + 2f * u);

            // Text changed size: measure the input again.
            _inputText = null;
        }

        private void MoveBubbles(float height)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float width = Screen.width;
            foreach (Bubble bubble in _bubbles)
            {
                if (bubble.Y < 0f || bubble.Y > height + 16f * Scale)
                {
                    // Start again from the bottom, somewhere along it.
                    bubble.Size = Random.value < 0.3f ? 2 : 1;
                    bubble.X = Random.Range(0f, width);
                    bubble.Y = bubble.Y < 0f && bubble.Speed == 0f ? Random.Range(0f, height) : 0.01f;
                    bubble.Speed = Random.Range(12f, 30f) * Scale / 2f * (bubble.Size == 2 ? 0.8f : 1f);
                    bubble.Phase = Random.Range(0f, 6.28f);
                    Color tint = BubbleColors[Random.Range(0, BubbleColors.Length)];
                    bubble.Image.color = new Color(tint.r, tint.g, tint.b, Random.Range(0.14f, 0.3f));
                    float side = 8f * Scale * bubble.Size;
                    bubble.Rect.sizeDelta = new Vector2(side, side);
                }
                bubble.Y += bubble.Speed * dt;
                float sway = Mathf.Sin(Time.unscaledTime * 1.3f + bubble.Phase) * 3f * Scale;
                // Whole font pixels, like the text.
                float step = Scale;
                bubble.Rect.anchoredPosition = new Vector2(Mathf.Round((bubble.X + sway) / step) * step,
                    Mathf.Round((bubble.Y - 8f * Scale) / step) * step);
            }
        }

        // ---- Building ----

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Gives letters from a colour-glyph font their colours back (see LinearColors above). Letters
        // from other fonts (the name tags' font, for other alphabets) already have theirs.
        private static void Recolor(TMP_TextInfo info)
        {
            if (info == null || info.characterInfo == null || info.meshInfo == null)
            {
                return;
            }
            bool linear = info.textComponent != null && LinearColors(info.textComponent);
            for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
            {
                TMP_CharacterInfo letter = info.characterInfo[i];
                if (!letter.isVisible || letter.fontAsset == null || (letter.fontAsset.atlasRenderMode & ColorGlyphs) == 0)
                {
                    continue;
                }
                int mesh = letter.materialReferenceIndex;
                if (mesh < 0 || mesh >= info.meshInfo.Length)
                {
                    continue;
                }
                Color32[] colors = info.meshInfo[mesh].colors32;
                int v = letter.vertexIndex;
                if (colors == null || v < 0 || v + 3 >= colors.Length)
                {
                    continue;
                }
                Color32 color = letter.color;
                if (linear)
                {
                    color = ((Color)color).linear;
                }
                // Keep the see-through-ness TextMeshPro worked out (the text's own alpha, fading).
                color.a = colors[v].a;
                colors[v] = color;
                colors[v + 1] = color;
                colors[v + 2] = color;
                colors[v + 3] = color;
            }
        }

        private static TextMeshProUGUI NewText(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.richText = true;
            text.color = Color.white;
            text.OnPreRenderText += Recolor;
            text.overflowMode = TextOverflowModes.Overflow;
            text.extraPadding = false;
            return text;
        }

        private static Texture2D MakeBubbleTexture()
        {
            int size = BubblePixels.Length;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                string row = BubblePixels[size - 1 - y];   // textures start at the bottom
                for (int x = 0; x < size; x++)
                {
                    char c = row[x];
                    pixels[y * size + x] = c == 'L' ? new Color32(255, 255, 255, 255)
                        : c == 'W' ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
