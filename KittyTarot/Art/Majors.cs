using System;

namespace KittyTarot
{
    // The major cards' pictures.
    internal static partial class Scenes
    {
        // ---- 0 The Fool: a calico kitten with a bundle on its back steps happily off a cliff, its puppy
        // friend jumping along behind; the sun shines and the mountains wait ----

        private static Picture Fool()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Butter2), (0.22f, Palette.Butter1), (0.5f, Palette.Pink1), (1f, Palette.Pink1));
            Sun(p, 41, 9, 5.5f);
            Cloud(p, Palette.White, Palette.Pink2, (6, 15, 2.5f), (10, 13, 3.5f), (14, 15, 2.5f));
            Sparkles(p, Palette.White, (22, 6), (30, 16), (3, 26));

            // Far mountains with snow on top, peeking out either side.
            Mask hills = Poly((-2, 40), (7, 30), (15, 38), (22, 33), (30, 42), (38, 38), (46, 26), (53, 33), (53, 56), (-2, 56));
            p.Fill(hills, Palette.Lav2);
            (int x, int y)[] peaks = { (7, 30), (22, 33), (46, 26) };
            p.Fill(hills.And(Where((x, y) => Array.Exists(peaks, k => y <= k.y + 3 + (x + y) % 2 && Math.Abs(x - k.x) <= 5))), Palette.White);
            p.Fill(hills.Edge(true), Palette.Lav3);

            // Clouds far below the edge.
            Cloud(p, Palette.White, Palette.Pink2, (43, 58, 2.5f), (46.5f, 57, 3f), (50, 58, 2.5f));
            Cloud(p, Palette.White, Palette.Pink2, (39, 67, 3.5f), (44, 64, 4.5f), (50, 66, 5f), (42, 72, 5f), (49, 72, 5f));

            // The cliff: rock with grass on top, and its edge right under the kitten's front paws.
            Mask cliff = Poly((-1, 54), (36, 54), (37, 57), (35, 60), (36, 63), (34, 66), (35, 70), (33, 73), (-1, 73));
            p.Blob(cliff, Palette.Peach2, Palette.Ink, border: false);
            Mask rock = cliff.Minus(cliff.Edge());
            p.Fill(rock.Minus(Poly((-1, 54), (33, 54), (34, 57), (32, 60), (33, 63), (31, 66), (32, 70), (30, 73), (-1, 73))), Palette.Peach3);
            foreach ((int x, int y, int length) in new[] { (2, 61, 9), (15, 64, 10), (5, 68, 8), (21, 69, 8), (24, 60, 7) })
            {
                p.Run(x, y, length, Palette.Peach3);
            }
            p.Fill(rock.And(Where((x, y) => y <= 56 + (x / 3 % 2))), Palette.Mint2);
            p.Run(0, 54, 36, Palette.Mint3);
            GroundBits(p, new[] { (5, 54), (25, 54) }, new[] { (2, 55), (14, 55), (29, 55) });

            // The puppy: white with a peach ear, jumping along.
            p.Blob(Line(2f, (2, 50), (-1, 53)), Palette.White);
            p.Blob(Line(2f, (7, 50), (10, 52)), Palette.White);
            p.Blob(Line(2f, (1, 46), (-1, 42)), Palette.White);
            p.Blob(Ellipse(-1, 45, 9, 51), Palette.White);
            p.Blob(Ellipse(4, 37, 12, 45).Or(Ellipse(10, 40, 14, 44)), Palette.White);
            p.Blob(Ellipse(3, 38, 6, 45), Palette.Peach2);
            p.Dots(Palette.Ink, (9, 40), (14, 41), (14, 42));
            p.Dots(Palette.Pink3, (12, 45));

            // The kitten: tail curling back, legs, body, the bundle on its back, then the head.
            p.Blob(Line(3f, (19, 46), (17, 42), (16, 38), (17.5f, 35)), Palette.Peach2);
            p.Blob(Mask.RoundedRect(W, H, 21, 47, 23, 54, 1), Palette.Lav1);
            p.Blob(Mask.RoundedRect(W, H, 18, 47, 20, 54, 1), Palette.White);
            p.Blob(Mask.RoundedRect(W, H, 31, 47, 33, 54, 1), Palette.White);
            Mask body = Ellipse(17, 41, 36, 52);
            p.Blob(body, Palette.White);
            Mask inside = body.Minus(body.Edge());
            p.Fill(inside.And(Ellipse(15, 40, 22, 46)), Palette.Ink2);
            p.Fill(inside.And(Ellipse(27, 39, 33, 45)), Palette.Peach2);
            p.Blob(Line(2.5f, (33, 48), (37, 48)).Or(Ellipse(36, 46, 39, 49)), Palette.White);

            p.Blob(Ellipse(20, 35, 28, 43), Palette.Pink3);
            p.Dots(Palette.White, (22, 38), (25, 37), (23, 41), (26, 40));
            p.Blob(Ellipse(20, 31, 23, 34).Or(Ellipse(25, 31, 28, 34)), Palette.Pink4);
            p.Blob(Ellipse(23, 33, 25, 35), Palette.Pink4);

            p.Blob(Poly((27, 36), (28, 26), (34, 31)), Palette.Peach2);
            p.Blob(Poly((34, 31), (40, 26), (41, 36)), Palette.Ink2);
            p.Fill(Poly((29, 33), (29.5f, 29), (32.5f, 31.5f)), Palette.Pink2);
            p.Fill(Poly((35.5f, 31.5f), (38.5f, 29), (39, 33)), Palette.Pink2);
            Mask head = Ellipse(26, 30, 42, 44);
            p.Blob(head, Palette.White);
            Mask face = head.Minus(head.Edge());
            p.Fill(face.And(Ellipse(25, 28, 32, 35)), Palette.Peach2);
            p.Fill(face.And(Ellipse(36, 28, 43, 34)), Palette.Ink2);
            p.Dots(Palette.Ink, (30, 38), (31, 37), (32, 38), (36, 38), (37, 37), (38, 38));   // happy closed eyes
            p.Dots(Palette.Pink4, (34, 39));                                                   // nose
            p.Dots(Palette.Ink, (33, 40), (35, 40));                                           // mouth
            p.Dots(Palette.Pink2, (29, 40), (30, 40), (38, 40), (39, 40));                     // blush
            p.Stamp(Sprites.Flower, 39, 27, letter => letter == 'y' ? Palette.Butter3 : Palette.Pink3);
            Hearts(p, Palette.Pink3, (9, 25), (43, 42));
            return p;
        }

        // A sun with short rays.
        private static void Sun(Picture p, float cx, float cy, float radius)
        {
            for (int k = 0; k < 8; k++)
            {
                double angle = k * Math.PI / 4;
                float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
                p.Fill(Line(1.5f, (cx + dx * (radius + 2.5f), cy + dy * (radius + 2.5f)), (cx + dx * (radius + 4.5f), cy + dy * (radius + 4.5f))), Palette.Butter3);
            }
            p.Blob(Circle(cx, cy, radius), Palette.Butter2, Palette.Ink, border: false);
            p.Fill(Circle(cx - 1, cy - 1, radius * 0.5f), Palette.Butter1);
        }

        // A puffy cloud: circles (x, y, radius) filled together, shaded along the bottom.
        private static void Cloud(Picture p, uint fill, uint edge, params (float x, float y, float r)[] puffs)
        {
            Mask cloud = new Mask(W, H);
            float bottom = float.MinValue;
            foreach ((float x, float y, float r) in puffs)
            {
                cloud = cloud.Or(Circle(x, y, r));
                bottom = Math.Max(bottom, y);
            }
            p.Fill(cloud, fill);
            p.Fill(cloud.Minus(cloud.Edge(true)).Rows(y => y >= bottom + 1), Palette.Pink1);
            p.Fill(cloud.Edge(true), edge);
        }

        // ---- I The Magician: a lavender cat in a pink cape raises a star wand and points at the table,
        // where the cup, wand, sword and coin wait; the infinity sign above, roses below ----

        private static Picture Magician()
        {
            var p = new Picture(W, H);
            p.Fill(Palette.Butter1);
            p.Fill(Where((x, y) => (int)Math.Floor((Math.Atan2(y - 22, x - 25) + Math.PI) / (Math.PI / 9)) % 2 == 0), Palette.Butter2);
            Sparkles(p, Palette.White, (2, 13), (41, 13), (45, 30), (3, 37));
            p.Stamp(Sprites.Infinity, 21, 3, Palette.Lav4);

            // The cat: body in its cape, head, then the arms and the wand.
            Mask body = Ellipse(14, 27, 36, 60);
            p.Blob(body, Palette.Pink3);
            p.Fill(body.Minus(body.Edge()).Rows(y => y > 50), Palette.Pink4);
            Mask chest = body.Minus(body.Edge()).And(Poly((22, 28), (28, 28), (31, 60), (19, 60)));
            p.Fill(chest, Palette.White);
            p.Fill(Line(1f, (22, 29), (19.5f, 47)).Or(Line(1f, (28, 29), (30.5f, 47))).And(body), Palette.Ink);
            p.Blob(Ellipse(23.5f, 30, 26.5f, 33), Palette.Butter3);

            CatHead(p, 16, 15, 34, 30, Palette.Lav1, Eyes.Open);

            p.Blob(Line(2.2f, (10.5f, 8), (10.5f, 30)), Palette.White);
            p.Stamp(Sprites.Star9, 6, 1, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('w', Palette.White)));
            p.Blob(Line(4.5f, (17, 36), (11, 30), (10.5f, 27)), Palette.Pink3);
            p.Blob(Ellipse(8, 22, 13, 27), Palette.Lav1);
            p.Dots(Palette.Lav2, (9, 25), (11, 25));
            p.Blob(Line(4.5f, (33, 36), (37.5f, 43)), Palette.Pink3);
            p.Blob(Ellipse(35, 42, 40, 47), Palette.Lav1);
            p.Dots(Palette.Lav2, (36, 45), (38, 45));

            // The table and the four tools on it.
            p.Blob(Rect(3, 53, 46, 63), Palette.Peach2);
            p.Fill(Rect(4, 59, 45, 62), Palette.Peach3);
            p.Blob(Rect(1, 47, 48, 53), Palette.Peach1);

            // A teacup on its saucer, a heart-tipped wand, a sword and a star coin: this deck's suits.
            p.Blob(Circle(11.5f, 46, 2.3f).Minus(Circle(11.5f, 46, 0.8f)), Palette.Blue2);
            p.Blob(Ellipse(3, 37, 11, 49).And(Where((x, y) => y >= 43)), Palette.Blue2);
            p.Blob(Ellipse(3, 42, 11, 45), Palette.Peach2);
            p.Dots(Palette.Pink3, (6, 47), (8, 47), (7, 48));
            p.Blob(Ellipse(1, 49, 13, 51), Palette.Blue3);

            p.Blob(Line(2f, (14, 52), (21, 48.5f)), Palette.Peach3);
            p.Stamp(Sprites.Heart7, 18, 43, Sprites.Two(Palette.Pink3));

            p.Blob(Line(2.2f, (28, 51), (37, 51)), Palette.White);
            p.Blob(Rect(26, 49, 27, 53), Palette.Lav3);
            p.Blob(Rect(23, 50, 25, 52), Palette.Peach3);

            p.Blob(Circle(43.5f, 47.5f, 3.6f), Palette.Butter2);
            p.Stamp(Sprites.Spark3, 42, 46, Palette.Butter3);

            // Roses and lilies along the bottom, and a few hanging from the top corner.
            Mask bushes = Where((x, y) => y >= 68);
            foreach ((float x, float y) in new[] { (2f, 67f), (9f, 65f), (16f, 67f), (23f, 65f), (30f, 67f), (37f, 65f), (44f, 67f), (50f, 65f) })
            {
                bushes = bushes.Or(Circle(x, y, 4.2f));
            }
            p.Fill(bushes, Palette.Mint2);
            p.Fill(bushes.Edge(true), Palette.Mint3);
            Roses(p, (3, 66), (17, 66), (31, 66), (44, 67));
            Lilies(p, (9, 64), (24, 64), (37, 64), (48, 65));

            Mask vine = Circle(49, -1, 7f).Or(Circle(42, -2, 5f));
            p.Fill(vine, Palette.Mint2);
            p.Fill(vine.Edge(true), Palette.Mint3);
            Roses(p, (45, 1), (40, 0));
            Roses(p, (47, 4));
            return p;
        }

        private static void Roses(Picture p, params (int x, int y)[] at)
        {
            foreach ((int x, int y) in at)
            {
                p.Stamp(Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Pink4 : Palette.Pink3);
            }
        }

        private static void Lilies(Picture p, params (int x, int y)[] at)
        {
            foreach ((int x, int y) in at)
            {
                p.Stamp(Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Butter3 : Palette.White);
            }
        }

        // How a cat looks out of its face.
        private enum Eyes { Open, Happy, Sleepy, Stern }

        // A cat's head facing you: the box is the round of the head (an odd number of pixels across, so
        // the face has a middle column), with the ears above it. Returns the face's middle column and the
        // eyes' row, for adding things on top.
        private static (int c, int eyeY) CatHead(Picture p, int left, int top, int right, int bottom, uint fur, Eyes eyes)
        {
            int c = (left + right) / 2;
            int w = right - left, h = bottom - top;
            foreach (int side in new[] { -1, 1 })
            {
                float outer = c + side * (w / 2f + 0.5f), tip = c + side * (w / 2f - 1), inner = c + side * (w * 0.08f);
                p.Blob(Poly((outer, top + h * 0.4f), (tip, top - 5.5f), (inner, top + 1)), fur);
                p.Fill(Poly((outer - side * 1.5f, top + 3), (tip - side * 0.3f, top - 3), (inner + side * 2.2f, top + 2)), Palette.Pink2);
            }
            p.Blob(Ellipse(left, top, right, bottom), fur);

            int eyeY = top + (int)Math.Round(h * 0.48f);
            int gap = (int)Math.Round(w * 0.2f);
            int l = c - gap - 1, r = c + gap;   // each eye's left column; open eyes are two wide
            switch (eyes)
            {
                case Eyes.Open:
                    foreach (int x in new[] { l, r })
                    {
                        p.Fill(Rect(x, eyeY, x + 1, eyeY + 2), Palette.Ink);
                        p[x, eyeY] = Palette.White;
                    }
                    break;
                case Eyes.Happy:
                    foreach (int x in new[] { l - 1, r })
                    {
                        p.Dots(Palette.Ink, (x, eyeY + 1), (x + 1, eyeY), (x + 2, eyeY + 1));
                    }
                    break;
                case Eyes.Sleepy:
                    foreach (int x in new[] { l - 1, r })
                    {
                        p.Dots(Palette.Ink, (x, eyeY + 1), (x + 1, eyeY + 2), (x + 2, eyeY + 1));
                    }
                    break;
                case Eyes.Stern:
                    foreach (int x in new[] { l, r })
                    {
                        p.Fill(Rect(x, eyeY + 1, x + 1, eyeY + 2), Palette.Ink);
                    }
                    p.Dots(Palette.Ink, (l - 1, eyeY - 1), (l, eyeY), (l + 1, eyeY), (r, eyeY), (r + 1, eyeY), (r + 2, eyeY - 1));
                    break;
            }
            int noseY = eyeY + 3;
            p[c, noseY] = Palette.Pink4;
            p.Dots(Palette.Ink, (c - 1, noseY + 1), (c + 1, noseY + 1));
            p.Dots(Palette.Pink2, (l - 3, noseY), (l - 2, noseY), (r + 3, noseY), (r + 4, noseY));
            return (c, eyeY);
        }

        // ---- II The High Priestess: a dark cat in a blue veil sits between a dark pillar and a light one,
        // a scroll in its paws, a moon crown on its head and a crescent moon at its feet; behind it hangs
        // a curtain of pomegranates ----

        private static Picture HighPriestess()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav3), (0.5f, Palette.Lav2), (1f, Palette.Blue2));
            Sparkles(p, Palette.White, (13, 2), (35, 4));

            // The curtain between the pillars, with pomegranates on it, and the water below it.
            Mask curtain = Rect(10, 8, 39, 58);
            p.Fill(curtain, Palette.Pink1);
            for (int y = 11; y < 58; y += 8)
            {
                for (int x = 12 + (y / 8 % 2) * 4; x < 39; x += 8)
                {
                    p.Stamp(Sprites.Pomegranate, x, y, Key(('k', Palette.Pink4), ('r', Palette.Pink3), ('m', Palette.Mint3)));
                }
            }
            p.Fill(Rect(10, 8, 39, 9), Palette.Pink2);
            p.Fill(Where((x, y) => y >= 58), Palette.Blue2);
            p.Fill(Where((x, y) => y == 58), Palette.Blue3);
            foreach ((int x, int y, int length) in new[] { (3, 62, 5), (40, 63, 6), (12, 67, 4), (33, 68, 5) })
            {
                p.Run(x, y, length, Palette.Blue3);
            }

            // The pillars: B dark, J light.
            Pillar(p, 1, Palette.Ink2, Palette.Ink, Palette.Lav2, "B");
            Pillar(p, 40, Palette.White, Palette.Lav2, Palette.Ink2, "J");

            // The cat: a blue mantle from its crown to the floor over a white gown, then its head.
            Mask mantle = Ellipse(13, 14, 37, 40).Or(Poly((15, 33), (35, 33), (40, 66), (10, 66)));
            p.Blob(mantle, Palette.Blue2);
            Mask inside = mantle.Minus(mantle.Edge());
            p.Fill(inside.Minus(Ellipse(14, 15, 36, 39)).Minus(Poly((16, 34), (34, 34), (38, 66), (12, 66))), Palette.Blue3);
            p.Blob(Poly((22, 36), (28, 36), (33, 67), (17, 67)), Palette.White);
            p.Fill(Line(1f, (25, 39), (25, 66)), Palette.Lav1);
            (int c, int eyeY) = CatHead(p, 16, 20, 34, 35, Palette.Ink2, Eyes.Open);
            foreach (int x in new[] { c - 6, c + 4 })
            {
                p.Fill(Rect(x - 1, eyeY, x + 3, eyeY + 2), Palette.Ink2);
                p.Fill(Rect(x, eyeY, x + 2, eyeY + 2), Palette.Butter2);
                p.Dots(Palette.Ink, (x + 1, eyeY + 1), (x + 1, eyeY + 2));
                p[x, eyeY] = Palette.White;
            }
            p.Stamp(Sprites.MoonCrown, c - 6, 9, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('w', Palette.White)));

            // Paws holding the scroll.
            p.Blob(Rect(18, 45, 32, 49), Palette.Cream);
            p.Fill(Rect(18, 45, 19, 49).Or(Rect(31, 45, 32, 49)), Palette.Butter2);
            p.Blob(Rect(17, 44, 19, 50), Palette.Butter2);
            p.Blob(Rect(31, 44, 33, 50), Palette.Butter2);
            p.Dots(Palette.Ink2, (22, 47), (23, 47), (24, 47), (26, 47), (27, 47));
            p.Blob(Ellipse(19, 43, 23, 47), Palette.Ink2);
            p.Blob(Ellipse(27, 43, 31, 47), Palette.Ink2);

            // The crescent moon at its feet.
            Mask moon = Ellipse(15, 56, 35, 68).Minus(Ellipse(13, 51, 37, 64));
            p.Blob(moon, Palette.Butter2, Palette.Ink, border: false);
            p.Stamp(Sprites.Spark3, 6, 64, Palette.White);
            p.Stamp(Sprites.Spark3, 42, 60, Palette.White);
            return p;
        }

        // A pillar nine pixels wide with a capital on top and a letter on it, shaded down its right side.
        private static void Pillar(Picture p, int left, uint fill, uint shade, uint letter, string text)
        {
            p.Blob(Rect(left, 6, left + 8, 72), fill, Palette.Ink, border: false);
            p.Fill(Rect(left + 7, 7, left + 7, 71), shade);
            p.Blob(Rect(left - 1, 2, left + 9, 6), fill, Palette.Ink, border: false);
            p.Fill(Rect(left, 5, left + 8, 5), shade);
            TarotFont.Draw(p, text, left + 2, 18, letter);
        }

        // ---- III The Empress: a fluffy cream cat rests on pink cushions in a garden, a crown of stars over
        // her head and a sceptre in her paw, the heart shield with Venus's sign beside her, wheat in front,
        // trees and a waterfall behind ----

        private static Picture Empress()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Butter1), (0.35f, Palette.Peach1), (1f, Palette.Peach1));

            // Trees and the waterfall behind.
            foreach ((float l, float t, float r, float b) in new[] { (-3f, 14f, 7f, 50f), (4f, 20f, 13f, 50f), (37f, 22f, 45f, 50f), (44f, 12f, 54f, 50f) })
            {
                Mask tree = Ellipse(l, t, r, b);
                p.Fill(tree, Palette.Mint3);
                p.Fill(tree.Edge(true), Palette.Mint4);
            }
            Mask fall = Rect(39, 26, 45, 50);
            p.Fill(fall, Palette.Blue2);
            p.Fill(fall.And(Where((x, y) => (x * 3 + y / 3) % 5 == 0)), Palette.White);
            p.Fill(fall.Edge(true).And(Where((x, y) => x == 39 || x == 45)), Palette.Blue3);
            p.Fill(Where((x, y) => y >= 46), Palette.Mint2);
            p.Run(0, 46, W, Palette.Mint3);
            Mask pool = Ellipse(33, 46, 52, 52);
            p.Fill(pool, Palette.Blue2);
            p.Fill(pool.Edge(true), Palette.Blue3);
            p.Dots(Palette.White, (38, 48), (41, 49), (44, 48), (47, 50));

            // Cushions, the cat, her pearls and her crown of stars.
            Mask back = Ellipse(10, 27, 40, 54);
            p.Blob(back, Palette.Pink2);
            p.Dots(Palette.Pink3, (14, 36), (20, 31), (30, 31), (36, 36), (12, 45), (38, 45));
            Mask body = Ellipse(13, 31, 37, 58);
            p.Blob(body, Palette.Cream);
            p.Fill(body.Minus(body.Edge()).Rows(y => y >= 53), Palette.Peach1);
            Mask ruff = Circle(20, 36, 3.5f).Or(Circle(25, 37, 3.5f)).Or(Circle(30, 36, 3.5f)).Or(Circle(25, 34, 5f));
            p.Blob(ruff, Palette.White);
            (int c, int eyeY) = CatHead(p, 16, 18, 34, 33, Palette.Cream, Eyes.Sleepy);
            foreach ((int x, int y) in new[] { (20, 40), (22, 41), (24, 41), (26, 41), (28, 41), (30, 40) })
            {
                p[x, y] = Palette.White;
                p[x, y + 1] = Palette.Lav2;
            }
            for (int k = 0; k < 7; k++)
            {
                double angle = Math.PI * (1.12 + k * 0.76 / 6);
                int x = c + (int)Math.Round(15 * Math.Cos(angle)), y = 22 + (int)Math.Round(14 * Math.Sin(angle));
                p.Stamp(Sprites.Spark3, x - 1, y - 1, Palette.Butter3);
                p[x, y] = Palette.White;
            }

            // The sceptre in her paw.
            p.Blob(Line(2f, (39, 21), (37, 47)), Palette.Butter3);
            p.Blob(Circle(39.5f, 19.5f, 2.6f), Palette.Butter2);
            p[39, 19] = Palette.White;
            p.Blob(Ellipse(34, 37, 39, 42), Palette.Cream);

            // Her front paws, the seat cushion and the heart shield.
            Mask seat = Ellipse(5, 52, 45, 63);
            p.Blob(seat, Palette.Pink3);
            p.Fill(seat.Minus(seat.Edge()).Rows(y => y <= 54), Palette.Pink2);
            p.Blob(Ellipse(19, 51, 24, 55), Palette.Cream);
            p.Blob(Ellipse(26, 51, 31, 55), Palette.Cream);
            Mask heart = Circle(6.5f, 52, 4.2f).Or(Circle(13.5f, 52, 4.2f)).Or(Poly((2.4f, 53.5f), (17.6f, 53.5f), (10, 62)));
            p.Blob(heart, Palette.Pink3, Palette.Ink, border: false);
            p.Fill(heart.Minus(heart.Edge()).And(Where((x, y) => y <= 50)), Palette.Pink2);
            p.Fill(Circle(10, 53, 2.2f).Minus(Circle(10, 53, 1.1f)), Palette.White);
            p.Fill(Rect(10, 55, 10, 59).Or(Rect(9, 57, 11, 57)), Palette.White);

            // Wheat in front.
            p.Fill(Where((x, y) => y >= 63), Palette.Mint2);
            for (int x = 1; x < W; x += 3)
            {
                int top = 61 + (x * 7 % 4);
                p.Fill(Rect(x, top + 3, x, 72), Palette.Butter3);
                p.Fill(Ellipse(x - 1, top, x + 1, top + 4), Palette.Butter2);
                p[x, top + 1] = Palette.Butter3;
            }
            return p;
        }

        // ---- IV The Emperor: a round orange cat with a serious little face sits on a stone throne with
        // rams' heads, in a crown and a red cloak, holding an ankh sceptre and a golden orb; mountains
        // behind ----

        private static Picture Emperor()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Peach2), (0.4f, Palette.Peach1), (1f, Palette.Peach1));
            Mask hills = Poly((-2, 52), (6, 34), (12, 40), (20, 30), (30, 42), (38, 32), (46, 40), (53, 30), (53, 52));
            p.Fill(hills, Palette.Peach2);
            p.Fill(hills.Edge(true), Palette.Peach3);
            p.Fill(Where((x, y) => y >= 52), Palette.Peach2);
            p.Run(0, 52, W, Palette.Peach3);

            // The throne: stone blocks, with a ram's head on a post at each top corner.
            Mask throne = Rect(9, 17, 41, 66).Or(Rect(5, 12, 13, 66)).Or(Rect(37, 12, 45, 66));
            p.Blob(throne, Palette.Lav2);
            Mask stone = throne.Minus(throne.Edge());
            p.Fill(stone.And(Where((x, y) => y % 7 == 4 || (x + (y / 7 % 2) * 4) % 8 == 0)), Palette.Lav3);
            p.Fill(stone.And(Where((x, y) => y == 13 || y == 18)), Palette.Lav1);
            foreach (int x in new[] { 3, 35 })
            {
                p.Stamp(Sprites.Ram, x, 3, Key(('k', Palette.Ink), ('w', Palette.White), ('f', Palette.Cream), ('h', Palette.Butter2),
                    ('e', Palette.Ink), ('n', Palette.Pink3)));
            }

            // The cat: cloak, body, head and crown; then the sceptre, orb and paws.
            Mask cloak = Poly((25, 30), (40, 40), (42, 66), (8, 66), (10, 40));
            p.Blob(cloak, Palette.Pink4);
            p.Fill(cloak.Minus(cloak.Edge()).Rows(y => y >= 61), Palette.Pink3);
            Mask belly = Ellipse(15, 34, 35, 64);
            p.Blob(belly, Palette.Peach2);
            p.Fill(Ellipse(19, 40, 31, 62), Palette.Cream);
            foreach ((int x, int y) in new[] { (16, 44), (16, 50), (34, 44), (34, 50) })
            {
                p.Run(x, y, 2, Palette.Peach3);
            }
            (int c, int eyeY) = CatHead(p, 15, 20, 35, 37, Palette.Peach2, Eyes.Stern);
            foreach ((int x, int y) in new[] { (c - 1, 21), (c + 1, 21), (c, 22), (c - 2, 23), (c + 2, 23) })
            {
                p[x, y] = Palette.Peach3;
            }
            p.Stamp(Sprites.Crown, c - 5, 11, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));

            p.Blob(Line(2f, (12, 22), (12, 52)), Palette.Butter3);
            p.Blob(Circle(12, 18.5f, 2.6f).Minus(Circle(12, 18.5f, 1f)).Or(Rect(9, 21, 15, 22)), Palette.Butter3);
            p.Blob(Ellipse(9, 42, 15, 48), Palette.Peach2);
            p.Blob(Circle(38, 45, 3.5f), Palette.Butter2);
            p.Blob(Rect(37, 40, 39, 42), Palette.Butter3);
            p[37, 44] = Palette.White;
            p.Blob(Ellipse(35, 47, 41, 52), Palette.Peach2);
            p.Blob(Ellipse(17, 60, 23, 65), Palette.Peach2);
            p.Blob(Ellipse(27, 60, 33, 65), Palette.Peach2);
            Sparkles(p, Palette.White, (17, 4), (24, 1), (31, 4));
            return p;
        }

        // ---- VIII Strength: a kitten with a flower crown sitting on a big, gentle, sleepy lion ----

        private static Picture Strength()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav1), (0.30f, Palette.Pink1), (0.55f, Palette.Peach1), (0.70f, Palette.Butter1), (1f, Palette.Butter1));
            Mask hills = Ellipse(-27, 43, 25, 75).Or(Ellipse(23, 45, 77, 79));
            p.Fill(hills, Palette.Lav2);
            p.Fill(hills.Edge(true), Palette.Lav3);
            p.Fill(Where((x, y) => y >= 52), Palette.Mint2);
            p.Run(0, 52, W, Palette.Mint3);
            GroundBits(p, new[] { (6, 58), (12, 64), (41, 61), (34, 66), (21, 66), (44, 55) }, new[] { (8, 62), (39, 64), (43, 59) });

            // The lion's body lying behind, and its tail with a heart for a tuft.
            Mask body = Ellipse(28, 44, 53, 63);
            p.Blob(body, Palette.Peach2);
            p.Fill(body.Minus(body.Edge(true)).Rows(y => y > 58), Palette.Peach3);
            Mask tail = Line(2f, (43, 47), (44, 43), (43, 40)).Minus(body);
            p.Blob(tail, Palette.Peach3);
            p.Stamp(Sprites.Heart7, 39, 34, Sprites.Two(Palette.Pink3));

            // The mane: a scalloped ring, shaded underneath.
            Mask mane = Ellipse(14, 32, 36, 54);
            for (int k = 0; k < 12; k++)
            {
                double angle = k * Math.PI / 6;
                float x = 25 + 11 * (float)Math.Cos(angle), y = 43 + 11 * (float)Math.Sin(angle);
                mane = mane.Or(Ellipse(x - 3, y - 3, x + 3, y + 3));
            }
            p.Blob(mane, Palette.Peach2);
            p.Fill(mane.Minus(mane.Erode().Erode().Erode()).Minus(mane.Edge()).Rows(y => y > 46), Palette.Peach3);

            // Ears, face and muzzle.
            foreach (int ex in new[] { 19, 31 })
            {
                p.Blob(Ellipse(ex - 3, 33, ex + 3, 39), Palette.Butter2);
                p.Fill(Ellipse(ex - 1, 35, ex + 1, 37), Palette.Pink2);
            }
            p.Blob(Ellipse(18, 36, 32, 50), Palette.Butter2);
            p.Fill(Ellipse(21, 43, 29, 49), Palette.Cream);
            p.Dots(Palette.Ink, (20, 41), (22, 41), (21, 42), (28, 41), (30, 41), (29, 42));   // sleepy eyes
            p.Dots(Palette.Pink4, (24, 44), (25, 44), (26, 44), (25, 45));                     // nose
            p.Dots(Palette.Ink, (23, 46), (25, 46), (27, 46), (24, 47), (26, 47));             // mouth
            p.Dots(Palette.Pink2, (19, 44), (20, 44), (30, 44), (31, 44));                     // blush

            foreach (int px in new[] { 14, 28 })
            {
                p.Blob(Ellipse(px, 51, px + 8, 57), Palette.Butter2);
                p.Dots(Palette.Ink, (px + 3, 55), (px + 3, 56), (px + 5, 55), (px + 5, 56));
            }

            p.Stamp(Sprites.Kitten, 19, 18, Sprites.KittenColour);
            p.Stamp(Sprites.Infinity, 21, 10, Palette.Lav4);
            Sparkles(p, Palette.White, (8, 12), (39, 9), (41, 23), (6, 30));
            Hearts(p, Palette.Pink3, (9, 21), (36, 16));
            return p;
        }

        // ---- XVII The Star: a cat with a bow on its tail gazing up at the star by a pond ----

        private static Picture Star()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav4), (0.28f, Palette.Lav3), (0.52f, Palette.Lav2), (0.72f, Palette.Pink2), (1f, Palette.Pink2));
            Sparkles(p, Palette.White, (6, 7), (39, 6), (5, 25), (41, 27), (10, 37), (36, 39));
            Hearts(p, Palette.Pink2, (15, 30), (31, 31));
            p.Dots(Palette.Lav1, (13, 11), (34, 13), (23, 5), (43, 17), (4, 17), (19, 40), (28, 41));

            Mask big = Mask.Star(W, H, 24.5f, 18.5f, 12f, 6f, 8);
            p.Blob(big, Palette.Butter2, Palette.Ink, border: false);
            p.Fill(Mask.Star(W, H, 24.5f, 18.5f, 6f, 3.2f, 8).Minus(big.Edge()), Palette.Butter1);
            p.Fill(Mask.Star(W, H, 24.5f, 18.5f, 3f, 1.6f, 8).Minus(big.Edge()), Palette.White);

            Mask far = Ellipse(-17, 43, 21, 65).Or(Ellipse(13, 45, 63, 67));
            p.Fill(far, Palette.Lav2);
            p.Fill(far.Edge(true), Palette.Lav3);
            Mask pond = Ellipse(-11, 51, 37, 83);
            p.Fill(pond, Palette.Blue2);
            p.Fill(pond.Edge(true), Palette.Blue3);
            foreach ((int x, int y, int length) in new[] { (7, 57, 4), (16, 60, 5), (6, 64, 3), (22, 65, 4), (12, 67, 3) })
            {
                p.Run(x, y, length, Palette.Blue3);
            }
            p.Stamp(Sprites.Spark3, 13, 54, Palette.Butter2);   // the star in the water
            p.Blob(Ellipse(25, 50, 75, 89), Palette.Mint2, Palette.Mint3);
            GroundBits(p, new[] { (36, 61), (43, 65), (30, 66) }, new[] { (41, 58), (33, 63) });
            p.Stamp(Sprites.CatBack, 26, 40, Sprites.CatBackColour);
            return p;
        }
    }
}
