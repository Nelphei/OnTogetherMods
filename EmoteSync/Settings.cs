using BepInEx.Configuration;
using UnityEngine;

namespace EmoteSync
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.emotesync.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<KeyCode> PanelKey;
        public readonly ConfigEntry<bool> ShowPanel;
        public readonly ConfigEntry<float> LeadTime;
        public readonly ConfigEntry<bool> AlignOthers;
        public readonly ConfigEntry<bool> SyncFocus;
        public readonly ConfigEntry<float> JoinWaitSeconds;
        public readonly ConfigEntry<bool> ShowNotices;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F7,
                "Turns sync on and off.");
            PanelKey = config.Bind("Keys", "PanelKey", KeyCode.F6,
                "Shows or hides the sync window while sync is on.");

            LeadTime = config.Bind("Sync", "LeadTime", 1f,
                new ConfigDescription(
                    "Seconds between picking an emote and everyone starting it. " +
                    "It gives the message time to reach everyone. Raise it if friends' emotes start late.",
                    new AcceptableValueRange<float>(0.3f, 3f)));
            AlignOthers = config.Bind("Sync", "AlignOthers", true,
                "Line up other players' synced emotes on your screen, so their ping doesn't put them slightly behind you. " +
                "Only changes what you see.");
            SyncFocus = config.Bind("Sync", "SyncFocus", true,
                "Focus together while emote sync is on. When someone in the group picks a focus activity, you start or " +
                "switch to the same one if you can, and yours are shared with them. Focus loops are also kept in step, " +
                "so everyone doing the same activity moves together. Set to false to only sync emotes.");
            JoinWaitSeconds = config.Bind("Sync", "JoinWaitSeconds", 30f,
                new ConfigDescription(
                    "When someone in the group picks a focus activity while you're busy (typing, a window open, " +
                    "mid-emote, in the air...), you join it as soon as you're free, if that's within this many seconds. " +
                    "Nothing is closed or stopped for you, but a \"Join now\" button does that when you click it. " +
                    "Set to 0 to only join when you're free right away.",
                    new AcceptableValueRange<float>(0f, 120f)));

            ShowPanel = config.Bind("Display", "ShowPanelByDefault", true,
                "Show the sync window (who's in your group, and the keys) when you turn sync on. Toggle with PanelKey. " +
                "It sits at the bottom of the screen, above Photo Mode's controls when both are open, and is never in photos.");
            ShowNotices = config.Bind("Display", "ShowNotices", true,
                "Show short messages at the top of the screen, like who has emote sync on.");
            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write extra details about each synced emote to the BepInEx log.");
        }
    }
}
