using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeskSnap
{
    // The short message at the top of the screen. It's drawn with TextMeshPro, like the game's name
    // tags, so a name with colours, sizes or other rich text looks the way it does above the player.
    //
    // It's on its own screen canvas, which Photo Mode switches off with the rest of the UI.
    internal sealed class TopNotice
    {
        private const float Top = 140f;   // in 1080p units: below Photo Mode's (40) and Emote Sync's (90) messages
        private const float Seconds = 3f;
        private const float FontSize = 18f;

        // Rich text tags that stay on until closed. A name that leaves one open would carry it on
        // into the rest of the message, so Name() closes them.
        private static readonly HashSet<string> Closable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "color", "b", "i", "u", "s", "size", "font", "font-weight", "material", "mark", "gradient",
            "cspace", "mspace", "voffset", "alpha", "indent", "line-height", "line-indent", "align",
            "rotate", "lowercase", "uppercase", "allcaps", "smallcaps", "sup", "sub", "width", "margin",
            "nobr", "link", "style", "noparse",
        };
        private static readonly Regex Tag = new Regex(@"<(/?)(#|[A-Za-z][\w-]*)[^<>]*>");

        private GameObject _root;
        private Image _background;
        private TextMeshProUGUI _text;
        private Sprite _boxSprite;
        private bool _lookedForBox;
        private float _until;

        // A player's name, ready to go inside a message: any tags it leaves open are closed after it.
        public static string Name(string name)
        {
            var open = new List<string>();
            bool noparse = false;
            foreach (Match match in Tag.Matches(name))
            {
                string tag = match.Groups[2].Value == "#" ? "color" : match.Groups[2].Value.ToLowerInvariant();
                bool closing = match.Groups[1].Value == "/";
                if (!Closable.Contains(tag) || (noparse && !(closing && tag == "noparse")))
                {
                    continue;   // not a tag that needs closing, or shown as plain text inside <noparse>
                }
                if (closing)
                {
                    int i = open.LastIndexOf(tag);
                    if (i >= 0)
                    {
                        open.RemoveAt(i);
                    }
                }
                else
                {
                    open.Add(tag);
                }
                noparse = open.Contains("noparse");
            }

            var text = new StringBuilder(name);
            for (int i = open.Count - 1; i >= 0; i--)
            {
                text.Append("</").Append(open[i]).Append('>');
            }
            return text.ToString();
        }

        // Shows the message for a few seconds. `font` is the name tags' font (null keeps the last one).
        public void Show(string message, TMP_FontAsset font)
        {
            if (_root == null)
            {
                Build();
            }
            if (font != null && _text.font != font)
            {
                _text.font = font;
            }
            _text.text = message;
            _until = Time.unscaledTime + Seconds;
            Update();
        }

        // Call every frame: hides the message when its time is up, or while Photo Mode takes a photo.
        public void Update()
        {
            if (_root == null)
            {
                return;
            }
            bool shown = Time.unscaledTime < _until && !PanelStack.Hidden;
            if (_root.activeSelf != shown)
            {
                _root.SetActive(shown);
            }
        }

        // Call from OnGUI. The dark box the other mods' messages use belongs to Unity's built-in GUI
        // skin, which can only be read inside OnGUI. Copying it keeps every message looking the same.
        public void CopyBoxLook()
        {
            if (_lookedForBox)
            {
                return;
            }
            _lookedForBox = true;
            GUIStyle box = GUI.skin.box;
            Texture2D texture = box.normal.background;
            if (texture == null)
            {
                return;
            }
            RectOffset border = box.border;
            _boxSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(border.left, border.bottom, border.right, border.top));
            ApplyBoxLook();
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        private void Build()
        {
            _root = new GameObject("DeskSnap Notice");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            // Laid out in 1080p units, scaled to the real screen height, like the other mods' windows.
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // The box grows to fit the text, centred at the top of the screen.
            var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(_root.transform, false);
            var rect = (RectTransform)box.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -Top);
            _background = box.GetComponent<Image>();
            _background.raycastTarget = false;
            ApplyBoxLook();
            var layout = box.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = box.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
            label.transform.SetParent(box.transform, false);
            _text = label.AddComponent<TextMeshProUGUI>();
            _text.raycastTarget = false;
            _text.richText = true;
            _text.fontSize = FontSize;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.overflowMode = TextOverflowModes.Overflow;

            _root.SetActive(false);
        }

        private void ApplyBoxLook()
        {
            if (_background == null)
            {
                return;
            }
            if (_boxSprite != null)
            {
                _background.sprite = _boxSprite;
                _background.type = Image.Type.Sliced;
                _background.color = Color.white;
            }
            else
            {
                _background.color = new Color(0f, 0f, 0f, 0.65f);
            }
        }
    }
}
