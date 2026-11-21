using System;

namespace KittyTarot
{
    // The suits' cards: the aces (a cat's paw reaching out of a cloud with the suit's thing), the number
    // cards (patterns of the suit's little symbol on its colours) and the court cards.
    internal static partial class Scenes
    {
        private const int Wands = 0, Cups = 1, Swords = 2, Pentacles = 3;

        // The suit's background: its three sky colours from top to bottom.
        private static void SuitSky(Picture p, int suit)
        {
            p.Gradient((0f, Palette.SkyTop(suit)), (0.55f, Palette.SkyMiddle(suit)), (1f, Palette.SkyBottom(suit)));
        }

        // ---- The number cards ----

        // Where the symbols go on the cards 2 to 10, like on playing cards: left, middle and right columns.
        private const int L = 14, M = 24, R = 35;

        // The bottom middle is left for the kitten peeking up at the symbols.
        private static readonly (int x, int y)[][] PipLayouts =
        {
            null,
            null,
            new[] { (M, 18), (M, 42) },
            new[] { (L, 20), (R, 20), (M, 43) },
            new[] { (L, 18), (R, 18), (L, 44), (R, 44) },
            new[] { (L, 15), (R, 15), (M, 31), (L, 47), (R, 47) },
            new[] { (L, 13), (R, 13), (L, 35), (R, 35), (L, 57), (R, 57) },
            new[] { (L, 13), (R, 13), (M, 24), (L, 35), (R, 35), (L, 57), (R, 57) },
            new[] { (L, 13), (R, 13), (M, 24), (L, 35), (R, 35), (M, 46), (L, 57), (R, 57) },
            new[] { (L, 9), (R, 9), (L, 24), (R, 24), (M, 31), (L, 39), (R, 39), (L, 54), (R, 54) },
            new[] { (L, 9), (R, 9), (M, 16), (L, 24), (R, 24), (L, 39), (R, 39), (M, 46), (L, 54), (R, 54) },
        };

        // The kittens that peek up from the bottom of the number cards: fur, eyes, and patches.
        private static readonly (uint fur, Eyes eyes, uint patch)[] Peekers =
        {
            (Palette.White, Eyes.Open, Palette.Peach2),
            (Palette.Peach2, Eyes.Happy, 0),
            (Palette.Lav1, Eyes.Open, 0),
            (Palette.Ink2, Eyes.Iris, 0),
            (Palette.Cream, Eyes.Sleepy, 0),
            (Palette.White, Eyes.Happy, Palette.Ink2),
            (Palette.Butter1, Eyes.Open, Palette.Peach2),
            (Palette.Lav2, Eyes.Happy, 0),
            (Palette.Peach1, Eyes.Open, 0),
        };

        private static Picture NumberCard(int suit, int n)
        {
            var p = new Picture(W, H);
            SuitSky(p, suit);

            // A soft wallpaper of paw prints behind the symbols.
            for (int row = 0; row < 6; row++)
            {
                for (int col = -1; col < 4; col++)
                {
                    p.Stamp(Sprites.PawSmall, col * 14 + (row % 2) * 7 + 3, row * 13 + 2, Palette.White);
                }
            }
            foreach ((int x, int y) in PipLayouts[n])
            {
                Pip(p, suit, x, y);
            }

            // A kitten peeking up from the bottom edge at them.
            (uint fur, Eyes eyes, uint patch) = Peekers[(n - 2 + suit * 2) % Peekers.Length];
            (int c, _) = CatHead(p, 18, 60, 30, 73, fur, eyes);
            if (patch != 0)
            {
                p.Fill(Ellipse(17, 59, 22, 64).And(Ellipse(18, 60, 30, 73).Erode()), patch);
            }
            p.Blob(Ellipse(c - 11, 66, c - 6, 72).Or(Ellipse(c + 6, 66, c + 11, 72)), fur);
            return p;
        }

        // A suit's little symbol centred on (cx, cy).
        private static void Pip(Picture p, int suit, int cx, int cy)
        {
            switch (suit)
            {
                case Wands:
                    p.Stamp(Sprites.PipWand, cx - 3, cy - 6, Key(('k', Palette.Ink), ('P', Palette.Pink3), ('b', Palette.Peach3), ('m', Palette.Mint3)));
                    break;
                case Cups:
                    p.Stamp(Sprites.PipCup, cx - 5, cy - 4, Key(('k', Palette.Ink), ('T', Palette.Peach2), ('w', Palette.White), ('P', Palette.Pink3), ('b', Palette.Blue2)));
                    break;
                case Swords:
                    p.Stamp(Sprites.PipSword, cx - 3, cy - 7, Key(('k', Palette.Ink), ('w', Palette.White), ('y', Palette.Butter3), ('P', Palette.Pink3), ('p', Palette.Pink3)));
                    break;
                default:
                    p.Stamp(Sprites.PipCoin, cx - 4, cy - 4, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('w', Palette.White)));
                    break;
            }
        }

        // ---- The court cards: Page, Knight, Queen and King ----

        private static Picture Court(int suit, int rank)
        {
            var p = new Picture(W, H);
            CourtBackground(p, suit);
            switch (rank)
            {
                case 11: Page(p, suit); break;
                case 12: Knight(p, suit); break;
                case 13: Throne(p, suit, queen: true); break;
                default: Throne(p, suit, queen: false); break;
            }
            return p;
        }

        // Each suit's country: warm dunes for wands, the sea for cups, a windy sky for swords and a garden
        // for pentacles.
        private static void CourtBackground(Picture p, int suit)
        {
            SuitSky(p, suit);
            switch (suit)
            {
                case Wands:
                    Sparkles(p, Palette.White, (5, 6), (43, 9));
                    foreach (int x in new[] { 6, 41 })
                    {
                        p.Fill(Poly((x - 4, 50), (x, 44), (x + 4, 50)), Palette.Peach3);
                    }
                    Mask dunes = Ellipse(-25, 48, 30, 90).Or(Ellipse(18, 50, 75, 90));
                    p.Fill(dunes, Palette.Peach2);
                    p.Fill(dunes.Edge(true), Palette.Peach3);
                    break;
                case Cups:
                    Sparkles(p, Palette.White, (5, 6), (43, 9));
                    p.Fill(Ellipse(36, 42, 52, 52).And(Where((x, y) => y <= 47)), Palette.Mint3);
                    p.Fill(Where((x, y) => y >= 47), Palette.Blue2);
                    p.Run(0, 47, W, Palette.Blue3);
                    foreach ((int x, int y, int length) in new[] { (2, 52, 5), (40, 54, 6), (6, 62, 4), (41, 64, 5), (18, 69, 6) })
                    {
                        p.Run(x, y, length, Palette.Blue3);
                    }
                    break;
                case Swords:
                    Cloud(p, Palette.White, Palette.Lav2, (2, 10, 3.5f), (7, 9, 4f), (11, 11, 3f));
                    Cloud(p, Palette.White, Palette.Lav2, (38, 16, 3f), (43, 14, 4f), (48, 16, 3.5f));
                    p.Dots(Palette.Ink2, (15, 6), (16, 7), (17, 6), (32, 4), (33, 5), (34, 4));
                    Mask hill = Ellipse(-20, 50, 70, 100);
                    p.Fill(hill, Palette.Lav2);
                    p.Fill(hill.Edge(true), Palette.Lav3);
                    break;
                default:
                    Sparkles(p, Palette.White, (5, 6), (43, 9));
                    p.Blob(Rect(38, 41, 45, 48), Palette.Peach1, Palette.Ink2);
                    p.Fill(Poly((37, 41.5f), (41.5f, 37), (46, 41.5f)), Palette.Pink3);
                    p.Fill(Where((x, y) => y >= 48), Palette.Mint2);
                    p.Run(0, 48, W, Palette.Mint3);
                    GroundBits(p, new[] { (3, 52), (44, 54) }, new[] { (6, 56), (40, 58), (2, 66), (45, 67) });
                    break;
            }
        }

        // The suit's thing, a size bigger than on the number cards, for the court cards to hold. For a
        // wand or a sword (x, y) is the top and length its height; a cup or a coin is centred on (x, y).
        private static void Held(Picture p, int suit, int x, int y, int length = 22)
        {
            switch (suit)
            {
                case Wands:
                    p.Blob(Line(2f, (x, y + 4), (x, y + length)), Palette.Peach3);
                    p.Fill(Ellipse(x + 2, y + 9, x + 4, y + 10).Or(Ellipse(x - 4, y + 14, x - 2, y + 15)), Palette.Mint3);
                    p.Stamp(Sprites.Heart7, x - 3, y, Sprites.Two(Palette.Pink3));
                    break;
                case Cups:
                    p.Stamp(Sprites.PipCup, x - 5, y - 4, Key(('k', Palette.Ink), ('T', Palette.Peach2), ('w', Palette.White), ('P', Palette.Pink3), ('b', Palette.Blue2)));
                    break;
                case Swords:
                    Mask blade = Rect(x - 1, y + 3, x + 1, y + length - 6).Or(Poly((x - 1, y + 3.5f), (x, y), (x + 1, y + 3.5f)));
                    p.Blob(blade, Palette.White);
                    p.Blob(Rect(x - 4, y + length - 6, x + 4, y + length - 5), Palette.Butter3);
                    p.Blob(Rect(x - 1, y + length - 4, x + 1, y + length), Palette.Pink3);
                    break;
                default:
                    p.Blob(Circle(x, y, 5.6f), Palette.Butter2);
                    p.Fill(Mask.Star(W, H, x, y + 0.5f, 3.8f, 1.6f, 5), Palette.White);
                    break;
            }
        }

        // Each suit's court has its own cats: ginger for wands, grey-blue for cups, white for swords and
        // cream for pentacles; the clothes are the suit's colour.
        private static uint CourtFur(int suit) => suit == Wands ? Palette.Peach2 : suit == Cups ? Palette.Lav1 : suit == Swords ? Palette.White : Palette.Butter1;

        // The Page: a kitten in a little tunic, holding the suit's thing up to look at it.
        private static void Page(Picture p, int suit)
        {
            uint fur = CourtFur(suit), cloth = Palette.Ribbon(suit);
            p.Blob(Rect(20, 51, 23, 61).Or(Rect(26, 51, 29, 61)), fur);
            p.Blob(Ellipse(18, 59, 23, 63).Or(Ellipse(26, 59, 31, 63)), Palette.Peach3);
            Mask tunic = Poly((19, 33), (30, 33), (33, 54), (16, 54));
            p.Blob(tunic, cloth);
            p.Fill(tunic.Minus(tunic.Edge()).And(Where((x, y) => y == 44)), Palette.Butter3);
            p.Fill(tunic.Minus(tunic.Edge()).And(Where((x, y) => y >= 52)), Palette.RibbonShade(suit));
            (int c, _) = CatHead(p, 18, 20, 30, 32, fur, Eyes.Open);
            p.Blob(Ellipse(c - 7, 14, c + 4, 20).And(Where((x, y) => y <= 18)), Palette.RibbonShade(suit));
            p.Blob(Line(2f, (c + 3, 15), (c + 8, 9)), Palette.White);

            // The arm reaching out, the thing held up in the paw.
            p.Blob(Line(3.4f, (29, 37), (35, 33)), cloth);
            if (suit == Wands || suit == Swords)
            {
                Held(p, suit, 37, 12, 40);
                p.Blob(Ellipse(35, 31, 39, 35), fur);
            }
            else
            {
                Held(p, suit, 39, 28);
                p.Blob(Ellipse(35, 32, 39, 36), fur);
            }
            if (suit == Cups)
            {
                p.Blob(Ellipse(37, 20, 41, 24), Palette.Peach2);
                p.Dots(Palette.Ink, (40, 21));
            }
        }

        // The Knight: a cat in a plumed helmet riding the white pony, holding the suit's thing out.
        private static void Knight(Picture p, int suit)
        {
            uint fur = CourtFur(suit), cloth = Palette.Ribbon(suit);
            p.Paste(Pony(Palette.SkyBottom(suit)), 0, 5);
            Mask blanket = Rect(13, 41, 27, 49);
            p.Blob(blanket, cloth);
            p.Fill(blanket.Minus(blanket.Edge()).And(Where((x, y) => y == 48 && x % 2 == 0)), Palette.White);
            p.Blob(Ellipse(14, 30, 26, 45), Palette.Lav1);
            p.Fill(Rect(16, 36, 24, 37), cloth);
            (int c, _) = CatHead(p, 14, 18, 26, 30, fur, Eyes.Open);
            p.Blob(Line(2.6f, (c - 1, 13), (c - 5, 8), (c - 10, 9)), cloth);
            p.Blob(Line(3.4f, (24, 36), (30, 33)), Palette.Lav1);
            if (suit == Wands || suit == Swords)
            {
                Held(p, suit, 32, 8, 30);
                p.Blob(Ellipse(30, 31, 34, 35), fur);
            }
            else
            {
                Held(p, suit, 33, 26);
                p.Blob(Ellipse(29, 31, 33, 35), fur);
            }
        }

        // The Queen or the King on a throne in the suit's colour, crowned, holding the suit's thing. The
        // Queen is soft and wears a gown; the King looks stern and wears a cape trimmed with white fur.
        private static void Throne(Picture p, int suit, bool queen)
        {
            uint fur = CourtFur(suit), cloth = Palette.Ribbon(suit);
            uint stone = suit == Cups || suit == Swords ? Palette.Butter2 : Palette.Lav2;
            uint shade = suit == Cups || suit == Swords ? Palette.Butter3 : Palette.Lav3;
            Mask back = Rect(10, 16, 39, 64).Or(Circle(24.5f, 16, 9f));
            p.Blob(back, stone);
            p.Fill(back.Minus(back.Edge()).And(Circle(24.5f, 16, 6.5f).Minus(Circle(24.5f, 16, 5.5f))), shade);
            Mask seat = Rect(8, 44, 41, 66);
            p.Blob(seat, stone);
            p.Fill(seat.Minus(seat.Edge()).And(Where((x, y) => y >= 63)), shade);
            p.Stamp(Sprites.Heart7, 21, 9, Sprites.Two(Palette.Pink3));

            Mask robe = Ellipse(13, 30, 36, 46).Or(Poly((14, 38), (35, 38), (38, 66), (11, 66)));
            p.Blob(robe, cloth);
            Mask inside = robe.Minus(robe.Edge());
            if (queen)
            {
                p.Fill(inside.And(Poly((22, 34), (27, 34), (31, 66), (18, 66))), Palette.White);
                p.Fill(inside.And(Where((x, y) => y == 50 || y == 58)).And(Poly((22, 34), (27, 34), (31, 66), (18, 66))), Palette.Pink2);
            }
            else
            {
                p.Fill(inside.And(Line(3f, (21, 33), (17, 66)).Or(Line(3f, (28, 33), (32, 66)))), Palette.White);
                p.Fill(inside.And(Line(3f, (21, 33), (17, 66)).Or(Line(3f, (28, 33), (32, 66)))).And(Where((x, y) => (x + y) % 4 == 0)), Palette.Ink2);
            }
            (int c, int eyeY) = CatHead(p, 17, 19, 32, 33, fur, queen ? Eyes.Happy : Eyes.Stern);
            if (!queen)
            {
                p.Blob(Poly((c - 3, eyeY + 6), (c + 3, eyeY + 6), (c, eyeY + 10)), fur);
            }
            p.Stamp(Sprites.Crown, c - 5, 13, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));

            // The paw holding the suit's thing, the other resting in the lap.
            p.Blob(Line(3.4f, (31, 42), (33, 38)), cloth);
            if (suit == Wands || suit == Swords)
            {
                Held(p, suit, 35, 14, 34);
                p.Blob(Ellipse(32, 36, 36, 40), fur);
            }
            else
            {
                Held(p, suit, 36, 33);
                p.Blob(Ellipse(31, 37, 35, 41), fur);
            }
            p.Blob(Ellipse(17, 46, 22, 50), fur);
            if (queen && suit == Wands)
            {
                SmallCat(p, 8, 54, Palette.Ink2, Eyes.Iris);
            }
        }

        // ---- The aces ----

        private static Picture Ace(int suit)
        {
            switch (suit)
            {
                case Wands: return AceOfWands();
                case Cups: return AceOfCups();
                case Swords: return AceOfSwords();
                default: return AceOfPentacles();
            }
        }

        // A cat's paw reaching out of a cloud at the left edge, its pad at (px, py), drawn over whatever it
        // holds.
        private static void PawFromCloud(Picture p, float px, float py)
        {
            p.Blob(Line(6f, (3, py + 1), (px - 2, py)), Palette.Cream);
            Cloud(p, Palette.White, Palette.Lav2, (0, py - 3, 5f), (6, py + 1, 5.5f), (1, py + 6, 5f));
            p.Blob(Ellipse(px - 5, py - 4, px + 4, py + 4), Palette.Cream);
            int x = (int)px, y = (int)py;
            p.Dots(Palette.Ink, (x + 2, y - 1), (x + 3, y - 1), (x + 2, y + 2), (x + 3, y + 2));
            p.Dots(Palette.Pink2, (x - 2, y), (x - 1, y));
        }

        // Ace of Wands: a big heart-tipped wand sprouting leaves, a castle on the hill below.
        private static Picture AceOfWands()
        {
            var p = new Picture(W, H);
            SuitSky(p, Wands);
            Sparkles(p, Palette.White, (8, 8), (42, 12), (38, 34), (6, 26));
            Mask hills = Ellipse(-20, 54, 30, 90).Or(Ellipse(20, 50, 70, 90));
            p.Fill(hills, Palette.Mint2);
            p.Fill(hills.Edge(true), Palette.Mint3);
            foreach (int x in new[] { 37, 44 })
            {
                p.Blob(Rect(x, 45, x + 4, 55).Or(Poly((x - 0.5f, 45.5f), (x + 2, 41), (x + 4.5f, 45.5f))), Palette.Lav1);
                p.Fill(Poly((x - 0.5f, 45), (x + 2, 41.5f), (x + 4.5f, 45)).Minus(Poly((x - 0.5f, 45.5f), (x + 2, 41), (x + 4.5f, 45.5f)).Edge()), Palette.Pink3);
            }
            p.Blob(Rect(39, 49, 46, 55), Palette.Lav1);

            p.Blob(Line(3.4f, (20, 63), (29, 25)), Palette.Peach3);
            foreach ((float x, float y, bool right) in new[] { (26f, 36f, true), (23.5f, 47f, false), (27.5f, 30f, false) })
            {
                float lx = right ? x + 3 : x - 3;
                p.Blob(Ellipse(lx - 2, y - 1, lx + 2, y + 1), Palette.Mint3);
            }
            p.Fill(Ellipse(9, 40, 11, 41).Or(Ellipse(36, 22, 38, 23)), Palette.Mint3);
            Mask heart = Circle(24.5f, 15, 4.6f).Or(Circle(33.5f, 15, 4.6f)).Or(Poly((20.2f, 16.5f), (37.8f, 16.5f), (29, 27)));
            p.Blob(heart, Palette.Pink3);
            p.Fill(Circle(23.5f, 14, 1.4f), Palette.Pink1);
            PawFromCloud(p, 24, 47);
            return p;
        }

        // Ace of Cups: a big white teacup overflowing into a lily pond, a little white bird above it.
        private static Picture AceOfCups()
        {
            var p = new Picture(W, H);
            SuitSky(p, Cups);
            Sparkles(p, Palette.White, (6, 6), (42, 9), (44, 30), (4, 24));
            Mask pond = Where((x, y) => y >= 58);
            p.Fill(pond, Palette.Blue2);
            p.Run(0, 58, W, Palette.Blue3);
            foreach ((int x, int y) in new[] { (4, 63), (38, 61), (30, 67) })
            {
                p.Blob(Ellipse(x, y, x + 6, y + 3), Palette.Mint3);
                p.Fill(Circle(x + 3, y + 1, 1f), Palette.Pink2);
            }

            // The water spilling over both sides.
            foreach ((float x0, float x1) in new[] { (14f, 9f), (35f, 40f) })
            {
                p.Blob(Line(2.2f, (x0, 31), (x1, 40), (x1, 58)), Palette.Blue1, Palette.Blue3);
            }
            Mask bowl = Ellipse(13, 18, 37, 50).And(Where((x, y) => y >= 31));
            p.Blob(Circle(37.5f, 38, 4.5f).Minus(Circle(37.5f, 38, 2f)), Palette.White);
            p.Blob(bowl, Palette.White);
            p.Fill(bowl.Minus(bowl.Edge()).And(Where((x, y) => y >= 37 && y <= 38)), Palette.Blue2);
            p.Blob(Ellipse(13, 28, 37, 34), Palette.Blue1);
            p.Stamp(Sprites.Heart7, 18, 41, Sprites.Two(Palette.Pink3));
            p.Blob(Ellipse(16, 50, 34, 54), Palette.Blue2);
            p.Stamp(Sprites.TinyBird, 20, 6, Key(('k', Palette.Ink), ('b', Palette.White), ('B', Palette.Lav1), ('y', Palette.Butter3)));
            p.Blob(Circle(24.5f, 20, 2.2f), Palette.Butter1);
            p.Fill(Rect(24, 19, 25, 21).Or(Rect(23, 20, 26, 20)).And(Circle(24.5f, 20, 1.5f)), Palette.Butter3);
            PawFromCloud(p, 25, 56);
            return p;
        }

        // Ace of Swords: a big upright sword through a crown hung with green sprigs, mountains below.
        private static Picture AceOfSwords()
        {
            var p = new Picture(W, H);
            SuitSky(p, Swords);
            Sparkles(p, Palette.White, (6, 10), (41, 6), (40, 28), (5, 33));
            Mask peaks = Poly((-2, 60), (8, 48), (14, 54), (24, 44), (34, 54), (42, 47), (52, 56), (52, 73), (-2, 73));
            p.Fill(peaks, Palette.Lav1);
            p.Fill(peaks.And(Where((x, y) => y >= 54 + (x / 3) % 2)), Palette.Lav2);
            p.Fill(peaks.Edge(true), Palette.Lav3);

            Mask blade = Rect(23, 9, 26, 46).Or(Poly((23, 9.5f), (24.5f, 4), (26, 9.5f)));
            p.Blob(blade, Palette.White);
            p.Fill(blade.Minus(blade.Edge()).And(Where((x, y) => x >= 25)), Palette.Lav1);
            p.Blob(Rect(15, 46, 34, 49), Palette.Butter3);
            p.Fill(Rect(24, 47, 25, 48), Palette.Pink3);
            p.Blob(Rect(23, 50, 26, 58), Palette.Pink3);
            p.Blob(Circle(24.5f, 60, 2.2f), Palette.Butter2);
            p.Stamp(Sprites.Crown, 19, 11, Key(('k', Palette.Ink), ('y', Palette.Butter2), ('Y', Palette.Butter3), ('r', Palette.Pink3), ('b', Palette.Blue2)));
            foreach ((float x, float y) in new[] { (17f, 20f), (16f, 24f), (32f, 20f), (33f, 24f) })
            {
                p.Blob(Ellipse(x - 1.5f, y - 1, x + 1.5f, y + 1.5f), Palette.Mint3);
            }
            PawFromCloud(p, 24, 54);
            return p;
        }

        // Ace of Pentacles: a big star coin held up on an open paw, a garden with a rose arch below.
        private static Picture AceOfPentacles()
        {
            var p = new Picture(W, H);
            SuitSky(p, Pentacles);
            Sparkles(p, Palette.White, (6, 7), (42, 8), (43, 31), (4, 30));
            p.Fill(Where((x, y) => y >= 55), Palette.Mint2);
            p.Run(0, 55, W, Palette.Mint3);
            p.Fill(Poly((22, 55), (27, 55), (33, 73), (16, 73)), Palette.Butter1);
            Mask hedge = Rect(-1, 52, 16, 60).Or(Rect(33, 52, 50, 60));
            p.Blob(hedge, Palette.Mint3, Palette.Mint4, border: false);
            Mask arch = Ellipse(14, 44, 35, 66).Minus(Ellipse(18, 48, 31, 70)).And(Where((x, y) => y <= 60));
            p.Blob(arch, Palette.Mint3, Palette.Mint4);
            foreach ((int x, int y) in new[] { (15, 53), (19, 46), (29, 46), (33, 52), (24, 44), (3, 55), (44, 55) })
            {
                p.Stamp(Sprites.Flower, x - 1, y - 1, letter => letter == 'y' ? Palette.Pink4 : Palette.Pink3);
            }

            Mask coin = Circle(25, 26, 12f);
            p.Blob(coin, Palette.Butter2);
            p.Fill(Circle(25, 26, 9.5f).Minus(Circle(25, 26, 8.6f)), Palette.Butter3);
            p.Blob(Mask.Star(W, H, 25, 26.5f, 7.5f, 3.2f, 5), Palette.White);
            p.Fill(Circle(20, 19, 1.2f), Palette.Butter1);
            PawFromCloud(p, 25, 41);
            return p;
        }
    }
}
