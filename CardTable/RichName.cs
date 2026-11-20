using System;
using System.Collections.Generic;
using System.Text;

namespace CardTable
{
    // Player names in the Card Table window. Players style their names with TextMeshPro rich text
    // tags, but the window is drawn with Unity's immediate-mode GUI (the cards and the table are
    // IMGUI textures, which would cover a TextMeshPro canvas), whose rich text knows only b, i, size
    // and color, and shows any other tag as code. This keeps colours, bold and italic, and drops the
    // other styling (sizes, gradients, underline...), so names come out in their colours at the
    // window's size.
    internal static class RichName
    {
        // Tags TextMeshPro reads (others it shows as text, and so does this).
        private static readonly HashSet<string> TmpTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "align", "allcaps", "alpha", "b", "br", "color", "cspace", "font", "font-weight", "gradient", "i",
            "indent", "line-height", "line-indent", "link", "lowercase", "margin", "mark", "material", "mspace",
            "nobr", "noparse", "page", "pos", "rotate", "s", "size", "smallcaps", "space", "sprite", "strikethrough",
            "style", "sub", "sup", "u", "uppercase", "voffset", "width", "zwsp", "zwj", "underline", "shy",
        };

        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "black", "000000FF" }, { "blue", "0000FFFF" }, { "green", "00FF00FF" }, { "orange", "FF8000FF" },
            { "purple", "A020F0FF" }, { "red", "FF0000FF" }, { "white", "FFFFFFFF" }, { "yellow", "FFFF00FF" },
        };

        private struct Open
        {
            public string Tag;      // "b", "i" or "color"
            public string Opener;   // what was written, e.g. "<color=#FF8000FF>"; null if it wasn't (a colour it couldn't read)
        }

        public static string ForImgui(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            var text = new StringBuilder(name.Length + 16);
            var open = new List<Open>();
            bool noparse = false;
            int i = 0;
            while (i < name.Length)
            {
                char c = name[i];
                if (c == '<')
                {
                    int end = name.IndexOf('>', i + 1);
                    int next = name.IndexOf('<', i + 1);
                    if (end > i && (next < 0 || next > end))
                    {
                        string inner = name.Substring(i + 1, end - i - 1).Trim();
                        if (noparse)
                        {
                            if (inner.Equals("/noparse", StringComparison.OrdinalIgnoreCase))
                            {
                                noparse = false;
                                i = end + 1;
                                continue;
                            }
                        }
                        else if (Tag(inner, text, open, ref noparse))
                        {
                            i = end + 1;
                            continue;
                        }
                    }
                    // Not a tag: shown as it is, with a look-alike so IMGUI doesn't read it as one.
                    text.Append('‹');
                    i++;
                    continue;
                }
                text.Append(c);
                i++;
            }
            for (int k = open.Count - 1; k >= 0; k--)
            {
                if (open[k].Opener != null)
                {
                    text.Append("</").Append(open[k].Tag).Append('>');
                }
            }
            return text.ToString();
        }

        // A name with its tags taken out, for measuring and sorting.
        public static string Plain(string imgui)
        {
            var text = new StringBuilder(imgui.Length);
            bool inTag = false;
            foreach (char c in imgui)
            {
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>' && inTag)
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    text.Append(c);
                }
            }
            return text.ToString();
        }

        // Handles one tag. False if TextMeshPro wouldn't read it as a tag either.
        private static bool Tag(string inner, StringBuilder text, List<Open> open, ref bool noparse)
        {
            if (inner.Length == 0)
            {
                return false;
            }
            bool closing = inner[0] == '/';
            string body = closing ? inner.Substring(1).Trim() : inner;
            string name;
            string value = null;
            if (body.StartsWith("#", StringComparison.Ordinal))
            {
                name = "color";
                value = body;
            }
            else
            {
                int cut = body.IndexOfAny(new[] { '=', ' ' });
                name = cut < 0 ? body : body.Substring(0, cut);
                if (cut >= 0 && body[cut] == '=')
                {
                    value = body.Substring(cut + 1).Trim();
                }
            }
            if (!TmpTags.Contains(name))
            {
                return false;
            }
            name = name.ToLowerInvariant();
            switch (name)
            {
                case "b":
                case "i":
                    if (closing)
                    {
                        Close(name, text, open);
                    }
                    else
                    {
                        string opener = "<" + name + ">";
                        text.Append(opener);
                        open.Add(new Open { Tag = name, Opener = opener });
                    }
                    return true;
                case "color":
                    if (closing)
                    {
                        Close(name, text, open);
                    }
                    else
                    {
                        string hex = Colour(value);
                        string opener = hex != null ? "<color=#" + hex + ">" : null;
                        if (opener != null)
                        {
                            text.Append(opener);
                        }
                        open.Add(new Open { Tag = name, Opener = opener });
                    }
                    return true;
                case "noparse":
                    if (!closing)
                    {
                        noparse = true;
                    }
                    return true;
                default:
                    return true;   // styling IMGUI can't show: left out
            }
        }

        // Closes the latest open tag of this kind. IMGUI needs tags nested properly, so any opened
        // after it are closed first and opened again after.
        private static void Close(string tag, StringBuilder text, List<Open> open)
        {
            int at = open.FindLastIndex(o => o.Tag == tag);
            if (at < 0)
            {
                return;   // nothing to close (a name ending in "</color>" before its colon colour)
            }
            for (int k = open.Count - 1; k > at; k--)
            {
                if (open[k].Opener != null)
                {
                    text.Append("</").Append(open[k].Tag).Append('>');
                }
            }
            if (open[at].Opener != null)
            {
                text.Append("</").Append(tag).Append('>');
            }
            open.RemoveAt(at);
            for (int k = at; k < open.Count; k++)
            {
                if (open[k].Opener != null)
                {
                    text.Append(open[k].Opener);
                }
            }
        }

        // "#RGB", "#RGBA", "#RRGGBB", "#RRGGBBAA" or a colour name, as RRGGBBAA. Null if it isn't one.
        private static string Colour(string value)
        {
            if (value == null)
            {
                return null;
            }
            value = value.Trim().Trim('"', '\'');
            if (Named.TryGetValue(value, out string named))
            {
                return named;
            }
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                return null;
            }
            string hex = value.Substring(1);
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
                    return $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}FF".ToUpperInvariant();
                case 4:
                    return $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}".ToUpperInvariant();
                case 6:
                    return (hex + "FF").ToUpperInvariant();
                case 8:
                    return hex.ToUpperInvariant();
                default:
                    return null;
            }
        }
    }
}
