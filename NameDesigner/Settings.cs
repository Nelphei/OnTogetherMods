using BepInEx.Configuration;
using UnityEngine;

namespace NameDesigner
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.namedesigner.cfg after the first launch.
    // The presets are saved in BepInEx\config\NameDesigner.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<bool> OpenWithCreateSession;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F4,
                "Opens and closes the Name Designer window.");

            OpenWithCreateSession = config.Bind("Display", "OpenWithCreateSession", true,
                "Open the window on its Session tab by itself while the main menu's Create Session box is showing.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a palette icon in the row of mod icons at the bottom-right (in a world). Clicking it opens " +
                "and closes the window.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write where the Create Session box was found on screen to the BepInEx log.");
        }
    }
}
