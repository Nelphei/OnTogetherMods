using BepInEx.Configuration;
using UnityEngine;

namespace CardTable
{
    internal enum TableSize
    {
        Medium,
        Large,
    }

    internal enum TableGame
    {
        Solitaire,
        GoFish,
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.cardtable.cfg after the first
    // launch. The game in progress and the stats are in BepInEx\config\CardTable.json.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<bool> DrawThree;
        public readonly ConfigEntry<bool> Animations;
        public readonly ConfigEntry<TableSize> Size;
        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<bool> HelpSeen;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<TableGame> Game;
        public readonly ConfigEntry<int> GoFishSeats;
        public readonly ConfigEntry<bool> GoFishHelpSeen;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Opens and closes the Card Table window. None by default (every F key is taken by other mods): " +
                "click the cards icon instead.");

            DrawThree = config.Bind("Solitaire", "DrawThree", false,
                "Turn over three cards at a time from the deck instead of one. Also the Draw button in the window, " +
                "which changes it straight away, in the game you're playing too.");

            Animations = config.Bind("Display", "Animations", true,
                "Cards slide to where they go, and the deal flies out from the deck.");
            Size = config.Bind("Display", "Size", TableSize.Medium,
                "How big the table is: Medium or Large. Also the size button in the window. The cards are pixel " +
                "art, so they grow in whole steps; on a small screen Large may come out as Medium.");
            WindowX = config.Bind("Display", "WindowX", -1f,
                "Where you dragged the window to: its left edge, in pixels on a 1080-pixel-high screen. -1 keeps " +
                "it with the other mods' windows. Double-clicking the window's title row sets this back to -1.");
            WindowY = config.Bind("Display", "WindowY", -1f,
                "Where you dragged the window to: its top edge, in pixels on a 1080-pixel-high screen.");
            HelpSeen = config.Bind("Display", "HelpSeen", false,
                "The how-to-play card (the ? button) shows by itself the first time you open the window, and this " +
                "remembers that it has. Set it to false to see it by itself again.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a cards icon in the row of mod icons at the bottom-right. Clicking it opens and closes the window.");
            Game = config.Bind("Display", "Game", TableGame.Solitaire,
                "The game the window shows: Solitaire or GoFish. Also the tabs in the window's title row, which " +
                "remember the last one you picked.");

            GoFishSeats = config.Bind("GoFish", "Players", 4,
                new ConfigDescription("How many seats a Go Fish table you start has, you included (2 to 6). Computer " +
                                      "players fill the ones nobody's in. Also the Players buttons in the window.",
                    new AcceptableValueRange<int>(GoFish.MinSeats, GoFish.MaxSeats)));
            GoFishHelpSeen = config.Bind("GoFish", "HelpSeen", false,
                "The how-to-play card for Go Fish shows by itself the first time you open the Go Fish tab, and this " +
                "remembers that it has.");

            DebugLog = config.Bind("Debug", "LogMessages", false,
                "Write every Go Fish message sent to and from other players to the BepInEx log.");
        }
    }
}
