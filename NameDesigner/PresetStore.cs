using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace NameDesigner
{
    // The saved presets: 5 for player names and 5 for session names, kept in
    // BepInEx\config\NameDesigner.json. Each is stored as the rich text the game gets, like
    // "<#FF8800>Soa</color><#66CCFF>py</color>", so it's easy to read or copy by hand.
    internal sealed class PresetStore
    {
        public const int Slots = 5;

        // The file's layout. Saved with Json.NET (Unity's JsonUtility leaves out lists in a mod's
        // own classes). An empty slot is null.
        private sealed class PresetFile
        {
            public int Version = 1;
            public List<string> Names = new List<string>();
            public List<string> Sessions = new List<string>();
        }

        public readonly Design[] Names = new Design[Slots];
        public readonly Design[] Sessions = new Design[Slots];

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "NameDesigner.json");

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            try
            {
                PresetFile file = JsonConvert.DeserializeObject<PresetFile>(File.ReadAllText(FilePath));
                Fill(Names, file?.Names, withColon: true);
                Fill(Sessions, file?.Sessions, withColon: false);
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
                for (int i = 0; i < Slots; i++)
                {
                    file.Names.Add(Names[i]?.ToRichText());
                    file.Sessions.Add(Sessions[i]?.ToRichText());
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

        private static void Fill(Design[] slots, List<string> saved, bool withColon)
        {
            for (int i = 0; i < Slots; i++)
            {
                string text = saved != null && i < saved.Count ? saved[i] : null;
                Design design = string.IsNullOrEmpty(text) ? null : Design.Parse(text, withColon, out _);
                slots[i] = design != null && design.Count > 0 ? design : null;
            }
        }
    }
}
