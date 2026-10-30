using BepInEx.Configuration;

namespace ChatRange
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.chatrange.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> ShowOnGlobalTab;
        public readonly ConfigEntry<int> MaxNames;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            Enabled = config.Bind("Display", "Enabled", true,
                "Show who's in range of your local chat messages, in a small box next to the chat box.");
            ShowOnGlobalTab = config.Bind("Display", "ShowOnGlobalTab", false,
                "Also show the box while the chat box is on its Global tab. Normally it only shows on the Local tab.");
            MaxNames = config.Bind("Display", "MaxNames", 8,
                new ConfigDescription(
                    "The most names listed at once. Any more are counted (\"and 3 more\").",
                    new AcceptableValueRange<int>(1, 30)));

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write where the chat box was found on screen to the BepInEx log.");
        }
    }
}
