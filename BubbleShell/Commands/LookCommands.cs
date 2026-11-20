using System.Collections;
using System.Globalization;

namespace BubbleShell
{
    // bighead, tiny: silly looks only you see (see Looks).
    internal static class LookCommands
    {
        public const string Group = "Looks";

        public static void Register(Shell shell)
        {
            shell.Register(new Command
            {
                Name = "bighead",
                Group = Group,
                Usage = "bighead [on|off|size]",
                Help = "Big heads on everyone, on your screen only. A size from 1.2 to 3 picks how big (on = 2).",
                Run = (s, a) => Toggle(s, a, ref Looks.HeadSize, 2f, 1.2f, 3f,
                    "Big heads on! Only you see them.", "Heads back to normal."),
                Complete = (s, a, i) => i == 0 ? new[] { "on", "off" } : null,
            });
            shell.Register(new Command
            {
                Name = "tiny",
                Group = Group,
                Usage = "tiny [on|off|size]",
                Help = "Makes you small, on your screen only. A size from 0.2 to 0.9 picks how small (on = 0.5).",
                Run = (s, a) => Toggle(s, a, ref Looks.BodySize, 0.5f, 0.2f, 0.9f,
                    "You're tiny! Only you see it.", "Back to your usual size."),
                Complete = (s, a, i) => i == 0 ? new[] { "on", "off" } : null,
            });
        }

        // No word flips it; on/off set it; a number sets the size.
        private static IEnumerator Toggle(Shell shell, Args args, ref float size, float on, float min, float max,
            string onText, string offText)
        {
            bool isOn = !UnityEngine.Mathf.Approximately(size, 1f);
            if (args.Count == 0)
            {
                size = isOn ? 1f : on;
            }
            else if (args.Is(0, "on", "1", "true", "yes"))
            {
                size = on;
            }
            else if (args.Is(0, "off", "0", "false", "no"))
            {
                size = 1f;
            }
            else if (float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float picked) &&
                     picked >= min && picked <= max)
            {
                size = picked;
            }
            else
            {
                shell.Warn($"Use on, off, or a size from {min} to {max}.");
                return null;
            }
            shell.Print(UnityEngine.Mathf.Approximately(size, 1f) ? offText : onText);
            return null;
        }
    }
}
