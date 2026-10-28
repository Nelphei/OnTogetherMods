using System;
using System.IO;
using BepInEx.Configuration;

namespace TraceOverlay
{
    // Everything here shows up in BepInEx\config\nelphei.ontogether.traceoverlay.cfg after the first launch.
    // The window also saves your last image, opacity and fit here, so they're kept next time.
    internal sealed class Settings
    {
        public readonly ConfigEntry<string> ImageFolder;
        public readonly ConfigEntry<string> LastImage;
        public readonly ConfigEntry<float> Opacity;
        public readonly ConfigEntry<bool> FillBoard;
        public readonly ConfigEntry<bool> Collapsed;
        public readonly ConfigEntry<int> VisibleRows;

        public Settings(ConfigFile config)
        {
            ImageFolder = config.Bind("Images", "ImageFolder", "",
                "Folder the image list reads .png and .jpg files from. Leave empty to use Pictures\\On-Together\\Trace.");
            LastImage = config.Bind("Images", "LastImage", "",
                "The image shown last time. It comes back when you start drawing again.");

            Opacity = config.Bind("Guide", "Opacity", 0.4f,
                new ConfigDescription("How see-through the guide image is. 1 is solid.",
                    new AcceptableValueRange<float>(0.05f, 1f)));
            FillBoard = config.Bind("Guide", "FillBoard", false,
                "Stretch the image to cover the whole board, cutting off the edges that don't fit. " +
                "When off, the whole image is shown and may leave empty bands at the sides.");

            Collapsed = config.Bind("Display", "Collapsed", false,
                "Shrink the window to one line so it covers less of the board.");
            VisibleRows = config.Bind("Display", "VisibleRows", 5,
                new ConfigDescription("How many images the window shows before it scrolls.",
                    new AcceptableValueRange<int>(3, 15)));
        }

        public string Folder()
        {
            string folder = ImageFolder.Value;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "On-Together", "Trace");
            }
            return folder;
        }
    }
}
