using System.Collections.Generic;
using UnityEngine;

namespace CardTable
{
    // The card pictures as textures, made once from CardPixels. Point filtered, so drawing them at a
    // whole number of screen pixels per art pixel keeps them sharp.
    internal sealed class CardArt
    {
        public readonly Texture2D[] Faces = new Texture2D[Cards.Count];
        public readonly Texture2D[] Suits = new Texture2D[4];
        public readonly Texture2D Back;
        public readonly Texture2D Slot;
        public readonly Texture2D AceSlot;
        public readonly Texture2D RecycleSlot;
        public readonly Texture2D Glow;
        public readonly Texture2D Table;
        // A plain white pixel, tinted when drawn (the shade behind the help card and the win box).
        public readonly Texture2D Pixel;

        // Go Fish: the table with its pond, finished books by rank (1-13), and the card count badge.
        public readonly Texture2D FishTable;
        public readonly Texture2D[] BookTiles = new Texture2D[Cards.King + 1];
        public readonly Texture2D Badge;

        private readonly List<Texture2D> _all = new List<Texture2D>();

        public CardArt(int tableWidth, int tableHeight, int rim)
        {
            for (int card = 0; card < Cards.Count; card++)
            {
                Faces[card] = Make(CardPixels.Face(card));
            }
            for (int suit = 0; suit < 4; suit++)
            {
                Suits[suit] = Make(CardPixels.Suit(suit));
            }
            Back = Make(CardPixels.Back());
            Slot = Make(CardPixels.Slot(CardPixels.SlotMark.None));
            AceSlot = Make(CardPixels.Slot(CardPixels.SlotMark.Ace));
            RecycleSlot = Make(CardPixels.Slot(CardPixels.SlotMark.Recycle));
            Glow = Make(CardPixels.Glow());
            Table = Make(CardPixels.Table(tableWidth, tableHeight, rim));
            var white = new Picture(1, 1);
            white[0, 0] = 0xFFFFFFFF;
            Pixel = Make(white);

            FishTable = Make(CardPixels.FishTable(tableWidth, tableHeight, rim,
                GoFishBoard.PondX, GoFishBoard.PondY, GoFishBoard.PondRX, GoFishBoard.PondRY));
            for (int rank = Cards.Ace; rank <= Cards.King; rank++)
            {
                BookTiles[rank] = Make(CardPixels.BookTile(rank));
            }
            Badge = Make(CardPixels.Badge(GoFishBoard.BadgeSize));
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
        }
    }
}
