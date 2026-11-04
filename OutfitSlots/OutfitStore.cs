using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace OutfitSlots
{
    // Slots 4-9, kept in BepInEx\config\OutfitSlots.json so you can back them up. Slots 1-3 stay in the
    // game's own save. Outfits are stored the way the game stores slots 1-3 (CustomizationDataIDs3:
    // item IDs and colour numbers), with Json.NET like the game.
    internal sealed class OutfitStore
    {
        private const int Count = Slots.Total - Slots.GameSlots;

        // The file's layout. Slot numbers are 1-9, as the game shows them.
        private sealed class OutfitFile
        {
            public int Version = 1;
            // The slot you had on, and which of slots 1-3 the game's own save said you were on then.
            // If the game's save says something else next time (you changed outfit without the mod),
            // the game's choice wins.
            public int SelectedSlot = 1;
            public int GameSlot = 1;
            public List<SavedSlot> Slots = new List<SavedSlot>();
        }

        private sealed class SavedSlot
        {
            public int Slot;
            public string Name = "";
            // Missing for a slot you haven't used yet.
            public CustomizationDataIDs3 Outfit;
        }

        private readonly string[] _names = new string[Count];
        private readonly CustomizationDataIDs3[] _outfits = new CustomizationDataIDs3[Count];

        // 0-8 and 0-2, like the game's SelectedStyleIndex.
        public int SelectedSlot;
        public int GameSlot;

        // What's in the file now, so it's only written when something changed.
        private string _written;

        public string FilePath { get; } = Path.Combine(Paths.ConfigPath, "OutfitSlots.json");

        public OutfitStore()
        {
            for (int i = 0; i < Count; i++)
            {
                _names[i] = "";
            }
        }

        public string Name(int slot) => _names[slot - Slots.GameSlots] ?? "";

        public void SetName(int slot, string name) => _names[slot - Slots.GameSlots] = name ?? "";

        public CustomizationDataIDs3 Outfit(int slot) => _outfits[slot - Slots.GameSlots];

        public void SetOutfit(int slot, CustomizationDataIDs3 outfit) => _outfits[slot - Slots.GameSlots] = outfit;

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                int copied = BlueSage.ImportSlots(this);
                if (copied > 0)
                {
                    Plugin.Log.LogInfo($"Copied {copied} outfits from BlueSage QoL's style slots 4-9. " +
                        "BlueSage's own file is left as it was.");
                }
                SaveIfChanged();
                return;
            }

            try
            {
                string text = File.ReadAllText(FilePath);
                OutfitFile file = JsonConvert.DeserializeObject<OutfitFile>(text);
                if (file == null)
                {
                    return;
                }
                SelectedSlot = Clamp(file.SelectedSlot - 1, 0, Slots.Total - 1);
                GameSlot = Clamp(file.GameSlot - 1, 0, Slots.GameSlots - 1);
                foreach (SavedSlot saved in file.Slots ?? new List<SavedSlot>())
                {
                    int slot = saved != null ? saved.Slot - 1 : -1;
                    if (!Slots.IsModSlot(slot))
                    {
                        continue;
                    }
                    SetName(slot, saved.Name);
                    if (saved.Outfit != null && !IsComplete(saved.Outfit))
                    {
                        Plugin.Log.LogWarning($"Slot {slot + 1}'s outfit in {FilePath} is missing parts, so the slot starts empty.");
                        continue;
                    }
                    SetOutfit(slot, saved.Outfit);
                }
                _written = text;
            }
            catch (Exception e)
            {
                // Keep a copy, so saving doesn't wipe out the outfits we couldn't read.
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

        public void SaveIfChanged()
        {
            try
            {
                var file = new OutfitFile
                {
                    SelectedSlot = SelectedSlot + 1,
                    GameSlot = GameSlot + 1,
                };
                for (int slot = Slots.GameSlots; slot < Slots.Total; slot++)
                {
                    file.Slots.Add(new SavedSlot { Slot = slot + 1, Name = Name(slot), Outfit = Outfit(slot) });
                }
                string json = JsonConvert.SerializeObject(file, Formatting.Indented);
                if (json == _written)
                {
                    return;
                }
                File.WriteAllText(FilePath, json);
                _written = json;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't save outfit slots to {FilePath}: {e}");
            }
        }

        // Every part of the character has an entry with its colours, as the game expects.
        public static bool IsComplete(CustomizationDataIDs3 outfit)
        {
            if (outfit?.AppearanceToShapeAndColors == null || outfit.OutfitToShapeAndColors == null)
            {
                return false;
            }
            foreach (AppearanceType part in Enum.GetValues(typeof(AppearanceType)))
            {
                if (!outfit.AppearanceToShapeAndColors.TryGetValue(part, out ShapeAndColorsData data) || data.ColorIndexes == null)
                {
                    return false;
                }
            }
            foreach (OutfitType part in Enum.GetValues(typeof(OutfitType)))
            {
                if (!outfit.OutfitToShapeAndColors.TryGetValue(part, out ShapeAndColorsData data) || data.ColorIndexes == null)
                {
                    return false;
                }
            }
            return true;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    // BlueSage QoL Tweaks has its own style slots 4-9. They change the same game code as this mod, so
    // this mod stays off while they're switched on, and copies their outfits the first time it runs.
    internal static class BlueSage
    {
        private static readonly ConfigDefinition SlotsSetting = new ConfigDefinition("Avatar Styles", "EnableExtendedAvatarStyles");

        public static string SettingsFile => Path.Combine(Paths.ConfigPath, "com.bluesage.ontogether.qoltweaks.beta.cfg");

        private static string StylesFile => Path.Combine(Paths.ConfigPath, "com.bluesage.ontogether.qol-avatar-styles.cfg");

        // True when an installed BlueSage plugin has its style slots switched on. Called from Awake:
        // BlueSage binds its settings in its own Awake, and the soft dependency loads it first.
        public static bool SlotsOn()
        {
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (info?.Instance == null || info.Metadata == null ||
                    !info.Metadata.GUID.StartsWith("com.bluesage.", StringComparison.Ordinal))
                {
                    continue;
                }
                if (info.Instance.Config.TryGetEntry(SlotsSetting, out ConfigEntry<bool> entry) && entry.Value)
                {
                    return true;
                }
            }
            return false;
        }

        // Copies BlueSage's slots 4-9 (names and outfits) into the store. Returns how many had something.
        public static int ImportSlots(OutfitStore store)
        {
            if (!File.Exists(StylesFile))
            {
                return 0;
            }
            int copied = 0;
            try
            {
                // Read with BepInEx's own parser. Nothing is written back to BlueSage's file.
                var config = new ConfigFile(StylesFile, saveOnInit: false) { SaveOnConfigSet = false };
                for (int slot = Slots.GameSlots; slot < Slots.Total; slot++)
                {
                    string section = "Style " + (slot + 1);
                    string name = config.Bind(section, "Name", "").Value ?? "";
                    string payload = config.Bind(section, "Payload", "").Value ?? "";
                    CustomizationDataIDs3 outfit = ParsePayload(payload);
                    if (outfit == null && payload.Trim().Length > 0)
                    {
                        Plugin.Log.LogWarning($"BlueSage's style slot {slot + 1} couldn't be read, so only its name was copied.");
                    }
                    store.SetName(slot, name.Trim());
                    store.SetOutfit(slot, outfit);
                    if (outfit != null || name.Trim().Length > 0)
                    {
                        copied++;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't read BlueSage's style slots from {StylesFile}: {e}");
            }
            return copied;
        }

        // BlueSage's format: "BSAS1;<full body 0/1>;<appearance>;<outfit>", where each part list is
        // "part:item:colour,colour,...|..." with the part as its enum number and the item as its ID.
        private static CustomizationDataIDs3 ParsePayload(string payload)
        {
            string[] sections = payload.Trim().Split(';');
            if (sections.Length != 4 || sections[0] != "BSAS1")
            {
                return null;
            }
            var outfit = new CustomizationDataIDs3 { IsFullBody = sections[1] == "1" };
            if (!ParseParts(sections[2], outfit.AppearanceToShapeAndColors) || !ParseParts(sections[3], outfit.OutfitToShapeAndColors))
            {
                return null;
            }
            return OutfitStore.IsComplete(outfit) ? outfit : null;
        }

        private static bool ParseParts<TPart>(string section, Dictionary<TPart, ShapeAndColorsData> into) where TPart : struct, Enum
        {
            int expected = Enum.GetValues(typeof(TPart)).Length;
            var seen = new HashSet<int>();
            foreach (string entry in section.Split('|'))
            {
                string[] fields = entry.Split(':');
                if (fields.Length != 3 ||
                    !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int part) ||
                    !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int item) ||
                    part < 0 || part >= expected)
                {
                    return false;
                }
                var colours = new List<int>();
                foreach (string colour in fields[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(colour, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c))
                    {
                        return false;
                    }
                    colours.Add(c);
                }
                into[(TPart)Enum.ToObject(typeof(TPart), part)] = new ShapeAndColorsData(item, colours);
                seen.Add(part);
            }
            return seen.Count == expected;
        }
    }
}
