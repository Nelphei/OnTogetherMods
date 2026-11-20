using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BubbleShell
{
    // fortune, roll, flip, 8ball, duck, sudo, hack, train. Just for laughs; all the text is our own.
    // No Unity in here (tested outside the game).
    internal static class FunCommands
    {
        public const string Group = "Laughs";

        private static readonly Random Dice = new Random();

        private static readonly string[] Fortunes =
        {
            "A clean desk is a sign of a full drawer.",
            "Your next bug will be a missing semicolon. Your next one after that will be two.",
            "It works on my machine. Ship the machine.",
            "Take a break. The code will still be broken when you come back.",
            "Today's focus streak brought to you by: not opening that other tab.",
            "The best time to start was an hour ago. The second best time is after this snack.",
            "There are two hard things: naming things, cache invalidation, and off-by-one errors.",
            "Somewhere, a rubber duck believes in you.",
            "You will find what you are looking for in the last place you look. Obviously.",
            "Drink some water. Your plants did.",
            "A watched progress bar never fills.",
            "Every expert was once a beginner who refused to read the error message.",
            "The bug is not in the line you're staring at. It's one line up.",
            "Small steps still go somewhere.",
            "Your pomodoro is proud of you.",
            "Rest is also productive. Tell your to-do list it said so.",
            "If at first you don't succeed, call it version 0.1.0.",
            "A friend nearby makes the work feel lighter.",
            "Comment your code. Future you is a stranger with your face.",
            "Today is a good day to finally close that one tab.",
            "The fish are biting. The deadlines are too.",
            "You can't spell 'focus' without 'u'.",
            "Have you tried turning it off and on again? No? Try it on yourself: stretch.",
            "Great things are built one tiny commit at a time.",
            "Somebody in this session is quietly rooting for you.",
            "The answer is in the documentation. The documentation is in your other window.",
            "Undo is a superpower. Use it wisely.",
            "Bubbles always float up. So can your mood.",
            "One more level? One more level.",
            "Nothing compiles like a fresh cup of tea.",
        };

        private static readonly string[] Answers =
        {
            "Yes, definitely.",
            "It is certain.",
            "Without a doubt.",
            "The bubbles say yes.",
            "Most likely.",
            "Signs point to yes.",
            "Ask again after a snack.",
            "Can't tell right now. The fish are distracting me.",
            "Better not tell you now.",
            "Concentrate and ask again.",
            "Don't count on it.",
            "The bubbles say no.",
            "Very doubtful.",
            "My sources say nope.",
            "Only on a Tuesday.",
            "Yes, but write a test first.",
        };

        private static readonly string[] DuckReplies =
        {
            "quack. (have you printed what the value really is?)",
            "quack quack. (which line runs right before it breaks?)",
            "quack? (what did you change last?)",
            "quack. (read the error message again, slowly, out loud.)",
            "...quack. (is it maybe a typo?)",
            "QUACK. (you just explained it. did you hear yourself?)",
            "quack. (try the smallest case that still breaks.)",
            "quack quack quack. (take a short walk. it'll still be here.)",
        };

        private static readonly string[] Duck =
        {
            @"     __",
            @"  __( o)>",
            @"  \ <_ )",
            @"   `---'",
        };

        private static readonly string[] Train =
        {
            @"      o  O  o",
            @"     _||_______   __________   __________",
            @"    |  []  |  ||_|  []  []  |_|  []  []  |",
            @"  _/_______|__||_|__________|_|__________|",
            @"  `-O--O--O--O-'  `-O------O-'  `-O------O-'",
        };

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "fortune",
                Group = Group,
                Usage = "fortune",
                Help = "A little fortune for your day.",
                Run = (s, a) =>
                {
                    s.Print(Fortunes[Dice.Next(Fortunes.Length)]);
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "roll",
                Group = Group,
                Usage = "roll [dice]",
                Help = "Rolls dice: roll, roll 20, roll 2d6, roll 3d8+2.",
                Run = Roll,
            });
            shell.Register(new Command
            {
                Name = "flip",
                Group = Group,
                Usage = "flip",
                Help = "Flips a coin.",
                Run = (s, a) =>
                {
                    int toss = Dice.Next(201);
                    s.Print(toss == 200 ? "It landed on its edge. Spooky." : toss % 2 == 0 ? "Heads!" : "Tails!");
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "8ball",
                Group = Group,
                Usage = "8ball <question>",
                Help = "Asks the magic bubble a yes-or-no question.",
                Run = (s, a) =>
                {
                    if (a.Count == 0)
                    {
                        s.Hint("Ask it something: 8ball will the build pass?");
                        return null;
                    }
                    s.Print(Answers[Dice.Next(Answers.Length)]);
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "duck",
                Group = Group,
                Usage = "duck [your bug]",
                Help = "Rubber duck debugging: explain your bug to the duck. It listens.",
                Run = (s, a) =>
                {
                    foreach (string line in Duck)
                    {
                        s.Print(line);
                    }
                    s.Print(a.Count == 0
                        ? "quack. (tell me your bug: duck it crashes when I click twice)"
                        : DuckReplies[Dice.Next(DuckReplies.Length)]);
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "sudo",
                Group = Group,
                Usage = "sudo <anything>",
                Help = "Does anything you say. Probably.",
                Run = (s, a) =>
                {
                    s.Print("Nice try. This incident will be reported to the duck.");
                    return null;
                },
            });
            shell.Register(new Command
            {
                Name = "hack",
                Group = Group,
                Usage = "hack [target]",
                Help = "Hacks the mainframe. (It doesn't. Nothing is hacked, sent or changed.)",
                Run = (s, a) => Hack(s, a.Count > 0 ? a.Rest() : "the mainframe"),
            });
            shell.Register(new Command
            {
                Name = "train",
                Aliases = new[] { "sl" },
                Group = Group,
                Usage = "train",
                Help = "Choo choo. Ctrl+C if you're in a hurry.",
                Run = (s, a) => RunTrain(s),
            });
        }

        private static IEnumerator Roll(Shell shell, Args args)
        {
            string text = args.Count > 0 ? args.Rest().Replace(" ", "").ToLowerInvariant() : "1d6";
            Match match = Regex.Match(text, @"^(\d*)d?(\d+)([+-]\d+)?$");
            if (!match.Success)
            {
                shell.Warn("Usage: roll [dice], like roll 20, roll 2d6 or roll 3d8+2");
                return null;
            }
            bool hasD = text.Contains("d");
            int count = hasD && match.Groups[1].Value.Length > 0 ? int.Parse(match.Groups[1].Value) : 1;
            if (!hasD && match.Groups[1].Value.Length > 0)
            {
                // "roll 20" reads as one 20-sided die (the regex put the 2 in the count).
                count = 1;
            }
            int sides = hasD ? int.Parse(match.Groups[2].Value) : int.Parse(match.Groups[1].Value + match.Groups[2].Value);
            int bonus = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            if (count < 1 || count > 100 || sides < 2 || sides > 1000)
            {
                shell.Warn("1 to 100 dice, with 2 to 1000 sides each.");
                return null;
            }
            var rolls = new List<int>();
            for (int i = 0; i < count; i++)
            {
                rolls.Add(Dice.Next(1, sides + 1));
            }
            int total = rolls.Sum() + bonus;
            string bonusText = bonus > 0 ? $" + {bonus}" : bonus < 0 ? $" - {-bonus}" : "";
            string name = $"{count}d{sides}{(bonus > 0 ? "+" + bonus : bonus < 0 ? bonus.ToString() : "")}";
            if (count == 1 && bonus == 0)
            {
                shell.Print($"{name}: {total}");
            }
            else
            {
                shell.Print($"{name}: {string.Join(" + ", rolls)}{bonusText} = {total}");
            }
            return null;
        }

        private static IEnumerator Hack(Shell shell, string target)
        {
            string[] steps =
            {
                $"Connecting to {target}",
                "Bypassing the firewall (it was a sticky note)",
                "Downloading more RAM",
                "Decrypting cat pictures",
                "Reticulating splines",
                "Asking nicely",
            };
            shell.Print($"Hacking {target}...");
            yield return 0.4f;
            foreach (string step in steps)
            {
                long line = shell.NextLineNumber;
                shell.Print(Bar(step, 0));
                for (int filled = 1; filled <= 10; filled++)
                {
                    yield return 0.04f + Dice.Next(8) * 0.01f;
                    shell.SetLine(line, Shell.Colored(Shell.TextColor, Shell.Escape(Bar(step, filled))));
                }
            }
            yield return 0.3f;
            shell.PrintRich(Shell.Colored(Shell.BrightColor, "ACCESS GRANTED"));
            yield return 0.8f;
            shell.Hint("...just kidding. Nothing was hacked. It's a cozy game.");
        }

        private static string Bar(string step, int filled)
        {
            return $"[{new string('#', filled)}{new string('.', 10 - filled)}] {step}{(filled == 10 ? " - done" : "...")}";
        }

        private static IEnumerator RunTrain(Shell shell)
        {
            int width = Train.Max(l => l.Length);
            int columns = Math.Max(20, shell.Columns);
            long top = shell.NextLineNumber;
            shell.PrintRich(new string('\n', Train.Length - 1));
            for (int x = columns; x > -width; x--)
            {
                for (int i = 0; i < Train.Length; i++)
                {
                    shell.SetLine(top + i, Shell.Colored(Shell.TextColor, Shell.Escape(Place(Train[i], x, columns))));
                }
                yield return 0.035f;
            }
            for (int i = 0; i < Train.Length; i++)
            {
                shell.SetLine(top + i, "");
            }
        }

        // One line of the train with its left edge at column x, cut to the console's width.
        private static string Place(string line, int x, int columns)
        {
            var text = new StringBuilder();
            for (int column = 0; column < columns - 1; column++)
            {
                int i = column - x;
                text.Append(i >= 0 && i < line.Length ? line[i] : ' ');
            }
            return text.ToString().TrimEnd();
        }
    }
}
