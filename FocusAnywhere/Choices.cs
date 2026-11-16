using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace FocusAnywhere
{
    // What shows while you focus: one switch per group (Part), exceptions for single objects, and
    // objects added to single activities, all kept in the settings file ([Objects]). The objects
    // window changes them and NoObjects follows.
    internal static class Choices
    {
        // The three quick setups at the top of the window. Each also clears the exceptions (but not
        // the added objects, which belong to one activity each).
        public enum Preset { All, HandsOnly, None }

        private static Settings S => Plugin.Settings;

        // Object name -> shown. Only objects that don't follow their group.
        private static readonly Dictionary<string, bool> ExceptionList = new Dictionary<string, bool>();

        // Activity ("Read/12", see NoObjects.ActivityKey) -> object names added to it, in the order added.
        private static readonly Dictionary<string, List<string>> AddedLists = new Dictionary<string, List<string>>();

        private static readonly IReadOnlyList<string> NoneAdded = new List<string>();

        // Goes up whenever something that changes what shows changes, so NoObjects knows to look again.
        public static int Version { get; private set; }

        private static bool _saving;

        public static void Init()
        {
            Load();
            LoadAdded();
            foreach (Part part in Parts.All)
            {
                Entry(part).SettingChanged += Changed;
            }
            S.Exceptions.SettingChanged += (sender, args) =>
            {
                if (!_saving)
                {
                    Load();
                }
                Version++;
            };
            S.Added.SettingChanged += (sender, args) =>
            {
                if (!_saving)
                {
                    LoadAdded();
                }
                Version++;
            };
            S.Enabled.SettingChanged += Changed;
        }

        // ---- Objects added to an activity ----

        public static IReadOnlyList<string> Added(string activity)
        {
            return activity != null && AddedLists.TryGetValue(activity, out List<string> names) ? names : NoneAdded;
        }

        public static bool IsAdded(string activity, string objectName)
        {
            return activity != null && AddedLists.TryGetValue(activity, out List<string> names) && names.Contains(objectName);
        }

        public static void SetAdded(string activity, string objectName, bool added)
        {
            if (activity == null || IsAdded(activity, objectName) == added)
            {
                return;
            }
            if (added)
            {
                if (!AddedLists.TryGetValue(activity, out List<string> names))
                {
                    names = new List<string>();
                    AddedLists[activity] = names;
                }
                names.Add(objectName);
            }
            else
            {
                List<string> names = AddedLists[activity];
                names.Remove(objectName);
                if (names.Count == 0)
                {
                    AddedLists.Remove(activity);
                }
            }
            SaveAdded();
        }

        // "Read/12: MD_Latte, MD_Headphone; Write/30: fx_Sleep"
        private static void LoadAdded()
        {
            AddedLists.Clear();
            foreach (string entry in (S.Added.Value ?? "").Split(';'))
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                string activity = entry.Substring(0, colon).Trim();
                List<string> names = entry.Substring(colon + 1).Split(',')
                    .Select(name => name.Trim())
                    .Where(name => name.Length > 0)
                    .Distinct()
                    .ToList();
                if (activity.Length > 0 && names.Count > 0)
                {
                    AddedLists[activity] = names;
                }
            }
        }

        private static void SaveAdded()
        {
            _saving = true;
            try
            {
                S.Added.Value = string.Join("; ", AddedLists
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}: {string.Join(", ", pair.Value)}"));
            }
            finally
            {
                _saving = false;
            }
            Version++;
        }

        private static void Changed(object sender, EventArgs args) => Version++;

        public static bool GroupShown(Part part) => Entry(part).Value;

        public static void SetGroup(Part part, bool shown)
        {
            Entry(part).Value = shown;
        }

        // Whether this object shows: its exception if it has one, otherwise its group's switch.
        public static bool Shown(Part part, string objectName)
        {
            if (HasExceptions(part) && ExceptionList.TryGetValue(objectName, out bool shown))
            {
                return shown;
            }
            return GroupShown(part);
        }

        // null when the object follows its group.
        public static bool? Exception(Part part, string objectName)
        {
            return HasExceptions(part) && ExceptionList.TryGetValue(objectName, out bool shown) ? shown : (bool?)null;
        }

        // Ticking an object in the "Right now" list. Back in line with its group, it stops being an exception.
        public static void SetObject(Part part, string objectName, bool shown)
        {
            if (shown == GroupShown(part))
            {
                ExceptionList.Remove(objectName);
            }
            else
            {
                ExceptionList[objectName] = shown;
            }
            Save();
        }

        public static void ClearException(string objectName)
        {
            if (ExceptionList.Remove(objectName))
            {
                Save();
            }
        }

        public static void Apply(Preset preset)
        {
            foreach (Part part in Parts.All)
            {
                SetGroup(part, PresetShows(preset, part));
            }
            if (ExceptionList.Count > 0)
            {
                ExceptionList.Clear();
                Save();
            }
        }

        // The preset the switches match right now, if any.
        public static Preset? Current()
        {
            if (ExceptionList.Count > 0)
            {
                return null;
            }
            foreach (Preset preset in new[] { Preset.All, Preset.HandsOnly, Preset.None })
            {
                if (Parts.All.All(part => GroupShown(part) == PresetShows(preset, part)))
                {
                    return preset;
                }
            }
            return null;
        }

        private static bool PresetShows(Preset preset, Part part)
        {
            switch (preset)
            {
                case Preset.All:
                    return true;
                case Preset.HandsOnly:
                    return part == Part.Hands || part == Part.Headphones || part == Part.Effects;
                default:
                    return false;
            }
        }

        // Only single activity objects can be exceptions. The desk, floaties and Zzz have their own switch.
        private static bool HasExceptions(Part part) => part == Part.Placed || part == Part.Hands || part == Part.Headphones;

        public static string Summary()
        {
            string groups = string.Join(", ", Parts.All.Select(part => $"{part} {(GroupShown(part) ? "shown" : "hidden")}"));
            if (ExceptionList.Count > 0)
            {
                groups += $", {ExceptionList.Count} exception(s)";
            }
            if (AddedLists.Count > 0)
            {
                groups += $", objects added to {AddedLists.Count} activit{(AddedLists.Count == 1 ? "y" : "ies")}";
            }
            return groups;
        }

        private static ConfigEntry<bool> Entry(Part part)
        {
            switch (part)
            {
                case Part.Desk: return S.ShowDesk;
                case Part.Placed: return S.ShowPlaced;
                case Part.Hands: return S.ShowHands;
                case Part.Headphones: return S.ShowHeadphones;
                case Part.Floaties: return S.ShowFloaties;
                default: return S.ShowEffects;
            }
        }

        // "MD_IroinTable:show, MD_Iron:hide"
        private static void Load()
        {
            ExceptionList.Clear();
            foreach (string item in (S.Exceptions.Value ?? "").Split(','))
            {
                int colon = item.LastIndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                string name = item.Substring(0, colon).Trim();
                string value = item.Substring(colon + 1).Trim().ToLowerInvariant();
                if (name.Length > 0 && (value == "show" || value == "hide"))
                {
                    ExceptionList[name] = value == "show";
                }
            }
        }

        private static void Save()
        {
            _saving = true;
            try
            {
                S.Exceptions.Value = string.Join(", ", ExceptionList
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}:{(pair.Value ? "show" : "hide")}"));
            }
            finally
            {
                _saving = false;
            }
            Version++;
        }
    }
}
