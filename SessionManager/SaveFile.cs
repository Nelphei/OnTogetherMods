using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace SessionManager
{
    // What Session Manager knows about a ban beyond the game's own list (which only has the Steam ID
    // and a name): when it started, when it ends, and the host's note.
    internal sealed class BanInfo
    {
        public string SteamId;
        public string Name;
        // UTC.
        public DateTime Banned;
        // UTC. Null for a ban that never ends.
        public DateTime? Until;
        public string Note;
    }

    // A session's settings, saved to put back in a later session.
    internal sealed class SavedSetup
    {
        public string Name;
        public int Players;
        public bool Listed;
        public bool AskToJoin;
        public List<string> Tags = new List<string>();
    }

    // Kept in BepInEx\config\SessionManager.json. Saved with Json.NET, because Unity's JsonUtility
    // leaves out lists of a mod's own classes.
    internal sealed class SaveFile
    {
        private sealed class Contents
        {
            public int Version = 1;
            public SavedSetup Setup;
            public List<BanInfo> Bans = new List<BanInfo>();
        }

        private Contents _contents = new Contents();

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "SessionManager.json");

        public SavedSetup Setup
        {
            get => _contents.Setup;
            set => _contents.Setup = value;
        }

        public List<BanInfo> Bans => _contents.Bans;

        public BanInfo Ban(string steamId)
        {
            return _contents.Bans.Find(b => b.SteamId == steamId);
        }

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            try
            {
                Contents contents = JsonConvert.DeserializeObject<Contents>(File.ReadAllText(FilePath),
                    new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc });
                if (contents != null)
                {
                    contents.Bans = contents.Bans ?? new List<BanInfo>();
                    contents.Bans.RemoveAll(b => b == null || string.IsNullOrEmpty(b.SteamId));
                    if (contents.Setup != null)
                    {
                        contents.Setup.Tags = contents.Setup.Tags ?? new List<string>();
                    }
                    _contents = contents;
                }
            }
            catch (Exception e)
            {
                // Start fresh, but keep a copy of the broken file, since the next save replaces it.
                string backup = FilePath + ".bak";
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

        // False if it couldn't be written (the error goes to the log).
        public bool Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_contents, Formatting.Indented,
                    new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Utc }));
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save {FilePath}: {e.Message}");
                return false;
            }
        }
    }
}
