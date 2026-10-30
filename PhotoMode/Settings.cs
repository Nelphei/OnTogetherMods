using BepInEx.Configuration;
using UnityEngine;

namespace PhotoMode
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.photomode.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<KeyCode> CaptureKey;
        public readonly ConfigEntry<KeyCode> NameTagsKey;
        public readonly ConfigEntry<KeyCode> ChatKey;
        public readonly ConfigEntry<KeyCode> ResetKey;
        public readonly ConfigEntry<KeyCode> HelpKey;

        public readonly ConfigEntry<float> MoveSpeed;
        public readonly ConfigEntry<float> FastMultiplier;
        public readonly ConfigEntry<float> SlowMultiplier;
        public readonly ConfigEntry<float> LookSensitivity;
        public readonly ConfigEntry<float> MaxDistance;

        public readonly ConfigEntry<bool> HideNameTags;
        public readonly ConfigEntry<bool> ShowChat;
        public readonly ConfigEntry<bool> ShowHelp;
        public readonly ConfigEntry<bool> ShutterSound;
        public readonly ConfigEntry<int> ResolutionMultiplier;
        public readonly ConfigEntry<string> SaveFolder;

        public Settings(ConfigFile config)
        {
            ToggleKey = config.Bind("Keys", "ToggleKey", KeyCode.F9,
                "Turns photo mode on and off. Escape also turns it off.");
            CaptureKey = config.Bind("Keys", "CaptureKey", KeyCode.P,
                "Takes a photo. Only works while photo mode is on.");
            NameTagsKey = config.Bind("Keys", "NameTagsKey", KeyCode.H,
                "Shows or hides name tags and chat bubbles while photo mode is on.");
            ChatKey = config.Bind("Keys", "ChatKey", KeyCode.T,
                "Shows or hides the chat box while photo mode is on. While it's showing, press Enter or click " +
                "the chat box to type, and Esc to stop typing. Photo mode keys pause while you type.");
            ResetKey = config.Bind("Keys", "ResetKey", KeyCode.R,
                "Moves the camera back to where photo mode started.");
            HelpKey = config.Bind("Keys", "HelpKey", KeyCode.F1,
                "Shows or hides the controls panel while photo mode is on.");

            MoveSpeed = config.Bind("Camera", "MoveSpeed", 3f,
                new ConfigDescription("Camera speed in metres per second. For comparison, players walk at 4.",
                    new AcceptableValueRange<float>(0.1f, 50f)));
            FastMultiplier = config.Bind("Camera", "FastMultiplier", 3f,
                new ConfigDescription("Speed multiplier while holding Shift.",
                    new AcceptableValueRange<float>(1f, 20f)));
            SlowMultiplier = config.Bind("Camera", "SlowMultiplier", 0.25f,
                new ConfigDescription("Speed multiplier while holding Ctrl.",
                    new AcceptableValueRange<float>(0.01f, 1f)));
            LookSensitivity = config.Bind("Camera", "LookSensitivity", 2f,
                new ConfigDescription("How fast the camera turns when you hold the right mouse button.",
                    new AcceptableValueRange<float>(0.1f, 20f)));
            MaxDistance = config.Bind("Camera", "MaxDistance", 30f,
                new ConfigDescription("How far the camera can fly from your character, in metres. 0 means no limit.",
                    new AcceptableValueRange<float>(0f, 1000f)));

            HideNameTags = config.Bind("Photos", "HideNameTagsByDefault", true,
                "Hide name tags and chat bubbles when photo mode starts. Toggle with NameTagsKey.");
            ShowChat = config.Bind("Photos", "ShowChatByDefault", false,
                "Keep the chat box visible when photo mode starts. Toggle with ChatKey. When shown, it appears in photos too.");
            ShowHelp = config.Bind("Photos", "ShowHelpByDefault", true,
                "Show the controls panel when photo mode starts. It is never included in photos.");
            ShutterSound = config.Bind("Photos", "ShutterSound", true,
                "Play a click when a photo is saved. Only you hear it.");
            ResolutionMultiplier = config.Bind("Photos", "ResolutionMultiplier", 1,
                new ConfigDescription("Saves photos at this multiple of your screen resolution. 2 on a 1080p screen gives 4K photos.",
                    new AcceptableValueRange<int>(1, 4)));
            SaveFolder = config.Bind("Photos", "SaveFolder", "",
                "Folder for photos. Leave empty to use Pictures\\On-Together.");
        }
    }
}
