using OnTogetherMods.Shared;
using UnityEngine.SceneManagement;

namespace DeskSnap
{
    // The snap command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs).
    internal static class ShellCommands
    {
        // The game's scenes by build index: 2 is the world.
        private const int WorldScene = 2;

        public static void Register()
        {
            ShellLink.Add("snap", Plugin.PluginName, "snap [on | off]",
                "snap lines your desk up beside the nearest friend's desk now (sitting at your desk, with snapping on). " +
                "snap on / snap off turn snapping on or off, like its key: off while you're snapped puts you back " +
                "where you sat down.",
                Run, (args, index) => index == 0 ? new[] { "on", "off" } : null);
        }

        private static string Run(string[] args)
        {
            SnapController snap = SnapController.Instance;
            if (snap == null || SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                return "! Join a world first.";
            }
            if (args.Length == 0)
            {
                return snap.SnapNow();
            }
            string word = args[0].ToLowerInvariant();
            if (word != "on" && word != "off")
            {
                return "! Usage: snap, snap on or snap off.";
            }
            bool on = word == "on";
            if (snap.On == on)
            {
                return $"~ Desk Snap is already {word}.";
            }
            snap.SetOn(on);
            return on ? "Desk Snap is on." : "Desk Snap is off.";
        }
    }
}
