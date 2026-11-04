using BepInEx.Configuration;
using UnityEngine;

namespace OutfitSlots
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.outfitslots.cfg after the first launch.
    // Slots 4-9 themselves are saved in BepInEx\config\OutfitSlots.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<bool> CloseAfterSwap;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F2,
                "Opens and closes the quick-swap window.");

            CloseAfterSwap = config.Bind("Display", "CloseAfterSwap", false,
                "Close the quick-swap window after you pick an outfit.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a shirt icon in the row of mod icons at the bottom-right (in a world). Clicking it opens " +
                "and closes the quick-swap window, and right-clicking it puts on your next outfit.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write the layout of the outfit screen's style list to the BepInEx log the first time it opens, " +
                "for fixing where the extra rows go.");
        }
    }
}
