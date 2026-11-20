using OnTogetherMods.Shared;

namespace PhotoMode
{
    // The photo command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs).
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("photo", Plugin.PluginName, "photo [shot]",
                "photo starts Photo Mode (or ends it): the free camera, with the game's UI hidden. The keys go back to " +
                "the camera; click the console to type again. photo shot takes a photo while it's on.",
                Run, (args, index) => index == 0 ? new[] { "shot" } : null);
        }

        private static string Run(string[] args)
        {
            PhotoModeController photo = PhotoModeController.Instance;
            if (photo == null)
            {
                return "! Photo Mode isn't ready yet.";
            }
            Settings s = Plugin.Settings;
            if (args.Length > 0)
            {
                if (!string.Equals(args[0], "shot", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "! Usage: photo, or photo shot.";
                }
                if (!PhotoModeController.IsActive)
                {
                    return "! Photo Mode isn't on. photo starts it.";
                }
                return photo.CommandShot() ? "Click! (Saved in Pictures\\On-Together, or your SaveFolder.)" : "~ Still taking the last one.";
            }
            if (PhotoModeController.IsActive)
            {
                photo.CommandToggle();
                return "Photo Mode off.";
            }
            if (!photo.CommandToggle())
            {
                return "! Photo Mode didn't start (it only works in the Normal window mode, off the swan, with no menu open).";
            }
            // The free camera flies with WASD, so the console hands the keyboard back.
            ShellLink.HandBackKeys();
            return $"Photo Mode on: WASD and Q/E fly, right-drag looks, {s.CaptureKey.Value} takes a photo, " +
                   $"{s.ToggleKey.Value} or Esc ends it.";
        }
    }
}
