using BepInEx.Configuration;
using UnityEngine;

namespace SessionManager
{
    internal enum IconStyle
    {
        Crown,
        Friends,
        Clipboard,
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.sessionmanager.cfg after the first
    // launch. The saved setup and the ban details are kept in BepInEx\config\SessionManager.json.
    internal sealed class Settings
    {
        // Steam lobbies hold at most 250 members.
        public const int LargestSession = 249;

        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<int> MaxPlayers;
        public readonly ConfigEntry<int> SizeStep;
        public readonly ConfigEntry<bool> LetFriendsIn;
        public readonly ConfigEntry<bool> ShowNotices;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<IconStyle> Icon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Shows or hides the Session Manager window. None by default (every F key is taken by other " +
                "mods): click the crown icon instead.");

            MaxPlayers = config.Bind("Session", "MaxPlayers", 128,
                new ConfigDescription(
                    "The biggest session you can pick, on the main menu's Create Session panel and in the window. " +
                    "The game stops at 16. More than 16 players hasn't been tried yet. When BlueSage's (or " +
                    "officerballs') PlayerLimitLift is installed, it decides this instead.",
                    new AcceptableValueRange<int>(16, LargestSession)));
            SizeStep = config.Bind("Session", "SizeStep", 10,
                new ConfigDescription(
                    "How far a Shift-click on the player count's arrows jumps, on the main menu and in the window.",
                    new AcceptableValueRange<int>(2, 50)));
            LetFriendsIn = config.Bind("Requests", "LetFriendsIn", false,
                "Let your Steam friends in straight away when they ask to join. Also a switch in the window.");

            ShowNotices = config.Bind("Display", "ShowNotices", true,
                "Show a short message at the top of the screen when Session Manager does something by itself " +
                "(lets a friend in, turns a request away, ends a ban).");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a crown icon in the row of mod icons at the bottom-right. Clicking it opens and closes the " +
                "window. It's greyed out while you aren't the host.");
            Icon = config.Bind("Display", "Icon", IconStyle.Crown,
                "Which picture the icon shows: Crown, Friends (a crown over three friends) or Clipboard (a guest list).");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write every change Session Manager makes to the session, and every join request it handles, to " +
                "the BepInEx log.");
        }
    }
}
