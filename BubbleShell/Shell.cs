using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BubbleShell
{
    internal enum LineKind
    {
        Normal,
        Echo,   // the line you typed
        Hint,   // dim: tips, lists of options
        Warn,   // peach: couldn't do it right now
        Error,  // rose: something went wrong
    }

    // A command. Run prints with shell.Print and returns null, or returns a job: an IEnumerator the
    // shell steps every frame. A job yields null to wait a frame, or a number to wait that many seconds.
    internal delegate IEnumerator CommandRun(Shell shell, Args args);

    // Tab completion for a command's words: what word `index` (0 = the first after the name) could be.
    internal delegate IEnumerable<string> CommandComplete(Shell shell, Args args, int index);

    internal sealed class Command
    {
        public string Name;
        public string Group;
        public string Usage = "";
        public string Help = "";
        public string[] Aliases = new string[0];
        public CommandRun Run;
        public CommandComplete Complete;
    }

    // The words after a command's name.
    internal sealed class Args
    {
        private readonly List<string> _words;

        public Args(string name, List<string> words)
        {
            Name = name;
            _words = words;
        }

        public string Name { get; }
        public int Count => _words.Count;
        public string this[int i] => i >= 0 && i < _words.Count ? _words[i] : null;

        // The words from `from` on, joined with spaces (for say, echo...).
        public string Rest(int from = 0) => from >= _words.Count ? "" : string.Join(" ", _words.Skip(from));

        // The words from `from` on as a command line (alias, bind): one word is used as it is (the usual
        // "quoted commands"), several are joined back with their quotes.
        public string Line(int from) =>
            from >= _words.Count ? "" : from == _words.Count - 1 ? _words[from] : string.Join(" ", _words.Skip(from).Select(Shell.Quote));

        // True if word i is one of these (any case).
        public bool Is(int i, params string[] options)
        {
            string word = this[i];
            return word != null && options.Any(o => string.Equals(o, word, StringComparison.OrdinalIgnoreCase));
        }
    }

    // The console itself: commands, aliases, binds, history, the lines on screen and running jobs.
    // No Unity in here, so it can be tested outside the game.
    //
    // Lines are TextMeshPro rich text. Text from the player (or anything else not made by us) goes
    // through Escape, so a typed "<b>" shows as typed.
    internal sealed class Shell
    {
        public const int MaxLines = 1000;
        public const int MaxHistory = 500;
        public const string Prompt = "o> ";
        private const int MaxDepth = 16;

        // TextMeshPro colours: soft teal, with peach and rose when something's wrong.
        public const string TextColor = "#8FE3D9";    // soft teal
        public const string DimColor = "#5FA39E";     // muted teal: hints, the prompt
        public const string BrightColor = "#E0FFFA";  // pale aqua: what you typed
        public const string WarnColor = "#FFCDA8";    // peach
        public const string ErrorColor = "#FF9DB8";   // rose

        private readonly Dictionary<string, Command> _byName = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Command> _commands = new List<Command>();
        private readonly List<Job> _jobs = new List<Job>();
        private readonly List<string> _lines = new List<string>();
        private long _firstNumber;
        private int _quiet;

        private sealed class Job
        {
            public IEnumerator Steps;
            public string Name;
            public double WakeAt;
        }

        public readonly SortedDictionary<string, string> Aliases = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Key name (as KeyName gives it) -> command line.
        public readonly SortedDictionary<string, string> Binds = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> History = new List<string>();

        // Seconds since some start, for jobs. The game passes Unity's clock.
        public Func<double> Clock = () => 0;
        // How many letters fit across the console, for the train and other wide output.
        public int Columns = 80;
        // A key's name as binds store it, or null if there's no such key. The game checks Unity's key list.
        public Func<string, string> KeyName = key => string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
        // Called after aliases or binds change, so they can be saved.
        public Action ConfigChanged;
        // Where errors inside commands go (the BepInEx log in the game).
        public Action<Exception> LogError;
        // The folder exec reads files from.
        public string Folder = ".";

        // Goes up by one whenever the lines change, so the screen knows to redraw.
        public int Version { get; private set; }
        public IReadOnlyList<string> Lines => _lines;
        public IReadOnlyList<Command> Commands => _commands;
        public int RunningJobs => _jobs.Count;

        // ---- Commands ----

        public void Register(Command command)
        {
            if (_byName.ContainsKey(command.Name))
            {
                throw new ArgumentException($"There's already a command called {command.Name}.");
            }
            _commands.Add(command);
            _byName[command.Name] = command;
            foreach (string alias in command.Aliases)
            {
                _byName[alias] = command;
            }
        }

        public Command Find(string name) => name != null && _byName.TryGetValue(name, out Command command) ? command : null;

        // ---- Printing ----

        public void Print(string text, LineKind kind = LineKind.Normal)
        {
            if (_quiet > 0 && kind != LineKind.Warn && kind != LineKind.Error)
            {
                return;
            }
            foreach (string line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                Add(Colored(ColorOf(kind), Escape(line)));
            }
        }

        // Rich text made by the mod (names with their colours, mixed colours). Escape anything typed.
        public void PrintRich(string rich)
        {
            if (_quiet > 0)
            {
                return;
            }
            foreach (string line in (rich ?? "").Replace("\r", "").Split('\n'))
            {
                Add(line);
            }
        }

        public void Hint(string text) => Print(text, LineKind.Hint);
        public void Warn(string text) => Print(text, LineKind.Warn);
        public void Error(string text) => Print(text, LineKind.Error);

        // Every line printed gets a number, counting from the first line ever. Animations (the train,
        // progress bars) note the number of the next line, print, then change their lines in place,
        // so lines printed meanwhile (what you type) stay put.
        public long NextLineNumber => _firstNumber + _lines.Count;

        // Changes a line, if it's still on screen (not cleared or scrolled out of the buffer).
        public void SetLine(long number, string rich)
        {
            long i = number - _firstNumber;
            if (i >= 0 && i < _lines.Count)
            {
                _lines[(int)i] = rich;
                Version++;
            }
        }

        public void Clear()
        {
            _firstNumber += _lines.Count;
            _lines.Clear();
            Version++;
        }

        private void Add(string rich)
        {
            _lines.Add(rich);
            if (_lines.Count > MaxLines)
            {
                int extra = _lines.Count - MaxLines;
                _lines.RemoveRange(0, extra);
                _firstNumber += extra;
            }
            Version++;
        }

        public static string ColorOf(LineKind kind)
        {
            switch (kind)
            {
                case LineKind.Echo: return BrightColor;
                case LineKind.Hint: return DimColor;
                case LineKind.Warn: return WarnColor;
                case LineKind.Error: return ErrorColor;
                default: return TextColor;
            }
        }

        public static string Colored(string color, string rich) => $"<color={color}>{rich}</color>";

        // Text shown as typed: TextMeshPro reads nothing between noparse tags as styling. A typed
        // closing noparse tag is broken up with a zero-width space so it can't end it early.
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            if (text.IndexOf('<') < 0)
            {
                return text;
            }
            return "<noparse>" + Regex.Replace(text, "</noparse", "</nopars​e", RegexOptions.IgnoreCase) + "</noparse>";
        }

        // ---- Running ----

        // A line typed by the player: shown after the prompt, kept in the history, then run.
        public void Submit(string line)
        {
            line = line ?? "";
            Add(Colored(DimColor, Prompt) + Colored(BrightColor, Escape(line)));
            AddHistory(line);
            Run(line);
        }

        // Runs a line without showing it (binds, config files).
        public void Run(string line) => RunLine(line, 0);

        private void RunLine(string line, int depth)
        {
            foreach (string statement in SplitStatements(line))
            {
                RunStatement(statement, depth);
            }
        }

        private void RunStatement(string statement, int depth)
        {
            List<string> words = Tokenize(statement);
            if (words.Count == 0)
            {
                return;
            }
            string name = words[0];
            if (Aliases.TryGetValue(name, out string alias))
            {
                if (depth >= MaxDepth)
                {
                    Error($"Alias {name} runs itself over and over, so it was stopped.");
                    return;
                }
                // Words after an alias go on the end of what it runs: alias e "emote", then e 3 = emote 3.
                string extra = string.Join(" ", words.Skip(1).Select(Quote));
                RunLine(extra.Length > 0 ? alias + " " + extra : alias, depth + 1);
                return;
            }
            Command command = Find(name);
            if (command == null)
            {
                string guess = Closest(name);
                Warn(guess != null ? $"No command called {name}. Did you mean {guess}?" : $"No command called {name}. Type help to see them all.");
                return;
            }
            try
            {
                IEnumerator job = command.Run(this, new Args(command.Name, words.Skip(1).ToList()));
                if (job != null)
                {
                    _jobs.Add(new Job { Steps = job, Name = command.Name, WakeAt = Clock() });
                }
            }
            catch (Exception e)
            {
                Error($"{command.Name} went wrong: {e.Message}");
                LogError?.Invoke(e);
            }
        }

        // Steps every running job whose wait is over. Call once a frame.
        public void Step()
        {
            if (_jobs.Count == 0)
            {
                return;
            }
            double now = Clock();
            foreach (Job job in _jobs.ToList())
            {
                if (now < job.WakeAt || !_jobs.Contains(job))
                {
                    continue;
                }
                try
                {
                    if (!job.Steps.MoveNext())
                    {
                        _jobs.Remove(job);
                        continue;
                    }
                    object wait = job.Steps.Current;
                    job.WakeAt = wait is IConvertible number ? now + Convert.ToDouble(number) : now;
                }
                catch (Exception e)
                {
                    _jobs.Remove(job);
                    Error($"{job.Name} went wrong: {e.Message}");
                    LogError?.Invoke(e);
                }
            }
        }

        // Ctrl+C. Returns how many jobs were stopped.
        public int StopJobs()
        {
            int stopped = _jobs.Count;
            _jobs.Clear();
            return stopped;
        }

        // Runs each line of a config file (// and # start comments). Quietly shows only warnings and
        // errors (config.cfg, which only sets aliases and binds again at start).
        public void RunText(string text, bool quietly = false)
        {
            _quiet += quietly ? 1 : 0;
            try
            {
                foreach (string raw in (text ?? "").Replace("\r", "").Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    Run(line);
                }
            }
            finally
            {
                _quiet -= quietly ? 1 : 0;
            }
        }

        // A file in Folder, by name: "autoexec" finds autoexec.cfg. Null if there's none.
        public string FindFile(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(".."))
            {
                return null;
            }
            foreach (string candidate in new[] { name, name + ".cfg", name + ".txt" })
            {
                string path = Path.Combine(Folder, candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        // ---- History ----

        public void AddHistory(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || (History.Count > 0 && History[History.Count - 1] == line))
            {
                return;
            }
            History.Add(line);
            if (History.Count > MaxHistory)
            {
                History.RemoveRange(0, History.Count - MaxHistory);
            }
        }

        public void LoadHistory(string path)
        {
            History.Clear();
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    AddHistory(line);
                }
            }
        }

        public void SaveHistory(string path)
        {
            WriteAtomically(path, string.Join("\n", History) + "\n");
        }

        // ---- Aliases and binds, saved as commands ----

        public void SetAlias(string name, string line)
        {
            Aliases[name] = line;
            ConfigChanged?.Invoke();
        }

        public bool RemoveAlias(string name)
        {
            bool removed = Aliases.Remove(name);
            if (removed)
            {
                ConfigChanged?.Invoke();
            }
            return removed;
        }

        public void SetBind(string key, string line)
        {
            Binds[key] = line;
            ConfigChanged?.Invoke();
        }

        public bool RemoveBind(string key)
        {
            bool removed = Binds.Remove(key);
            if (removed)
            {
                ConfigChanged?.Invoke();
            }
            return removed;
        }

        // config.cfg: the aliases and binds as commands, run again at start.
        public string ConfigText()
        {
            var text = new StringBuilder();
            text.Append("// Bubble Shell writes this file whenever you change an alias or a bind, and runs it at start.\n");
            text.Append("// Put your own start-up commands in autoexec.cfg instead.\n");
            foreach (KeyValuePair<string, string> alias in Aliases)
            {
                text.Append("alias ").Append(Quote(alias.Key)).Append(' ').Append(Quote(alias.Value)).Append('\n');
            }
            foreach (KeyValuePair<string, string> bind in Binds)
            {
                text.Append("bind ").Append(Quote(bind.Key)).Append(' ').Append(Quote(bind.Value)).Append('\n');
            }
            return text.ToString();
        }

        public static void WriteAtomically(string path, string text)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        // ---- Tab completion ----

        // Completes the word at the end of `input`. Returns the new input; `options` gets the choices
        // when there's more than one.
        public string Complete(string input, out List<string> options)
        {
            options = new List<string>();
            input = input ?? "";
            int start = LastStatementStart(input);
            string statement = input.Substring(start);
            List<string> words = Tokenize(statement);
            bool newWord = statement.Length == 0 || char.IsWhiteSpace(statement[statement.Length - 1]) || words.Count == 0;
            string partial = newWord ? "" : words[words.Count - 1];
            int index = newWord ? words.Count : words.Count - 1;

            IEnumerable<string> candidates;
            if (index == 0)
            {
                candidates = _commands.Select(c => c.Name).Concat(Aliases.Keys);
            }
            else
            {
                Command command = Find(words[0]);
                if (command?.Complete == null)
                {
                    return input;
                }
                candidates = command.Complete(this, new Args(command.Name, words.Skip(1).Take(index - 1).ToList()), index - 1)
                             ?? Enumerable.Empty<string>();
            }

            List<string> matches = candidates
                .Where(c => !string.IsNullOrEmpty(c) && c.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (matches.Count == 0)
            {
                return input;
            }

            // Where the partial word starts in the input (it may have been typed with a quote).
            string before = input.Substring(0, input.Length - PartialLength(statement, newWord));
            if (matches.Count == 1)
            {
                return before + Quote(matches[0]) + " ";
            }
            options = matches;
            string common = CommonPrefix(matches);
            return common.Length > partial.Length ? before + common : input;
        }

        // How many characters at the end of the statement belong to the word being completed.
        private static int PartialLength(string statement, bool newWord)
        {
            if (newWord)
            {
                return 0;
            }
            bool quoted = false;
            int wordStart = 0;
            for (int i = 0; i < statement.Length; i++)
            {
                char c = statement[i];
                if (c == '\\' && quoted && i + 1 < statement.Length)
                {
                    i++;
                }
                else if (c == '"')
                {
                    if (!quoted && (i == 0 || char.IsWhiteSpace(statement[i - 1])))
                    {
                        wordStart = i;
                    }
                    quoted = !quoted;
                }
                else if (!quoted && char.IsWhiteSpace(c))
                {
                    wordStart = i + 1;
                }
            }
            return statement.Length - wordStart;
        }

        private static string CommonPrefix(List<string> words)
        {
            string first = words[0];
            int length = first.Length;
            foreach (string word in words.Skip(1))
            {
                int i = 0;
                while (i < length && i < word.Length && char.ToLowerInvariant(word[i]) == char.ToLowerInvariant(first[i]))
                {
                    i++;
                }
                length = i;
            }
            return first.Substring(0, length);
        }

        private static int LastStatementStart(string input)
        {
            bool quoted = false;
            int start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\\' && quoted && i + 1 < input.Length)
                {
                    i++;
                }
                else if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == ';' && !quoted)
                {
                    start = i + 1;
                }
            }
            return start;
        }

        // ---- Words ----

        // Splits a line at semicolons that aren't inside quotes.
        public static List<string> SplitStatements(string line)
        {
            var statements = new List<string>();
            if (string.IsNullOrEmpty(line))
            {
                return statements;
            }
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && quoted && i + 1 < line.Length)
                {
                    current.Append(c).Append(line[++i]);
                }
                else if (c == '"')
                {
                    quoted = !quoted;
                    current.Append(c);
                }
                else if (c == ';' && !quoted)
                {
                    statements.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            statements.Add(current.ToString());
            return statements.Where(s => s.Trim().Length > 0).ToList();
        }

        // Splits a statement into words. "Quoted words" stay together; \" and \\ inside quotes are a
        // quote and a backslash.
        public static List<string> Tokenize(string statement)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(statement))
            {
                return words;
            }
            var current = new StringBuilder();
            bool quoted = false;
            bool inWord = false;
            for (int i = 0; i < statement.Length; i++)
            {
                char c = statement[i];
                if (quoted)
                {
                    if (c == '\\' && i + 1 < statement.Length && (statement[i + 1] == '"' || statement[i + 1] == '\\'))
                    {
                        current.Append(statement[++i]);
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                    inWord = true;
                }
                else if (char.IsWhiteSpace(c))
                {
                    if (inWord)
                    {
                        words.Add(current.ToString());
                        current.Clear();
                        inWord = false;
                    }
                }
                else
                {
                    current.Append(c);
                    inWord = true;
                }
            }
            if (inWord)
            {
                words.Add(current.ToString());
            }
            return words;
        }

        // A word as it would be typed: quoted if it has spaces, quotes or semicolons in it.
        public static string Quote(string word)
        {
            if (word == null)
            {
                return "\"\"";
            }
            bool plain = word.Length > 0 && word.All(c => !char.IsWhiteSpace(c) && c != '"' && c != ';' && c != '\\');
            if (plain)
            {
                return word;
            }
            return "\"" + word.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // The command or alias name closest to a mistyped one, if it's close.
        private string Closest(string name)
        {
            string best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in _commands.Select(c => c.Name).Concat(Aliases.Keys))
            {
                int distance = Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return bestDistance <= Math.Max(1, Math.Min(2, name.Length / 3)) ? best : null;
        }

        // How many letters to add, remove, change or swap with a neighbour to get from a to b.
        private static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }
            for (int j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                    }
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
