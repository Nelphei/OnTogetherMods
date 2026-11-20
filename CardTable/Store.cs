using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace CardTable
{
    // Wins and losses, for the Stats button.
    internal sealed class SolitaireStats
    {
        public int Played;
        public int Won;
        // Wins in a row, now and at best. Starting a new game before winning ends a streak.
        public int Streak;
        public int BestStreak;
        // Of the games won. 0 means none yet.
        public double BestSeconds;
        public int FewestMoves;
    }

    // Go Fish games played to the end (a game joined after it ended doesn't count).
    internal sealed class GoFishStats
    {
        public int Played;
        public int Won;   // ties for the most books count
        public int Books;
        public int MostBooks;
    }

    // BepInEx\config\CardTable.json: the Solitaire game in progress (so closing the window or the game
    // doesn't lose it) and the stats. Saved with Json.NET (Unity's JsonUtility leaves out nested classes).
    // Go Fish games aren't saved: a table ends when you leave the world.
    internal sealed class Store
    {
        private sealed class StoreFile
        {
            public int Version = 1;
            public Solitaire.Saved Solitaire;
            public SolitaireStats SolitaireStats;
            public GoFishStats GoFishStats;
        }

        public Solitaire.Saved SavedGame;
        public SolitaireStats Stats = new SolitaireStats();
        public GoFishStats FishStats = new GoFishStats();

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "CardTable.json");

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }
            try
            {
                StoreFile file = JsonConvert.DeserializeObject<StoreFile>(File.ReadAllText(FilePath));
                SavedGame = file?.Solitaire;
                Stats = file?.SolitaireStats ?? new SolitaireStats();
                FishStats = file?.GoFishStats ?? new GoFishStats();
            }
            catch (Exception e)
            {
                // Keep a copy, so the next save doesn't wipe out stats we couldn't read.
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

        public void Save(Solitaire game)
        {
            try
            {
                var file = new StoreFile { Solitaire = game.Save(), SolitaireStats = Stats, GoFishStats = FishStats };
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
