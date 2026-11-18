using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace CardDesigner
{
    // 5 saved card designs, kept in BepInEx\config\CardDesigner.json. Each line is stored as the rich
    // text the game gets, so it's easy to read or copy by hand.
    internal sealed class PresetStore
    {
        public const int Slots = 5;

        // The file's layout. Saved with Json.NET (Unity's JsonUtility leaves out a mod's own classes
        // inside lists). An empty slot is null.
        private sealed class PresetFile
        {
            public int Version = 1;
            public List<CardPreset> Presets = new List<CardPreset>();
        }

        private sealed class CardPreset
        {
            public string DateOfBirth;
            public string HoroscopeSign;
            public string Location;
            public string AreaOfStudy;
            public string FavoriteQuote;
        }

        // Each slot holds the card's 5 lines in Game's order, or null.
        public readonly string[][] Cards = new string[Slots][];

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "CardDesigner.json");

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            try
            {
                PresetFile file = JsonConvert.DeserializeObject<PresetFile>(File.ReadAllText(FilePath));
                for (int i = 0; i < Slots; i++)
                {
                    CardPreset p = file?.Presets != null && i < file.Presets.Count ? file.Presets[i] : null;
                    Cards[i] = p == null ? null : new[]
                    {
                        p.DateOfBirth ?? "", p.HoroscopeSign ?? "", p.Location ?? "", p.AreaOfStudy ?? "", p.FavoriteQuote ?? "",
                    };
                }
            }
            catch (Exception e)
            {
                // Keep a copy, so saving a preset doesn't wipe out the ones we couldn't read.
                string backup = FilePath + ".bad";
                Plugin.Log.LogError($"Couldn't read {FilePath}, so it was copied to {backup}: {e.Message}");
                try
                {
                    File.Copy(FilePath, backup, overwrite: true);
                }
                catch (Exception copyError)
                {
                    Plugin.Log.LogError(copyError);
                }
            }
        }

        public bool Save()
        {
            try
            {
                var file = new PresetFile();
                foreach (string[] card in Cards)
                {
                    file.Presets.Add(card == null ? null : new CardPreset
                    {
                        DateOfBirth = card[Game.DateOfBirth],
                        HoroscopeSign = card[Game.HoroscopeSign],
                        Location = card[Game.Location],
                        AreaOfStudy = card[Game.AreaOfStudy],
                        FavoriteQuote = card[Game.Quote],
                    });
                }
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save presets to {FilePath}: {e}");
                return false;
            }
        }
    }
}
