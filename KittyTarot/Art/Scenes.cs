using System;

namespace KittyTarot
{
    // The cards' pictures, each drawn for the window on the card (CardFaces.PictureWidth x PictureHeight).
    // Cards not listed in Of() show CardFaces' stand-in until theirs is drawn.
    internal static class Scenes
    {
        private const int W = CardFaces.PictureWidth;
        private const int H = CardFaces.PictureHeight;

        // The first two pictures were sketched for a window 6 pixels smaller each way; everything in
        // them moves by this much.
        private const int Dx = 3;
        private const int Dy = 3;

        public static bool Has(int card) => card == 8 || card == 17;

        public static Picture Of(int card)
        {
            switch (card)
            {
                case 8: return Strength();
                case 17: return Star();
                default: return null;
            }
        }

        // ---- VIII Strength: a kitten with a flower crown sitting on a big, gentle, sleepy lion ----

        private static Picture Strength()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav1), (0.30f, Palette.Pink1), (0.55f, Palette.Peach1), (0.70f, Palette.Butter1), (1f, Palette.Butter1));
            Mask hills = Ellipse(-30, 40, 22, 72).Or(Ellipse(20, 42, 74, 76));
            p.Fill(hills, Palette.Lav2);
            p.Fill(hills.Edge(true), Palette.Lav3);
            p.Fill(Mask.Of(W, H, (x, y) => y >= 49 + Dy), Palette.Mint2);
            p.Run(0, 49 + Dy, W, Palette.Mint3);
            GroundBits(p, new[] { (3, 55), (9, 61), (38, 58), (31, 63), (18, 63), (41, 52) }, new[] { (5, 59), (36, 61), (40, 56) });

            // The lion's body lying behind, and its tail with a heart for a tuft.
            Mask body = Ellipse(25, 41, 50, 60);
            p.Blob(body, Palette.Peach2);
            p.Fill(body.Minus(body.Edge(true)).Rows(y => y > 55 + Dy), Palette.Peach3);
            Mask tail = Mask.Line(W, H, 2f, (40 + Dx, 44 + Dy), (41 + Dx, 40 + Dy), (40 + Dx, 37 + Dy)).Minus(body);
            p.Blob(tail, Palette.Peach3);
            Stamp(p, Sprites.Heart7, 36, 31, Sprites.Two(Palette.Pink3));

            // The mane: a scalloped ring, shaded underneath.
            Mask mane = Ellipse(11, 29, 33, 51);
            for (int k = 0; k < 12; k++)
            {
                double angle = k * Math.PI / 6;
                float x = 22 + 11 * (float)Math.Cos(angle), y = 40 + 11 * (float)Math.Sin(angle);
                mane = mane.Or(Ellipse(x - 3, y - 3, x + 3, y + 3));
            }
            p.Blob(mane, Palette.Peach2);
            p.Fill(mane.Minus(mane.Erode().Erode().Erode()).Minus(mane.Edge()).Rows(y => y > 43 + Dy), Palette.Peach3);

            // Ears, face and muzzle.
            foreach (int ex in new[] { 16, 28 })
            {
                p.Blob(Ellipse(ex - 3, 30, ex + 3, 36), Palette.Butter2);
                p.Fill(Ellipse(ex - 1, 32, ex + 1, 34), Palette.Pink2);
            }
            p.Blob(Ellipse(15, 33, 29, 47), Palette.Butter2);
            p.Fill(Ellipse(18, 40, 26, 46), Palette.Cream);
            Dots(p, Palette.Ink, (17, 38), (19, 38), (18, 39), (25, 38), (27, 38), (26, 39));   // sleepy eyes
            Dots(p, Palette.Pink4, (21, 41), (22, 41), (23, 41), (22, 42));                     // nose
            Dots(p, Palette.Ink, (20, 43), (22, 43), (24, 43), (21, 44), (23, 44));             // mouth
            Dots(p, Palette.Pink2, (16, 41), (17, 41), (27, 41), (28, 41));                     // blush

            foreach (int px in new[] { 11, 25 })
            {
                p.Blob(Ellipse(px, 48, px + 8, 54), Palette.Butter2);
                Dots(p, Palette.Ink, (px + 3, 52), (px + 3, 53), (px + 5, 52), (px + 5, 53));
            }

            Stamp(p, Sprites.Kitten, 16, 15, Sprites.KittenColour);
            Stamp(p, Sprites.Infinity, 18, 7, _ => Palette.Lav4);
            foreach ((int x, int y) in new[] { (5, 9), (36, 6), (38, 20), (3, 27) })
            {
                Stamp(p, Sprites.Spark3, x, y, _ => Palette.White);
            }
            foreach ((int x, int y) in new[] { (6, 18), (33, 13) })
            {
                Stamp(p, Sprites.Heart5, x, y, _ => Palette.Pink3);
            }
            return p;
        }

        // ---- XVII The Star: a cat with a bow on its tail gazing up at the star by a pond ----

        private static Picture Star()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav4), (0.28f, Palette.Lav3), (0.52f, Palette.Lav2), (0.72f, Palette.Pink2), (1f, Palette.Pink2));
            foreach ((int x, int y) in new[] { (3, 4), (36, 3), (2, 22), (38, 24), (7, 34), (33, 36) })
            {
                Stamp(p, Sprites.Spark3, x, y, _ => Palette.White);
            }
            foreach ((int x, int y) in new[] { (12, 27), (28, 28) })
            {
                Stamp(p, Sprites.Heart5, x, y, _ => Palette.Pink2);
            }
            Dots(p, Palette.Lav1, (10, 8), (31, 10), (20, 2), (40, 14), (1, 14), (16, 37), (25, 38));

            Mask big = Mask.Star(W, H, 21.5f + Dx, 15.5f + Dy, 12f, 6f, 8);
            p.Blob(big, Palette.Butter2, Palette.Ink, border: false);
            p.Fill(Mask.Star(W, H, 21.5f + Dx, 15.5f + Dy, 6f, 3.2f, 8).Minus(big.Edge()), Palette.Butter1);
            p.Fill(Mask.Star(W, H, 21.5f + Dx, 15.5f + Dy, 3f, 1.6f, 8).Minus(big.Edge()), Palette.White);

            Mask far = Ellipse(-20, 40, 18, 62).Or(Ellipse(10, 42, 60, 64));
            p.Fill(far, Palette.Lav2);
            p.Fill(far.Edge(true), Palette.Lav3);
            Mask pond = Ellipse(-14, 48, 34, 80);
            p.Fill(pond, Palette.Blue2);
            p.Fill(pond.Edge(true), Palette.Blue3);
            foreach ((int x, int y, int length) in new[] { (4, 54, 4), (13, 57, 5), (3, 61, 3), (19, 62, 4), (9, 64, 3) })
            {
                p.Run(x + Dx, y + Dy, length, Palette.Blue3);
            }
            Stamp(p, Sprites.Spark3, 10, 51, _ => Palette.Butter2);   // the star in the water
            p.Blob(Ellipse(22, 47, 72, 86), Palette.Mint2, Palette.Mint3);
            GroundBits(p, new[] { (33, 58), (40, 62), (27, 63) }, new[] { (38, 55), (30, 60) });
            Stamp(p, Sprites.CatBack, 23, 37, Sprites.CatBackColour);
            return p;
        }

        // ---- Helpers, in the sketches' coordinates ----

        private static Mask Ellipse(float left, float top, float right, float bottom)
        {
            return Mask.Ellipse(W, H, left + Dx, top + Dy, right + Dx, bottom + Dy);
        }

        private static void Stamp(Picture p, string[] rows, int x, int y, Func<char, uint> colour)
        {
            p.Stamp(rows, x + Dx, y + Dy, colour);
        }

        private static void Dots(Picture p, uint colour, params (int x, int y)[] points)
        {
            foreach ((int x, int y) in points)
            {
                p[x + Dx, y + Dy] = colour;
            }
        }

        private static void GroundBits(Picture p, (int x, int y)[] tufts, (int x, int y)[] flowers)
        {
            foreach ((int x, int y) in tufts)
            {
                Stamp(p, Sprites.Tuft, x, y, _ => Palette.Mint3);
            }
            foreach ((int x, int y) in flowers)
            {
                Stamp(p, Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Butter3 : Palette.Pink3);
            }
        }
    }
}
