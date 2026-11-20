using System;

namespace KittyTarot
{
    // The cards' faces and back. A face is a pastel frame with a stitched band (pink for the major
    // cards, the suit's colour for the others), the card's number between two paws, its picture in a
    // window and its name on a ribbon. Cards whose picture isn't drawn yet show a stand-in: a paw print
    // on the suit's colours.
    internal static class CardFaces
    {
        public const int Width = 70;
        public const int Height = 120;

        // The picture window, outline included.
        private const int WindowLeft = 9;
        private const int WindowTop = 17;
        private const int WindowRight = 60;
        private const int WindowBottom = 90;

        // The picture inside it.
        public const int PictureX = WindowLeft + 1;
        public const int PictureY = WindowTop + 1;
        public const int PictureWidth = WindowRight - WindowLeft - 1;
        public const int PictureHeight = WindowBottom - WindowTop - 1;

        public static Picture Face(int card)
        {
            int suit = Deck.Suit(card);
            Picture p = Base(Palette.Band(suit), Palette.White, Palette.Cream, out _);

            // The number between two paws; aces and court cards have a heart instead.
            string numeral = Deck.Numeral(card);
            int width = numeral != null ? TarotFont.Width(numeral) : 5;
            int left = (Width - width) / 2;
            if (numeral != null)
            {
                TarotFont.Draw(p, numeral, left, 9, Palette.Ink);
            }
            else
            {
                p.Stamp(Sprites.Heart5, left, 10, Palette.Ribbon(suit));
            }
            p.Stamp(Sprites.PawSmall, left - 11, 9, Palette.Pink3);
            p.Stamp(Sprites.PawSmall, left + width + 4, 9, Palette.Pink3);

            Mask window = Mask.RoundedRect(Width, Height, WindowLeft, WindowTop, WindowRight, WindowBottom, 3);
            p.Fill(window, Palette.Ink);
            Picture picture = Scenes.Of(card) ?? Placeholder(card);
            p.Paste(picture, PictureX, PictureY, window.Erode());

            Ribbon(p, Deck.RibbonLines(card), Palette.Ribbon(suit), Palette.RibbonShade(suit));
            return p;
        }

        // Whether the card has its own picture yet (the rest show the stand-in).
        public static bool HasPicture(int card) => Scenes.Has(card);

        // The back: a pink card with paws, moons, a heart clover and a jewel. Turned upside down it looks
        // the same, so a reversed card can't be spotted from its back (real decks do this too).
        public static Picture Back()
        {
            Picture p = Base(Palette.Lav3, Palette.White, Palette.Pink1, out Mask[] rings);
            p.Fill(rings[7].And(Mask.Of(Width, Height, (x, y) => (x + y) % 6 == 0 && (x - y) % 6 == 0)), Palette.Pink2);

            const float cx = (Width - 1) / 2f;
            const float cy = (Height - 1) / 2f;
            p.Blob(Mask.Circle(Width, Height, cx, cy, 14.5f), Palette.Lav2, Palette.Ink, border: false);
            Mask ring = Mask.Circle(Width, Height, cx, cy, 11.5f).Edge();
            p.Fill(ring.And(Mask.Of(Width, Height, (x, y) => (x + y) % 3 == 0)), Palette.White);

            // Four hearts pointing in at the middle, like a clover: three here, the bottom one comes
            // from the turn below.
            Picture heart = Sprite(Sprites.Heart8, Sprites.Two(Palette.Pink3));
            p.Paste(heart, 31, 51);
            p.Paste(heart.QuarterTurn(false), 24, 56);
            p.Paste(heart.QuarterTurn(true), 40, 56);

            p.Stamp(Sprites.PawBig, 30, 17, Sprites.Two(Palette.Pink3));
            Picture moon = Moon();
            p.Paste(moon, 10, 10);
            p.Paste(moon.Mirrored(), Width - 10 - moon.Width, 10);
            foreach ((int x, int y) in new[] { (20, 24), (47, 25), (13, 36), (54, 39), (33, 33) })
            {
                p.Stamp(Sprites.Spark3, x, y, Palette.White);
            }

            // The bottom half is the top half turned round.
            for (int y = Height / 2; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    p[x, y] = p[Width - 1 - x, Height - 1 - y];
                }
            }
            p.Stamp(Sprites.Gem4, 33, 58, letter => letter == 'w' ? Palette.White : Palette.Butter3);
            return p;
        }

        // The shape every card has: for outlines, glows and the edges of a pile.
        public static Mask Shape(int width = Width, int height = Height, float radius = 6f)
        {
            return Mask.RoundedRect(width, height, 0, 0, width - 1, height - 1, radius);
        }

        // The card's outline, a stitched band, an inner line and the inside. rings: the shape peeled
        // one pixel at a time, [0] the whole card to [7] the inside.
        private static Picture Base(uint band, uint stitch, uint inner, out Mask[] rings)
        {
            var p = new Picture(Width, Height);
            rings = new Mask[8];
            rings[0] = Shape();
            for (int i = 1; i < rings.Length; i++)
            {
                rings[i] = rings[i - 1].Erode();
            }
            p.Fill(rings[0], Palette.Ink);
            p.Fill(rings[1], band);
            p.Fill(rings[3].Minus(rings[4]).And(Mask.Of(Width, Height, (x, y) => (x + y) / 2 % 2 == 0)), stitch);
            p.Fill(rings[6], Palette.Ink);
            p.Fill(rings[7], inner);
            return p;
        }

        // The name on a ribbon across the bottom, its notched tails tucked over the card's edges.
        private static void Ribbon(Picture p, string[] lines, uint body, uint shade)
        {
            bool two = lines.Length > 1;
            int top = two ? 92 : 95;
            int bottom = two ? 112 : 107;
            int tailTop = top + 3;
            int tailBottom = Math.Min(bottom + 2, Height - 7);
            float mid = (tailTop + tailBottom) / 2f;
            Mask tails = Mask.Polygon(Width, Height, (0, tailTop), (9, tailTop), (9, tailBottom), (0, tailBottom), (3, mid))
                .Or(Mask.Polygon(Width, Height, (Width - 1, tailTop), (Width - 10, tailTop), (Width - 10, tailBottom),
                    (Width - 1, tailBottom), (Width - 4, mid)));
            p.Blob(tails, shade, Palette.Ink, border: false);
            p.Blob(Mask.Rect(Width, Height, 4, top, Width - 5, bottom), body, Palette.Ink, border: false);

            int y = top + (two ? 2 : 3);
            foreach (string line in lines)
            {
                int x = (Width - TarotFont.Width(line)) / 2;
                TarotFont.Draw(p, line, x, y + 1, shade);
                TarotFont.Draw(p, line, x, y, Palette.White);
                y += 9;
            }
        }

        // A card still waiting for its picture: the suit's colours, a big paw print and sparkles.
        private static Picture Placeholder(int card)
        {
            int suit = Deck.Suit(card);
            var p = new Picture(PictureWidth, PictureHeight);
            p.Gradient((0f, Palette.SkyTop(suit)), (0.55f, Palette.SkyMiddle(suit)), (1f, Palette.SkyBottom(suit)));
            p.Stamp(Sprites.PawBig, (PictureWidth - 9) / 2, (PictureHeight - 10) / 2, Sprites.Two(Palette.White, Palette.Ink2));
            foreach ((int x, int y) in new[] { (8, 12), (39, 18), (11, 52), (37, 58) })
            {
                p.Stamp(Sprites.Spark3, x, y, Palette.White);
            }
            return p;
        }

        private static Picture Moon()
        {
            Mask moon = Mask.Ellipse(9, 9, 0, 0, 8, 8).Minus(Mask.Ellipse(9, 9, 3, -1, 10, 6));
            var p = new Picture(9, 9);
            p.Blob(moon, Palette.Butter2, Palette.Ink, border: false);
            return p;
        }

        public static Picture Sprite(string[] rows, Func<char, uint> colour)
        {
            int width = 0;
            foreach (string row in rows)
            {
                width = Math.Max(width, row.Length);
            }
            var p = new Picture(width, rows.Length);
            p.Stamp(rows, 0, 0, colour);
            return p;
        }
    }
}
