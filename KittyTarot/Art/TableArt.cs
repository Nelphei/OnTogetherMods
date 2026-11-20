namespace KittyTarot
{
    // The table the cards lie on, and the bits drawn over it.
    internal static class TableArt
    {
        private const int Lace = 6;      // the white band round the edge
        private const int Scallop = 6;   // the lace's round bumps, one every this many pixels

        // A pink cloth with little hearts and a white scalloped lace edge. slots: the top-left corners of
        // the spots where cards go, marked with a dashed outline.
        public static Picture Cloth(int width, int height, (int x, int y)[] slots)
        {
            var p = new Picture(width, height);
            p.Fill(Palette.Pink1);
            for (int y = 10; y < height - 10; y += 14)
            {
                for (int x = 10 + (y / 14 % 2) * 7; x < width - 10; x += 14)
                {
                    p.Stamp(Sprites.Heart5, x, y, Palette.Pink2);
                }
            }

            foreach ((int x, int y) in slots)
            {
                p.Paste(SlotMark(), x, y);
            }

            // The lace: a white band, round bumps along its inside edge, a row of little holes.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < Lace || y < Lace || x >= width - Lace || y >= height - Lace)
                    {
                        p[x, y] = Palette.White;
                    }
                }
            }
            for (int x = 0; x < width; x += Scallop)
            {
                Bump(p, x - 1, 3, x + 5, 9);
                Bump(p, x - 1, height - 10, x + 5, height - 4);
            }
            for (int y = 0; y < height; y += Scallop)
            {
                Bump(p, 3, y - 1, 9, y + 5);
                Bump(p, width - 10, y - 1, width - 4, y + 5);
            }
            for (int x = 2; x < width; x += Scallop)
            {
                p[x, 3] = Palette.Pink2;
                p[x, height - 4] = Palette.Pink2;
            }
            for (int y = 2; y < height; y += Scallop)
            {
                p[3, y] = Palette.Pink2;
                p[width - 4, y] = Palette.Pink2;
            }
            for (int x = 0; x < width; x++)
            {
                p[x, 0] = Palette.Ink;
                p[x, height - 1] = Palette.Ink;
            }
            for (int y = 0; y < height; y++)
            {
                p[0, y] = Palette.Ink;
                p[width - 1, y] = Palette.Ink;
            }
            return p;
        }

        // How far in from the table's edge the lace reaches.
        public const int LaceReach = 10;

        private static void Bump(Picture p, int left, int top, int right, int bottom)
        {
            float cx = (left + right) / 2f, cy = (top + bottom) / 2f;
            float rx = (right - left) / 2f + 0.5f, ry = (bottom - top) / 2f + 0.5f;
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        p[x, y] = Palette.White;
                    }
                }
            }
        }

        // Where a card goes: a dashed outline.
        public static Picture SlotMark()
        {
            var p = new Picture(CardFaces.Width, CardFaces.Height);
            Mask edge = CardFaces.Shape().Edge();
            p.Fill(edge.And(Mask.Of(p.Width, p.Height, (x, y) => (x + y) / 3 % 2 == 0)), Palette.Pink3);
            return p;
        }

        // A soft ring round a card that's picked or under the mouse, 2 pixels bigger all round.
        public static Picture Glow()
        {
            int w = CardFaces.Width + 4, h = CardFaces.Height + 4;
            var p = new Picture(w, h);
            Mask outer = CardFaces.Shape(w, h, 8f);
            Mask inner = outer.Erode();
            p.Fill(outer.Minus(inner), Palette.Pink3);
            p.Fill(inner.Minus(inner.Erode()), Palette.White);
            return p;
        }

        // The side of the cards under the top one of a pile.
        public static Picture PileEdge()
        {
            var p = new Picture(CardFaces.Width, CardFaces.Height);
            p.Blob(CardFaces.Shape(), Palette.Lav2, Palette.Ink, border: false);
            return p;
        }
    }
}
