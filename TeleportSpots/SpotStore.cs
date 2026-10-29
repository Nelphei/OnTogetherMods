using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace TeleportSpots
{
    internal sealed class Spot
    {
        public string Name;
        public Vector3 Position;
        // Which way the character faced (degrees around the up axis).
        public float Facing;
        // Which way the camera looked, so teleporting shows the same view.
        public float CameraYaw;
    }

    // The saved spots, kept in BepInEx\config\TeleportSpots.json so you can back it up or edit it.
    internal sealed class SpotStore
    {
        public const int MaxNameLength = 32;

        // The file's layout. Saved with Json.NET because Unity's JsonUtility leaves out lists of a
        // mod's own classes (it only wrote "Version"). Json.NET can't write a Vector3 by itself,
        // so the position is stored as plain numbers.
        private sealed class SpotFile
        {
            public int Version = 1;
            public List<SavedSpot> Spots = new List<SavedSpot>();
        }

        private sealed class SavedSpot
        {
            public string Name;
            public SavedPosition Position;
            public float Facing;
            public float CameraYaw;
        }

        private sealed class SavedPosition
        {
            public float x;
            public float y;
            public float z;
        }

        public readonly List<Spot> Spots = new List<Spot>();

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "TeleportSpots.json");

        public void Load()
        {
            Spots.Clear();
            if (!File.Exists(FilePath))
            {
                return;
            }

            try
            {
                SpotFile file = JsonConvert.DeserializeObject<SpotFile>(File.ReadAllText(FilePath));
                if (file?.Spots == null)
                {
                    return;
                }
                foreach (SavedSpot saved in file.Spots)
                {
                    // Without a position there's nowhere to go.
                    if (saved?.Position == null)
                    {
                        continue;
                    }
                    var spot = new Spot
                    {
                        Name = CleanName(saved.Name),
                        Position = new Vector3(saved.Position.x, saved.Position.y, saved.Position.z),
                        Facing = saved.Facing,
                        CameraYaw = saved.CameraYaw,
                    };
                    if (spot.Name.Length == 0)
                    {
                        spot.Name = NextDefaultName();
                    }
                    Spots.Add(spot);
                }
                Plugin.Log.LogInfo($"Loaded {Spots.Count} teleport spots.");
            }
            catch (Exception e)
            {
                // Keep a copy, so saving a new spot doesn't wipe out the ones we couldn't read.
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
                var file = new SpotFile();
                foreach (Spot spot in Spots)
                {
                    file.Spots.Add(new SavedSpot
                    {
                        Name = spot.Name,
                        Position = new SavedPosition { x = spot.Position.x, y = spot.Position.y, z = spot.Position.z },
                        Facing = spot.Facing,
                        CameraYaw = spot.CameraYaw,
                    });
                }
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save teleport spots to {FilePath}: {e}");
                return false;
            }
        }

        // "Spot 1", "Spot 2"... skipping names already in the list.
        public string NextDefaultName()
        {
            for (int n = Spots.Count + 1; ; n++)
            {
                string name = "Spot " + n;
                if (!Spots.Exists(s => s.Name == name))
                {
                    return name;
                }
            }
        }

        // One line, no control characters, trimmed and capped in length.
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            var clean = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                clean.Append(char.IsControl(c) ? ' ' : c);
            }
            string result = clean.ToString().Trim();
            return result.Length > MaxNameLength ? result.Substring(0, MaxNameLength).TrimEnd() : result;
        }
    }
}
