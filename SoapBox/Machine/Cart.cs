using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SoapBox
{
    // A cart: one game's code, sprites, map and (later) sounds, saved as a plain text file ending in
    // .soap, so it can be read, backed up and passed around like any text:
    //
    //   soap box cart 1
    //   == info ==
    //   name: bubble pop
    //   by: soapy
    //   == code ==
    //   (the Lua code, as typed)
    //   == sprites ==
    //   (128 rows of 128 hex digits, one colour per pixel; rows of only 0s at the end left out)
    //   == flags ==
    //   (4 rows of 128 hex digits: each sprite's flag byte, 64 sprites a row; left out when all 0)
    //   == map ==
    //   (64 rows of 256 hex digits: each tile's sprite number, two digits; empty rows at the end left out)
    //
    // A section line must match exactly. Sections this version doesn't know (from a newer one) are
    // kept as they are and written back.
    internal sealed class Cart
    {
        public const string Extension = ".soap";
        public const int SheetSize = 128;          // the sprite sheet: 16 x 16 sprites of 8 x 8 pixels
        public const int SpriteCount = 256;
        public const int MapWidth = 128;           // in tiles
        public const int MapHeight = 64;

        private const string Header = "soap box cart 1";
        private const string InfoMark = "== info ==";
        private const string CodeMark = "== code ==";
        private const string SpritesMark = "== sprites ==";
        private const string FlagsMark = "== flags ==";
        private const string MapMark = "== map ==";

        public string Name = "";
        public string Author = "";
        public string Code = "";
        public readonly Surface Sprites = new Surface(SheetSize, SheetSize);
        public readonly byte[] Flags = new byte[SpriteCount];
        public readonly byte[] Map = new byte[MapWidth * MapHeight];

        // Sections from a newer version, kept as they were: name line, then the lines under it.
        private readonly List<KeyValuePair<string, List<string>>> _unknown = new List<KeyValuePair<string, List<string>>>();

        // What a new cart starts with: something that runs straight away and shows the main ideas.
        public const string StarterCode =
            "-- my new cart\n" +
            "-- the arrows move the bubble, z changes its colour.\n" +
            "-- ctrl+r runs it, esc goes back to the game.\n" +
            "\n" +
            "x, y = 96, 64\n" +
            "c = 9\n" +
            "\n" +
            "function _update()\n" +
            "  if btn(\"left\") then x = x - 1 end\n" +
            "  if btn(\"right\") then x = x + 1 end\n" +
            "  if btn(\"up\") then y = y - 1 end\n" +
            "  if btn(\"down\") then y = y + 1 end\n" +
            "  if btnp(\"a\") then c = c % 15 + 1 end\n" +
            "end\n" +
            "\n" +
            "function _draw()\n" +
            "  cls(13)\n" +
            "  circf(x, y, 10, c)\n" +
            "  circ(x, y, 10, 3)\n" +
            "  pset(x - 4, y - 4, 3)\n" +
            "  print(\"hello from soap box!\", 4, 4, 3)\n" +
            "end";

        // An exact copy, so a running cart can change its sprites and map without touching the one
        // being edited and saved.
        public Cart Clone()
        {
            var copy = new Cart { Name = Name, Author = Author, Code = Code };
            Buffer.BlockCopy(Sprites.Pixels, 0, copy.Sprites.Pixels, 0, Sprites.Pixels.Length);
            Buffer.BlockCopy(Flags, 0, copy.Flags, 0, Flags.Length);
            Buffer.BlockCopy(Map, 0, copy.Map, 0, Map.Length);
            copy._unknown.AddRange(_unknown);
            return copy;
        }

        // Just the name from a cart file's text, without reading the rest (for the cart list).
        public static string ReadName(string text)
        {
            string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            bool info = false;
            foreach (string line in lines)
            {
                if (IsMark(line))
                {
                    if (info)
                    {
                        break;
                    }
                    info = line == InfoMark;
                    continue;
                }
                if (info && line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(5).Trim();
                }
            }
            return "";
        }

        public static Cart Parse(string text)
        {
            var cart = new Cart();
            string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string section = null;
            var body = new List<string>();
            foreach (string line in lines)
            {
                if (IsMark(line))
                {
                    cart.Take(section, body);
                    section = line;
                    body = new List<string>();
                }
                else if (section != null)
                {
                    body.Add(line);
                }
                // Lines before the first section (the header) are skipped.
            }
            cart.Take(section, body);
            return cart;
        }

        // A section line: "== " + a lowercase word + " ==", exactly.
        private static bool IsMark(string line)
        {
            if (line.Length < 7 || !line.StartsWith("== ", StringComparison.Ordinal) || !line.EndsWith(" ==", StringComparison.Ordinal))
            {
                return false;
            }
            for (int i = 3; i < line.Length - 3; i++)
            {
                if (line[i] < 'a' || line[i] > 'z')
                {
                    return false;
                }
            }
            return true;
        }

        private void Take(string section, List<string> body)
        {
            switch (section)
            {
                case null:
                    return;
                case InfoMark:
                    foreach (string line in body)
                    {
                        int colon = line.IndexOf(':');
                        if (colon < 0)
                        {
                            continue;
                        }
                        string key = line.Substring(0, colon).Trim().ToLowerInvariant();
                        string value = line.Substring(colon + 1).Trim();
                        if (key == "name")
                        {
                            Name = value;
                        }
                        else if (key == "by")
                        {
                            Author = value;
                        }
                    }
                    return;
                case CodeMark:
                    // The file ends with a newline, which isn't part of the code.
                    while (body.Count > 0 && body[body.Count - 1].Length == 0)
                    {
                        body.RemoveAt(body.Count - 1);
                    }
                    Code = string.Join("\n", body);
                    return;
                case SpritesMark:
                    for (int y = 0; y < SheetSize && y < body.Count; y++)
                    {
                        string row = body[y];
                        for (int x = 0; x < SheetSize && x < row.Length; x++)
                        {
                            Sprites.Pixels[y * SheetSize + x] = (byte)Hex(row[x]);
                        }
                    }
                    return;
                case FlagsMark:
                    ReadBytes(body, Flags, 64);
                    return;
                case MapMark:
                    ReadBytes(body, Map, MapWidth);
                    return;
                default:
                    _unknown.Add(new KeyValuePair<string, List<string>>(section, body));
                    return;
            }
        }

        // Rows of two-digit hex bytes, perRow bytes a row.
        private static void ReadBytes(List<string> rows, byte[] into, int perRow)
        {
            for (int r = 0; r < rows.Count && r * perRow < into.Length; r++)
            {
                string row = rows[r];
                for (int i = 0; i < perRow && 2 * i + 1 < row.Length; i++)
                {
                    into[r * perRow + i] = (byte)(Hex(row[2 * i]) * 16 + Hex(row[2 * i + 1]));
                }
            }
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            return 0;
        }

        public string Write()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            sb.Append(InfoMark).Append('\n');
            sb.Append("name: ").Append(OneLine(Name)).Append('\n');
            sb.Append("by: ").Append(OneLine(Author)).Append('\n');
            sb.Append(CodeMark).Append('\n');
            if (Code.Length > 0)
            {
                // A line in the code that looks like a section line would end the code there.
                foreach (string line in Code.Replace("\r\n", "\n").Split('\n'))
                {
                    sb.Append(IsMark(line) ? " " + line : line).Append('\n');
                }
            }

            int spriteRows = SheetSize;
            while (spriteRows > 0 && AllZero(Sprites.Pixels, (spriteRows - 1) * SheetSize, SheetSize))
            {
                spriteRows--;
            }
            if (spriteRows > 0)
            {
                sb.Append(SpritesMark).Append('\n');
                for (int y = 0; y < spriteRows; y++)
                {
                    for (int x = 0; x < SheetSize; x++)
                    {
                        sb.Append("0123456789abcdef"[Sprites.Pixels[y * SheetSize + x] & 15]);
                    }
                    sb.Append('\n');
                }
            }

            if (!AllZero(Flags, 0, Flags.Length))
            {
                sb.Append(FlagsMark).Append('\n');
                WriteBytes(sb, Flags, 64, Flags.Length / 64);
            }

            int mapRows = MapHeight;
            while (mapRows > 0 && AllZero(Map, (mapRows - 1) * MapWidth, MapWidth))
            {
                mapRows--;
            }
            if (mapRows > 0)
            {
                sb.Append(MapMark).Append('\n');
                WriteBytes(sb, Map, MapWidth, mapRows);
            }

            foreach (KeyValuePair<string, List<string>> section in _unknown)
            {
                sb.Append(section.Key).Append('\n');
                foreach (string line in section.Value)
                {
                    sb.Append(line).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static void WriteBytes(StringBuilder sb, byte[] bytes, int perRow, int rows)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int i = 0; i < perRow; i++)
                {
                    sb.Append(bytes[r * perRow + i].ToString("x2", CultureInfo.InvariantCulture));
                }
                sb.Append('\n');
            }
        }

        private static bool AllZero(byte[] bytes, int start, int count)
        {
            for (int i = start; i < start + count; i++)
            {
                if (bytes[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static string OneLine(string text)
        {
            return (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
