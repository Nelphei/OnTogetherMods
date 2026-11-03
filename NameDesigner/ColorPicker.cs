using UnityEngine;

namespace NameDesigner
{
    // A colour picker for Unity's immediate-mode GUI: a square for how strong and how bright the
    // colour is, and a strip beside it for the hue. Drag in either one.
    internal sealed class ColorPicker
    {
        private const int SquareSize = 64;
        private const int StripSize = 128;

        // Hue, saturation and value, each 0 to 1.
        public float H;
        public float S;
        public float V = 1f;

        private Texture2D _square;
        private Texture2D _strip;
        private float _squareHue = -1f;
        private int _dragging;   // 0: not dragging, 1: the square, 2: the hue strip

        public Color Value => Color.HSVToRGB(H, S, V);

        public void Set(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            // Greys have no hue of their own, so keep the strip where it was.
            if (s > 0.001f && v > 0.001f)
            {
                H = h;
            }
            S = s;
            V = v;
        }

        // Draws the picker and handles the mouse. True when the user moved it during this event.
        public bool Draw(Rect square, Rect strip)
        {
            int id = GUIUtility.GetControlID(UnityEngine.FocusType.Passive);
            Event e = Event.current;
            bool changed = false;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0)
                    {
                        break;
                    }
                    if (square.Contains(e.mousePosition))
                    {
                        _dragging = 1;
                    }
                    else if (strip.Contains(e.mousePosition))
                    {
                        _dragging = 2;
                    }
                    else
                    {
                        break;
                    }
                    GUIUtility.hotControl = id;
                    GUIUtility.keyboardControl = 0;
                    changed = Move(e.mousePosition, square, strip);
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        changed = Move(e.mousePosition, square, strip);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        _dragging = 0;
                        e.Use();
                    }
                    break;

                case EventType.Repaint:
                    Paint(square, strip);
                    break;
            }
            return changed;
        }

        public void Destroy()
        {
            if (_square != null)
            {
                Object.Destroy(_square);
            }
            if (_strip != null)
            {
                Object.Destroy(_strip);
            }
        }

        private bool Move(Vector2 mouse, Rect square, Rect strip)
        {
            if (_dragging == 1)
            {
                S = Mathf.Clamp01((mouse.x - square.x) / square.width);
                V = 1f - Mathf.Clamp01((mouse.y - square.y) / square.height);
                return true;
            }
            if (_dragging == 2)
            {
                // Red at both ends, so the very top and bottom are the same colour.
                H = Mathf.Clamp01(1f - (mouse.y - strip.y) / strip.height);
                return true;
            }
            return false;
        }

        private void Paint(Rect square, Rect strip)
        {
            if (_strip == null)
            {
                _strip = NewTexture(1, StripSize);
                var pixels = new Color32[StripSize];
                for (int y = 0; y < StripSize; y++)
                {
                    pixels[y] = Color.HSVToRGB(y / (StripSize - 1f), 1f, 1f);
                }
                _strip.SetPixels32(pixels);
                _strip.Apply();
            }
            if (_square == null)
            {
                _square = NewTexture(SquareSize, SquareSize);
            }
            if (!Mathf.Approximately(_squareHue, H))
            {
                // Textures start from the bottom row, so brightness goes up the square.
                var pixels = new Color32[SquareSize * SquareSize];
                for (int y = 0; y < SquareSize; y++)
                {
                    for (int x = 0; x < SquareSize; x++)
                    {
                        pixels[y * SquareSize + x] = Color.HSVToRGB(H, x / (SquareSize - 1f), y / (SquareSize - 1f));
                    }
                }
                _square.SetPixels32(pixels);
                _square.Apply();
                _squareHue = H;
            }

            GUI.DrawTexture(square, _square);
            GUI.DrawTexture(strip, _strip);

            var point = new Vector2(square.x + S * square.width, square.y + (1f - V) * square.height);
            Outline(new Rect(point.x - 5f, point.y - 5f, 10f, 10f), Color.black);
            Outline(new Rect(point.x - 4f, point.y - 4f, 8f, 8f), Color.white);
            float hueY = strip.y + (1f - H) * strip.height;
            Outline(new Rect(strip.x - 3f, hueY - 3f, strip.width + 6f, 6f), Color.black);
            Outline(new Rect(strip.x - 2f, hueY - 2f, strip.width + 4f, 4f), Color.white);
        }

        // A one-unit frame around `rect`.
        public static void Outline(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            Texture2D white = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), white);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), white);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 1f, rect.height), white);
            GUI.DrawTexture(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), white);
            GUI.color = previous;
        }

        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}
