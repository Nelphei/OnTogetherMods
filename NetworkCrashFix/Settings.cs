using BepInEx.Configuration;

namespace NetworkCrashFix
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.networkcrashfix.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Read the end of each network packet safely, so the game can't crash there. The mod's icon at the " +
                "bottom-right switches this on and off, and it's remembered between games.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a shield icon in the row of mod icons at the bottom-right, with a green dot while Enabled is " +
                "on. Clicking it switches Enabled.");

            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Every 5 minutes, write to the BepInEx log how many reads at the end of a packet the fix made safe.");
        }
    }
}
