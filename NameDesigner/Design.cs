using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace NameDesigner
{
    // One letter of a design and its colour as "RRGGBB", or null for the game's own text colour.
    internal sealed class Letter
    {
        public string Text;
        public string Color;

        public bool IsBlank => string.IsNullOrWhiteSpace(Text);
    }

    // A name being designed: its letters, each with its own colour. It turns into TextMeshPro rich
    // text, the same colour tags players can type into their names by hand, so everyone sees the
    // colours without the mod.
    internal sealed class Design
    {
        // A letter takes at most "<#RRGGBB>" + 2 characters + "</color>" = 19 characters, and the
        // colon colour "</color><#RRGGBB>" 17 more. The game sends at most 1000 characters of a name
        // (PlayerData.GetPlayerIdInfo), so 40 letters always fit.
        public const int MaxLetters = 40;

        // The colours TextMeshPro knows by name (TMP_Text.ValidateHtmlTag).
        private static readonly Dictionary<string, string> NamedColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", "FF0000" }, { "lightblue", "ADD8E6" }, { "blue", "0000FF" }, { "grey", "808080" },
            { "black", "000000" }, { "green", "00FF00" }, { "white", "FFFFFF" }, { "orange", "FF8000" },
            { "purple", "A020F0" }, { "yellow", "FFFF00" },
        };

        public readonly List<Letter> Letters = new List<Letter>();

        // The colour of the colon after the name in chat as "RRGGBB", or null for the game's own
        // colour (the same as the letters without a colour). Only player names have one.
        public string ColonColor;

        public int Count => Letters.Count;

        public string Plain
        {
            get
            {
                var text = new StringBuilder();
                foreach (Letter letter in Letters)
                {
                    text.Append(letter.Text);
                }
                return text.ToString();
            }
        }

        public bool IsBlank => string.IsNullOrWhiteSpace(Plain);

        public Design Clone()
        {
            var copy = new Design { ColonColor = ColonColor };
            foreach (Letter letter in Letters)
            {
                copy.Letters.Add(new Letter { Text = letter.Text, Color = letter.Color });
            }
            return copy;
        }

        public void Trim(int maxLetters)
        {
            if (Letters.Count > maxLetters)
            {
                Letters.RemoveRange(maxLetters, Letters.Count - maxLetters);
            }
        }

        // Changes the letters to `text`, keeping the colours of the letters that stay. New letters
        // take the colour of the letter they replace, or of the letter before them, so typing more
        // at the end of a coloured name carries on in its colour.
        public void SetText(string text, int maxLetters)
        {
            List<string> next = SplitLetters(Clean(text));
            if (next.Count > maxLetters)
            {
                next.RemoveRange(maxLetters, next.Count - maxLetters);
            }

            int prefix = 0;
            while (prefix < next.Count && prefix < Letters.Count && next[prefix] == Letters[prefix].Text)
            {
                prefix++;
            }
            int suffix = 0;
            while (suffix < next.Count - prefix && suffix < Letters.Count - prefix &&
                   next[next.Count - 1 - suffix] == Letters[Letters.Count - 1 - suffix].Text)
            {
                suffix++;
            }

            List<Letter> replaced = Letters.GetRange(prefix, Letters.Count - prefix - suffix);
            string around = prefix > 0 ? Letters[prefix - 1].Color
                : suffix > 0 ? Letters[Letters.Count - suffix].Color
                : null;
            var added = new List<Letter>();
            for (int i = prefix; i < next.Count - suffix; i++)
            {
                int j = i - prefix;
                string color = j < replaced.Count ? replaced[j].Color
                    : replaced.Count > 0 ? replaced[replaced.Count - 1].Color
                    : around;
                added.Add(new Letter { Text = next[i], Color = color });
            }
            Letters.RemoveRange(prefix, replaced.Count);
            Letters.InsertRange(prefix, added);
        }

        // The name as TextMeshPro rich text: "<#FF8800>Soa</color><#66CCFF>py</color>". Letters in a
        // row with the same colour share one tag, spaces never start a new one, and every tag is
        // closed, so nothing carries on into the text after the name (like a chat message).
        //
        // A colon colour is the one exception. Chat writes "<color=#player>" + name + ":</color> " +
        // message (TextChannelManager.AddMessageUI), so the name ends with "</color><#colon>": that
        // closes the game's colour, the colon gets ours, and the game's own "</color>" closes ours
        // again, so the message keeps the game's text colour. Elsewhere the extra "</color>" does
        // nothing (TextMeshPro never closes its base colour), but text the game puts right after
        // the name takes the colon colour too: " has joined the server!", " joined your pomodoro
        // session." and the player list's " (Host)".
        public string ToRichText()
        {
            var text = new StringBuilder();
            string open = null;
            foreach (Letter letter in Letters)
            {
                if (!letter.IsBlank && letter.Color != open)
                {
                    if (open != null)
                    {
                        text.Append("</color>");
                    }
                    if (letter.Color != null)
                    {
                        text.Append("<#").Append(letter.Color).Append('>');
                    }
                    open = letter.Color;
                }
                text.Append(letter.Text);
            }
            if (open != null)
            {
                text.Append("</color>");
            }
            if (ColonColor != null)
            {
                text.Append("</color><#").Append(ColonColor).Append('>');
            }
            return text.ToString();
        }

        // The same for Unity's immediate-mode GUI (the mod's own buttons), which spells colour tags
        // differently. Letters without a colour use the style's text colour. A colon colour shows
        // as a coloured colon after the name.
        public string ToGuiText()
        {
            var text = new StringBuilder();
            foreach (Letter letter in Letters)
            {
                if (letter.Color != null && !letter.IsBlank)
                {
                    text.Append("<color=#").Append(letter.Color).Append('>').Append(letter.Text).Append("</color>");
                }
                else
                {
                    text.Append(letter.Text);
                }
            }
            if (ColonColor != null)
            {
                text.Append("<color=#").Append(ColonColor).Append(">:</color>");
            }
            return text.ToString();
        }

        // Reads a name back into letters and colours. Understands colour tags written any way
        // TextMeshPro accepts them (<#F80>, <#FF8800>, <color=#FF8800>, <color="red">...). Other
        // tags, like sizes or bold, can't be edited here: they're left out and `dropped` is set.
        // With `withColon`, a colour left open after the last letter is the colon colour (see
        // ToRichText).
        public static Design Parse(string styled, bool withColon, out bool dropped)
        {
            dropped = false;
            var design = new Design();
            if (string.IsNullOrEmpty(styled))
            {
                return design;
            }

            var plain = new StringBuilder();
            var charColors = new List<string>();
            var stack = new List<string>();
            // How many characters came before each colour on the stack.
            var openedAt = new List<int>();
            for (int i = 0; i < styled.Length; i++)
            {
                char c = styled[i];
                if (c == '<')
                {
                    int end = styled.IndexOf('>', i + 1);
                    if (end > i)
                    {
                        if (!ReadTag(styled.Substring(i + 1, end - i - 1).Trim(), stack))
                        {
                            dropped = true;
                        }
                        // A tag adds or removes at most one colour.
                        if (openedAt.Count > stack.Count)
                        {
                            openedAt.RemoveAt(openedAt.Count - 1);
                        }
                        else if (openedAt.Count < stack.Count)
                        {
                            openedAt.Add(plain.Length);
                        }
                        i = end;
                        continue;
                    }
                }
                if (IsAllowed(c))
                {
                    plain.Append(c);
                    charColors.Add(stack.Count > 0 ? stack[stack.Count - 1] : null);
                }
            }

            string text = plain.ToString();
            int[] starts = StringInfo.ParseCombiningCharacters(text);
            for (int k = 0; k < starts.Length && design.Letters.Count < MaxLetters; k++)
            {
                int start = starts[k];
                int length = (k + 1 < starts.Length ? starts[k + 1] : text.Length) - start;
                design.Letters.Add(new Letter { Text = text.Substring(start, length), Color = charColors[start] });
            }
            if (withColon && stack.Count > 0 && openedAt[openedAt.Count - 1] == text.Length)
            {
                design.ColonColor = stack[stack.Count - 1];
            }
            return design;
        }

        // Handles one tag. Returns false for a tag this mod can't keep.
        private static bool ReadTag(string tag, List<string> stack)
        {
            if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase))
            {
                if (stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                }
                return true;
            }

            string value = null;
            if (tag.StartsWith("#", StringComparison.Ordinal))
            {
                value = tag;
            }
            else if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
            {
                value = tag.Substring(6).Trim().Trim('"', '\'');
            }
            if (value == null)
            {
                return false;
            }

            string hex = value.StartsWith("#", StringComparison.Ordinal) ? NormalizeHex(value.Substring(1))
                : NamedColors.TryGetValue(value, out string named) ? named
                : null;
            if (hex == null)
            {
                return false;
            }
            stack.Add(hex);
            return true;
        }

        // "F80", "F80C", "FF8800" or "FF8800CC" as "FF8800". Any see-through part is left out.
        // Null if it isn't a colour.
        public static string NormalizeHex(string hex)
        {
            if (hex == null)
            {
                return null;
            }
            hex = hex.Trim().TrimStart('#');
            foreach (char c in hex)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return null;
                }
            }
            switch (hex.Length)
            {
                case 3:
                case 4:
                    return new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] }).ToUpperInvariant();
                case 6:
                case 8:
                    return hex.Substring(0, 6).ToUpperInvariant();
                default:
                    return null;
            }
        }

        public static string ToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        public static Color FromHex(string hex)
        {
            return hex != null && ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : UnityEngine.Color.white;
        }

        // The letters as the player sees them: a letter with accents or an emoji counts as one.
        private static List<string> SplitLetters(string text)
        {
            var letters = new List<string>();
            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext())
            {
                letters.Add(e.GetTextElement());
            }
            return letters;
        }

        // No tag brackets (they'd start a tag) and no line breaks or other control characters.
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            var clean = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (IsAllowed(c))
                {
                    clean.Append(c);
                }
            }
            return clean.ToString();
        }

        private static bool IsAllowed(char c)
        {
            return c != '<' && c != '>' && !char.IsControl(c);
        }
    }
}
