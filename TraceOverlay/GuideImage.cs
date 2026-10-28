using System;
using System.IO;
using UnityEngine;

namespace TraceOverlay
{
    // Loads a .png or .jpg into a texture for the guide.
    internal static class GuideImage
    {
        // Boards are only 320 x 180 squares, so bigger images just use memory. Larger ones are shrunk.
        private const int MaxSize = 1600;

        // Returns null and sets error when the file can't be used.
        public static Texture2D Load(string path, out string error)
        {
            error = null;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(source);
                error = "isn't a .png or .jpg the game can read";
                return null;
            }

            Texture2D result = source.width > MaxSize || source.height > MaxSize ? Shrink(source) : source;
            if (result != source)
            {
                UnityEngine.Object.Destroy(source);
            }
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = FilterMode.Bilinear;
            result.name = "TraceOverlay " + Path.GetFileName(path);
            return result;
        }

        // Picks every nth pixel. Rough, but it's only a guide.
        private static Texture2D Shrink(Texture2D source)
        {
            float scale = (float)MaxSize / Math.Max(source.width, source.height);
            int width = Math.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Math.Max(1, Mathf.RoundToInt(source.height * scale));

            Color32[] from = source.GetPixels32();
            var to = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy = Math.Min(source.height - 1, (int)((y + 0.5f) / scale));
                for (int x = 0; x < width; x++)
                {
                    int sx = Math.Min(source.width - 1, (int)((x + 0.5f) / scale));
                    to[y * width + x] = from[sy * source.width + sx];
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, true);
            result.SetPixels32(to);
            result.Apply(true, true);
            return result;
        }
    }
}
