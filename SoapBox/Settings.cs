using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace SoapBox
{
    internal enum WindowSize
    {
        Small,
        Medium,
        Large,
    }

    // Everything here shows up in BepInEx\config\nelphei.ontogether.soapbox.cfg after the first launch.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> WindowKey;
        public readonly ConfigEntry<string> CartFolder;
        public readonly ConfigEntry<string> LastCart;
        public readonly ConfigEntry<bool> PauseWithoutKeys;
        public readonly ConfigEntry<bool> SitDown;
        public readonly ConfigEntry<WindowSize> Size;
        public readonly ConfigEntry<Handheld.Shell> Shell;
        public readonly ConfigEntry<float> WindowX;
        public readonly ConfigEntry<float> WindowY;
        public readonly ConfigEntry<bool> ControlsSeen;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            WindowKey = config.Bind("Keys", "WindowKey", KeyCode.None,
                "Opens and closes Soap Box. None by default (every F key is taken by other mods): " +
                "click the handheld icon instead.");

            CartFolder = config.Bind("Carts", "CartFolder", "",
                "Where your own carts (.soap files) are kept. Empty means Documents/On-Together/Soap Box.");
            LastCart = config.Bind("Carts", "LastCart", "",
                "The cart that was open last (its file), so Soap Box opens it again. Empty starts with bubble pop.");

            PauseWithoutKeys = config.Bind("Play", "PauseWithoutKeys", true,
                "Pause the cart while Soap Box doesn't have the keyboard (you clicked outside the window, " +
                "or opened chat).");

            SitDown = config.Bind("Character", "SitDown", true,
                "Opening Soap Box while you're standing sits your character down with the handheld console. " +
                "If you're already sitting (at your desk, flying with Desk Swarm...), nothing changes. " +
                "Closing Soap Box never stands you up.");

            Size = config.Bind("Display", "Size", WindowSize.Medium,
                "How big the window is: Small, Medium or Large. Also the S/M/L button in the window.");
            Shell = config.Bind("Display", "Shell", Handheld.Shell.Pink,
                "The handheld's colour: Pink, Aqua, Lilac or Cream. Also the round button in the window's title row.");
            WindowX = config.Bind("Display", "WindowX", -1f,
                "Where you dragged the window to: its left edge, in pixels on a 1080-pixel-high screen. -1 keeps " +
                "it with the other mods' windows. Double-clicking the window's title row sets this back to -1.");
            WindowY = config.Bind("Display", "WindowY", -1f,
                "Where you dragged the window to: its top edge, in pixels on a 1080-pixel-high screen.");
            ControlsSeen = config.Bind("Display", "ControlsSeen", false,
                "The controls card (the ? button) shows by itself the first time you open Soap Box, and this " +
                "remembers that it has. Set it to false to see it by itself again.");
            ShowIcon = config.Bind("Display", "ShowIcon", true,
                "Show a handheld icon in the row of mod icons at the bottom-right. Clicking it opens and closes the window.");
        }

        // Where your own carts are kept.
        public string Folder()
        {
            string folder = CartFolder.Value;
            if (string.IsNullOrWhiteSpace(folder))
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(documents))
                {
                    documents = Application.persistentDataPath;
                }
                folder = Path.Combine(documents, "On-Together", "Soap Box");
            }
            return folder;
        }
    }
}
