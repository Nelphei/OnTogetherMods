using BepInEx.Configuration;
using UnityEngine;

namespace DeskSnap
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.desksnap.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<float> Gap;
        public readonly ConfigEntry<float> Range;
        public readonly ConfigEntry<bool> ShowNotices;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F3,
                "Turns snapping on and off. If you're sitting at your desk, turning it on snaps you next to a " +
                "friend's desk right away, and turning it off moves you back to where you sat down.");

            Enabled = config.Bind("Snap", "Enabled", true,
                "When you sit down at your desk near a friend's desk, move yours into line beside it, facing the " +
                "same way. ToggleKey changes this, and it's remembered between games.");
            Gap = config.Bind("Snap", "Gap", 0.03f,
                new ConfigDescription(
                    "Space between the two desks, in metres.",
                    new AcceptableValueRange<float>(0f, 0.5f)));
            Range = config.Bind("Snap", "Range", 3f,
                new ConfigDescription(
                    "How close to a friend you need to sit (in metres) for your desk to snap next to theirs.",
                    new AcceptableValueRange<float>(1f, 10f)));

            ShowNotices = config.Bind("Display", "ShowNotices", true,
                "Show a short message at the top of the screen when your desk snaps or you press ToggleKey.");
            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write desk sizes, targets and why a snap didn't happen to the BepInEx log.");
        }
    }
}
