using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OnTogetherMods.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubbleShell
{
    // The console: opens and closes it (the ` key or the icon), owns the keyboard while you type,
    // handles the editing keys, runs binds, steps running commands and draws it all through ShellView.
    internal sealed class ShellWindow : MonoBehaviour
    {
        private const float SlideSeconds = 0.15f;
        private const float RepeatDelay = 0.4f;
        private const float RepeatRate = 0.035f;
        private const string ConfigFile = "config.cfg";
        private const string AutoexecFile = "autoexec.cfg";
        private const string HistoryFile = "history.txt";
        private const string MotdFile = "motd.txt";

        private static ShellWindow _instance;

        // While true the game ignores the keys (see Patches).
        internal static bool HoldsKeys => _instance != null && _instance.KeysToShell;
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private Shell _shell;
        private ShellView _view;
        // Made the first time it's needed (in the world), not while the game starts.
        private ShellView View => _view ?? (_view = new ShellView());
        private ClickBlocker _blocker;

        private bool _open;
        private bool _hasKeys;
        private float _slide;
        private bool _iconClicked;
        private bool _ranAutoexec;
        private int _handBacks = ShellLink.HandBackCount;

        // The line being typed, and the cursor in it.
        private string _input = "";
        private int _caret;
        private int _shift;          // the first letter shown when the line is wider than the console
        private int _historyIndex;   // History.Count = the line being typed, not one from the history
        private string _draft = "";
        private float _typedAt;

        // Lines scrolled back from the newest.
        private int _scroll;
        private long _seenNext;

        private readonly Dictionary<KeyCode, float> _repeatAt = new Dictionary<KeyCode, float>();
        private readonly Dictionary<string, KeyCode> _bindKeys = new Dictionary<string, KeyCode>();

        private float _fps;
        private int _fpsFrames;
        private float _fpsSince;

        private bool KeysToShell => _open && _hasKeys && Game.InWorld && !PanelStack.Hidden;

        // ---- Setting up ----

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "BubbleShell ClickBlocker");

            _shell = new Shell
            {
                Clock = () => Time.unscaledTimeAsDouble,
                KeyName = KeyName,
                Folder = Settings.DataFolder(),
                LogError = e => Plugin.Log.LogError(e),
                ConfigChanged = SaveConfig,
            };
            BasicCommands.KeyWarning = KeyWarning;
            BasicCommands.Register(_shell);
            _shell.Register(new Command
            {
                Name = "motd",
                Aliases = new[] { "welcome" },
                Group = BasicCommands.Group,
                Usage = "motd",
                Help = "Shows the welcome message again, with a new tip. Your own message can go in motd.txt " +
                       "in BepInEx/config/BubbleShell: it shows under the welcome.",
                Run = (s, a) =>
                {
                    Motd();
                    return null;
                },
            });
            _shell.Register(new Command
            {
                Name = "reload",
                Group = BasicCommands.Group,
                Usage = "reload",
                Help = "Reads config.cfg (your aliases and binds) and autoexec.cfg again, after you've changed them " +
                       "in a text editor.",
                Run = (s, a) =>
                {
                    Reload();
                    return null;
                },
            });
            MeCommands.Register(_shell);
            ModCommands.Register(_shell);
            InfoCommands.Register(_shell);
            FunCommands.Register(_shell);
            LookCommands.Register(_shell);

            try
            {
                Directory.CreateDirectory(_shell.Folder);
                LoadConfig();
                _shell.LoadHistory(Path.Combine(_shell.Folder, HistoryFile));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read Bubble Shell's files: {e.Message}");
            }
            _historyIndex = _shell.History.Count;

            Motd();
        }

        private static readonly string[] Tips =
        {
            "bind g \"emote 3\" makes G play emote 3.",
            "alias hi \"emote 1; say hello!\" makes your own command.",
            "Tab finishes commands and player names.",
            "↑ brings back what you typed before.",
            "tp ~ ~3 ~ hops you 3 up.",
            "sit list shows every kind of activity.",
            "Put commands in autoexec.cfg to run them every time you join.",
            "open lists every mod you can open from here.",
            "Ctrl+C stops the train. If you must.",
            "players shows who's here and how far away.",
            "duck <your bug> asks the duck for help.",
            "bighead. You know you want to.",
            "PageUp (or the mouse wheel) scrolls back.",
            "Your own message goes in motd.txt, beside autoexec.cfg.",
            "where tells you where you are, and how to come back.",
            "Several commands on one line: emote 1; say hi",
        };

        // The welcome message: bubbles, the name, a tip, and the player's own motd.txt if there is one.
        private void Motd()
        {
            string dim = Shell.DimColor;
            string tip = Tips[UnityEngine.Random.Range(0, Tips.Length)];
            _shell.PrintRich(Shell.Colored(dim, "  o   O  °") + "      " + Shell.Colored(Shell.BrightColor, $"{Plugin.PluginName} {Plugin.PluginVersion}"));
            _shell.PrintRich(Shell.Colored(dim, " °  o    O") + "      " + Shell.Colored(Shell.TextColor, "type help to see what it can do"));
            _shell.PrintRich(Shell.Colored(dim, "  O   °  o") + "      " + Shell.Colored(dim, "tip: " + Shell.Escape(tip)));
            try
            {
                string own = Path.Combine(_shell.Folder, MotdFile);
                if (File.Exists(own))
                {
                    _shell.PrintRich("");
                    foreach (string line in File.ReadAllLines(own).Take(20))
                    {
                        _shell.Print(line);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read {MotdFile}: {e.Message}");
            }
            _shell.PrintRich("");
        }

        // config.cfg: the aliases and binds, set again quietly (and without saving them back).
        private void LoadConfig()
        {
            string config = Path.Combine(_shell.Folder, ConfigFile);
            _shell.ConfigChanged = null;
            try
            {
                if (File.Exists(config))
                {
                    _shell.RunText(File.ReadAllText(config), quietly: true);
                }
            }
            finally
            {
                _shell.ConfigChanged = SaveConfig;
                RebuildBinds();
            }
        }

        private void Reload()
        {
            _shell.Aliases.Clear();
            _shell.Binds.Clear();
            try
            {
                LoadConfig();
            }
            catch (Exception e)
            {
                _shell.Error($"Couldn't read {ConfigFile}: {e.Message}");
            }
            _shell.Print($"Read {ConfigFile} again: {_shell.Aliases.Count} aliases, {_shell.Binds.Count} binds.");
            if (File.Exists(Path.Combine(_shell.Folder, AutoexecFile)))
            {
                _shell.Print($"Running {AutoexecFile}...");
                RunAutoexec();
            }
        }

        // For the open command: the window it opened may want the keyboard, so the console lets go
        // (it stays open; clicking it takes the keyboard again).
        internal static void LetGoOfKeys()
        {
            if (_instance != null)
            {
                _instance._hasKeys = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            SaveHistory();
            _view?.Destroy();
            _blocker?.Destroy();
            ShellFont.Destroy();
        }

        // ---- Every frame ----

        private void Update()
        {
            bool toggleFrame = false;
            try
            {
                // Other mods' commands (cheap: only when their list grows).
                LinkedCommands.Sync(_shell);
                // After tp moved you while sitting: the areas you sat down in, once you stand up.
                SeatedMoves.Update();
                // One of them started something steered with the keys (Desk Swarm flying...).
                int handBacks = ShellLink.HandBackCount;
                if (handBacks != _handBacks)
                {
                    _handBacks = handBacks;
                    _hasKeys = false;
                }

                bool inWorld = Game.InWorld;
                bool clicked = _iconClicked;
                _iconClicked = false;
                KeyCode key = S.ConsoleKey.Value;
                toggleFrame = key != KeyCode.None && Input.GetKeyDown(key);

                if (inWorld && clicked)
                {
                    if (_open)
                    {
                        Close();
                    }
                    else
                    {
                        Open();
                    }
                }
                else if (inWorld && toggleFrame && (KeysToShell || (!Game.IsTyping() && !OtherTextFocused())))
                {
                    if (!_open)
                    {
                        Open();
                    }
                    else if (KeysToShell)
                    {
                        Close();
                    }
                    else
                    {
                        _hasKeys = true;
                    }
                }
                if (!inWorld && _open)
                {
                    Close();
                }

                // Your autoexec.cfg, once the world is ready for it.
                if (inWorld && !_ranAutoexec && Game.MyMovement != null)
                {
                    _ranAutoexec = true;
                    RunAutoexec();
                }

                UpdateKeyboardOwner();
                if (KeysToShell)
                {
                    // For mods that read keys straight from Unity (Photo Mode's free camera).
                    ShellLink.MarkConsoleTyping();
                }
                if (KeysToShell && !toggleFrame)
                {
                    Typing();
                }
                RunBinds();
                Scrolling();

                if (_view != null)
                {
                    _shell.Columns = _view.Columns;
                }
                _shell.Step();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }

            CountFps();
        }

        private void LateUpdate()
        {
            try
            {
                Looks.LateUpdate();
                Draw();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        private void OnGUI()
        {
            // The icon first thing, every frame (the row's host draws it).
            if (S.ShowIcon.Value)
            {
                IconBar.State state = _open ? IconBar.State.Open : IconBar.State.Plain;
                if (IconBar.Draw("BubbleShell", IconBar.BubbleShellOrder, Icon.Pixels, state, Plugin.PluginName,
                        S.ConsoleKey.Value) == IconBar.Click.Left)
                {
                    _iconClicked = true;
                }
            }
        }

        // ---- Opening and closing ----

        private void Open()
        {
            _open = true;
            _hasKeys = true;
            _typedAt = Time.unscaledTime;
        }

        private void Close()
        {
            _open = false;
            _hasKeys = false;
            _repeatAt.Clear();
            SaveHistory();
        }

        // Who gets the keyboard: the console after it opens or is clicked, the game after a click
        // anywhere else (except the mod icons), or when chat, a game menu or Photo Mode takes over.
        private void UpdateKeyboardOwner()
        {
            if (_open && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
            {
                if (_slide > 0.5f && ClickBlocker.MouseIn(View.ScreenRect))
                {
                    _hasKeys = true;
                    _typedAt = Time.unscaledTime;
                }
                else if (!IconBar.MouseOver)
                {
                    _hasKeys = false;
                }
            }
            if (_hasKeys && (Game.IsTyping() || Game.MenuOpen() || PanelStack.Hidden))
            {
                _hasKeys = false;
            }
            if (!KeysToShell)
            {
                _repeatAt.Clear();
            }
        }

        // ---- Typing ----

        private void Typing()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                        Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                {
                    if (_caret > 0)
                    {
                        _input = _input.Remove(_caret - 1, 1);
                        _caret--;
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                }
                else if (c >= ' ' && c != '\u007f' && !ctrl)
                {
                    _input = _input.Insert(_caret, c.ToString());
                    _caret++;
                }
                Typed();
            }

            if (Repeat(KeyCode.LeftArrow))
            {
                _caret = ctrl ? WordLeft() : Math.Max(0, _caret - 1);
                Typed();
            }
            if (Repeat(KeyCode.RightArrow))
            {
                _caret = ctrl ? WordRight() : Math.Min(_input.Length, _caret + 1);
                Typed();
            }
            if (Input.GetKeyDown(KeyCode.Home))
            {
                _caret = 0;
                Typed();
            }
            if (Input.GetKeyDown(KeyCode.End))
            {
                _caret = _input.Length;
                Typed();
            }
            if (Repeat(KeyCode.Delete) && _caret < _input.Length)
            {
                _input = _input.Remove(_caret, 1);
                Typed();
            }
            if (Repeat(KeyCode.UpArrow))
            {
                Recall(-1);
            }
            if (Repeat(KeyCode.DownArrow))
            {
                Recall(1);
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Complete();
            }
            if (Repeat(KeyCode.PageUp))
            {
                _scroll += Math.Max(1, View.Rows / 2);
            }
            if (Repeat(KeyCode.PageDown))
            {
                _scroll -= Math.Max(1, View.Rows / 2);
            }

            if (ctrl && Input.GetKeyDown(KeyCode.V))
            {
                string paste = (GUIUtility.systemCopyBuffer ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
                _input = _input.Insert(_caret, paste);
                _caret += paste.Length;
                Typed();
            }
            if (ctrl && Input.GetKeyDown(KeyCode.C))
            {
                int stopped = _shell.StopJobs();
                if (stopped > 0)
                {
                    _shell.Hint("^C (stopped)");
                }
                else if (_input.Length > 0)
                {
                    _shell.PrintRich(Shell.Colored(Shell.DimColor, Shell.Prompt) + Shell.Colored(Shell.BrightColor, Shell.Escape(_input)) +
                                     Shell.Colored(Shell.DimColor, "^C"));
                    SetInput("");
                }
                Typed();
            }
            if (ctrl && Input.GetKeyDown(KeyCode.L))
            {
                _shell.Clear();
                _scroll = 0;
            }
            if (ctrl && Input.GetKeyDown(KeyCode.U))
            {
                SetInput("");
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Typed()
        {
            _typedAt = Time.unscaledTime;
        }

        private void SetInput(string text)
        {
            _input = text ?? "";
            _caret = _input.Length;
            Typed();
        }

        private void Submit()
        {
            string line = _input;
            _input = "";
            _caret = 0;
            _shift = 0;
            _scroll = 0;
            _draft = "";
            _shell.Submit(line);
            _historyIndex = _shell.History.Count;
            SaveHistory();
        }

        // ↑ and ↓: the lines typed before. The line being typed comes back after the newest.
        private void Recall(int step)
        {
            List<string> history = _shell.History;
            if (_historyIndex == history.Count)
            {
                _draft = _input;
            }
            int index = Mathf.Clamp(_historyIndex + step, 0, history.Count);
            if (index == _historyIndex)
            {
                return;
            }
            _historyIndex = index;
            SetInput(index == history.Count ? _draft : history[index]);
        }

        private void Complete()
        {
            // Completes the word before the cursor; anything after it stays.
            string before = _input.Substring(0, _caret);
            string after = _input.Substring(_caret);
            string completed = _shell.Complete(before, out List<string> options);
            if (options.Count > 0)
            {
                PrintOptions(options);
            }
            _input = completed + after;
            _caret = completed.Length;
            Typed();
        }

        // The choices for Tab, side by side as far as they fit.
        private void PrintOptions(List<string> options)
        {
            int width = 0;
            foreach (string option in options)
            {
                width = Math.Max(width, option.Length);
            }
            width += 2;
            int perLine = Math.Max(1, View.Columns / width);
            for (int i = 0; i < options.Count; i += perLine)
            {
                var line = new System.Text.StringBuilder();
                for (int j = i; j < i + perLine && j < options.Count; j++)
                {
                    line.Append(options[j].PadRight(width));
                }
                _shell.Hint(line.ToString().TrimEnd());
            }
        }

        private int WordLeft()
        {
            int i = _caret;
            while (i > 0 && _input[i - 1] == ' ')
            {
                i--;
            }
            while (i > 0 && _input[i - 1] != ' ')
            {
                i--;
            }
            return i;
        }

        private int WordRight()
        {
            int i = _caret;
            while (i < _input.Length && _input[i] != ' ')
            {
                i++;
            }
            while (i < _input.Length && _input[i] == ' ')
            {
                i++;
            }
            return i;
        }

        // True on the press, then again and again while it's held.
        private bool Repeat(KeyCode key)
        {
            float now = Time.unscaledTime;
            if (Input.GetKeyDown(key))
            {
                _repeatAt[key] = now + RepeatDelay;
                return true;
            }
            if (Input.GetKey(key) && _repeatAt.TryGetValue(key, out float at) && now >= at)
            {
                _repeatAt[key] = now + RepeatRate;
                return true;
            }
            if (!Input.GetKey(key))
            {
                _repeatAt.Remove(key);
            }
            return false;
        }

        // ---- Scrolling ----

        private void Scrolling()
        {
            // New lines while scrolled back: stay on the same lines.
            long next = _shell.NextLineNumber;
            if (_scroll > 0 && next > _seenNext)
            {
                _scroll += (int)(next - _seenNext);
            }
            _seenNext = next;

            if (_open && _slide > 0.5f && ClickBlocker.MouseIn(View.ScreenRect))
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel > 0.01f)
                {
                    _scroll += 3;
                }
                else if (wheel < -0.01f)
                {
                    _scroll -= 3;
                }
            }
            _scroll = Mathf.Clamp(_scroll, 0, Math.Max(0, _shell.Lines.Count - 1));
        }

        // ---- Binds ----

        private void RunBinds()
        {
            if (_bindKeys.Count == 0 || !Game.InWorld || KeysToShell || Game.KeysBusy() || OtherTextFocused())
            {
                return;
            }
            KeyCode consoleKey = S.ConsoleKey.Value;
            foreach (KeyValuePair<string, KeyCode> bind in _bindKeys)
            {
                if (bind.Value != consoleKey && Input.GetKeyDown(bind.Value) && _shell.Binds.TryGetValue(bind.Key, out string line))
                {
                    _shell.Run(line);
                }
            }
        }

        private void RebuildBinds()
        {
            _bindKeys.Clear();
            foreach (string key in _shell.Binds.Keys)
            {
                if (TryKey(key, out KeyCode code))
                {
                    _bindKeys[key] = code;
                }
            }
        }

        // True while another mod's text box (or a game one) has the keyboard, so typing there doesn't
        // open the console or run binds.
        private static bool OtherTextFocused()
        {
            if (GUIUtility.keyboardControl != 0)
            {
                return true;
            }
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }
            TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused)
            {
                return true;
            }
            UnityEngine.UI.InputField legacy = selected.GetComponent<UnityEngine.UI.InputField>();
            return legacy != null && legacy.isFocused;
        }

        // A key's name for binds: Unity's names (g, f6, keypad1, leftbracket), plus 1-9 for the number
        // row and the symbols themselves (` - = [ ] ' , . / \). Null if there's no such key.
        private static string KeyName(string typed)
        {
            return TryKey(typed, out KeyCode code) ? code.ToString().ToLowerInvariant() : null;
        }

        private static bool TryKey(string typed, out KeyCode code)
        {
            code = KeyCode.None;
            if (string.IsNullOrWhiteSpace(typed))
            {
                return false;
            }
            string text = typed.Trim();
            if (text.Length == 1 && char.IsDigit(text[0]))
            {
                code = KeyCode.Alpha0 + (text[0] - '0');
                return true;
            }
            switch (text.ToLowerInvariant())
            {
                case "`": code = KeyCode.BackQuote; return true;
                case "-": code = KeyCode.Minus; return true;
                case "=": code = KeyCode.Equals; return true;
                case "[": code = KeyCode.LeftBracket; return true;
                case "]": code = KeyCode.RightBracket; return true;
                case "'": code = KeyCode.Quote; return true;
                case ",": code = KeyCode.Comma; return true;
                case ".": code = KeyCode.Period; return true;
                case "/": code = KeyCode.Slash; return true;
                case "\\": code = KeyCode.Backslash; return true;
                case "enter": code = KeyCode.Return; return true;
                case "esc": code = KeyCode.Escape; return true;
                case "ctrl": code = KeyCode.LeftControl; return true;
                case "shift": code = KeyCode.LeftShift; return true;
                case "alt": code = KeyCode.LeftAlt; return true;
            }
            // Enum.TryParse also reads plain numbers ("13" would be Return), so leave those out.
            if (int.TryParse(text, out _))
            {
                return false;
            }
            return Enum.TryParse(text, true, out code) && Enum.IsDefined(typeof(KeyCode), code) && code != KeyCode.None;
        }

        // Says when a new bind's key also does something else.
        private static string KeyWarning(string key)
        {
            if (!TryKey(key, out KeyCode code))
            {
                return null;
            }
            if (code == S.ConsoleKey.Value)
            {
                return "That's the console's own key, so the bind won't run.";
            }
            try
            {
                DataManager data = Game.InWorld ? MonoSingleton<DataManager>.I : null;
                if (data != null && data.SettingsData != null && data.SettingsData.Keys != null)
                {
                    foreach (KeyValuePair<KeyType, (KeyCode, string)> pair in data.SettingsData.Keys)
                    {
                        if (pair.Value.Item1 == code)
                        {
                            return $"The game uses {key} too ({pair.Key}), so both happen.";
                        }
                    }
                }
            }
            catch (Exception)
            {
                // No settings yet.
            }
            switch (code)
            {
                case KeyCode.F1:
                case KeyCode.F9: return "Photo Mode may use that key too.";
                case KeyCode.F2: return "Outfit Slots may use that key too.";
                case KeyCode.F3: return "Desk Snap may use that key too.";
                case KeyCode.F4: return "Name Designer may use that key too.";
                case KeyCode.F5: return "Teleport Spots may use that key too.";
                case KeyCode.F6:
                case KeyCode.F7: return "Emote Sync may use that key too.";
                case KeyCode.F8: return "BlueSage's mod may use that key too.";
                case KeyCode.F10: return "Quick Hide may use that key too.";
                case KeyCode.F11: return "Desk Swarm may use that key too.";
                case KeyCode.F12: return "Steam takes screenshots with that key too.";
            }
            return null;
        }

        // ---- Files ----

        private void SaveConfig()
        {
            RebuildBinds();
            try
            {
                Shell.WriteAtomically(Path.Combine(_shell.Folder, ConfigFile), _shell.ConfigText());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't save {ConfigFile}: {e.Message}");
            }
        }

        private void SaveHistory()
        {
            try
            {
                _shell?.SaveHistory(Path.Combine(_shell.Folder, HistoryFile));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't save {HistoryFile}: {e.Message}");
            }
        }

        private void RunAutoexec()
        {
            string path = Path.Combine(_shell.Folder, AutoexecFile);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path,
                        "// Bubble Shell runs these commands every time you enter the world.\n" +
                        "// One command per line; lines starting with // are skipped. For example:\n" +
                        "// fps on\n" +
                        "// bighead on\n" +
                        "// echo welcome back!\n");
                    return;
                }
                _shell.RunText(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't run {AutoexecFile}: {e.Message}");
            }
        }

        // ---- Drawing ----

        private void Draw()
        {
            bool inWorld = Game.InWorld;
            _slide = Mathf.MoveTowards(_slide, _open && inWorld ? 1f : 0f, Time.unscaledDeltaTime / SlideSeconds);
            float fps = InfoCommands.ShowFps && inWorld ? Mathf.Max(_fps, 0.5f) : 0f;
            int scale = S.TextScale.Value > 0 ? S.TextScale.Value : Mathf.Max(1, Mathf.RoundToInt(2f * Screen.height / 1080f));

            if (!inWorld || PanelStack.Hidden || (_slide <= 0f && fps <= 0f))
            {
                _view?.Hide();
                IsMouseOver = false;
                _blocker.Update(false, Rect.zero);
                return;
            }

            View.Show(_slide, S.Height.Value, S.Opacity.Value, scale, S.Bubbles.Value, fps);
            if (_slide > 0f)
            {
                View.SetOutput(OutputText());
                View.SetTitle(TitleText());
                string input = InputText(out int caret);
                bool blinkOn = (Time.unscaledTime - _typedAt) % 1f < 0.55f;
                View.SetInput(input, caret, KeysToShell && blinkOn);
            }

            bool shown = _slide > 0f;
            IsMouseOver = shown && ClickBlocker.MouseIn(View.ScreenRect);
            _blocker.Update(shown, View.ScreenRect);
        }

        private string OutputText()
        {
            IReadOnlyList<string> lines = _shell.Lines;
            int end = Math.Max(0, lines.Count - _scroll);
            int start = Math.Max(0, end - View.Rows);
            var text = new System.Text.StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (i > start)
                {
                    text.Append('\n');
                }
                text.Append(lines[i]);
            }
            return text.ToString();
        }

        private string TitleText()
        {
            string scrolled = _scroll > 0 ? $"↓ {_scroll} newer (PageDown)   " : "";
            string jobs = _shell.RunningJobs > 0 ? "Ctrl+C stops   " : "";
            return Shell.Colored(Shell.DimColor, Shell.Escape($"{scrolled}{jobs}{Plugin.PluginName} {Plugin.PluginVersion}"));
        }

        // The prompt and what you're typing, scrolled sideways to keep the cursor in view.
        private string InputText(out int caret)
        {
            string prompt = Shell.Prompt;
            if (!KeysToShell && _input.Length == 0)
            {
                caret = prompt.Length;
                KeyCode key = S.ConsoleKey.Value;
                string label = key == KeyCode.None ? null : key == KeyCode.BackQuote ? "`" : key.ToString();
                return Shell.Colored(Shell.DimColor, Shell.Escape(prompt + (label != null ? $"click here or press {label} to type" : "click here to type")));
            }
            int room = Math.Max(4, View.Columns - prompt.Length - 1);
            _caret = Mathf.Clamp(_caret, 0, _input.Length);
            if (_caret < _shift)
            {
                _shift = _caret;
            }
            if (_caret > _shift + room)
            {
                _shift = _caret - room;
            }
            _shift = Mathf.Clamp(_shift, 0, Math.Max(0, _input.Length - 1));
            if (_input.Length <= room)
            {
                _shift = 0;
            }
            string shown = _input.Substring(_shift, Math.Min(room + 1, _input.Length - _shift));
            caret = prompt.Length + _caret - _shift;
            string color = KeysToShell ? Shell.BrightColor : Shell.DimColor;
            return Shell.Colored(Shell.DimColor, prompt) + Shell.Colored(color, Shell.Escape(shown));
        }

        private void CountFps()
        {
            _fpsFrames++;
            float now = Time.unscaledTime;
            if (now - _fpsSince >= 0.5f)
            {
                _fps = _fpsFrames / (now - _fpsSince);
                _fpsFrames = 0;
                _fpsSince = now;
            }
        }
    }
}
