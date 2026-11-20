using System;

namespace SoapBox
{
    // A picture made of colour numbers, one byte per pixel, and the drawing calls that carts, the
    // editors and the handheld frame use. The numbers mean whatever palette shows the picture: the
    // game screen uses Palette, the frame has its own. No Unity here, so tests outside the game can
    // draw with it too.
    internal sealed class Surface
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Pixels;

        // Drawing state, which carts change through the API and ResetState puts back.
        public int CameraX;
        public int CameraY;
        public readonly byte[] DrawColours = new byte[256]; // colour swaps while drawing
        public readonly bool[] SeeThrough = new bool[256];  // colours a Blit skips

        // The clip box, in surface pixels: x0 <= x < x1, y0 <= y < y1.
        private int _clipX0, _clipY0, _clipX1, _clipY1;

        public Surface(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height];
            ResetState();
        }

        public void ResetState()
        {
            CameraX = 0;
            CameraY = 0;
            for (int i = 0; i < 256; i++)
            {
                DrawColours[i] = (byte)i;
                SeeThrough[i] = false;
            }
            SeeThrough[0] = true;
            ClipOff();
        }

        public void Clip(int x, int y, int w, int h)
        {
            _clipX0 = Math.Max(0, x);
            _clipY0 = Math.Max(0, y);
            _clipX1 = Math.Min(Width, x + Math.Max(0, w));
            _clipY1 = Math.Min(Height, y + Math.Max(0, h));
        }

        public void ClipOff()
        {
            _clipX0 = 0;
            _clipY0 = 0;
            _clipX1 = Width;
            _clipY1 = Height;
        }

        // Fills the whole picture, ignoring the clip box and camera.
        public void Clear(int colour)
        {
            byte c = (byte)colour;
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = c;
            }
        }

        public void Pset(int x, int y, int colour)
        {
            Plot(x - CameraX, y - CameraY, DrawColours[colour & 0xFF]);
        }

        // The colour at a spot (camera applied), or 0 outside the picture.
        public int Pget(int x, int y)
        {
            x -= CameraX;
            y -= CameraY;
            return x >= 0 && y >= 0 && x < Width && y < Height ? Pixels[y * Width + x] : 0;
        }

        public void Line(int x0, int y0, int x1, int y1, int colour)
        {
            byte c = DrawColours[colour & 0xFF];
            x0 -= CameraX;
            x1 -= CameraX;
            y0 -= CameraY;
            y1 -= CameraY;
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(x0, y0, c);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        public void Rect(int x, int y, int w, int h, int colour)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            x -= CameraX;
            y -= CameraY;
            Span(x, x + w - 1, y, c);
            Span(x, x + w - 1, y + h - 1, c);
            for (int j = y + 1; j < y + h - 1; j++)
            {
                Plot(x, j, c);
                Plot(x + w - 1, j, c);
            }
        }

        public void RectFill(int x, int y, int w, int h, int colour)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            x -= CameraX;
            y -= CameraY;
            for (int j = y; j < y + h; j++)
            {
                Span(x, x + w - 1, j, c);
            }
        }

        public void Circ(int cx, int cy, int r, int colour)
        {
            if (r < 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            cx -= CameraX;
            cy -= CameraY;
            int x = r, y = 0, err = 1 - r;
            while (x >= y)
            {
                Plot(cx + x, cy + y, c);
                Plot(cx - x, cy + y, c);
                Plot(cx + x, cy - y, c);
                Plot(cx - x, cy - y, c);
                Plot(cx + y, cy + x, c);
                Plot(cx - y, cy + x, c);
                Plot(cx + y, cy - x, c);
                Plot(cx - y, cy - x, c);
                y++;
                if (err < 0)
                {
                    err += 2 * y + 1;
                }
                else
                {
                    x--;
                    err += 2 * (y - x) + 1;
                }
            }
        }

        public void CircFill(int cx, int cy, int r, int colour)
        {
            if (r < 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            cx -= CameraX;
            cy -= CameraY;
            int x = r, y = 0, err = 1 - r;
            while (x >= y)
            {
                Span(cx - x, cx + x, cy + y, c);
                Span(cx - x, cx + x, cy - y, c);
                Span(cx - y, cx + y, cy + x, c);
                Span(cx - y, cx + y, cy - x, c);
                y++;
                if (err < 0)
                {
                    err += 2 * y + 1;
                }
                else
                {
                    x--;
                    err += 2 * (y - x) + 1;
                }
            }
        }

        // A box with rounded corners of radius r. The editors and the frame use these.
        public void RoundRectFill(int x, int y, int w, int h, int r, int colour)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            int[] insets = Insets(w, h, r);
            x -= CameraX;
            y -= CameraY;
            for (int j = 0; j < h; j++)
            {
                Span(x + insets[j], x + w - 1 - insets[j], y + j, c);
            }
        }

        public void RoundRect(int x, int y, int w, int h, int r, int colour)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }
            byte c = DrawColours[colour & 0xFF];
            int[] insets = Insets(w, h, r);
            x -= CameraX;
            y -= CameraY;
            // A pixel is on the edge when it's inside and one of its four neighbours isn't.
            for (int j = 0; j < h; j++)
            {
                int left = insets[j], right = w - 1 - insets[j];
                for (int i = left; i <= right; i++)
                {
                    bool edge = i == left || i == right || j == 0 || j == h - 1 ||
                                i < insets[j - 1] || i > w - 1 - insets[j - 1] ||
                                i < insets[j + 1] || i > w - 1 - insets[j + 1];
                    if (edge)
                    {
                        Plot(x + i, y + j, c);
                    }
                }
            }
        }

        // How far in each row of a rounded box starts, for a corner circle of radius r.
        public static int[] Insets(int w, int h, int r)
        {
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            var insets = new int[h];
            for (int j = 0; j < h; j++)
            {
                int k = Math.Min(j, h - 1 - j);
                if (k < r)
                {
                    double dy = r - k - 0.5;
                    insets[j] = (int)Math.Round(r - Math.Sqrt(r * r - dy * dy));
                }
            }
            return insets;
        }

        // Copies part of another picture here (sprites, the font sheet, the game screen into the
        // frame). Skips SeeThrough colours and swaps the rest through DrawColours.
        public void Blit(Surface source, int sx, int sy, int w, int h, int dx, int dy, bool flipX = false, bool flipY = false)
        {
            dx -= CameraX;
            dy -= CameraY;
            for (int j = 0; j < h; j++)
            {
                int ty = dy + j;
                int fromY = sy + (flipY ? h - 1 - j : j);
                if (ty < _clipY0 || ty >= _clipY1 || fromY < 0 || fromY >= source.Height)
                {
                    continue;
                }
                int fromRow = fromY * source.Width;
                int toRow = ty * Width;
                for (int i = 0; i < w; i++)
                {
                    int tx = dx + i;
                    int fromX = sx + (flipX ? w - 1 - i : i);
                    if (tx < _clipX0 || tx >= _clipX1 || fromX < 0 || fromX >= source.Width)
                    {
                        continue;
                    }
                    byte c = source.Pixels[fromRow + fromX];
                    if (!SeeThrough[c])
                    {
                        Pixels[toRow + tx] = DrawColours[c];
                    }
                }
            }
        }

        // Part of another picture stretched (or squashed) to dw x dh, nearest pixel, like Blit.
        public void BlitScaled(Surface source, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh, bool flipX = false, bool flipY = false)
        {
            if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0)
            {
                return;
            }
            dx -= CameraX;
            dy -= CameraY;
            for (int j = 0; j < dh; j++)
            {
                int ty = dy + j;
                if (ty < _clipY0 || ty >= _clipY1)
                {
                    continue;
                }
                int v = (int)((long)j * sh / dh);
                int fromY = sy + (flipY ? sh - 1 - v : v);
                if (fromY < 0 || fromY >= source.Height)
                {
                    continue;
                }
                int fromRow = fromY * source.Width;
                int toRow = ty * Width;
                for (int i = 0; i < dw; i++)
                {
                    int tx = dx + i;
                    if (tx < _clipX0 || tx >= _clipX1)
                    {
                        continue;
                    }
                    int u = (int)((long)i * sw / dw);
                    int fromX = sx + (flipX ? sw - 1 - u : u);
                    if (fromX < 0 || fromX >= source.Width)
                    {
                        continue;
                    }
                    byte c = source.Pixels[fromRow + fromX];
                    if (!SeeThrough[c])
                    {
                        Pixels[toRow + tx] = DrawColours[c];
                    }
                }
            }
        }

        // Text in the console font. Returns the x just past the last letter.
        public int Print(string text, int x, int y, int colour, int scale = 1)
        {
            return PixelFont.Draw(this, text, x, y, colour, scale);
        }

        private void Plot(int x, int y, byte c)
        {
            if (x >= _clipX0 && x < _clipX1 && y >= _clipY0 && y < _clipY1)
            {
                Pixels[y * Width + x] = c;
            }
        }

        // A row of pixels from x0 to x1 (both included), already moved by the camera.
        private void Span(int x0, int x1, int y, byte c)
        {
            if (y < _clipY0 || y >= _clipY1)
            {
                return;
            }
            if (x0 > x1)
            {
                int t = x0;
                x0 = x1;
                x1 = t;
            }
            if (x0 < _clipX0)
            {
                x0 = _clipX0;
            }
            if (x1 >= _clipX1)
            {
                x1 = _clipX1 - 1;
            }
            int row = y * Width;
            for (int x = x0; x <= x1; x++)
            {
                Pixels[row + x] = c;
            }
        }
    }
}
