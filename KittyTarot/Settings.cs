using BepInEx.Configuration;
using UnityEngine;

namespace KittyTarot
{
    internal enum TableSize
    {
        Medium,
        Large,
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.kittytarot.cfg after the first launch.
    // The deck and the reading on the table are in BepInEx\config\KittyTarot.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<bool> Reversals;
        public readonly ConfigEntry<bool> MajorsOnly;
        public readonly ConfigEntry<bool> Animations;
        public readonly ConfigEntry<TableSize> Size;
        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<bool> HelpSeen;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Opens and closes the Kitty Tarot window. None by default (every F key is taken by other mods): " +
                "click the moon cat icon instead.");

            Reversals = config.Bind("Reading", "ReversedCards", true,
                "Cards lying upside down show upside down (reversed). Off: every card shows the right way up. " +
                "Only a wash turns cards round. Also in the window's Options.");
            MajorsOnly = config.Bind("Reading", "MajorCardsOnly", false,
                "Read with the 22 major cards only; the four suits wait to one side. Changes between readings. " +
                "Also in the window's Options.");

            Animations = config.Bind("Display", "Animations", true,
                "Cards glide where they go, shuffles riffle, washes swirl and cards flip over.");
            Size = config.Bind("Display", "Size", TableSize.Medium,
                "How big the table is: Medium or Large. Also the size button in the window. The cards are pixel " +
                "art, so they grow in whole steps; on a small screen Large may come out as Medium.");
            WindowX = config.Bind("Display", "WindowX", -1f,
                "Where you dragged the window to: its left edge, in pixels on a 1080-pixel-high screen. -1 keeps " +
                "it with the other mods' windows. Double-clicking the window's title row sets this back to -1.");
            WindowY = config.Bind("Display", "WindowY", -1f,
                "Where you dragged the window to: its top edge, in pixels on a 1080-pixel-high screen.");
            HelpSeen = config.Bind("Display", "HelpSeen", false,
                "The how-it-works card (the ? button) shows by itself the first time you open the window, and this " +
                "remembers that it has. Set it to false to see it by itself again.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a moon cat icon in the row of mod icons at the bottom-right. Clicking it opens and closes the window.");
        }
    }
}
