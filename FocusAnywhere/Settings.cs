using BepInEx.Configuration;

namespace FocusAnywhere
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.focusanywhere.cfg after the first launch.
    // Changes made in the file take effect the next time the game starts. Enabled also changes from the
    // mod's icon while you play.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> AllActivities;
        public readonly ConfigEntry<bool> AnyGround;
        public readonly ConfigEntry<bool> MidAir;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            Enabled = config.Bind("Focus", "Enabled", true,
                "Switches the whole mod on or off. The mod's icon at the bottom-right changes this, and it's " +
                "remembered between games. Switched off, the focus menu goes back to the game's own activities " +
                "the next time you're not focusing.");
            AllActivities = config.Bind("Focus", "AllActivities", true,
                "The focus menu shows every activity in the game: the normal ones, the water ones, and the " +
                "ones from seat spots (play tower, bean bag, tree hanger...). Seat spots keep their own menu.");
            AnyGround = config.Bind("Focus", "AnyGround", true,
                "The focus key opens the menu on any ground, near the music hill, and next to a seat someone " +
                "else is using. The basketball court stays off limits.");
            MidAir = config.Bind("Focus", "MidAir", true,
                "The focus key works in the air too: you stop where you are and focus floating there. " +
                "Closing the menu without picking, or standing up, lets you fall again. Needs AnyGround on " +
                "(or BlueSage QoL's own Focus Anywhere).");

            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show an icon in the row of mod icons at the bottom-right, with a green dot while the mod is on. " +
                "Clicking it switches Enabled.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write extra details to the BepInEx log (the activity lists and the menu buttons).");
        }
    }
}
