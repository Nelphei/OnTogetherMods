using BepInEx.Configuration;
using UnityEngine;

namespace QuickHide
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.quickhide.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> HideKey;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            HideKey = config.Bind("Keys", "HideKey", KeyCode.F10,
                "Hides the whole game (world, characters and UI), and brings it back. Works in the split-screen " +
                "and mini window modes (keys 2-4). On Windows it works even while another app is focused, so pick " +
                "a key you don't type with. None switches the key off: the icon still hides the game, and clicking " +
                "On-Together in the taskbar brings it back.");

            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show an eye icon in the row of mod icons at the bottom-right. Clicking it does what HideKey does.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write each hide and show, and which window gets the keyboard, to the BepInEx log.");
        }
    }
}
