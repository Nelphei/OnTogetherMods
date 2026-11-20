using System;
using System.Collections.Generic;
using System.IO;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BubbleShell
{
    // The console's font: Monogram, a free (CC0) pixel font by datagoblin, loaded from the file beside
    // the DLL. Its pixels are 64 font units each (1024 to the em), so at 16 points one font pixel is
    // one screen pixel; the console draws it at whole multiples of that.
    //
    // TextMeshPro picks a shader when it builds a font from a file: the game has its Sprite shader
    // (used here for crisp, unsmoothed pixels) and its mobile distance-field shader (smooth edges,
    // used if the crisp try comes out wrong), but not the bitmap one. Letters the font doesn't have
    // (other alphabets in player names) come from the name tags' font.
    internal static class ShellFont
    {
        public const string FileName = "monogram-extended.ttf";
        // Font units per pixel of the font: 1024 to the em / 16 pixels.
        public const float PixelsPerEm = 16f;
        // Every letter is this many font pixels wide (it's monospaced).
        public const float Advance = 6f;

        private static TMP_FontAsset _font;
        private static bool _tried;
        private static bool _fallbackAdded;

        // True when the font is drawn as crisp pixels (the console then sizes it in whole pixels).
        public static bool Crisp { get; private set; }

        // True when it's the pixel font (monospaced), not the game's.
        public static bool IsPixelFont => _font != null;

        // The font to use, or null for the game's own font.
        public static TMP_FontAsset Get()
        {
            if (!_tried)
            {
                _tried = true;
                if (Plugin.Settings.PixelFont.Value)
                {
                    Load();
                }
            }
            if (_font != null && !_fallbackAdded)
            {
                TMP_FontAsset names = PlayerNames.NameTagFont();
                if (names != null)
                {
                    _fallbackAdded = true;
                    _font.fallbackFontAssetTable = new List<TMP_FontAsset> { names };
                }
            }
            if (_font != null && Crisp)
            {
                // New atlas pages are made as letters come in; keep them all unsmoothed.
                foreach (Texture2D page in _font.atlasTextures)
                {
                    if (page != null && page.filterMode != FilterMode.Point)
                    {
                        page.filterMode = FilterMode.Point;
                    }
                }
            }
            return _font;
        }

        private static void Load()
        {
            string path = Path.Combine(Plugin.ModFolder, FileName);
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning($"{FileName} isn't beside the DLL, so the console uses the game's font.");
                return;
            }
            try
            {
                if (Shader.Find("TextMeshPro/Sprite") != null)
                {
                    // Colour glyphs: an RGBA atlas drawn with the Sprite shader, no smoothing.
                    TMP_FontAsset crisp = TMP_FontAsset.CreateFontAsset(path, 0, (int)PixelsPerEm, 2, GlyphRenderMode.COLOR_HINTED, 512, 512);
                    if (crisp != null && LooksRight(crisp))
                    {
                        _font = crisp;
                        Crisp = true;
                        Name("crisp pixels");
                        return;
                    }
                    if (crisp != null)
                    {
                        UnityEngine.Object.Destroy(crisp);
                    }
                }
                if (Shader.Find("TextMeshPro/Mobile/Distance Field") != null)
                {
                    TMP_FontAsset smooth = TMP_FontAsset.CreateFontAsset(path, 0, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024);
                    if (smooth != null)
                    {
                        _font = smooth;
                        Name("smooth edges");
                        return;
                    }
                }
                Plugin.Log.LogWarning("Couldn't make the pixel font, so the console uses the game's font.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't load the pixel font ({e.Message}), so the console uses the game's font.");
                _font = null;
            }
        }

        private static void Name(string how)
        {
            _font.name = "BubbleShell Monogram";
            Plugin.Log.LogInfo($"Loaded the Monogram pixel font ({how}).");
        }

        // The crisp try works only if the font engine draws the letters white (the text's colour is
        // multiplied in). Draws "M" and checks the atlas.
        private static bool LooksRight(TMP_FontAsset font)
        {
            try
            {
                if (!font.TryAddCharacters("M", out string missing) || !string.IsNullOrEmpty(missing))
                {
                    Plugin.Log.LogInfo("Crisp pixel font: couldn't draw a letter.");
                    return false;
                }
                Texture2D atlas = font.atlasTextures != null && font.atlasTextures.Length > 0 ? font.atlasTextures[0] : null;
                if (atlas == null || !atlas.isReadable)
                {
                    Plugin.Log.LogInfo("Crisp pixel font: can't read its atlas.");
                    return false;
                }
                int ink = 0;
                int white = 0;
                foreach (Color32 pixel in atlas.GetPixels32())
                {
                    if (pixel.a > 128)
                    {
                        ink++;
                        if (pixel.r > 200 && pixel.g > 200 && pixel.b > 200)
                        {
                            white++;
                        }
                    }
                }
                Plugin.Log.LogInfo($"Crisp pixel font check: {ink} inked pixels, {white} white.");
                return ink > 0 && white * 10 >= ink * 9;
            }
            catch (Exception e)
            {
                Plugin.Log.LogInfo($"Crisp pixel font check failed: {e.Message}");
                return false;
            }
        }

        public static void Destroy()
        {
            if (_font != null)
            {
                UnityEngine.Object.Destroy(_font);
                _font = null;
            }
        }
    }
}
