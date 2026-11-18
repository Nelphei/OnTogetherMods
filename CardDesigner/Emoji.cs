using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CardDesigner
{
    // The emoji and symbols the card can show, for everyone, without the mod.
    //
    // Emoji: the game ships TextMeshPro's EmojiOne sprite sheet (15 smileys) where TextMeshPro looks
    // up sprite sheets by name, so a <sprite="EmojiOne" index=N> tag shows the picture in any of the
    // game's text. Typing an emoji character doesn't work: the game's default sprite sheet holds only
    // keyboard keys and doesn't fall back to EmojiOne.
    //
    // Symbols: the card's font (Fredoka) falls back to Noto fonts (Japanese, Korean, Chinese, Thai),
    // which have hearts, stars, music notes, weather, arrows and more. Each one is checked against the
    // font when the list is first built, so a game update that changes the fonts can't add boxes.
    internal sealed class Emoji
    {
        public const string SheetName = "EmojiOne";

        // Grouped: hearts and flowers, music, weather and nature, cards and games, shapes, arrows,
        // faces and bits for making faces, writing, numbers, lines and blocks.
        private const string Candidates =
            "♥♡★☆✿❀✽❖♪♫♬♩♭♮♯" +
            "☀☁☂☃☉☯♨♻⚠☎☏✂✓✚♀♂" +
            "♠♣♦♤♧♢⚽⚾☜☞☝☟" +
            "●○◎◉◆◇■□▲△▼▽▶▷◀◁◐◑◒◓◢◣◤◥▪▫◦◯◊" +
            "←→↑↓↔↕↖↗↘↙⇐⇒⇔⇦⇨⇧⇩➡⬅⬆⬇" +
            "ツᴗωᵕ‿︶･｡ﾟ°˚∀⊂⊃´｀⌒╯╰⊙ʚɞᵔᴥノヽʘ" +
            "※∞≈≠±×÷√∴§¶†‡•…‼⁉™©®℃№〜「」『』【】《》〈〉" +
            "①②③④⑤⑥⑦⑧⑨⑩❶❷❸❹❺" +
            "─│═║░▒▓█";

        public struct Picture
        {
            public int Index;
            public string Name;
            public Rect Uv;
        }

        private TMP_SpriteAsset _sheet;
        private bool _lookedForSheet;
        private readonly List<Picture> _sprites = new List<Picture>();
        private List<string> _symbols;
        private TMP_FontAsset _symbolsFont;

        public Texture SheetTexture => Sheet != null ? _sheet.spriteSheet : null;

        public IReadOnlyList<Picture> Pictures
        {
            get
            {
                TMP_SpriteAsset unused = Sheet;
                return _sprites;
            }
        }

        public static string Tag(int index) => "<sprite=\"" + SheetName + "\" index=" + index + ">";

        // Loaded the way TextMeshPro loads it for a <sprite="EmojiOne"> tag.
        private TMP_SpriteAsset Sheet
        {
            get
            {
                if (!_lookedForSheet)
                {
                    _lookedForSheet = true;
                    try
                    {
                        _sheet = Resources.Load<TMP_SpriteAsset>(TMP_Settings.defaultSpriteAssetPath + SheetName);
                        ReadSprites();
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"Couldn't load the {SheetName} sprite sheet: {e.Message}");
                    }
                    if (_sheet == null)
                    {
                        Plugin.Log.LogWarning($"The game's {SheetName} sprite sheet wasn't found, so the emoji are left out.");
                    }
                }
                return _sheet;
            }
        }

        private void ReadSprites()
        {
            if (_sheet == null || _sheet.spriteSheet == null)
            {
                return;
            }
            float w = _sheet.spriteSheet.width;
            float h = _sheet.spriteSheet.height;
            List<TMP_SpriteCharacter> characters = _sheet.spriteCharacterTable;
            for (int i = 0; i < characters.Count; i++)
            {
                TMP_SpriteCharacter character = characters[i];
                // The sheet's "missing" picture.
                if (character == null || character.glyph == null || character.name == ".notdef")
                {
                    continue;
                }
                UnityEngine.TextCore.GlyphRect r = character.glyph.glyphRect;
                _sprites.Add(new Picture
                {
                    Index = i,
                    Name = character.name,
                    // Glyph rects count up from the bottom-left, like texture coordinates.
                    Uv = new Rect(r.x / w, r.y / h, r.width / w, r.height / h),
                });
            }
        }

        // The symbols the font (or its fallbacks) has.
        public IReadOnlyList<string> Symbols(TMP_FontAsset font)
        {
            if (_symbols != null && font == _symbolsFont)
            {
                return _symbols;
            }
            _symbolsFont = font;
            _symbols = new List<string>();
            foreach (char c in Candidates)
            {
                if (font == null || Has(font, c))
                {
                    _symbols.Add(c.ToString());
                }
            }
            return _symbols;
        }

        private static bool Has(TMP_FontAsset font, char c)
        {
            try
            {
                // The game's fonts are dynamic: a symbol is added to the font's texture the first time
                // it's asked for, which is what showing it would do anyway.
                return font.HasCharacter(c, true, true);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
