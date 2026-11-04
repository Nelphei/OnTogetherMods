using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OutfitSlots
{
    // The game has 3 outfit slots, kept in its save (PlayerDataZip). This mod adds slots 4-9 and makes
    // them real to the game: while you're on one, PlayerDataZip.SelectedStyleIndex holds its number
    // (3-8) and StyleNames holds all 9 names, so the game's own outfit screen code handles every slot.
    // Every read and write of "the selected slot's outfit" goes through
    // PlayerDataZip.CurrentCustomizationDataIDs, which Patches.cs points at this mod's file for
    // slots 4-9. So whatever the game saves (closing the outfit screen, the lemonade hat) lands in the
    // slot you're on, and you load back in wearing it.
    //
    // The game's save file never sees more than 3 slots: while it's written, SelectedStyleIndex goes
    // back to the last of slots 1-3 you used and the extra names are left out (Deflate). The game
    // crashes loading a slot number it doesn't know, so removing the mod just puts you in that slot.
    internal static class Slots
    {
        public const int GameSlots = 3;
        public const int Total = 9;

        public static readonly OutfitStore Store = new OutfitStore();

        // False when BlueSage QoL's own style slots are on, or the patches failed: then the game runs
        // as if the mod wasn't there.
        public static bool Active { get; set; }

        // For each save the game has loaded, the slot (0-2) its file says you're on: the last of
        // slots 1-3 you used.
        private static readonly ConditionalWeakTable<PlayerDataZip, StrongBox<int>> GameSlotOf =
            new ConditionalWeakTable<PlayerDataZip, StrongBox<int>>();

        public static bool IsModSlot(int slot) => slot >= GameSlots && slot < Total;

        // ---- Loading ----

        // Called for every save file the game reads at startup (it reads the save and two backups,
        // then picks one), before it dresses you from it.
        public static void Inflate(PlayerDataZip zip)
        {
            if (!Active || zip == null)
            {
                return;
            }
            int fileSlot = zip.SelectedStyleIndex;
            if (fileSlot < 0 || fileSlot >= GameSlots)
            {
                Plugin.Log.LogWarning($"The game's save said outfit slot {fileSlot + 1}, which it can't load, so it's on slot 1 instead.");
                fileSlot = 0;
                zip.SelectedStyleIndex = 0;
            }
            GameSlotOf.Remove(zip);
            GameSlotOf.Add(zip, new StrongBox<int>(fileSlot));
            zip.StyleNames = NineNames(zip.StyleNames);

            // Back on the slot you had on, unless you changed slot without the mod since.
            int wanted = Store.SelectedSlot;
            if (IsModSlot(wanted) && Store.GameSlot == fileSlot && Store.Outfit(wanted) != null)
            {
                zip.SelectedStyleIndex = wanted;
            }
        }

        // Makes sure a save has all 9 names, in case the game loaded it before the mod could add them.
        public static void EnsureNames(PlayerDataZip zip)
        {
            if (zip != null && (zip.StyleNames == null || zip.StyleNames.Count < Total))
            {
                zip.StyleNames = NineNames(zip.StyleNames);
            }
        }

        // The game's 3 names followed by this mod's 6.
        private static List<string> NineNames(List<string> game)
        {
            var names = new List<string>(Total);
            for (int i = 0; i < GameSlots; i++)
            {
                names.Add(game != null && i < game.Count && game[i] != null ? game[i] : "");
            }
            for (int i = GameSlots; i < Total; i++)
            {
                names.Add(Store.Name(i));
            }
            return names;
        }

        // ---- The selected slot's outfit ----

        public static CustomizationDataIDs3 OutfitFor(PlayerDataZip zip, int slot)
        {
            CustomizationDataIDs3 outfit = Store.Outfit(slot);
            if (outfit == null)
            {
                // A new slot starts as a copy of what you're wearing, so you can change it from there.
                DataManager data = Game.Data;
                outfit = data != null && data.CustomizationData != null
                    ? new CustomizationDataIDs3(data.CustomizationData)
                    : new CustomizationDataIDs3();
                Store.SetOutfit(slot, outfit);
                SaveStore(zip);
            }
            return outfit;
        }

        public static void SetOutfitFor(PlayerDataZip zip, int slot, CustomizationDataIDs3 outfit)
        {
            if (outfit == null)
            {
                return;
            }
            Store.SetOutfit(slot, outfit);
            SaveStore(zip);
        }

        // ---- Saving ----

        // What Deflate changed, to put back once the game's save is written.
        internal sealed class Saved
        {
            public PlayerDataZip Zip;
            public int Selected;
            public List<string> Names;
        }

        // Makes the save look like the game's own 3-slot one while it's written to disk.
        public static Saved Deflate(PlayerDataZip zip)
        {
            if (zip == null)
            {
                return null;
            }
            int gameSlot = GameSlot(zip);
            bool extraSlot = zip.SelectedStyleIndex >= GameSlots;
            bool extraNames = zip.StyleNames != null && zip.StyleNames.Count > GameSlots;
            if (!extraSlot && !extraNames)
            {
                return null;
            }
            var saved = new Saved { Zip = zip, Selected = zip.SelectedStyleIndex, Names = zip.StyleNames };
            if (extraSlot)
            {
                zip.SelectedStyleIndex = gameSlot;
            }
            if (extraNames)
            {
                zip.StyleNames = zip.StyleNames.GetRange(0, GameSlots);
            }
            return saved;
        }

        public static void Restore(Saved saved)
        {
            if (saved == null)
            {
                return;
            }
            saved.Zip.SelectedStyleIndex = saved.Selected;
            saved.Zip.StyleNames = saved.Names;
        }

        // The last of slots 1-3 used with this save, which is what its file says you're on.
        private static int GameSlot(PlayerDataZip zip)
        {
            int now = zip.SelectedStyleIndex;
            if (!GameSlotOf.TryGetValue(zip, out StrongBox<int> box))
            {
                box = new StrongBox<int>(now >= 0 && now < GameSlots ? now : 0);
                GameSlotOf.Add(zip, box);
            }
            if (now >= 0 && now < GameSlots)
            {
                box.Value = now;
            }
            return box.Value;
        }

        // Writes this mod's file (if anything changed) from the save the game is using.
        public static void SaveStore(PlayerDataZip zip)
        {
            DataManager data = Game.Data;
            if (!Active || zip == null || data == null || data.PlayerDataZip != zip)
            {
                return;
            }
            Store.SelectedSlot = zip.SelectedStyleIndex >= 0 && zip.SelectedStyleIndex < Total ? zip.SelectedStyleIndex : 0;
            Store.GameSlot = GameSlot(zip);
            if (zip.StyleNames != null && zip.StyleNames.Count >= Total)
            {
                for (int slot = GameSlots; slot < Total; slot++)
                {
                    Store.SetName(slot, zip.StyleNames[slot]);
                }
            }
            Store.SaveIfChanged();
        }

        // ---- Names for the window ----

        public static string Name(PlayerDataZip zip, int slot)
        {
            if (zip?.StyleNames != null && slot < zip.StyleNames.Count)
            {
                return zip.StyleNames[slot] ?? "";
            }
            return IsModSlot(slot) ? Store.Name(slot) : "";
        }

        // "5 (dressie)", or "slot 5" with no name.
        public static string Label(PlayerDataZip zip, int slot)
        {
            string name = Name(zip, slot).Trim();
            return name.Length > 0 ? $"{slot + 1} ({name})" : $"slot {slot + 1}";
        }

        public static bool IsEmpty(int slot) => IsModSlot(slot) && Store.Outfit(slot) == null;
    }
}
