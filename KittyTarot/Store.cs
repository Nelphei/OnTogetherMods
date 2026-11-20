using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace KittyTarot
{
    // BepInEx\config\KittyTarot.json: your deck (its order, and which cards lie upside down) and the
    // reading on the table, so closing the game doesn't lose them. Saved with Json.NET (Unity's
    // JsonUtility leaves out nested classes).
    internal sealed class Store
    {
        private sealed class StoreFile
        {
            public int Version = 1;
            public Reading.Saved Table;
        }

        public Reading.Saved Saved;

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "KittyTarot.json");

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            try
            {
                Saved = JsonConvert.DeserializeObject<StoreFile>(File.ReadAllText(FilePath))?.Table;
            }
            catch (Exception e)
            {
                // Keep a copy, so the next save doesn't wipe out a deck we couldn't read.
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

        public void Save(Reading reading)
        {
            try
            {
                var file = new StoreFile { Table = reading.Save() };
                // Written beside the old file and swapped in, so quitting halfway can't leave half a file.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(file, Formatting.Indented));
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save to {FilePath}: {e}");
            }
        }
    }
}
