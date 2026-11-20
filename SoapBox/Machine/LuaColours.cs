using System.Collections.Generic;

namespace SoapBox
{
    // Colours Lua code for the code editor, one line at a time. A line can start inside a long
    // comment or long string (--[[ ... ]] or [[ ... ]] spanning lines), so each line gets the state
    // the previous one ended in: 0 normal, otherwise inside a long bracket (see Pack).
    internal static class LuaColours
    {
        public const byte Plain = Palette.White;
        public const byte Keyword = Palette.Pink;
        public const byte Function = Palette.LightBlue;
        public const byte Number = Palette.Orange;
        public const byte Text = Palette.Yellow;
        public const byte Comment = Palette.Grey;
        public const byte Symbol = Palette.Peach;

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in",
            "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
        };

        // Soap Box's own functions and the Lua ones carts can use.
        private static readonly HashSet<string> Functions = new HashSet<string>
        {
            "cls", "color", "pset", "pget", "line", "rect", "rectf", "rrect", "rrectf", "circ", "circf", "print",
            "spr", "sspr", "map", "camera", "clip", "pal", "palt", "sget", "sset", "fget", "fset", "mget", "mset",
            "btn", "btnp", "mouse", "mousep", "sfx", "music", "rnd", "srand", "flr", "ceil", "abs", "sgn", "min",
            "max", "mid", "sqrt", "sin", "cos", "atan2", "time", "log",
            "_init", "_update", "_draw",
            "ipairs", "pairs", "next", "select", "type", "tostring", "tonumber", "error", "assert", "pcall",
            "xpcall", "setmetatable", "getmetatable", "rawget", "rawset", "rawequal", "rawlen", "unpack",
            "table", "string", "math", "coroutine", "os", "self",
        };

        // State: 0 normal; else 1 + level * 2 + (1 if a comment, 0 if a string), where level is the
        // number of = signs in the brackets ([[ is level 0, [==[ is level 2).
        private static int Pack(int level, bool comment)
        {
            return 1 + level * 2 + (comment ? 1 : 0);
        }

        // Fills colours[i] for each letter of line and returns the state the next line starts in.
        public static int Colour(string line, int state, byte[] colours)
        {
            int i = 0;
            int n = line.Length;
            if (state != 0)
            {
                int level = (state - 1) / 2;
                byte c = (state - 1) % 2 == 1 ? Comment : Text;
                int end = FindClose(line, 0, level);
                int stop = end < 0 ? n : end;
                for (; i < stop; i++)
                {
                    colours[i] = c;
                }
                if (end < 0)
                {
                    return state;
                }
            }

            while (i < n)
            {
                char ch = line[i];
                int start = i;

                // Comments: --[[ long ]] or -- to the end of the line.
                if (ch == '-' && i + 1 < n && line[i + 1] == '-')
                {
                    int level = LongOpen(line, i + 2);
                    if (level >= 0)
                    {
                        int open = i + 2 + level + 2;
                        int end = FindClose(line, open, level);
                        int stop = end < 0 ? n : end;
                        Fill(colours, start, stop, Comment);
                        if (end < 0)
                        {
                            return Pack(level, true);
                        }
                        i = end;
                        continue;
                    }
                    Fill(colours, start, n, Comment);
                    return 0;
                }

                // Long strings: [[ ... ]].
                if (ch == '[')
                {
                    int level = LongOpen(line, i);
                    if (level >= 0)
                    {
                        int open = i + level + 2;
                        int end = FindClose(line, open, level);
                        int stop = end < 0 ? n : end;
                        Fill(colours, start, stop, Text);
                        if (end < 0)
                        {
                            return Pack(level, false);
                        }
                        i = end;
                        continue;
                    }
                }

                // Short strings, with \ escapes.
                if (ch == '"' || ch == '\'')
                {
                    i++;
                    while (i < n && line[i] != ch)
                    {
                        i += line[i] == '\\' ? 2 : 1;
                    }
                    i = i < n ? i + 1 : n;
                    Fill(colours, start, i, Text);
                    continue;
                }

                // Numbers: 12, 3.5, .5, 1e3, 0xff.
                if (char.IsDigit(ch) || (ch == '.' && i + 1 < n && char.IsDigit(line[i + 1])))
                {
                    i++;
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '.' ||
                                     ((line[i] == '+' || line[i] == '-') && (line[i - 1] == 'e' || line[i - 1] == 'E'))))
                    {
                        i++;
                    }
                    Fill(colours, start, i, Number);
                    continue;
                }

                // Names.
                if (char.IsLetter(ch) || ch == '_')
                {
                    while (i < n && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                    {
                        i++;
                    }
                    string word = line.Substring(start, i - start);
                    // A name after a dot or colon is a field (t.x, s:sub), not a global.
                    bool field = start > 0 && (line[start - 1] == '.' || line[start - 1] == ':') &&
                                 !(start > 1 && line[start - 2] == '.');
                    byte c = Keywords.Contains(word) ? Keyword : !field && Functions.Contains(word) ? Function : Plain;
                    Fill(colours, start, i, c);
                    continue;
                }

                colours[i] = char.IsWhiteSpace(ch) ? Plain : Symbol;
                i++;
            }
            return 0;
        }

        // The level of a long bracket opening at i ("[[" = 0, "[=[" = 1...), or -1.
        private static int LongOpen(string line, int i)
        {
            if (i >= line.Length || line[i] != '[')
            {
                return -1;
            }
            int j = i + 1;
            while (j < line.Length && line[j] == '=')
            {
                j++;
            }
            return j < line.Length && line[j] == '[' ? j - i - 1 : -1;
        }

        // Where the closing bracket of the given level ends (just past it), from i on, or -1.
        private static int FindClose(string line, int i, int level)
        {
            for (int j = i; j < line.Length; j++)
            {
                if (line[j] != ']')
                {
                    continue;
                }
                int k = j + 1;
                int count = 0;
                while (k < line.Length && line[k] == '=')
                {
                    k++;
                    count++;
                }
                if (count == level && k < line.Length && line[k] == ']')
                {
                    return k + 1;
                }
            }
            return -1;
        }

        private static void Fill(byte[] colours, int from, int to, byte c)
        {
            for (int i = from; i < to; i++)
            {
                colours[i] = c;
            }
        }
    }
}
