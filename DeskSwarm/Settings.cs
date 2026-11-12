using BepInEx.Configuration;
using UnityEngine;

namespace DeskSwarm
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.deskswarm.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<KeyCode> FlyKey;
        public readonly ConfigEntry<KeyCode> UpKey;
        public readonly ConfigEntry<KeyCode> DownKey;
        public readonly ConfigEntry<float> FlySpeed;
        public readonly ConfigEntry<float> ClimbSpeed;
        public readonly ConfigEntry<bool> Tilt;
        public readonly ConfigEntry<bool> ThroughBuildings;
        public readonly ConfigEntry<Formation> StartFormation;
        public readonly ConfigEntry<bool> ShowNotices;
        public readonly ConfigEntry<bool> ShowIcon;
        public readonly ConfigEntry<bool> DebugLog;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.F11,
                "Shows or hides the Desk Swarm window.");
            FlyKey = config.Bind("Keys", "FlyKey", KeyCode.None,
                "Takes off or stops flying while you're sitting at your desk. None by default: use the window's " +
                "button or right-click the icon.");
            UpKey = config.Bind("Keys", "UpKey", KeyCode.Space,
                "Flies up while you're flying your desk. While you fly, this key doesn't stand you up: stop flying first.");
            DownKey = config.Bind("Keys", "DownKey", KeyCode.LeftShift,
                "Flies down while you're flying your desk.");

            FlySpeed = config.Bind("Flying", "FlySpeed", 6f,
                new ConfigDescription(
                    "How fast your desk flies with the movement keys, in metres per second.",
                    new AcceptableValueRange<float>(1f, 20f)));
            ClimbSpeed = config.Bind("Flying", "ClimbSpeed", 3f,
                new ConfigDescription(
                    "How fast your desk flies up and down, in metres per second.",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            Tilt = config.Bind("Flying", "Tilt", true,
                "Tip the desk a little into the way it's flying, like a drone. Everyone sees it.");
            ThroughBuildings = config.Bind("Flying", "ThroughBuildings", true,
                "Fly through bridges, buildings and everything else on the islands, but never down through the " +
                "ground or water. Off: you can't fly down through anything, so you can land on roofs and bridges. " +
                "While you lead a swarm, your followers fly the same way. Also a switch in the window.");

            StartFormation = config.Bind("Swarm", "StartFormation", Formation.Ring,
                "The formation a swarm you start begins with. You can change it in the window.");

            ShowNotices = config.Bind("Display", "ShowNotices", true,
                "Show a short message at the top of the screen when someone joins or leaves your swarm, and so on.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a flying desk icon in the row of mod icons at the bottom-right. Clicking it does what " +
                "WindowKey does, and right-clicking it takes off or stops flying.");
            DebugLog = config.Bind("Debug", "DebugLog", false,
                "Write every swarm message and flight change to the BepInEx log.");
        }
    }
}
