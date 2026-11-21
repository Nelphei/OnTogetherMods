using System;
using System.Collections.Generic;

namespace KittyTarot
{
    // The cards' pictures, each drawn for the window on the card (CardFaces.PictureWidth x PictureHeight).
    // Cards not in Drawn show CardFaces' stand-in until theirs is drawn. The pictures themselves are in
    // the other Scenes files (Majors.cs, ...); this one has the list and the drawing helpers they share.
    internal static partial class Scenes
    {
        private const int W = CardFaces.PictureWidth;
        private const int H = CardFaces.PictureHeight;

        private static readonly Dictionary<int, Func<Picture>> Drawn = new Dictionary<int, Func<Picture>>
        {
            [0] = Fool,
            [1] = Magician,
            [2] = HighPriestess,
            [3] = Empress,
            [4] = Emperor,
            [5] = Hierophant,
            [6] = Lovers,
            [7] = Chariot,
            [8] = Strength,
            [9] = Hermit,
            [10] = Wheel,
            [11] = Justice,
            [12] = HangedMan,
            [13] = Death,
            [14] = Temperance,
            [15] = Devil,
            [16] = Tower,
            [17] = Star,
            [18] = Moon,
            [19] = Sun,
            [20] = Judgement,
            [21] = World,
        };

        public static bool Has(int card) => Drawn.ContainsKey(card);

        public static Picture Of(int card) => Drawn.TryGetValue(card, out Func<Picture> draw) ? draw() : null;

        // ---- Shapes, in the picture's coordinates (pixel centres, as in Mask) ----

        private static Mask Ellipse(float left, float top, float right, float bottom) => Mask.Ellipse(W, H, left, top, right, bottom);

        private static Mask Circle(float cx, float cy, float radius) => Mask.Circle(W, H, cx, cy, radius);

        private static Mask Rect(int left, int top, int right, int bottom) => Mask.Rect(W, H, left, top, right, bottom);

        private static Mask Poly(params (float x, float y)[] points) => Mask.Polygon(W, H, points);

        private static Mask Line(float thickness, params (float x, float y)[] points) => Mask.Line(W, H, thickness, points);

        private static Mask Where(Func<int, int, bool> inside) => Mask.Of(W, H, inside);

        // ---- Colours ----

        // A sprite's letters and their colours, e.g. Key(('k', Palette.Ink), ('f', Palette.White)).
        // Letters not listed are skipped.
        private static Func<char, uint> Key(params (char letter, uint colour)[] pairs)
        {
            return letter =>
            {
                foreach ((char l, uint c) in pairs)
                {
                    if (l == letter)
                    {
                        return c;
                    }
                }
                return 0u;
            };
        }

        // ---- Bits used on many cards ----

        private static void Sparkles(Picture p, uint colour, params (int x, int y)[] at)
        {
            foreach ((int x, int y) in at)
            {
                p.Stamp(Sprites.Spark3, x, y, colour);
            }
        }

        private static void Hearts(Picture p, uint colour, params (int x, int y)[] at)
        {
            foreach ((int x, int y) in at)
            {
                p.Stamp(Sprites.Heart5, x, y, colour);
            }
        }

        private static void GroundBits(Picture p, (int x, int y)[] tufts, (int x, int y)[] flowers)
        {
            foreach ((int x, int y) in tufts)
            {
                p.Stamp(Sprites.Tuft, x, y, Palette.Mint3);
            }
            foreach ((int x, int y) in flowers)
            {
                p.Stamp(Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Butter3 : Palette.Pink3);
            }
        }
    }
}
