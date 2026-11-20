using UnityEngine;

namespace SoapBox
{
    // A point-filtered texture that shows a Surface through a palette. Upload copies the picture
    // over (textures count rows from the bottom, surfaces from the top).
    internal sealed class PixelTexture
    {
        public readonly Texture2D Texture;
        private readonly Color32[] _pixels;
        private readonly int _width;
        private readonly int _height;

        public PixelTexture(int width, int height)
        {
            _width = width;
            _height = height;
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _pixels = new Color32[width * height];
        }

        public void Upload(Surface surface, Color32[] palette)
        {
            byte[] source = surface.Pixels;
            for (int y = 0; y < _height; y++)
            {
                int from = y * _width;
                int to = (_height - 1 - y) * _width;
                for (int x = 0; x < _width; x++)
                {
                    _pixels[to + x] = palette[source[from + x]];
                }
            }
            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
        }

        public void Destroy()
        {
            if (Texture != null)
            {
                Object.Destroy(Texture);
            }
        }

        // 0xAARRGGBB colours (256 of them, or fewer: the rest are see-through) as Unity colours.
        public static Color32[] Colours(uint[] argb, bool opaque)
        {
            var colours = new Color32[256];
            for (int i = 0; i < argb.Length && i < 256; i++)
            {
                uint c = argb[i];
                colours[i] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, opaque ? (byte)255 : (byte)(c >> 24));
            }
            return colours;
        }
    }
}
