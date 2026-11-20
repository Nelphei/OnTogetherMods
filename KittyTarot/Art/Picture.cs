using System;

namespace KittyTarot
{
    // A small picture in memory, top row first. Colours are 0xRRGGBBAA; 0 is see-through.
    // TarotArt turns these into textures; keeping Unity out of the Art folder lets the art be checked
    // outside the game.
    internal sealed class Picture
    {
        public readonly int Width;
        public readonly int Height;
        public readonly uint[] Pixels;

        public Picture(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        public uint this[int x, int y]
        {
            get => Inside(x, y) ? Pixels[y * Width + x] : 0u;
            set
            {
                if (Inside(x, y))
                {
                    Pixels[y * Width + x] = value;
                }
            }
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public void Fill(uint colour)
        {
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = colour;
            }
        }

        public void Fill(Mask mask, uint colour)
        {
            for (int y = 0; y < Height && y < mask.Height; y++)
            {
                for (int x = 0; x < Width && x < mask.Width; x++)
                {
                    if (mask[x, y])
                    {
                        Pixels[y * Width + x] = colour;
                    }
                }
            }
        }

        // Fills a shape and paints its edge pixels with an outline. border: whether the picture's own
        // edges count as inside the shape, so a shape running off the picture gets no outline there.
        public void Blob(Mask mask, uint fill, uint outline = Palette.Ink, bool border = true)
        {
            Fill(mask, fill);
            if (outline != 0)
            {
                Fill(mask.Edge(border), outline);
            }
        }

        // Draws rows of letters ('.' skipped) with the colour each letter maps to (0 skips it too).
        public void Stamp(string[] rows, int left, int top, Func<char, uint> colour)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char letter = rows[y][x];
                    if (letter == '.')
                    {
                        continue;
                    }
                    uint c = colour(letter);
                    if (c != 0)
                    {
                        this[left + x, top + y] = c;
                    }
                }
            }
        }

        // Every letter in one colour.
        public void Stamp(string[] rows, int left, int top, uint colour) => Stamp(rows, left, top, _ => colour);

        // Copies another picture on top, leaving its see-through pixels out.
        public void Paste(Picture other, int left, int top)
        {
            for (int y = 0; y < other.Height; y++)
            {
                for (int x = 0; x < other.Width; x++)
                {
                    uint c = other.Pixels[y * other.Width + x];
                    if (c != 0)
                    {
                        this[left + x, top + y] = c;
                    }
                }
            }
        }

        // Like Paste, but only where the mask (in this picture's coordinates) is set.
        public void Paste(Picture other, int left, int top, Mask where)
        {
            for (int y = 0; y < other.Height; y++)
            {
                for (int x = 0; x < other.Width; x++)
                {
                    uint c = other.Pixels[y * other.Width + x];
                    if (c != 0 && where[left + x, top + y])
                    {
                        this[left + x, top + y] = c;
                    }
                }
            }
        }

        public void Dots(uint colour, params (int x, int y)[] points)
        {
            foreach ((int x, int y) in points)
            {
                this[x, y] = colour;
            }
        }

        // A row of pixels from (x, y) to the right.
        public void Run(int x, int y, int length, uint colour)
        {
            for (int i = 0; i < length; i++)
            {
                this[x + i, y] = colour;
            }
        }

        // Turned upside down (half a turn).
        public Picture Turned()
        {
            var turned = new Picture(Width, Height);
            for (int i = 0; i < Pixels.Length; i++)
            {
                turned.Pixels[Pixels.Length - 1 - i] = Pixels[i];
            }
            return turned;
        }

        // A quarter turn, clockwise or not. Width and height swap.
        public Picture QuarterTurn(bool clockwise)
        {
            var turned = new Picture(Height, Width);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (clockwise)
                    {
                        turned[Height - 1 - y, x] = this[x, y];
                    }
                    else
                    {
                        turned[y, Width - 1 - x] = this[x, y];
                    }
                }
            }
            return turned;
        }

        // Left and right swapped.
        public Picture Mirrored()
        {
            var mirrored = new Picture(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    mirrored[Width - 1 - x, y] = this[x, y];
                }
            }
            return mirrored;
        }

        // An ordered-dither pattern for blending one colour into the next.
        private static readonly float[,] Bayer =
        {
            { 0f / 16, 8f / 16, 2f / 16, 10f / 16 },
            { 12f / 16, 4f / 16, 14f / 16, 6f / 16 },
            { 3f / 16, 11f / 16, 1f / 16, 9f / 16 },
            { 15f / 16, 7f / 16, 13f / 16, 5f / 16 },
        };

        // A sky from top to bottom: each stop is (how far down, 0 to 1; colour). Between two stops the
        // colours stay solid with a short dithered blend in the middle, which reads as pixel art.
        public void Gradient(params (float at, uint colour)[] stops)
        {
            for (int y = 0; y < Height; y++)
            {
                float t = Height > 1 ? (float)y / (Height - 1) : 0f;
                uint c0 = stops[0].colour, c1 = stops[0].colour;
                float f = 0f;
                for (int i = 0; i < stops.Length - 1; i++)
                {
                    if (t >= stops[i].at && t <= stops[i + 1].at)
                    {
                        float span = Math.Max(stops[i + 1].at - stops[i].at, 1e-6f);
                        f = (t - stops[i].at) / span;
                        c0 = stops[i].colour;
                        c1 = stops[i + 1].colour;
                        break;
                    }
                }
                f = Math.Min(Math.Max((f - 0.5f) * 2.5f + 0.5f, 0f), 1f);
                for (int x = 0; x < Width; x++)
                {
                    Pixels[y * Width + x] = f > Bayer[y % 4, x % 4] ? c1 : c0;
                }
            }
        }
    }

    // Which pixels a shape covers, for filling and outlining. Coordinates are pixel centres, as in
    // Pillow, which the art was first sketched with: pixel (3, 5) is the point (3, 5).
    internal sealed class Mask
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Bits;

        public Mask(int width, int height)
        {
            Width = width;
            Height = height;
            Bits = new bool[width * height];
        }

        public bool this[int x, int y]
        {
            get => x >= 0 && y >= 0 && x < Width && y < Height && Bits[y * Width + x];
            set
            {
                if (x >= 0 && y >= 0 && x < Width && y < Height)
                {
                    Bits[y * Width + x] = value;
                }
            }
        }

        public static Mask Of(int width, int height, Func<int, int, bool> inside)
        {
            var mask = new Mask(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    mask.Bits[y * width + x] = inside(x, y);
                }
            }
            return mask;
        }

        // The ellipse filling a box of pixels, edges included, like Pillow's ellipse().
        public static Mask Ellipse(int width, int height, float left, float top, float right, float bottom)
        {
            float cx = (left + right) / 2f, cy = (top + bottom) / 2f;
            float rx = (right - left) / 2f + 0.5f, ry = (bottom - top) / 2f + 0.5f;
            return Of(width, height, (x, y) =>
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                return dx * dx + dy * dy <= 1f;
            });
        }

        public static Mask Circle(int width, int height, float cx, float cy, float radius)
        {
            return Of(width, height, (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius);
        }

        public static Mask Rect(int width, int height, int left, int top, int right, int bottom)
        {
            return Of(width, height, (x, y) => x >= left && x <= right && y >= top && y <= bottom);
        }

        // A box of pixels (edges included) with its corners rounded off.
        public static Mask RoundedRect(int width, int height, int left, int top, int right, int bottom, float radius)
        {
            float r2 = (radius + 0.25f) * (radius + 0.25f);
            return Of(width, height, (x, y) =>
            {
                if (x < left || x > right || y < top || y > bottom)
                {
                    return false;
                }
                float cx = x < left + radius ? left + radius : x > right - radius ? right - radius : x;
                float cy = y < top + radius ? top + radius : y > bottom - radius ? bottom - radius : y;
                return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2;
            });
        }

        // Even-odd fill: a pixel is inside when a ray from its centre crosses the edges an odd number of times.
        public static Mask Polygon(int width, int height, params (float x, float y)[] points)
        {
            return Of(width, height, (x, y) =>
            {
                bool inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    (float xi, float yi) = points[i];
                    (float xj, float yj) = points[j];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        inside = !inside;
                    }
                }
                return inside;
            });
        }

        // A star with its first point straight up.
        public static Mask Star(int width, int height, float cx, float cy, float outer, float inner, int points)
        {
            var corners = new (float, float)[points * 2];
            for (int k = 0; k < points * 2; k++)
            {
                double angle = -Math.PI / 2 + k * Math.PI / points;
                float r = k % 2 == 0 ? outer : inner;
                corners[k] = (cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
            }
            return Polygon(width, height, corners);
        }

        // A thick line through the points.
        public static Mask Line(int width, int height, float thickness, params (float x, float y)[] points)
        {
            float half = thickness / 2f;
            return Of(width, height, (x, y) =>
            {
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    if (Distance(x, y, points[i], points[i + 1]) <= half)
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        private static float Distance(float x, float y, (float x, float y) a, (float x, float y) b)
        {
            float vx = b.x - a.x, vy = b.y - a.y;
            float length = vx * vx + vy * vy;
            float t = length > 0f ? Math.Min(Math.Max(((x - a.x) * vx + (y - a.y) * vy) / length, 0f), 1f) : 0f;
            float dx = x - (a.x + t * vx), dy = y - (a.y + t * vy);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public Mask Or(Mask other) => Combine(other, (a, b) => a || b);

        public Mask And(Mask other) => Combine(other, (a, b) => a && b);

        public Mask Minus(Mask other) => Combine(other, (a, b) => a && !b);

        // Only the rows that pass.
        public Mask Rows(Func<int, bool> keep) => Of(Width, Height, (x, y) => this[x, y] && keep(y));

        private Mask Combine(Mask other, Func<bool, bool, bool> rule)
        {
            var mask = new Mask(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    mask.Bits[y * Width + x] = rule(this[x, y], other[x, y]);
                }
            }
            return mask;
        }

        // One pixel peeled off all round. border: whether outside the picture counts as inside.
        public Mask Erode(bool border = false)
        {
            var mask = new Mask(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    mask.Bits[y * Width + x] = this[x, y]
                        && Get(x - 1, y, border) && Get(x + 1, y, border)
                        && Get(x, y - 1, border) && Get(x, y + 1, border);
                }
            }
            return mask;
        }

        // The shape's own edge pixels: the ones with a side-by-side neighbour outside it.
        public Mask Edge(bool border = false) => Minus(Erode(border));

        private bool Get(int x, int y, bool border)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return border;
            }
            return Bits[y * Width + x];
        }
    }
}
