using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace TeleportSpots
{
    [Serializable]
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

        [Serializable]
        private sealed class SpotFile
        {
            public int Version = 1;
            public List<Spot> Spots = new List<Spot>();
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
                SpotFile file = JsonUtility.FromJson<SpotFile>(File.ReadAllText(FilePath));
                if (file?.Spots == null)
                {
                    return;
                }
                foreach (Spot spot in file.Spots)
                {
                    if (spot != null)
                    {
                        spot.Name = CleanName(spot.Name);
                        if (spot.Name.Length == 0)
                        {
                            spot.Name = NextDefaultName();
                        }
                        Spots.Add(spot);
                    }
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
                var file = new SpotFile { Spots = Spots };
                File.WriteAllText(FilePath, JsonUtility.ToJson(file, prettyPrint: true));
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
