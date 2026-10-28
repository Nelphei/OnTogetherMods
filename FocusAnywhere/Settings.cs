using BepInEx.Configuration;

namespace FocusAnywhere
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.focusanywhere.cfg after the first launch.
    // Changes take effect the next time the game starts.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> AllActivities;
        public readonly ConfigEntry<bool> AnyGround;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            AllActivities = config.Bind("Focus", "AllActivities", true,
                "The focus menu shows every activity in the game: the normal ones, the water ones, and the " +
                "ones from seat spots (play tower, bean bag, tree hanger...). Seat spots keep their own menu.");
            AnyGround = config.Bind("Focus", "AnyGround", true,
                "The focus key opens the menu on any ground, near the music hill, and next to a seat someone " +
                "else is using. The basketball court stays off limits.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write extra details to the BepInEx log (the activity lists and the menu buttons).");
        }
    }
}
