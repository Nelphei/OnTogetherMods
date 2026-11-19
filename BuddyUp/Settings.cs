using BepInEx.Configuration;
using UnityEngine;

namespace BuddyUp
{
    // Where you walk while you follow a buddy.
    internal enum WalkSpot
    {
        Beside,   // next to them, one on each side
        Behind,   // in a line behind them, along the way they walked
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.buddyup.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<float> RideHeight;
        public readonly ConfigEntry<FocusType> RideActivity;
        public readonly ConfigEntry<int> RideChoice;
        public readonly ConfigEntry<WalkSpot> Walk;
        public readonly ConfigEntry<float> WalkGap;
        public readonly ConfigEntry<bool> SitTogether;
        public readonly ConfigEntry<bool> CatchUp;
        public readonly ConfigEntry<float> CatchUpDistance;
        public readonly ConfigEntry<bool> ShowNotices;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Shows or hides the Buddy Up window. None by default (other mods use every F key): click the icon.");

            RideHeight = config.Bind("Riding", "RideHeight", 0.35f,
                new ConfigDescription(
                    "How far above your buddy's head you sit, in metres. Raise it if a big hat pokes through you. " +
                    "Also the - and + buttons in the window while you ride.",
                    new AcceptableValueRange<float>(0f, 1.5f)));
            RideActivity = config.Bind("Riding", "RideActivity", FocusType.Meditate,
                "The kind of activity you sit down in when you climb on someone's head while standing. If you're " +
                "already sitting, you keep doing what you're doing up there.");
            RideChoice = config.Bind("Riding", "RideChoice", 0,
                new ConfigDescription(
                    "Which activity of that kind, counting as the focus menu does (1 is the first). 0 picks the " +
                    "first one without a desk or anything in your hands.",
                    new AcceptableValueRange<int>(0, 20)));

            Walk = config.Bind("Following", "Walk", WalkSpot.Beside,
                "Beside: walk next to your buddy. Behind: walk in a line behind them, along the way they went. " +
                "Also in the window.");
            WalkGap = config.Bind("Following", "WalkGap", 1f,
                new ConfigDescription(
                    "How far from your buddy you walk, in metres.",
                    new AcceptableValueRange<float>(0.6f, 3f)));
            SitTogether = config.Bind("Following", "SitTogether", true,
                "While you walk with a buddy, sit down beside them in the same activity when they sit down. " +
                "Standing up again is up to you. Also in the window.");
            CatchUp = config.Bind("Following", "CatchUp", true,
                "Hop over to your buddy when you get stuck or fall far behind.");
            CatchUpDistance = config.Bind("Following", "CatchUpDistance", 25f,
                new ConfigDescription(
                    "How far behind you can fall before you hop over to your buddy, in metres.",
                    new AcceptableValueRange<float>(10f, 100f)));

            ShowNotices = config.Bind("Display", "ShowNotices", true,
                "Show a short message at the top of the screen when someone asks to be your buddy, climbs on, and so on.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show the Buddy Up icon in the row of mod icons at the bottom-right. Clicking it opens the window, " +
                "and right-clicking it gets you off a head or stops walking with your buddy.");
            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write every Buddy Up message and what the mod does to the BepInEx log.");
        }
    }
}
