using BepInEx.Configuration;
using UnityEngine;

namespace CardDesigner
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.carddesigner.cfg after the first
    // launch. The presets are saved in BepInEx\config\CardDesigner.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<bool> ShowCardButton;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Opens and closes the Card Designer window. None by default (every F key is taken by other " +
                "mods): click the card icon, or the Design card button on the game's ID card.");

            ShowCardButton = config.Bind("Display", "ShowCardButton", true,
                "Show a Design card button on the game's own ID card screen (on the main menu and in a world).");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show an ID card icon in the row of mod icons at the bottom-right (in a world). Clicking it opens " +
                "and closes the window.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write where the Design card button was put on the game's ID card screen to the BepInEx log.");
        }
    }
}
