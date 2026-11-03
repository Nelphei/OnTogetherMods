using BepInEx.Configuration;
using UnityEngine;

namespace TeleportSpots
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.teleportspots.cfg after the first launch.
    // The spots themselves are saved in BepInEx\config\TeleportSpots.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<bool> RestoreView;
        public readonly ConfigEntry<bool> CloseAfterTeleport;
        public readonly ConfigEntry<int> VisibleRows;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F5,
                "Opens and closes the teleport window.");

            RestoreView = config.Bind("Teleport", "RestoreView", true,
                "Also turn the camera to face the way it did when you saved the spot.");
            CloseAfterTeleport = config.Bind("Teleport", "CloseAfterTeleport", false,
                "Close the window after you teleport.");

            VisibleRows = config.Bind("Display", "VisibleRows", 8,
                new ConfigDescription(
                    "How many spots the window shows before it scrolls.",
                    new AcceptableValueRange<int>(3, 20)));
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a map pin icon in the row of mod icons at the bottom-right. Clicking it opens and closes the window.");
        }
    }
}
