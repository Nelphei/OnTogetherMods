using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace OnTogetherMods.Shared
{
    // Player names for mod text. Players can style their names with TextMeshPro rich text tags
    // (colours, sizes, gradients...), and the name tags above them show that styling. Mod text
    // drawn with TextBox shows it the same way.
    internal static class PlayerNames
    {
        // Rich text tags that stay on until closed. A name that leaves one open would carry it on
        // into the rest of the message, so ForText() closes them.
        private static readonly HashSet<string> Closable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "color", "b", "i", "u", "s", "size", "font", "font-weight", "material", "mark", "gradient",
            "cspace", "mspace", "voffset", "alpha", "indent", "line-height", "line-indent", "align",
            "rotate", "lowercase", "uppercase", "allcaps", "smallcaps", "sup", "sub", "width", "margin",
            "nobr", "link", "style", "noparse",
        };
        private static readonly Regex Tag = new Regex(@"<(/?)(#|[A-Za-z][\w-]*)[^<>]*>");

        private static TMP_FontAsset _font;
        private static float _nextFontLookup;

        // The name on this player's name tag, ready to go inside a message, or null if they don't
        // have one yet.
        public static string Of(PlayerController player)
        {
            string name = player != null && player.PlayerNameText != null ? player.PlayerNameText.text : null;
            return string.IsNullOrWhiteSpace(name) ? null : ForText(name.Trim());
        }

        // A name from somewhere other than a name tag (a Steam name), shown exactly as typed.
        public static string Plain(string name)
        {
            return name.Replace("<", "‹");
        }

        // The name without its tags, for sorting names by what they say.
        public static string WithoutTags(string name)
        {
            return Tag.Replace(name, "");
        }

        // A styled name, ready to go inside a message: any tags it leaves open are closed after it.
        public static string ForText(string name)
        {
            var open = new List<string>();
            bool noparse = false;
            foreach (Match match in Tag.Matches(name))
            {
                string tag = match.Groups[2].Value == "#" ? "color" : match.Groups[2].Value.ToLowerInvariant();
                bool closing = match.Groups[1].Value == "/";
                if (!Closable.Contains(tag) || (noparse && !(closing && tag == "noparse")))
                {
                    continue;   // not a tag that needs closing, or shown as plain text inside <noparse>
                }
                if (closing)
                {
                    int i = open.LastIndexOf(tag);
                    if (i >= 0)
                    {
                        open.RemoveAt(i);
                    }
                }
                else
                {
                    open.Add(tag);
                }
                noparse = open.Contains("noparse");
            }

            var text = new StringBuilder(name);
            for (int i = open.Count - 1; i >= 0; i--)
            {
                text.Append("</").Append(open[i]).Append('>');
            }
            return text.ToString();
        }

        // The font the name tags use (every name tag uses the same one, and it carries the game's
        // fallback fonts for other alphabets). Null until a player has been seen.
        public static TMP_FontAsset NameTagFont()
        {
            // Looked for at most once a second, since there are no players on the main menu.
            if (_font == null && Time.unscaledTime >= _nextFontLookup)
            {
                _nextFontLookup = Time.unscaledTime + 1f;
                PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                if (player != null && player.PlayerNameText != null)
                {
                    _font = player.PlayerNameText.font;
                }
            }
            return _font;
        }
    }
}
