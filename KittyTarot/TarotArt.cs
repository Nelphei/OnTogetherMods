using System.Collections.Generic;
using UnityEngine;

namespace KittyTarot
{
    // The pictures as textures. A card's face is made the first time it shows (drawing all 78 at once
    // would stall a frame), the other way up only when it lies reversed. Point filtered, so drawing at a
    // whole number of screen pixels per art pixel keeps them sharp.
    internal sealed class TarotArt
    {
        private readonly Texture2D[] _faces = new Texture2D[Deck.Count];
        private readonly Texture2D[] _turned = new Texture2D[Deck.Count];
        private readonly Dictionary<string, Texture2D> _texts = new Dictionary<string, Texture2D>();
        private readonly List<Texture2D> _all = new List<Texture2D>();

        public readonly Texture2D Back;
        public readonly Texture2D Glow;
        public readonly Texture2D PileEdge;
        // A plain white pixel, tinted when drawn (the shade behind the boxes over the table).
        public readonly Texture2D Pixel;

        private Texture2D _table;
        private TableLayout _tableLayout;

        public TarotArt()
        {
            Back = Make(CardFaces.Back());
            Glow = Make(TableArt.Glow());
            PileEdge = Make(TableArt.PileEdge());
            var white = new Picture(1, 1);
            white[0, 0] = 0xFFFFFFFF;
            Pixel = Make(white);
        }

        public Texture2D Face(int card, bool upsideDown)
        {
            if (_faces[card] == null)
            {
                _faces[card] = Make(CardFaces.Face(card));
            }
            if (!upsideDown)
            {
                return _faces[card];
            }
            if (_turned[card] == null)
            {
                _turned[card] = Make(CardFaces.Face(card).Turned());
            }
            return _turned[card];
        }

        public Texture2D Table(TableLayout layout)
        {
            if (_table == null || _tableLayout != layout)
            {
                if (_table != null)
                {
                    _all.Remove(_table);
                    Object.Destroy(_table);
                }
                _table = Make(TableArt.Cloth(layout.Width, layout.Height, layout.Slots));
                _tableLayout = layout;
            }
            return _table;
        }

        // A line of text in the cards' pixel font.
        public Texture2D Text(string text, uint colour)
        {
            string key = colour.ToString("X8") + text;
            if (!_texts.TryGetValue(key, out Texture2D texture))
            {
                texture = Make(TarotFont.Render(text, colour));
                _texts[key] = texture;
            }
            return texture;
        }

        private Texture2D Make(Picture picture)
        {
            var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[picture.Width * picture.Height];
            for (int y = 0; y < picture.Height; y++)
            {
                for (int x = 0; x < picture.Width; x++)
                {
                    uint c = picture.Pixels[y * picture.Width + x];
                    // Textures count rows from the bottom.
                    pixels[(picture.Height - 1 - y) * picture.Width + x] =
                        new Color32((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _all.Add(texture);
            return texture;
        }

        public void Destroy()
        {
            foreach (Texture2D texture in _all)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
            _all.Clear();
            _texts.Clear();
        }
    }
}
