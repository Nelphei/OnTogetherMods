using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace NetworkCrashFix
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.networkcrashfix.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Every 5 minutes, write to the BepInEx log how many reads at the end of a packet the fix made safe.");

            // 0.1.0 had an on/off switch and an icon. BepInEx keeps settings nobody reads in the file, so
            // take those out, or Enabled = false would look like it still switches the fix off.
            RemoveOld(config, new ConfigDefinition("General", "Enabled"), new ConfigDefinition("Display", "ShowIcon"));
        }

        private static void RemoveOld(ConfigFile config, params ConfigDefinition[] old)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")
                    ?.GetValue(config) as Dictionary<ConfigDefinition, string>;
                if (orphans == null)
                {
                    return;
                }
                bool removed = false;
                foreach (var definition in old)
                {
                    removed |= orphans.Remove(definition);
                }
                if (removed)
                {
                    config.Save();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogDebug($"Couldn't tidy old settings out of the settings file: {e.Message}");
            }
        }
    }
}
