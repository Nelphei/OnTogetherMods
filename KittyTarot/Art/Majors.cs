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

        // A puffy cloud: circles (x, y, radius) filled together, shaded pink along the bottom.
        private static void Cloud(Picture p, uint fill, uint edge, params (float x, float y, float r)[] puffs)
        {
            ShadedCloud(p, fill, edge, Palette.Pink1, puffs);
        }

        private static void ShadedCloud(Picture p, uint fill, uint edge, uint shade, params (float x, float y, float r)[] puffs)
        {
            Mask cloud = new Mask(W, H);
            float bottom = float.MinValue;
            foreach ((float x, float y, float r) in puffs)
            {
                cloud = cloud.Or(Circle(x, y, r));
                bottom = Math.Max(bottom, y);
            }
            p.Fill(cloud, fill);
            p.Fill(cloud.Minus(cloud.Edge(true)).Rows(y => y >= bottom + 1), shade);
            p.Fill(cloud.Edge(true), edge);
        }

        // ---- XVI The Tower: lightning strikes a tall pastel tower on a rocky peak and pops its crown
        // off; little flames at the windows and on top, two kittens tumbling out, sparks falling ----

        private static Picture Tower()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Ink2), (0.55f, Palette.Lav4), (1f, Palette.Lav4));
            ShadedCloud(p, Palette.Lav3, Palette.Lav2, Palette.Lav4, (33, 3, 4f), (39, 2, 5f), (45, 3, 5f), (50, 6, 4f));

            // The rock and the tower with its broken top.
            Mask rock = Poly((3, 73), (9, 62), (16, 57), (33, 57), (40, 62), (46, 73));
            p.Blob(rock, Palette.Lav3, Palette.Ink, border: false);
            p.Fill(rock.Minus(rock.Edge()).And(Where((x, y) => y <= 59)), Palette.Lav2);
            Mask tower = Poly((17, 58), (17, 21), (19, 19), (21, 22), (23, 18), (26, 21), (28, 19), (30, 22), (32, 20), (32, 58));
            p.Blob(tower, Palette.Lav1);
            p.Fill(tower.Minus(tower.Edge()).And(Where((x, y) => y % 5 == 0 || ((x + (y / 5 % 2) * 3) % 6 == 0 && y > 22))), Palette.Lav2);
            foreach ((int x, int y) in new[] { (20, 27), (27, 34), (20, 42), (27, 47) })
            {
                p.Fill(Rect(x, y + 1, x + 2, y + 4).Or(Circle(x + 1, y + 1, 1.2f)), Palette.Ink2);
                p.Dots(Palette.Peach2, (x + 1, y + 1), (x, y + 3), (x + 1, y + 2), (x + 2, y + 3), (x + 1, y + 3));
                p[x + 1, y + 3] = Palette.Butter2;
            }
            p.Blob(Rect(23, 53, 26, 58).Or(Circle(24.5f, 53, 1.6f)), Palette.Peach3);

            // Flames on top, the lightning, the crown popping off.
            foreach ((float x, float y) in new[] { (19f, 15f), (24f, 12f), (29f, 15f) })
            {
                p.Blob(Circle(x, y + 3, 2.7f).Or(Poly((x - 2.6f, y + 3), (x - 0.5f, y - 3.5f), (x + 2.6f, y + 3))), Palette.Peach2);
                p.Fill(Circle(x, y + 3.5f, 1.2f), Palette.Butter2);
            }
            p.Blob(Poly((44, 3), (34, 11), (38.5f, 12), (26, 22), (32.5f, 13.5f), (28, 13), (37, 3)), Palette.Butter2);
            p.Fill(Line(1f, (39, 5), (33, 10)), Palette.White);
            p.Stamp(Sprites.Crown, 5, 8, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));
            p.Dots(Palette.White, (4, 6), (16, 9), (15, 5));
            Sparkles(p, Palette.Butter2, (11, 21), (41, 22), (7, 50), (43, 54), (12, 62), (37, 66));

            // Two kittens tumbling out, one upside down and one sideways.
            Picture kitten = FallingKitten(Palette.White);
            p.Paste(kitten.Turned(), 3, 27);
            p.Paste(FallingKitten(Palette.Peach2).QuarterTurn(true), 34, 36);
            return p;
        }

        // A little kitten with its paws flung out, on its own 14 x 18 picture to turn and paste.
        private static Picture FallingKitten(uint fur)
        {
            var k = new Picture(14, 18);
            k.Blob(Line(2.6f, (4, 11), (1, 8)).Or(Line(2.6f, (9.5f, 11), (12.5f, 8))), fur);
            k.Blob(Ellipse(3, 15, 6, 17).Or(Ellipse(7, 15, 10, 17)), fur);
            k.Blob(Ellipse(3, 9, 10, 17), fur);
            CatHead(k, 2, 5, 12, 13, fur, Eyes.Open);
            return k;
        }

        // ---- XVIII The Moon: a big sleepy full moon over a path between two towers; a house cat in a
        // collar and a wild lynx kitten sing up at it, and a little crayfish climbs out of the pool ----

        private static Picture Moon()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav4), (0.5f, Palette.Lav3), (1f, Palette.Lav3));
            p.Dots(Palette.Lav1, (4, 4), (12, 9), (40, 5), (46, 13), (3, 20), (45, 26));

            // The moon with its face, glow and falling drops.
            p.Fill(Circle(24.5f, 15, 13.5f).And(Where((x, y) => (x + y) % 2 == 0)), Palette.Lav2);
            p.Blob(Circle(24.5f, 15, 10.5f), Palette.Butter1);
            p.Fill(Circle(20, 10, 1.6f).Or(Circle(30, 19, 1.3f)).Or(Circle(28, 8, 1f)), Palette.Butter2);
            p.Dots(Palette.Ink, (19, 15), (20, 16), (21, 15), (28, 15), (29, 16), (30, 15), (24, 18), (25, 18));
            p.Dots(Palette.Pink2, (17, 17), (18, 17), (31, 17), (32, 17));
            Sparkles(p, Palette.Butter2, (14, 29), (34, 30), (22, 33));

            // Hills, two towers with lit windows, the meadow, the path and the pool.
            Mask hills = Poly((-2, 46), (8, 38), (16, 42), (25, 35), (34, 42), (42, 37), (52, 46), (52, 50), (-2, 50));
            p.Fill(hills, Palette.Lav2);
            p.Fill(hills.Edge(true), Palette.Lav4);
            foreach (int x in new[] { 1, 41 })
            {
                p.Blob(Rect(x, 28, x + 7, 52), Palette.Lav1);
                p.Blob(Rect(x - 1, 26, x + 8, 28), Palette.Lav1);
                p.Fill(Rect(x + 3, 32, x + 4, 34), Palette.Butter2);
            }
            p.Fill(Where((x, y) => y >= 48), Palette.Mint3);
            p.Run(0, 48, W, Palette.Mint4);
            p.Fill(Poly((24.5f, 37), (25.5f, 37), (27, 44), (24, 50), (29, 60), (21, 60), (22, 50), (25, 44)), Palette.Peach1);
            Mask pool = Ellipse(13, 60, 37, 76);
            p.Fill(pool, Palette.Blue2);
            p.Fill(pool.Edge(true), Palette.Blue3);
            p.Run(18, 66, 4, Palette.Blue3);
            p.Run(27, 69, 5, Palette.Blue3);
            p.Stamp(Sprites.Crayfish, 22, 59, Key(('k', Palette.Ink), ('p', Palette.Pink3)));

            // The house cat with its collar and bell, and the lynx kitten with tufts and spots.
            (int hc, int hy) = SmallCat(p, 10, 45, Palette.White, Eyes.Happy, singing: true);
            p.Fill(Rect(hc - 5, 55, hc + 5, 55), Palette.Pink3);
            p.Blob(Circle(hc, 57, 1.3f), Palette.Butter2);
            (int lc, int ly) = SmallCat(p, 40, 45, Palette.Peach2, Eyes.Happy, singing: true);
            p.Dots(Palette.Ink, (lc - 5, 37), (lc - 5, 38), (lc + 5, 37), (lc + 5, 38));
            p.Dots(Palette.Peach3, (lc - 4, ly - 2), (lc + 4, ly - 2), (lc - 1, 47), (lc + 1, 47), (lc - 3, 58), (lc + 3, 60), (lc + 4, 57));
            return p;
        }

        // A cat sitting up, smaller than SittingCat: head 13 pixels across with its top at top, body and
        // paws below. singing: its mouth open in a little o. Returns the head's middle column and eye row.
        private static (int c, int eyeY) SmallCat(Picture p, int c, int top, uint fur, Eyes eyes, bool singing = false)
        {
            p.Blob(Ellipse(c - 7, top + 8, c + 7, top + 22), fur);
            p.Blob(Ellipse(c - 5, top + 19, c - 1, top + 22).Or(Ellipse(c + 1, top + 19, c + 5, top + 22)), fur);
            (int hc, int eyeY) = CatHead(p, c - 6, top, c + 6, top + 10, fur, eyes);
            if (singing)
            {
                int noseY = eyeY + 3;
                p.Dots(Palette.Ink, (hc - 1, noseY + 1), (hc + 1, noseY + 1), (hc, noseY + 2));
                p[hc, noseY + 1] = Palette.Pink4;
            }
            return (hc, eyeY);
        }

        // ---- XIX The Sun: a big smiling sun over a wall with sunflowers peeking over it; a happy
        // kitten in a flower crown rides the white pony, waving a pink banner ----

        private static Picture Sun()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Butter1), (0.45f, Palette.Blue1), (1f, Palette.Blue1));

            // The sun: rays all round, a round face.
            for (int k = 0; k < 16; k++)
            {
                double angle = k * Math.PI / 8;
                float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
                float r = k % 2 == 0 ? 15f : 12.5f;
                p.Fill(Line(1.6f, (25 + dx * 10.5f, 13 + dy * 10.5f), (25 + dx * r, 13 + dy * r)), Palette.Butter3);
            }
            p.Blob(Circle(25, 13, 9.5f), Palette.Butter2, Palette.Ink, border: false);
            p.Fill(Circle(23, 11, 4.5f), Palette.Butter1);
            p.Dots(Palette.Ink, (20, 12), (21, 11), (22, 12), (28, 12), (29, 11), (30, 12), (23, 16), (24, 17), (25, 17), (26, 17), (27, 16));
            p.Dots(Palette.Pink3, (18, 14), (19, 14), (31, 14), (32, 14));

            // Sunflowers peeking over a garden wall, and the grass.
            foreach ((int x, int y) in new[] { (3, 33), (47, 31) })
            {
                p.Fill(Rect(x, y, x, 42), Palette.Mint4);
                Mask petals = Circle(x, y, 3.6f);
                p.Blob(petals, Palette.Butter2);
                p.Fill(petals.And(Where((px, py) => (px + py) % 2 == 0)).Minus(petals.Edge()), Palette.Butter3);
                p.Blob(Circle(x, y, 1.6f), Palette.Peach3);
            }
            Mask wall = Rect(-1, 40, 50, 50);
            p.Blob(wall, Palette.Lav1, Palette.Ink, border: false);
            p.Fill(wall.Minus(wall.Edge()).And(Where((x, y) => y == 45 || (y < 45 && (x + 2) % 6 == 0) || (y > 45 && (x + 5) % 6 == 0))), Palette.Lav2);
            p.Fill(Where((x, y) => y >= 51), Palette.Mint2);
            p.Run(0, 51, W, Palette.Mint3);
            GroundBits(p, new[] { (4, 66), (44, 64) }, new[] { (8, 69), (42, 69), (24, 69) });

            // The pony, the kitten with its flower crown, and the banner.
            p.Paste(Pony(Palette.Butter2), 0, 5);
            p.Blob(Line(2f, (13, 45), (9, 9)), Palette.Peach3);
            Mask banner = Poly((9, 10), (-1, 7), (1, 12), (-1, 17), (10, 18));
            p.Blob(banner, Palette.Pink3);
            p.Fill(banner.Minus(banner.Edge()).And(Where((x, y) => y == 13)), Palette.Pink2);
            p.Stamp(Sprites.Kitten, 14, 28, Key(('k', Palette.Ink), ('w', Palette.Peach2), ('p', Palette.Pink2), ('R', Palette.Pink4),
                ('P', Palette.Pink3), ('m', Palette.Mint3), ('y', Palette.Butter3)));
            p.Dots(Palette.Peach3, (19, 31), (21, 31), (20, 32));
            p.Blob(Ellipse(10, 38, 14, 42), Palette.Peach2);
            Hearts(p, Palette.Pink3, (40, 17), (3, 23));
            return p;
        }

        // ---- XX Judgement: an angel cat in the clouds blows a golden trumpet with a little flag; below,
        // three kittens pop up out of boxes floating on the sea, paws in the air; snowy mountains behind ----

        private static Picture Judgement()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav1), (0.4f, Palette.Blue1), (1f, Palette.Blue1));

            // Snowy mountains and the sea.
            Mask peaks = Poly((-2, 52), (6, 38), (12, 44), (20, 34), (28, 44), (36, 36), (44, 44), (52, 38), (52, 52));
            p.Fill(peaks, Palette.White);
            p.Fill(peaks.And(Where((x, y) => y >= 44 + (x / 3) % 2)), Palette.Lav1);
            p.Fill(peaks.Edge(true), Palette.Lav2);
            Mask sea = Where((x, y) => y >= 50);
            p.Fill(sea, Palette.Blue2);
            p.Run(0, 50, W, Palette.Blue3);
            foreach ((int x, int y, int length) in new[] { (2, 55, 4), (40, 56, 5), (20, 69, 6), (44, 68, 4), (5, 70, 3) })
            {
                p.Run(x, y, length, Palette.Blue3);
            }

            // The angel among the clouds, with the trumpet and its flag.
            Mask wing = Poly((22, 14), (15, 7), (8, 4), (5, 7), (7, 11), (5, 15), (9, 18), (15, 18), (20, 19));
            p.Blob(wing.Or(Mirror(wing)), Palette.White);
            p.Blob(Poly((21, 15), (29, 15), (31, 24), (19, 24)), Palette.Pink2);
            (int c, int eyeY) = CatHead(p, 19, 5, 31, 16, Palette.Butter1, Eyes.Happy);
            Cloud(p, Palette.White, Palette.Lav2, (3, 24, 4f), (10, 22, 4.5f), (17, 25, 4f), (25, 23, 5f), (33, 25, 4f), (40, 22, 4.5f), (47, 24, 4f));
            Mask trumpet = Line(2f, (c - 2, eyeY + 5), (12, 30)).Or(Poly((13, 28), (7, 34), (11, 36), (15, 31)));
            p.Blob(trumpet, Palette.Butter3);
            Mask flag = Rect(9, 31, 16, 38);
            p.Blob(flag.Minus(trumpet), Palette.White);
            p.Fill(Rect(12, 33, 13, 37).Or(Rect(10, 34, 15, 35)).Minus(trumpet), Palette.Pink3);

            // Three kittens popping out of their boxes, paws up.
            foreach ((int x, int y, uint fur) in new[] { (2, 52, Palette.Lav3), (18, 55, Palette.Pink2), (34, 52, Palette.Peach2) })
            {
                p.Blob(Line(2.6f, (x + 3, y + 3), (x - 0.5f, y - 3)).Or(Line(2.6f, (x + 11, y + 3), (x + 14.5f, y - 3))), fur);
                p.Blob(Ellipse(x - 3, y - 7, x + 1, y - 3).Or(Ellipse(x + 13, y - 7, x + 17, y - 3)), fur);
                CatHead(p, x + 2, y - 5, x + 12, y + 3, fur, Eyes.Happy);
                Mask box = Rect(x, y + 4, x + 14, y + 13);
                p.Blob(box, Palette.Peach2);
                p.Fill(Rect(x + 1, y + 5, x + 13, y + 5), Palette.Peach3);
                p.Blob(Poly((x, y + 4), (x - 3, y + 1), (x - 1, y + 4)).Or(Poly((x + 14, y + 4), (x + 17, y + 1), (x + 15, y + 4))), Palette.Peach1);
            }
            return p;
        }

        // ---- XXI The World: the Fool's calico kitten, grown up, dances on one foot inside a big wreath
        // tied with pink bows, a heart-tipped wand in each paw and a lavender sash; the cat angel, the bird,
        // the calf and the lion cub watch from the corners ----

        private static Picture World()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Blue1), (0.5f, Palette.Lav1), (1f, Palette.Blue1));
            Sparkles(p, Palette.White, (15, 3), (34, 2), (2, 34), (46, 36), (14, 66), (35, 67));
            FourCreatures(p);

            // The wreath: leaves with little flowers, a bow at the top and the bottom.
            Mask wreath = Ellipse(8, 9, 41, 63).Minus(Ellipse(13, 14, 36, 58));
            p.Blob(wreath, Palette.Mint3);
            p.Fill(wreath.Minus(wreath.Edge()).And(Where((x, y) => (x * 3 + y * 2) % 7 == 0)), Palette.Mint2);
            p.Fill(wreath.Minus(wreath.Edge()).And(Where((x, y) => (x * 5 + y * 3) % 11 == 0)), Palette.Mint4);
            foreach ((int x, int y) in new[] { (9, 25), (39, 27), (10, 46), (38, 44), (17, 59), (31, 58) })
            {
                p.Stamp(Sprites.Flower, x - 1, y - 1, letter => letter == 'y' ? Palette.Butter3 : Palette.Pink3);
            }
            foreach (int y in new[] { 8, 61 })
            {
                p.Blob(Poly((24.5f, y + 1.5f), (19, y - 1), (19, y + 4)).Or(Poly((24.5f, y + 1.5f), (30, y - 1), (30, y + 4))), Palette.Pink3);
                p.Blob(Circle(24.5f, y + 1.5f, 1.6f), Palette.Pink4);
            }

            // The dancer: legs, body with the sash, arms, head with its calico patches, then the wands.
            uint fur = Palette.White;
            p.Blob(Line(3.4f, (23.5f, 43), (23.5f, 54)), fur);
            p.Blob(Ellipse(21, 53, 25, 56), fur);
            p.Blob(Line(3.4f, (27, 43), (30, 48), (33, 46)), fur);
            Mask body = Ellipse(20, 28, 30, 45);
            p.Blob(body, fur);
            p.Fill(body.Minus(body.Edge()).And(Line(3f, (20, 31), (30, 43))), Palette.Lav3);
            p.Fill(body.Minus(body.Edge()).And(Ellipse(25, 36, 31, 45)), Palette.Peach2);
            p.Blob(Line(3f, (21, 33), (17.5f, 30.5f)), fur);
            p.Blob(Ellipse(14, 27, 18, 31), fur);
            p.Blob(Line(3f, (29, 36), (32.5f, 38.5f)), fur);
            p.Blob(Ellipse(31, 37, 35, 41), fur);
            Mask head = Ellipse(19, 18, 31, 29);
            CatHead(p, 19, 18, 31, 29, fur, Eyes.Happy, ears: Palette.Peach2);
            p.Fill(Ellipse(18, 17, 23, 21).And(head.Erode()), Palette.Peach2);
            p.Fill(Ellipse(27, 17, 32, 21).And(head.Erode()), Palette.Ink2);
            p.Blob(Line(2f, (16, 28), (16, 23)).Or(Line(2f, (33, 40), (34, 47))), Palette.Peach3);
            p.Stamp(Sprites.Heart5, 14, 19, Palette.Pink3);
            p.Paste(CardFaces.Sprite(Sprites.Heart5, letter => Palette.Pink3).Turned(), 32, 47);
            return p;
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

        // How a cat looks out of its face. Iris: coloured eyes with a slit pupil, for dark cats and
        // pointed ones.
        private enum Eyes { Open, Happy, Sleepy, Stern, Iris }

        // A cat's head facing you: the box is the round of the head (an odd number of pixels across, so
        // the face has a middle column), with the ears above it. iris: the eyes' colour for Eyes.Iris;
        // ears: the ears' fur when it differs (0 = fur); muzzle: a darker patch down the middle of the face
        // (0 = none). Returns the face's middle column and the eyes' row, for adding things on top.
        private static (int c, int eyeY) CatHead(Picture p, int left, int top, int right, int bottom, uint fur, Eyes eyes,
            uint iris = Palette.Butter2, uint ears = 0, uint muzzle = 0)
        {
            int c = (left + right) / 2;
            int w = right - left, h = bottom - top;
            foreach (int side in new[] { -1, 1 })
            {
                float outer = c + side * (w / 2f + 0.5f), tip = c + side * (w / 2f - 1), inner = c + side * (w * 0.08f);
                p.Blob(Poly((outer, top + h * 0.4f), (tip, top - 5.5f), (inner, top + 1)), ears != 0 ? ears : fur);
                p.Fill(Poly((outer - side * 1.5f, top + 3), (tip - side * 0.3f, top - 3), (inner + side * 2.2f, top + 2)), Palette.Pink2);
            }
            Mask head = Ellipse(left, top, right, bottom);
            p.Blob(head, fur);

            int eyeY = top + (int)Math.Round(h * 0.48f);
            int gap = (int)Math.Round(w * 0.2f);
            int l = c - gap - 1, r = c + gap;   // each eye's left column; open eyes are two wide
            if (muzzle != 0)
            {
                p.Fill(head.Minus(head.Edge()).And(Ellipse(c - 3, eyeY + 1, c + 3, bottom + 1)), muzzle);
            }
            switch (eyes)
            {
                case Eyes.Iris:
                    foreach (int x in new[] { c - gap - 2, c + gap })
                    {
                        p.Fill(Rect(x, eyeY, x + 2, eyeY + 2), iris);
                        p.Dots(Palette.Ink, (x + 1, eyeY + 1), (x + 1, eyeY + 2));
                        p[x, eyeY] = Palette.White;
                    }
                    break;
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
            (int c, _) = CatHead(p, 16, 20, 34, 35, Palette.Ink2, Eyes.Iris);
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

        // A pillar nine pixels wide with a capital on top and maybe a letter on it, shaded down its right
        // side.
        private static void Pillar(Picture p, int left, uint fill, uint shade, uint letter, string text)
        {
            p.Blob(Rect(left, 6, left + 8, 72), fill, Palette.Ink, border: false);
            p.Fill(Rect(left + 7, 7, left + 7, 71), shade);
            p.Blob(Rect(left - 1, 2, left + 9, 6), fill, Palette.Ink, border: false);
            p.Fill(Rect(left, 5, left + 8, 5), shade);
            if (text.Length > 0)
            {
                TarotFont.Draw(p, text, left + 2, 18, letter);
            }
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

        // ---- V The Hierophant: a lilac-point cat in a triple crown and red robes gives a lucky-cat
        // blessing between two grey pillars, holding a triple cross; two kittens sit before it, the crossed
        // keys between them ----

        private static Picture Hierophant()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav1), (0.55f, Palette.Lav1), (1f, Palette.Lav2));
            Sparkles(p, Palette.White, (12, 3), (35, 4));
            Pillar(p, 0, Palette.Lav2, Palette.Lav3, 0, "");
            Pillar(p, 41, Palette.Lav2, Palette.Lav3, 0, "");

            // The floor: pale tiles.
            Mask floor = Where((x, y) => y >= 58);
            p.Fill(floor, Palette.Lav2);
            p.Fill(floor.And(Where((x, y) => (x / 5 + (y - 58) / 4) % 2 == 0)), Palette.Lav1);
            p.Run(0, 58, W, Palette.Lav3);

            // The cat: robe and white stole, then its head and crown.
            Mask robe = Ellipse(12, 31, 38, 47).Or(Poly((13, 39), (37, 39), (41, 66), (9, 66)));
            p.Blob(robe, Palette.Pink4);
            p.Blob(Line(3f, (21, 34), (19.5f, 66)).Or(Line(3f, (29, 34), (30.5f, 66))).And(robe.Minus(robe.Edge())), Palette.White);
            foreach ((int x, int y) in new[] { (19, 45), (29, 45), (19, 53), (29, 53) })
            {
                p.Stamp(Sprites.Spark3, x, y, Palette.Butter3);
            }
            (int c, _) = CatHead(p, 17, 20, 33, 34, Palette.Cream, Eyes.Iris, iris: Palette.Blue3, ears: Palette.Lav3, muzzle: Palette.Lav2);
            p.Stamp(Sprites.Tiara, c - 5, 8, Key(('k', Palette.Ink), ('w', Palette.White), ('y', Palette.Butter2), ('r', Palette.Pink3), ('b', Palette.Blue2)));

            // A raised paw blessing like a lucky cat, toe beans showing; the other paw holds the triple cross.
            p.Blob(Line(5f, (15, 41), (11.5f, 33)), Palette.Pink4);
            p.Blob(Ellipse(7, 25, 14, 33), Palette.Cream);
            p.Fill(Rect(9, 30, 12, 31), Palette.Pink2);
            p.Dots(Palette.Pink2, (8, 28), (10, 27), (11, 27), (13, 28));
            Mask staff = Line(2f, (40, 11), (40, 60)).Or(Line(2f, (38, 13), (42, 13))).Or(Line(2f, (37, 17), (43, 17))).Or(Line(2f, (36, 21), (44, 21)));
            p.Blob(staff, Palette.Butter3);
            p.Blob(Line(5f, (35, 41), (38.5f, 45)), Palette.Pink4);
            p.Blob(Ellipse(37, 42, 42, 47), Palette.Cream);

            // The crossed keys, gold and silver, and the two kittens.
            CrossedKeys(p, 25, 64);
            KittenBack(p, 3, 57, Palette.Peach2, Palette.Pink3, Palette.White, tailRight: false);
            KittenBack(p, 36, 57, Palette.Lav1, Palette.White, Palette.Butter3, tailRight: true);
            return p;
        }

        // Two keys crossed over a point: gold from top left to bottom right, silver the other way.
        private static void CrossedKeys(Picture p, int cx, int cy)
        {
            foreach ((int dir, uint colour) in new[] { (-1, Palette.Lav1), (1, Palette.Butter2) })
            {
                float bx = cx - dir * 5.5f, by = cy - 5.5f, tx = cx + dir * 5, ty = cy + 5;
                Mask key = Line(2.4f, (bx, by), (tx, ty))
                    .Or(Circle(bx, by, 2.8f))
                    .Or(Line(2.4f, (tx - dir * 2, ty), (tx - dir * 2, ty + 2.5f)));
                p.Blob(key, colour);
                p.Fill(Circle(bx, by, 0.9f), Palette.Ink);
            }
        }

        // A kitten seen from behind in a little cape with dots on it: (x, y) is the top left of its head,
        // which is eleven pixels across.
        private static void KittenBack(Picture p, int x, int y, uint fur, uint cape, uint dots, bool tailRight)
        {
            int side = tailRight ? 1 : -1;
            float root = tailRight ? x + 10 : x;
            p.Blob(Line(2.6f, (root, y + 15), (root + side * 3, y + 12), (root + side * 3, y + 8)), fur);
            Mask body = Poly((x + 1, y + 7), (x + 9, y + 7), (x + 12, y + 17), (x - 2, y + 17));
            p.Blob(body, cape);
            p.Fill(body.Minus(body.Edge()).And(Where((px, py) => (px + py * 2) % 4 == 0 && py % 3 == 0)), dots);
            p.Blob(Poly((x - 0.5f, y + 5), (x + 0.5f, y - 3), (x + 4.5f, y + 1)).Or(Poly((x + 10.5f, y + 5), (x + 9.5f, y - 3), (x + 5.5f, y + 1))), fur);
            p.Blob(Ellipse(x, y, x + 10, y + 9), fur);
        }

        // ---- VI The Lovers: a pink cat and a blue cat sit side by side, their tails making a heart between
        // them; an angel cat blesses them from the clouds in front of the sun; an apple tree with a little
        // snake on one side, a tree of flames on the other ----

        private static Picture Lovers()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Butter1), (0.3f, Palette.Blue1), (0.65f, Palette.Pink1), (1f, Palette.Pink1));
            Sun(p, 25, 9, 9.5f);

            // The angel: wings, robe, head and halo, on a cloud.
            Mask wing = Poly((21, 15), (15, 8), (7, 3), (2, 5), (3, 9), (6, 12), (4, 15), (8, 18), (12, 18), (17, 21), (21, 20));
            Mask wings = wing.Or(Mirror(wing));
            p.Blob(wings, Palette.White);
            foreach ((float x0, float y0, float x1, float y1) in new[] { (19f, 15f, 6f, 7f), (19f, 17f, 7f, 14f), (19f, 19f, 11f, 18f) })
            {
                Mask feather = Line(1f, (x0, y0), (x1, y1));
                p.Fill(feather.Or(Mirror(feather)).And(wings.Minus(wings.Edge())), Palette.Lav2);
            }
            p.Blob(Poly((21, 15), (29, 15), (31, 25), (19, 25)), Palette.Pink2);
            CatHead(p, 19, 6, 31, 17, Palette.White, Eyes.Happy);
            Cloud(p, Palette.White, Palette.Lav2, (6, 26, 3.5f), (12, 24, 4.5f), (19, 26, 4f), (25, 24, 5f), (31, 26, 4f), (38, 24, 4.5f), (44, 26, 3.5f));

            // The grass and the trees.
            p.Fill(Where((x, y) => y >= 50), Palette.Mint2);
            p.Run(0, 50, W, Palette.Mint3);

            p.Blob(Rect(4, 36, 6, 52), Palette.Peach3);
            Mask apples = Circle(4, 30, 7f).Or(Circle(9, 34, 5f)).Or(Circle(1, 37, 4.5f));
            p.Blob(apples, Palette.Mint3);
            foreach ((int x, int y) in new[] { (2, 28), (7, 31), (3, 34), (6, 25) })
            {
                p.Fill(Rect(x, y, x + 1, y + 1), Palette.Pink3);
                p[x, y] = Palette.White;
            }
            p.Blob(Line(2.2f, (11, 27), (14, 30), (12, 33), (14, 35.5f)).Or(Circle(14.5f, 36.5f, 1.9f)), Palette.Mint2);
            p.Dots(Palette.Ink, (15, 36));
            p.Dots(Palette.Pink3, (15, 39));

            p.Blob(Rect(43, 36, 45, 52), Palette.Peach3);
            foreach ((float x, float y) in new[] { (44f, 23f), (40f, 28f), (48f, 28f), (37f, 33f), (44f, 31f), (50f, 34f), (41f, 36f), (47f, 37f) })
            {
                Mask flame = Circle(x, y + 1.5f, 2.4f).Or(Poly((x - 2.3f, y + 1.5f), (x, y - 3.5f), (x + 2.3f, y + 1.5f)));
                p.Blob(flame, Palette.Peach2, Palette.Peach4);
                p[(int)x, (int)y + 2] = Palette.Butter2;
            }

            // The two cats, tails first: they twist round each other between the cats and make a heart
            // above them.
            Mask tail = Line(2.6f, (19, 63), (28, 56), (22, 48), (25, 41), (21.5f, 37), (19.5f, 32.5f), (21, 29), (23.5f, 29), (25, 31.5f));
            p.Blob(tail, Palette.Pink2);
            p.Blob(Mirror(tail), Palette.Blue2);
            p.Blob(tail.And(Where((x, y) => y >= 49 && y <= 55)), Palette.Pink2);
            SittingCat(p, 11, 39, Palette.Pink2, Eyes.Happy);
            SittingCat(p, 39, 39, Palette.Blue2, Eyes.Happy);
            GroundBits(p, new[] { (24, 66), (2, 69), (45, 68) }, new[] { (29, 69), (19, 69), (48, 63) });
            return p;
        }

        // A cat sitting up and facing you, its head's middle column at c and the top of its head (not
        // counting the ears) at top: head 17 pixels across, body below, front paws at the bottom.
        private static (int c, int eyeY) SittingCat(Picture p, int c, int top, uint fur, Eyes eyes, uint belly = 0)
        {
            Mask body = Ellipse(c - 9, top + 10, c + 9, top + 30);
            p.Blob(body, fur);
            if (belly != 0)
            {
                p.Fill(Ellipse(c - 4, top + 15, c + 4, top + 29), belly);
            }
            p.Blob(Ellipse(c - 6, top + 26, c - 1, top + 30), fur);
            p.Blob(Ellipse(c + 1, top + 26, c + 6, top + 30), fur);
            return CatHead(p, c - 8, top, c + 8, top + 14, fur, eyes);
        }

        // The mask flipped left to right across the picture's middle.
        private static Mask Mirror(Mask m) => Where((x, y) => m[W - 1 - x, y]);

        // ---- VII The Chariot: a silver tabby in blue armour with moons on its shoulders rides a golden
        // chariot under a canopy of stars, wand in paw; a dark sphinx cat and a white one lie in front;
        // castle towers behind ----

        private static Picture Chariot()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Blue1), (0.45f, Palette.Butter1), (1f, Palette.Butter1));

            // Castle towers with pink roofs, and the ground.
            foreach ((int l, int t, int r) in new[] { (-1, 22, 8), (42, 26, 51) })
            {
                p.Blob(Rect(l, t, r, 60), Palette.Lav1, Palette.Lav3, border: false);
                p.Fill(Where((x, y) => y == t + 6 && x > l && x < r && x % 3 == 0).Or(Where((x, y) => y == t + 14 && x > l && x < r && x % 3 == 1)), Palette.Lav3);
                Mask roof = Poly((l - 1, t + 0.5f), ((l + r) / 2f, t - 10), (r + 1, t + 0.5f));
                p.Blob(roof, Palette.Pink3, Palette.Pink4, border: false);
            }
            p.Fill(Where((x, y) => y >= 56), Palette.Mint2);
            p.Run(0, 56, W, Palette.Mint3);

            // The canopy of stars on its poles.
            p.Blob(Rect(9, 10, 10, 44).Or(Rect(39, 10, 40, 44)), Palette.Butter3);
            Mask canopy = Rect(6, 4, 43, 11);
            for (int x = 8; x <= 41; x += 5)
            {
                canopy = canopy.Or(Circle(x + 0.5f, 11, 2.6f));
            }
            p.Blob(canopy, Palette.Blue3);
            p.Fill(canopy.Minus(canopy.Edge()).And(Where((x, y) => (x * 5 + y * 3) % 11 == 0)), Palette.White);
            p.Fill(Rect(6, 4, 43, 5).Minus(canopy.Edge()), Palette.Blue4);

            // The charioteer: armour with moons on the shoulders, head and crown, wand.
            Mask armour = Ellipse(13, 32, 37, 50);
            p.Blob(armour, Palette.Blue2);
            p.Fill(armour.Minus(armour.Edge()).And(Rect(22, 34, 28, 50)), Palette.Blue1);
            foreach (int x in new[] { 15, 30 })
            {
                p.Blob(Ellipse(x, 33, x + 5, 38).Minus(Ellipse(x + 2, 32, x + 7, 37)), Palette.Butter2);
            }
            (int c, int eyeY) = CatHead(p, 17, 19, 33, 33, Palette.White, Eyes.Open);
            foreach ((int x, int y) in new[] { (c - 1, 20), (c + 1, 20), (c, 21), (c - 7, eyeY + 1), (c - 7, eyeY + 2), (c + 7, eyeY + 1), (c + 7, eyeY + 2) })
            {
                p[x, y] = Palette.Lav2;
            }
            p.Stamp(Sprites.Crown, c - 5, 13, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));

            // The chariot: wheels, a golden box with a winged heart on the front, the cat's paws on its edge.
            foreach (float x in new[] { 6.5f, 43.5f })
            {
                Mask rim = Circle(x, 49, 5.8f).Minus(Circle(x, 49, 3.9f));
                Mask spokes = Line(1.2f, (x - 4, 49), (x + 4, 49)).Or(Line(1.2f, (x, 45), (x, 53)))
                    .Or(Line(1.2f, (x - 3, 46), (x + 3, 52))).Or(Line(1.2f, (x - 3, 52), (x + 3, 46)));
                p.Fill(spokes.And(Circle(x, 49, 4.2f)), Palette.Peach4);
                p.Blob(rim, Palette.Peach3);
                p.Blob(Circle(x, 49, 1.5f), Palette.Butter2);
            }
            Mask box = Rect(9, 41, 41, 55);
            p.Blob(box, Palette.Butter2);
            p.Fill(Rect(10, 52, 40, 54), Palette.Butter3);
            p.Fill(Rect(10, 42, 40, 42), Palette.Butter1);
            Picture wing = CardFaces.Sprite(Sprites.Wing, Sprites.Two(Palette.White));
            p.Paste(wing, 14, 45);
            p.Paste(wing.Mirrored(), 30, 45);
            p.Stamp(Sprites.Heart8, 21, 44, Sprites.Two(Palette.Pink3));
            p.Blob(Ellipse(17, 38, 22, 43), Palette.White);
            p.Blob(Ellipse(28, 38, 33, 43), Palette.White);

            // The two sphinx cats lying in front, dark and white.
            SphinxCat(p, 13, 54, Palette.Ink2, Eyes.Iris);
            SphinxCat(p, 37, 54, Palette.White, Eyes.Open);
            return p;
        }

        // A cat lying like a sphinx and facing you: head (thirteen pixels across, its top at top) over
        // its front paws, the loaf of its body behind.
        private static void SphinxCat(Picture p, int c, int top, uint fur, Eyes eyes)
        {
            p.Blob(Ellipse(c - 10, top + 7, c + 10, top + 18), fur);
            p.Blob(Ellipse(c - 7, top + 11, c - 2, top + 15), fur);
            p.Blob(Ellipse(c + 2, top + 11, c + 7, top + 15), fur);
            CatHead(p, c - 6, top, c + 6, top + 10, fur, eyes);
        }

        // ---- IX The Hermit: an old cat in a grey hooded cloak (ears on the hood), with long white brows and
        // a beard, stands on a snowy peak at night holding up a lantern with a star in it, leaning on a
        // staff ----

        private static Picture Hermit()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Ink2), (0.35f, Palette.Lav4), (1f, Palette.Lav4));
            Sparkles(p, Palette.White, (40, 4), (29, 10), (44, 19), (3, 6), (18, 3));
            p.Dots(Palette.Lav1, (11, 3), (24, 6), (35, 9), (6, 14), (47, 11), (20, 16), (45, 29), (2, 24));

            // Far peaks below, then the snowy top it stands on.
            Mask far = Poly((-2, 64), (5, 52), (11, 57), (19, 49), (28, 58), (38, 48), (46, 55), (52, 51), (52, 66), (-2, 66));
            p.Fill(far, Palette.Lav3);
            (int x, int y)[] tops = { (5, 52), (19, 49), (38, 48), (52, 51) };
            p.Fill(far.And(Where((x, y) => Array.Exists(tops, k => y <= k.y + 2 + (x + y) % 2 && Math.Abs(x - k.x) <= 4))), Palette.Lav1);
            p.Fill(far.Edge(true), Palette.Lav4);
            Mask snow = Poly((-2, 73), (-2, 66), (10, 62), (25, 60), (40, 62), (52, 66), (52, 73));
            p.Fill(snow, Palette.White);
            p.Fill(snow.Minus(snow.Edge(true)).Rows(y => y >= 68), Palette.Lav1);
            p.Fill(snow.Edge(true), Palette.Lav2);

            // The lantern's glow.
            p.Fill(Circle(9, 37, 11.5f).And(Where((x, y) => (x + y) % 2 == 0)), Palette.Lav2);
            p.Fill(Circle(9, 37, 8.5f), Palette.Lav2);
            p.Fill(Circle(9, 37, 8.5f).And(Where((x, y) => (x + y) % 2 == 0)), Palette.Butter1);
            p.Fill(Circle(9, 37, 6f), Palette.Butter1);

            // The staff, the cloak and hood, the head with its ears through the hood.
            p.Blob(Line(2f, (38, 21), (40, 64)), Palette.Peach3);
            Mask cloak = Poly((18, 28), (32, 28), (38, 61), (12, 61)).Or(Ellipse(12, 56, 38, 65));
            p.Blob(cloak, Palette.Lav2);
            p.Fill(cloak.Minus(cloak.Edge()).And(Where((x, y) => x >= 31 || y >= 62)), Palette.Lav3);
            p.Blob(Ellipse(14, 15, 36, 38), Palette.Lav2);
            (int c, int eyeY) = CatHead(p, 18, 19, 32, 32, Palette.Cream, Eyes.Sleepy, ears: Palette.Lav2);
            p.Fill(Ellipse(14, 15, 36, 38).Edge().Rows(y => y >= 30), Palette.Ink);
            p.Blob(Poly((c - 4, eyeY + 5), (c + 4, eyeY + 5), (c, eyeY + 14)), Palette.White);

            // The paw holding the lantern up, and the paw on the staff.
            p.Blob(Line(4.5f, (18, 38), (12.5f, 32)), Palette.Lav2);
            p.Blob(Ellipse(8, 28, 13, 33), Palette.Lav1);
            p.Stamp(Sprites.Lantern, 5, 32, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('w', Palette.White)));
            p.Blob(Ellipse(35, 40, 40, 45), Palette.Lav1);
            return p;
        }

        // ---- X Wheel of Fortune: a golden wheel lettered T A R O with hearts between, a blue sphinx cat
        // with a sword on top, a little snake going down one side and a puppy coming up the other; in the
        // corners on clouds, a cat angel, a bird, a calf and a lion cub ----

        private static Picture Wheel()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Blue2), (0.22f, Palette.Blue1), (1f, Palette.Blue1));
            Sparkles(p, Palette.White, (16, 2), (31, 1), (1, 24), (45, 22), (14, 63), (34, 63));

            // The snake slides down behind the wheel's left side.
            p.Blob(Line(3f, (12, 27), (6, 30), (4, 36), (7, 42), (3, 47), (5, 51)).Or(Circle(5, 53.5f, 2.6f)), Palette.Mint2);
            p.Dots(Palette.Ink, (4, 53), (7, 53));
            p.Dots(Palette.Pink3, (5, 57), (6, 57));

            // The wheel: a golden ring with T A R O and hearts, a pink middle with spokes, a hub.
            const float cx = 24.5f, cy = 41.5f;
            p.Blob(Circle(cx, cy, 17.5f), Palette.Butter2, Palette.Ink, border: false);
            Mask middle = Circle(cx, cy, 7.6f);
            p.Blob(middle, Palette.Pink2);
            for (int k = 0; k < 8; k++)
            {
                double angle = k * Math.PI / 4;
                float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
                p.Fill(Line(1f, (cx, cy), (cx + dx * 7, cy + dy * 7)).And(middle.Minus(middle.Edge())), Palette.Pink3);
            }
            p.Blob(Circle(cx, cy, 2.4f), Palette.Butter3);
            TarotFont.Draw(p, "T", 22, 26, Palette.Ink);
            TarotFont.Draw(p, "A", 35, 38, Palette.Ink);
            TarotFont.Draw(p, "R", 22, 51, Palette.Ink);
            TarotFont.Draw(p, "O", 10, 38, Palette.Ink);
            Hearts(p, Palette.Pink3, (14, 31), (31, 31), (14, 49), (31, 49));

            // The sphinx cat on top with its sword.
            p.Blob(Line(2f, (35, 7), (35, 20)), Palette.White);
            p.Blob(Rect(32, 17, 38, 18), Palette.Butter3);
            p.Blob(Ellipse(17, 13, 31, 23), Palette.Blue2);
            CatHead(p, 18, 5, 30, 15, Palette.Blue2, Eyes.Open);
            p.Blob(Ellipse(31, 17, 35, 21), Palette.Blue2);

            // The puppy coming up the right side, paws on the rim.
            p.Blob(Ellipse(41, 46, 49, 54), Palette.Peach2);
            p.Blob(Ellipse(39, 46, 42, 52), Palette.Peach3);
            p.Blob(Ellipse(48, 46, 51, 52), Palette.Peach3);
            p.Dots(Palette.Ink, (43, 49), (47, 49), (45, 51));
            p.Dots(Palette.Pink3, (45, 53));
            p.Blob(Ellipse(34, 52, 38, 56), Palette.Peach2);
            p.Blob(Ellipse(42, 54, 46, 58), Palette.Peach2);

            FourCreatures(p);
            return p;
        }

        // The four in the corners of the Wheel and the World, each on a cloud: a cat angel, a bird, a
        // calf and a lion cub.
        private static void FourCreatures(Picture p)
        {
            Cloud(p, Palette.White, Palette.Lav2, (2, 12, 3f), (7, 11, 3.5f), (11, 13, 2.5f));
            p.Stamp(Sprites.TinyCat, 1, 3, Key(('k', Palette.Ink), ('e', Palette.Pink2), ('w', Palette.White), ('b', Palette.Pink2), ('n', Palette.Pink4)));
            p.Fill(Ellipse(1, 0, 9, 2).Minus(Ellipse(3, 1, 7, 1)), Palette.Butter2);
            Cloud(p, Palette.White, Palette.Lav2, (39, 12, 2.5f), (43, 11, 3.5f), (48, 12, 3f));
            p.Stamp(Sprites.TinyBird, 39, 3, Key(('k', Palette.Ink), ('b', Palette.Blue2), ('B', Palette.Blue3), ('y', Palette.Butter3)));
            Cloud(p, Palette.White, Palette.Lav2, (2, 71, 3.5f), (7, 70, 4f), (12, 72, 3f));
            p.Stamp(Sprites.TinyCalf, 1, 62, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('w', Palette.White), ('p', Palette.Pink2)));
            Cloud(p, Palette.White, Palette.Lav2, (38, 72, 3f), (43, 70, 4f), (48, 71, 3.5f));
            p.Stamp(Sprites.TinyLion, 38, 60, Key(('k', Palette.Ink), ('o', Palette.Peach2), ('y', Palette.Butter2), ('n', Palette.Pink4)));
        }

        // ---- XI Justice: a tuxedo cat in a red robe and a crown sits before a purple veil between two
        // pillars, a sword raised in one paw and golden scales with a heart in each pan in the other ----

        private static Picture Justice()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Butter2), (0.15f, Palette.Butter1), (1f, Palette.Butter1));
            Mask veil = Rect(8, 9, 41, 72);
            p.Fill(veil, Palette.Lav3);
            p.Fill(veil.And(Where((x, y) => x % 6 == 1)), Palette.Lav4);
            p.Fill(veil.And(Where((x, y) => x % 6 == 3)), Palette.Lav2);
            p.Blob(Rect(6, 7, 43, 9), Palette.Butter3);
            Pillar(p, 0, Palette.Lav1, Palette.Lav2, 0, "");
            Pillar(p, 41, Palette.Lav1, Palette.Lav2, 0, "");
            p.Blob(Rect(-1, 63, 50, 73), Palette.Lav2, Palette.Ink, border: false);
            p.Fill(Rect(0, 64, 49, 64), Palette.Lav1);

            // The cat: robe with a white bib, the tuxedo head and its crown.
            Mask robe = Ellipse(12, 31, 38, 47).Or(Poly((13, 39), (37, 39), (40, 67), (10, 67)));
            p.Blob(robe, Palette.Pink4);
            Mask cape = robe.Minus(robe.Edge()).And(Ellipse(10, 29, 40, 45)).And(Where((x, y) => y <= 40));
            p.Fill(cape, Palette.Mint3);
            p.Fill(cape.And(Where((x, y) => y == 40)), Palette.Mint4);
            p.Blob(Poly((21, 33), (29, 33), (25, 42)), Palette.White);
            p.Fill(Line(1f, (25, 44), (25, 66)), Palette.Pink3);
            p.Fill(robe.Minus(robe.Edge()).And(Where((x, y) => y >= 64 && y <= 65)), Palette.Butter3);
            p.Blob(Rect(23, 40, 27, 43), Palette.Butter2);
            (int c, _) = CatHead(p, 17, 20, 33, 34, Palette.Ink2, Eyes.Iris, muzzle: Palette.White);
            p.Stamp(Sprites.Crown, c - 5, 14, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));

            // The sword raised in one paw.
            p.Blob(Line(2f, (8, 6), (8, 32)), Palette.White);
            p.Blob(Line(2f, (5, 32), (11, 32)), Palette.Butter3);
            p.Blob(Line(4.5f, (15, 43), (9.5f, 37)), Palette.Pink4);
            p.Blob(Ellipse(6, 33, 11, 38), Palette.White);

            // The scales held lower in the other: rod, beam, strings and two pans, a heart in each.
            p.Blob(Line(1.8f, (40, 27), (40, 47)).Or(Line(1.8f, (34, 30), (46, 30))), Palette.Butter3);
            p.Blob(Circle(40, 26, 1.6f), Palette.Butter2);
            foreach (int x in new[] { 34, 46 })
            {
                p.Fill(Line(1f, (x, 31), (x - 2.5f, 37)).Or(Line(1f, (x, 31), (x + 2.5f, 37))), Palette.Ink2);
                p.Stamp(Sprites.Heart5, x - 2, 33, Palette.Pink3);
                p.Blob(Ellipse(x - 4, 36, x + 4, 40).And(Where((px, py) => py >= 37)), Palette.Butter2);
            }
            p.Blob(Line(4.5f, (34, 48), (38, 47)), Palette.Pink4);
            p.Blob(Ellipse(37, 44, 42, 49), Palette.White);
            return p;
        }

        // ---- XII The Hanged Man: a ginger cat in a blue scarf hangs happily upside down from a leafy
        // branch by its back paws, one leg crossed behind the other, a golden glow round its head ----

        private static Picture HangedMan()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav1), (0.5f, Palette.Blue1), (1f, Palette.Blue1));
            Sparkles(p, Palette.White, (11, 14), (37, 17), (5, 40), (42, 44), (13, 58));
            p.Fill(Where((x, y) => y >= 66), Palette.Mint2);
            p.Run(0, 66, W, Palette.Mint3);
            GroundBits(p, new[] { (4, 68), (40, 69) }, new[] { (10, 68), (30, 69), (45, 67) });

            // The living wood: two posts and the bar across, with leaves sprouting.
            p.Blob(Rect(3, 5, 6, 67).Or(Rect(43, 5, 46, 67)).Or(Rect(3, 5, 46, 8)), Palette.Peach3);
            p.Fill(Rect(4, 6, 45, 6), Palette.Peach2);
            foreach ((int x, int y) in new[] { (8, 2), (16, 2), (31, 2), (40, 2), (0, 20), (46, 27), (0, 43), (46, 50), (0, 58), (46, 12) })
            {
                p.Fill(Ellipse(x, y, x + 3, y + 2), Palette.Mint3);
                p[x + 1, y] = Palette.Mint2;
                p[x + 2, y + 2] = Palette.Mint4;
            }

            // The glow round where its head will hang.
            p.Fill(Circle(24.5f, 49, 12.5f).And(Where((x, y) => (x + y) % 2 == 0)), Palette.Butter1);
            p.Fill(Circle(24.5f, 49, 12.5f).Minus(Circle(24.5f, 49, 11.3f)), Palette.Butter2);

            // The cat, drawn standing on the bar and then turned upside down.
            var q = new Picture(W, H);
            uint fur = Palette.Peach1;
            q.Blob(Line(2.6f, (33, 46), (37, 41), (37, 35)), fur);
            q.Blob(Line(4f, (28, 48), (32, 55), (26, 58)), fur);
            q.Blob(Line(4f, (22.5f, 48), (22.5f, 63)), fur);
            q.Blob(Ellipse(20, 61, 25, 66), fur);
            q.Blob(Ellipse(16, 27, 33, 51), fur);
            q.Fill(Ellipse(20, 33, 29, 49), Palette.Cream);
            foreach (int y in new[] { 30, 34, 38 })
            {
                q.Run(17, y, 2, Palette.Peach2);
                q.Run(31, y, 2, Palette.Peach2);
            }
            q.Blob(Line(4f, (18, 33), (14, 25)), fur);
            q.Blob(Line(4f, (31, 33), (35, 25)), fur);
            q.Blob(Ellipse(11, 21, 16, 26), fur);
            q.Blob(Ellipse(33, 21, 38, 26), fur);
            q.Blob(Rect(18, 29, 31, 32), Palette.Blue2);
            q.Blob(Rect(27, 31, 30, 37), Palette.Blue2);
            (int c, _) = CatHead(q, 16, 16, 32, 30, fur, Eyes.Happy);
            q.Dots(Palette.Peach2, (c - 1, 17), (c + 1, 17), (c, 18));
            p.Paste(q.Turned(), 0, 0);
            return p;
        }

        // ---- XIII Death: a little reaper cat in a black hood with ears and a bone-print top rides a white
        // pony at dawn, holding a black banner with a white rose; the sun comes up between two towers ----

        private static Picture Death()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Lav3), (0.35f, Palette.Pink2), (0.62f, Palette.Peach1), (1f, Palette.Peach1));
            Sparkles(p, Palette.White, (27, 3), (43, 9), (21, 13));

            // The sun coming up between two towers, and the ground.
            Sun(p, 44.5f, 52, 5.5f);
            foreach (int x in new[] { 37, 47 })
            {
                p.Blob(Rect(x, 43, x + 3, 56).Or(Poly((x - 0.5f, 43.5f), (x + 1.5f, 38), (x + 3.5f, 43.5f))), Palette.Lav3);
            }
            p.Fill(Where((x, y) => y >= 55), Palette.Mint3);
            p.Run(0, 55, W, Palette.Mint4);
            foreach ((int x, int y) in new[] { (3, 62), (40, 60), (45, 67), (21, 67), (33, 64) })
            {
                p.Stamp(Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Butter3 : Palette.White);
            }

            // The banner: a pole and a black flag with a white rose, flying back.
            p.Blob(Line(2f, (12, 2), (12, 40)), Palette.Peach3);
            Mask flag = Poly((-1, 3), (11, 3), (11, 17), (-1, 17), (2, 10));
            p.Blob(flag, Palette.Ink2);
            p.Stamp(Sprites.Rose, 3, 7, Key(('k', Palette.Ink), ('w', Palette.White), ('y', Palette.Butter3)));

            // The pony, a round little white one with a pink mane and tail.
            p.Paste(Pony(Palette.Pink2), 0, 0);

            // The saddle blanket, then the rider: body with bones, the hood with ears, a white face.
            Mask blanket = Rect(13, 36, 27, 44);
            p.Blob(blanket, Palette.Ink2);
            p.Fill(blanket.Minus(blanket.Edge()).And(Where((x, y) => y == 43 && x % 2 == 0)), Palette.White);
            p.Blob(Ellipse(14, 26, 26, 41), Palette.Ink2);
            foreach (int y in new[] { 31, 34, 37 })
            {
                p.Run(18, y, 5, Palette.White);
            }
            p.Blob(Ellipse(13, 14, 27, 31), Palette.Ink2);
            CatHead(p, 15, 18, 25, 27, Palette.White, Eyes.Open, ears: Palette.Ink2);
            p.Blob(Ellipse(10, 31, 14, 35), Palette.White);
            p.Blob(Ellipse(22, 37, 26, 41), Palette.White);
            return p;
        }

        // A round little white pony walking right, with its mane and tail in the given colour, on its own
        // see-through picture (paste it where it goes): hooves at y 60, nose at x 45.
        private static Picture Pony(uint mane)
        {
            var p = new Picture(W, H);
            uint white = Palette.White;
            p.Blob(Line(3.6f, (7, 40), (2.5f, 46), (2.5f, 54)), mane);
            foreach (int x in new[] { 8, 14, 23, 29 })
            {
                p.Blob(Rect(x, 47, x + 3, 61), white);
                p.Fill(Rect(x + 1, 59, x + 2, 60), Palette.Lav2);
            }
            p.Blob(Ellipse(5, 35, 34, 52), white);
            p.Blob(Poly((26, 45), (29, 30), (37, 28), (36, 46)).Or(Ellipse(29, 21, 41, 33)).Or(Ellipse(36, 25, 45, 35)), white);
            p.Blob(Poly((31.5f, 25), (32.5f, 17), (35.5f, 23)), white);
            Mask locks = Circle(31, 22, 2.4f).Or(Circle(29.5f, 26, 2.4f)).Or(Circle(28.5f, 30, 2.4f)).Or(Circle(27.5f, 34, 2.4f)).Or(Circle(27, 38, 2.2f));
            p.Blob(locks, mane);
            p.Fill(Rect(38, 27, 39, 28), Palette.Ink);
            p[38, 27] = Palette.White;
            p.Dots(Palette.Ink, (43, 30));
            p.Dots(Palette.Pink2, (40, 31), (41, 31));
            return p;
        }

        // ---- XIV Temperance: a silver angel cat with pink wings pours water from one teacup into the
        // other, one foot in a pool and one on the bank among irises; a path leads off to the hills and a
        // glowing crown of light ----

        private static Picture Temperance()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Blue1), (0.55f, Palette.Butter1), (1f, Palette.Butter1));

            // Far hills with the light between them, and the path leading there.
            Mask hills = Poly((26, 58), (35, 46), (40, 50), (46, 44), (53, 50), (53, 58));
            p.Fill(hills, Palette.Lav2);
            p.Fill(hills.Edge(true), Palette.Lav3);
            p.Fill(Circle(40.5f, 48, 3.2f).Minus(hills).And(Where((x, y) => y <= 49)), Palette.Butter2);
            p.Fill(Where((x, y) => y >= 56), Palette.Mint2);
            p.Run(0, 56, W, Palette.Mint3);
            p.Fill(Poly((40, 50), (42, 50), (46, 72), (33, 72)).And(Where((x, y) => y >= 56)), Palette.Butter1);

            // The pool her front foot stands in.
            Mask pool = Ellipse(-12, 58, 27, 82);
            p.Fill(pool, Palette.Blue2);
            p.Fill(pool.Edge(true), Palette.Blue3);
            p.Run(3, 66, 5, Palette.Blue3);
            p.Run(10, 69, 6, Palette.Blue3);

            // Wings, robe, head.
            Mask wing = Poly((22, 30), (16, 18), (8, 10), (3, 12), (4, 18), (2, 24), (6, 30), (5, 36), (10, 39), (16, 38), (21, 36));
            Mask wings = wing.Or(Mirror(wing));
            p.Blob(wings, Palette.Pink2);
            foreach ((float x0, float y0, float x1, float y1) in new[] { (20f, 28f, 8f, 15f), (20f, 31f, 6f, 26f), (20f, 34f, 9f, 36f) })
            {
                Mask feather = Line(1f, (x0, y0), (x1, y1));
                p.Fill(feather.Or(Mirror(feather)).And(wings.Minus(wings.Edge())), Palette.Pink3);
            }
            Mask robe = Ellipse(15, 26, 35, 40).Or(Poly((16, 33), (34, 33), (37, 63), (13, 63)));
            p.Blob(robe, Palette.White);
            p.Fill(robe.Minus(robe.Edge()).And(Where((x, y) => x >= 31 && y >= 36)), Palette.Lav1);
            p.Blob(Rect(22, 45, 28, 51), Palette.White);
            p.Fill(Poly((25, 46), (27.5f, 50.5f), (22.5f, 50.5f)), Palette.Butter3);
            (int c, _) = CatHead(p, 17, 13, 33, 27, Palette.Lav1, Eyes.Sleepy);
            p.Blob(Circle(c, 16, 1.4f), Palette.Butter2);

            // Feet: one in the pool, one on the bank.
            p.Blob(Ellipse(16, 61, 21, 65), Palette.Lav1);
            p.Blob(Ellipse(28, 61, 33, 65), Palette.Lav1);
            p.Run(14, 64, 9, Palette.Blue3);

            // Irises on the bank.
            foreach ((int x, int y) in new[] { (36, 60), (44, 62), (40, 66), (47, 68) })
            {
                p.Fill(Rect(x + 1, y + 3, x + 1, y + 6), Palette.Mint4);
                p.Stamp(Sprites.Flower, x, y, letter => letter == 'y' ? Palette.Butter3 : Palette.Lav3);
            }

            // The two teacups and the water running from the high one to the low one.
            p.Blob(Line(4f, (18, 38), (13, 36)), Palette.White);
            p.Blob(Line(4f, (33, 38), (38, 44)), Palette.White);
            Mask water = Line(2.2f, (14, 31), (19, 29.5f), (24, 30.5f), (29, 34), (33, 38.5f), (36, 44));
            p.Blob(water, Palette.Blue1, Palette.Blue3);
            p.Dots(Palette.White, (19, 30), (25, 31), (30, 35), (34, 39));
            Teacup(p, 7, 30, tilted: true);
            Teacup(p, 34, 44, tilted: false);
            return p;
        }

        // A little blue teacup with a heart on it, its top-left corner at (x, y); tilted pours to the right.
        private static void Teacup(Picture p, int x, int y, bool tilted)
        {
            Mask cup = tilted
                ? Poly((x, y + 1), (x + 8, y - 1), (x + 9, y + 3), (x + 6, y + 7), (x + 2, y + 7))
                : Ellipse(x, y - 2, x + 8, y + 7).And(Where((px, py) => py >= y + 1));
            p.Blob(Circle(x + (tilted ? 1 : 8.5f), y + 3.5f, 1.8f).Minus(Circle(x + (tilted ? 1 : 8.5f), y + 3.5f, 0.6f)), Palette.Blue2);
            p.Blob(cup, Palette.Blue2);
            p.Dots(Palette.Pink3, (x + 3, y + 3), (x + 5, y + 3), (x + 4, y + 4));
        }

        // ---- XV The Devil: a pink imp cat with little horns and bat wings sits on a pedestal grinning,
        // one paw up and a torch held low in the other; two kittens with tiny horns sit below on loose
        // chains (they could slip them off), one tail tipped with a flame, one with grapes ----

        private static Picture Devil()
        {
            var p = new Picture(W, H);
            p.Gradient((0f, Palette.Ink2), (0.6f, Palette.Lav4), (1f, Palette.Lav4));
            Sparkles(p, Palette.Lav2, (5, 5), (44, 8), (3, 40), (46, 38));

            // Bat wings, the pedestal.
            Mask wing = Poly((21, 34), (14, 22), (6, 14), (2, 18), (3, 24), (1, 30), (5, 33), (4, 38), (10, 37), (14, 40), (20, 39));
            Mask wings = wing.Or(Mirror(wing));
            p.Fill(wings, Palette.Lav3);
            p.Fill(wings.Edge(true), Palette.Ink2);
            foreach ((float x0, float y0, float x1, float y1) in new[] { (19f, 33f, 6f, 15f), (19f, 35f, 2f, 25f), (19f, 37f, 6f, 34f) })
            {
                Mask bone = Line(1f, (x0, y0), (x1, y1));
                p.Fill(bone.Or(Mirror(bone)).And(wings), Palette.Lav4);
            }
            Mask pedestal = Rect(15, 48, 34, 65);
            p.Blob(pedestal, Palette.Lav3);
            p.Fill(Rect(16, 49, 33, 50), Palette.Lav2);
            p.Fill(Rect(31, 51, 33, 64), Palette.Lav4);
            p.Blob(Circle(24.5f, 57, 2.3f).Minus(Circle(24.5f, 57, 0.9f)), Palette.Butter3);

            // The imp: body, raised paw with its toe beans, the torch, the head with horns and a fang.
            uint fur = Palette.Pink3;
            p.Blob(Line(2.6f, (18, 47), (12, 48), (9.5f, 44), (10, 40)), fur);
            p.Blob(Poly((7.5f, 41), (10, 36.5f), (12.5f, 41)), fur);
            p.Blob(Ellipse(15, 29, 34, 50), fur);
            p.Fill(Ellipse(20, 35, 29, 49), Palette.Pink2);
            p.Blob(Line(4f, (17, 36), (11, 28)), fur);
            p.Blob(Ellipse(7, 22, 14, 30), fur);
            p.Fill(Rect(9, 27, 12, 28), Palette.Pink1);
            p.Dots(Palette.Pink1, (8, 25), (10, 24), (11, 24), (13, 25));
            p.Blob(Line(2f, (39, 23), (39, 37)), Palette.Peach3);
            p.Blob(Circle(39, 19, 3.6f).Or(Poly((35.5f, 19), (36.5f, 11), (38.5f, 15), (40.5f, 10), (42.5f, 19))), Palette.Peach2);
            p.Fill(Circle(39, 19.5f, 1.8f).Or(Poly((37.8f, 19), (39, 14.5f), (40.2f, 19))), Palette.Butter2);
            p.Blob(Line(4f, (32, 37), (37, 34)), fur);
            p.Blob(Ellipse(36, 31, 41, 36), fur);
            (int c, int eyeY) = CatHead(p, 17, 16, 33, 30, fur, Eyes.Iris);
            p[c + 2, eyeY + 5] = Palette.White;
            p.Stamp(Sprites.Horn, c - 4, 11, Key(('k', Palette.Ink), ('y', Palette.Butter2)));
            p.Paste(CardFaces.Sprite(Sprites.Horn, Key(('k', Palette.Ink), ('y', Palette.Butter2))).Mirrored(), c + 1, 11);

            // The two kittens on their loose chains.
            Mask chains = Line(1f, (23, 58), (16, 61), (8, 60)).Or(Line(1f, (26, 58), (33, 61), (41, 60)));
            p.Fill(chains.And(Where((x, y) => (x + y) % 2 == 0)), Palette.Butter3);
            p.Fill(chains.And(Where((x, y) => (x + y) % 2 == 1)), Palette.Lav1);
            ChainedKitten(p, 8, 52, Palette.White, flameTail: true);
            ChainedKitten(p, 41, 52, Palette.Cream, flameTail: false);
            return p;
        }

        // A small kitten sitting up with tiny horns, a loose collar and a tail tipped with a flame or with
        // grapes; c is its middle column, top the top of its head.
        private static void ChainedKitten(Picture p, int c, int top, uint fur, bool flameTail)
        {
            int side = flameTail ? -1 : 1;
            p.Blob(Line(2.4f, (c + side * 5, top + 16), (c + side * 7.5f, top + 12), (c + side * 7, top + 8)), fur);
            if (flameTail)
            {
                p.Blob(Circle(c + side * 7, top + 7, 1.8f).Or(Poly((c + side * 7 - 1.7f, top + 7), (c + side * 7, top + 3), (c + side * 7 + 1.7f, top + 7))), Palette.Peach2);
            }
            else
            {
                foreach ((float dx, float dy) in new[] { (0f, 6f), (-1.5f, 4.5f), (1.5f, 4.5f), (0f, 3f) })
                {
                    p.Blob(Circle(c + side * 7 + dx, top + dy, 1.2f), Palette.Lav3);
                }
            }
            p.Blob(Ellipse(c - 6, top + 8, c + 6, top + 21), fur);
            p.Blob(Ellipse(c - 4, top + 17, c - 1, top + 20).Or(Ellipse(c + 1, top + 17, c + 4, top + 20)), fur);
            p.Fill(Rect(c - 4, top + 9, c + 4, top + 9), Palette.Butter3);
            CatHead(p, c - 5, top, c + 5, top + 9, fur, Eyes.Open);
            p.Blob(Poly((c - 2.5f, top + 0.5f), (c - 2.5f, top - 3), (c - 0.5f, top)).Or(Poly((c + 2.5f, top + 0.5f), (c + 2.5f, top - 3), (c + 0.5f, top))), Palette.Butter2);
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
