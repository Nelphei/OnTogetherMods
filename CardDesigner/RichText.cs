using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CardDesigner
{
    // How one letter (or emoji, or line break) looks: the parts of TextMeshPro's rich text the
    // toolbar edits. A colour, highlight or size is kept as written in its tag ("#FF8800", "70%").
    internal struct Style : IEquatable<Style>
    {
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public bool Strike;
        public string Color;
        public string Mark;
        public string Size;

        public bool IsPlain => !Bold && !Italic && !Underline && !Strike && Color == null && Mark == null && Size == null;

        public bool Equals(Style other)
        {
            return Bold == other.Bold && Italic == other.Italic && Underline == other.Underline && Strike == other.Strike &&
                   Color == other.Color && Mark == other.Mark && Size == other.Size;
        }

        public override bool Equals(object obj) => obj is Style other && Equals(other);

        public override int GetHashCode() => (Color ?? "").GetHashCode() ^ (Size ?? "").GetHashCode();
    }

    internal enum Flag { Bold, Italic, Underline, Strike }

    internal enum PieceKind
    {
        Letter,   // one character (two for an emoji outside the basic range)
        Break,    // a line break
        Sprite,   // a <sprite> tag: shows as one picture
        Other,    // a tag that shows as a character (<nbsp>...), or a <noparse> block
    }

    // One thing the card shows, with its look and where it was in the text it was read from.
    internal sealed class Piece
    {
        public PieceKind Kind;
        public string Text;
        public Style Style;
        // Tags this mod doesn't edit (rotate, voffset, align...) that came just before this piece,
        // kept exactly as they were.
        public string Before;
        public int Start;
        public int End;

        public bool IsBlank => Kind == PieceKind.Break || (Kind == PieceKind.Letter && Text.Trim().Length == 0);
    }

    // A card line's rich text, read into pieces so the toolbar can restyle any run of them, and
    // written back with as few tags as possible.
    //
    // It reads tags the way TextMeshPro does: <b>, <i>, <u> and <s> count up and down, <color>,
    // <mark> and <size> stack, and each kind is separate from the others, so tags don't have to
    // nest. Anything that isn't a real TextMeshPro tag (like "<3") shows as letters, as it does in
    // the game.
    internal sealed class RichText
    {
        // Every tag TextMeshPro knows. A '<' that doesn't start one of these shows as itself.
        private static readonly HashSet<string> KnownTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "action", "align", "allcaps", "alpha", "b", "br", "color", "cr", "cspace", "font", "font-weight",
            "gradient", "i", "indent", "line-height", "line-indent", "link", "lowercase", "margin", "margin-left",
            "margin-right", "mark", "material", "mspace", "nbsp", "nobr", "noparse", "page", "pos", "rotate", "s",
            "shy", "size", "smallcaps", "space", "sprite", "style", "sub", "sup", "u", "uppercase", "voffset",
            "width", "zwj", "zwsp",
        };

        // TextMeshPro's colour names.
        private static readonly HashSet<string> ColourNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "red", "lightblue", "blue", "grey", "black", "green", "white", "orange", "purple", "yellow",
        };

        private static readonly Regex SizeValue = new Regex(@"^[+-]?\d+(\.\d+)?(%|em|px)?$", RegexOptions.CultureInvariant);

        public readonly List<Piece> Pieces = new List<Piece>();

        // Tags this mod doesn't edit that came after the last piece.
        public string Trailing = "";

        public int Count => Pieces.Count;

        // ---- Reading ----

        public static RichText Parse(string text)
        {
            var doc = new RichText();
            text = text ?? "";
            int bold = 0, italic = 0, underline = 0, strike = 0;
            var colours = new List<string>();
            var marks = new List<string>();
            var sizes = new List<string>();
            var before = new StringBuilder();

            int i = 0;
            while (i < text.Length)
            {
                int start = i;
                if (text[i] == '<' && TryReadTag(text, i, out Tag tag))
                {
                    i = tag.End;
                    switch (tag.Name)
                    {
                        case "b":
                            Step(ref bold, tag.Closing);
                            continue;
                        case "i":
                            Step(ref italic, tag.Closing);
                            continue;
                        case "u":
                            Step(ref underline, tag.Closing);
                            continue;
                        case "s":
                            Step(ref strike, tag.Closing);
                            continue;
                        case "color":
                            Stack(colours, tag);
                            continue;
                        case "mark":
                            Stack(marks, tag);
                            continue;
                        case "size":
                            Stack(sizes, tag);
                            continue;
                        case "br":
                            if (!tag.Closing)
                            {
                                Add(PieceKind.Break, text.Substring(start, i - start));
                                continue;
                            }
                            break;
                        case "sprite":
                            if (!tag.Closing)
                            {
                                Add(PieceKind.Sprite, text.Substring(start, i - start));
                                continue;
                            }
                            break;
                        case "nbsp":
                        case "zwsp":
                        case "zwj":
                        case "shy":
                        case "cr":
                            if (!tag.Closing)
                            {
                                Add(PieceKind.Other, text.Substring(start, i - start));
                                continue;
                            }
                            break;
                        case "noparse":
                            if (!tag.Closing)
                            {
                                // Everything up to </noparse> shows as letters. Kept as one piece.
                                int close = text.IndexOf("</noparse>", i, StringComparison.OrdinalIgnoreCase);
                                i = close < 0 ? text.Length : close + "</noparse>".Length;
                                Add(PieceKind.Other, text.Substring(start, i - start));
                                continue;
                            }
                            break;
                    }
                    // A tag this mod doesn't edit: kept as it was, in front of the next piece.
                    before.Append(text, start, i - start);
                    continue;
                }

                char c = text[i];
                int length = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                i += length;
                Add(c == '\n' ? PieceKind.Break : PieceKind.Letter, text.Substring(start, length));

                void Add(PieceKind kind, string pieceText)
                {
                    doc.Pieces.Add(new Piece
                    {
                        Kind = kind,
                        Text = pieceText,
                        Before = before.Length > 0 ? before.ToString() : null,
                        Start = start,
                        End = i,
                        Style = new Style
                        {
                            Bold = bold > 0,
                            Italic = italic > 0,
                            Underline = underline > 0,
                            Strike = strike > 0,
                            Color = Top(colours),
                            Mark = Top(marks),
                            Size = Top(sizes),
                        },
                    });
                    before.Clear();
                }
            }
            doc.Trailing = before.ToString();
            return doc;
        }

        private static void Step(ref int count, bool closing)
        {
            count = closing ? Math.Max(0, count - 1) : count + 1;
        }

        private static void Stack(List<string> stack, Tag tag)
        {
            if (!tag.Closing)
            {
                stack.Add(tag.Value);
            }
            else if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        private static string Top(List<string> stack) => stack.Count > 0 ? stack[stack.Count - 1] : null;

        private struct Tag
        {
            public string Name;
            public string Value;
            public bool Closing;
            public int End;
        }

        // Reads the tag starting at `at` (a '<'). False if TextMeshPro would show it as letters.
        private static bool TryReadTag(string text, int at, out Tag tag)
        {
            tag = default;
            int end = -1;
            for (int j = at + 1; j < text.Length && j - at <= 128; j++)
            {
                if (text[j] == '>')
                {
                    end = j;
                    break;
                }
                if (text[j] == '<')
                {
                    return false;
                }
            }
            if (end < 0)
            {
                return false;
            }
            string inner = text.Substring(at + 1, end - at - 1);
            bool closing = inner.StartsWith("/", StringComparison.Ordinal);
            string body = closing ? inner.Substring(1) : inner;
            if (body.Length == 0)
            {
                return false;
            }

            string name;
            string value = null;
            if (!closing && body[0] == '#')
            {
                // <#FF8800> is short for <color=#FF8800>.
                value = Hex(body);
                if (value == null)
                {
                    return false;
                }
                name = "color";
            }
            else
            {
                int cut = body.IndexOfAny(new[] { '=', ' ' });
                name = (cut < 0 ? body : body.Substring(0, cut)).Trim().ToLowerInvariant();
                if (!KnownTags.Contains(name))
                {
                    return false;
                }
                if (cut >= 0 && body[cut] == '=')
                {
                    value = body.Substring(cut + 1).Trim();
                }
                if (!closing && !ValueWorks(name, ref value))
                {
                    return false;
                }
            }
            tag = new Tag { Name = name, Value = value, Closing = closing, End = end + 1 };
            return true;
        }

        // The tags this mod edits only count when TextMeshPro would take their value; otherwise
        // the game shows them as letters.
        private static bool ValueWorks(string name, ref string value)
        {
            switch (name)
            {
                case "color":
                    if (value == null)
                    {
                        return false;
                    }
                    string hex = Hex(value);
                    if (hex != null)
                    {
                        value = hex;
                        return true;
                    }
                    return ColourNames.Contains(value.Trim('"'));
                case "mark":
                    return value != null && value.StartsWith("#", StringComparison.Ordinal);
                case "size":
                    return value != null && SizeValue.IsMatch(value);
                default:
                    return true;
            }
        }

        // "#f80" or "#FF8800" (with or without alpha) as "#FF8800" / "#FF8800CC". Null if it isn't one.
        public static string Hex(string text)
        {
            if (text == null)
            {
                return null;
            }
            string h = text.Trim().Trim('"');
            if (!h.StartsWith("#", StringComparison.Ordinal))
            {
                return null;
            }
            h = h.Substring(1);
            if (h.Length == 3 || h.Length == 4)
            {
                var sb = new StringBuilder();
                foreach (char c in h)
                {
                    sb.Append(c).Append(c);
                }
                h = sb.ToString();
            }
            if (h.Length != 6 && h.Length != 8)
            {
                return null;
            }
            foreach (char c in h)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return null;
                }
            }
            return "#" + h.ToUpperInvariant();
        }

        // ---- Writing ----

        public string Write()
        {
            var sb = new StringBuilder();
            Style current = default;
            foreach (Piece piece in Pieces)
            {
                if (piece.Before != null)
                {
                    sb.Append(piece.Before);
                }
                Change(sb, current, piece.Style);
                current = piece.Style;
                sb.Append(piece.Text);
            }
            sb.Append(Trailing);
            // Tags still open at the end stay open: each card line is its own text, and the game puts
            // an invisible character after it that should keep the last letters' size.
            return sb.ToString();
        }

        // The tags that change one look into the other. Closing ones first.
        private static void Change(StringBuilder sb, Style from, Style to)
        {
            if (from.Size != null && from.Size != to.Size)
            {
                sb.Append("</size>");
            }
            if (from.Mark != null && from.Mark != to.Mark)
            {
                sb.Append("</mark>");
            }
            if (from.Color != null && from.Color != to.Color)
            {
                sb.Append("</color>");
            }
            if (from.Bold && !to.Bold)
            {
                sb.Append("</b>");
            }
            if (from.Italic && !to.Italic)
            {
                sb.Append("</i>");
            }
            if (from.Underline && !to.Underline)
            {
                sb.Append("</u>");
            }
            if (from.Strike && !to.Strike)
            {
                sb.Append("</s>");
            }

            if (to.Size != null && to.Size != from.Size)
            {
                sb.Append("<size=").Append(to.Size).Append('>');
            }
            if (to.Mark != null && to.Mark != from.Mark)
            {
                sb.Append("<mark=").Append(to.Mark).Append('>');
            }
            if (to.Color != null && to.Color != from.Color)
            {
                sb.Append(ColourTag(to.Color));
            }
            if (to.Bold && !from.Bold)
            {
                sb.Append("<b>");
            }
            if (to.Italic && !from.Italic)
            {
                sb.Append("<i>");
            }
            if (to.Underline && !from.Underline)
            {
                sb.Append("<u>");
            }
            if (to.Strike && !from.Strike)
            {
                sb.Append("<s>");
            }
        }

        // The shortest tag for a colour: <#F80> when the colour allows it, <#FF8800> otherwise.
        private static string ColourTag(string colour)
        {
            string hex = Hex(colour);
            if (hex == null)
            {
                return "<color=" + colour + ">";
            }
            string digits = hex.Substring(1);
            var shortForm = new StringBuilder("#");
            for (int i = 0; i < digits.Length; i += 2)
            {
                if (digits[i] != digits[i + 1])
                {
                    return "<" + hex + ">";
                }
                shortForm.Append(digits[i]);
            }
            return "<" + shortForm + ">";
        }

        // ---- Finding pieces ----

        // The pieces that lie wholly between two places in the text this was read from: [from, to).
        public void PiecesBetween(int start, int end, out int from, out int to)
        {
            from = Pieces.Count;
            for (int k = 0; k < Pieces.Count; k++)
            {
                if (Pieces[k].Start >= start)
                {
                    from = k;
                    break;
                }
            }
            to = from;
            while (to < Pieces.Count && Pieces[to].End <= end)
            {
                to++;
            }
        }

        // The letters without any styling, for labels.
        public string Plain()
        {
            var sb = new StringBuilder();
            foreach (Piece piece in Pieces)
            {
                switch (piece.Kind)
                {
                    case PieceKind.Letter:
                        sb.Append(piece.Text);
                        break;
                    case PieceKind.Break:
                        sb.Append(' ');
                        break;
                }
            }
            return sb.ToString();
        }

        // ---- Restyling a run of pieces [from, to) ----

        public bool AllHave(int from, int to, Flag flag)
        {
            bool any = false;
            for (int k = from; k < to; k++)
            {
                if (Pieces[k].Kind == PieceKind.Break)
                {
                    continue;
                }
                any = true;
                if (!Has(Pieces[k].Style, flag))
                {
                    return false;
                }
            }
            return any;
        }

        public void SetFlag(int from, int to, Flag flag, bool on)
        {
            for (int k = from; k < to; k++)
            {
                Style s = Pieces[k].Style;
                switch (flag)
                {
                    case Flag.Bold: s.Bold = on; break;
                    case Flag.Italic: s.Italic = on; break;
                    case Flag.Underline: s.Underline = on; break;
                    case Flag.Strike: s.Strike = on; break;
                }
                Pieces[k].Style = s;
            }
        }

        private static bool Has(Style s, Flag flag)
        {
            switch (flag)
            {
                case Flag.Bold: return s.Bold;
                case Flag.Italic: return s.Italic;
                case Flag.Underline: return s.Underline;
                default: return s.Strike;
            }
        }

        public void SetColour(int from, int to, string colour)
        {
            for (int k = from; k < to; k++)
            {
                Pieces[k].Style.Color = colour;
            }
        }

        public void SetMark(int from, int to, string mark)
        {
            for (int k = from; k < to; k++)
            {
                Pieces[k].Style.Mark = mark;
            }
        }

        public void SetSize(int from, int to, string size)
        {
            for (int k = from; k < to; k++)
            {
                Pieces[k].Style.Size = size;
            }
        }

        public void ClearStyle(int from, int to)
        {
            for (int k = from; k < to; k++)
            {
                Pieces[k].Style = default;
            }
        }

        // The colour of the first piece in the run that has one, or null.
        public string FirstColour(int from, int to)
        {
            for (int k = from; k < to; k++)
            {
                if (Pieces[k].Style.Color != null && !Pieces[k].IsBlank)
                {
                    return Pieces[k].Style.Color;
                }
            }
            return null;
        }

        // The size every piece in the run has, or null when they differ or have none.
        public string CommonSize(int from, int to)
        {
            string size = null;
            for (int k = from; k < to; k++)
            {
                if (k == from)
                {
                    size = Pieces[k].Style.Size;
                }
                else if (Pieces[k].Style.Size != size)
                {
                    return null;
                }
            }
            return size;
        }

        // ---- Lining up the whole line ----

        // The alignment set by an <align> tag at the very start ("center", "right"), or null.
        public string Align
        {
            get
            {
                string first = Pieces.Count > 0 ? Pieces[0].Before : Trailing;
                if (first == null)
                {
                    return null;
                }
                Match m = Regex.Match(first, @"^<align=""?(\w+)""?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
            }
        }

        // Lines the whole text up: null (or "left") for the game's normal, "center" or "right".
        // Any other <align> tags in the line go.
        public void SetAlign(string align)
        {
            foreach (Piece piece in Pieces)
            {
                piece.Before = WithoutAlign(piece.Before);
            }
            Trailing = WithoutAlign(Trailing) ?? "";
            if (align == null || align == "left")
            {
                return;
            }
            string tag = "<align=" + align + ">";
            if (Pieces.Count > 0)
            {
                Pieces[0].Before = tag + Pieces[0].Before;
            }
            else
            {
                Trailing = tag + Trailing;
            }
        }

        private static string WithoutAlign(string tags)
        {
            if (string.IsNullOrEmpty(tags))
            {
                return tags;
            }
            string left = Regex.Replace(tags, @"</?align(=[^>]*)?>", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return left.Length > 0 ? left : null;
        }

        // ---- Colours ----

        public static UnityEngine.Color ToColour(string hex)
        {
            hex = Hex(hex);
            if (hex == null)
            {
                return UnityEngine.Color.white;
            }
            uint v = uint.Parse(hex.Substring(1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new UnityEngine.Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
        }

        public static string ToHex(UnityEngine.Color colour)
        {
            UnityEngine.Color32 c = colour;
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}";
        }
    }
}
