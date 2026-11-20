using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace BubbleShell
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.bubbleshell.cfg after the first
    // launch. The console's own files (your binds and aliases, autoexec.cfg, the history) are in
    // BepInEx\config\BubbleShell.
    internal sealed class Settings
    {
        public readonly ConfigEntry<KeyCode> ConsoleKey;
        public readonly ConfigEntry<float> Height;
        public readonly ConfigEntry<float> Opacity;
        public readonly ConfigEntry<int> TextScale;
        public readonly ConfigEntry<bool> PixelFont;
        public readonly ConfigEntry<bool> Bubbles;
        public readonly ConfigEntry<bool> ShowIcon;

        public Settings(ConfigFile config)
        {
            ConsoleKey = config.Bind("Keys", "ConsoleKey", KeyCode.BackQuote,
                "Opens and closes the console. BackQuote is the ` key (left of 1 on US keyboards). If your keyboard " +
                "has no such key, pick another here, or click the bubble icon.");

            Height = config.Bind("Look", "Height", 0.4f,
                new ConfigDescription("How far down the console drops, as a part of the screen's height (0.4 = 40%).",
                    new AcceptableValueRange<float>(0.2f, 0.9f)));
            Opacity = config.Bind("Look", "Opacity", 0.85f,
                new ConfigDescription("How dark the console's background is: 1 hides the game behind it, 0.3 barely shades it.",
                    new AcceptableValueRange<float>(0.3f, 1f)));
            TextScale = config.Bind("Look", "TextScale", 0,
                new ConfigDescription("How big the text is: screen pixels per pixel of the font. 0 picks one by itself " +
                                      "(2 on a 1080p screen, 3 at 1440p). Whole numbers keep the pixels crisp.",
                    new AcceptableValueRange<int>(0, 8)));
            PixelFont = config.Bind("Look", "PixelFont", true,
                "Use the Monogram pixel font that comes with the mod. Off uses the game's own font instead.");
            Bubbles = config.Bind("Look", "Bubbles", true,
                "Little bubbles drift up behind the text.");
            ShowIcon = config.Bind("Look", "ShowIcon", true,
                "Show a bubble icon in the row of mod icons at the bottom-right. Clicking it opens and closes the console.");
        }

        // The console's own files: BepInEx\config\BubbleShell.
        public static string DataFolder() => Path.Combine(Paths.ConfigPath, "BubbleShell");
    }
}
