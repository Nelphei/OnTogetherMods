using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OnTogetherMods.Shared;

namespace OutfitSlots
{
    // The outfit command in Bubble Shell's console, if it's installed (see Shared/ShellLink.cs).
    internal static class ShellCommands
    {
        public static void Register()
        {
            ShellLink.Add("outfit", Plugin.PluginName, "outfit [slot | next | copy <from> <to>]",
                "outfit lists your outfit slots. outfit 4 (or a slot's name) puts that outfit on, and outfit next " +
                "the next one you've used. outfit copy 2 5 copies the outfit in slot 2 into slot 5, so you can " +
                "make a variation of it.",
                Run, Complete);
        }

        private static string Run(string[] args)
        {
            if (!Slots.Active)
            {
                return "! Outfit Slots is off (BlueSage QoL's own style slots are on, or the game changed).";
            }
            DataManager data = Game.Data;
            PlayerDataZip zip = data != null ? data.PlayerDataZip : null;
            if (zip == null || data.CustomizationData == null)
            {
                return "! Your save hasn't loaded yet.";
            }
            Slots.EnsureNames(zip);

            if (args.Length == 0 || Is(args[0], "list"))
            {
                return List(zip);
            }
            if (Is(args[0], "next"))
            {
                return Said(Swap.Wear(Swap.NextSlot()));
            }
            if (Is(args[0], "copy"))
            {
                return Copy(data, zip, args);
            }
            string typed = string.Join(" ", args);
            if (!TrySlot(zip, typed, out int slot, out string why))
            {
                return "! " + why;
            }
            if (Slots.IsEmpty(slot))
            {
                return $"! Slot {slot + 1} is empty. Pick it in the outfit screen once to start it.";
            }
            return Said(Swap.Wear(slot));
        }

        private static string List(PlayerDataZip zip)
        {
            var text = new StringBuilder("Your outfits:");
            for (int slot = 0; slot < Slots.Total; slot++)
            {
                string name = Slots.Name(zip, slot).Trim();
                string note = slot == zip.SelectedStyleIndex ? "  (wearing)" : Slots.IsEmpty(slot) ? "  (empty)" : "";
                text.Append('\n').Append($"  {slot + 1}  {(name.Length > 0 ? name : "-")}{note}");
            }
            text.Append("\n~ outfit 4 puts slot 4 on, outfit copy 2 5 copies slot 2 into slot 5.");
            return text.ToString();
        }

        // outfit copy <from> <to>
        private static string Copy(DataManager data, PlayerDataZip zip, string[] args)
        {
            if (args.Length != 3)
            {
                return "! Usage: outfit copy <from> <to>, like outfit copy 2 5.";
            }
            if (!TrySlot(zip, args[1], out int from, out string why) || !TrySlot(zip, args[2], out int to, out why))
            {
                return "! " + why;
            }
            if (from == to)
            {
                return "! That's the same slot.";
            }
            if (Game.ScreenOpen)
            {
                return "! Close the outfit screen first.";
            }
            CustomizationDataIDs3 source = from == zip.SelectedStyleIndex
                ? new CustomizationDataIDs3(data.CustomizationData)   // what you have on right now
                : OutfitIn(zip, from);
            if (source == null)
            {
                return $"! Slot {from + 1} is empty.";
            }

            // A copy of its own, so changing one slot later doesn't change the other.
            var copy = new CustomizationDataIDs3(new CustomizationData(source));
            if (Slots.IsModSlot(to))
            {
                Slots.SetOutfitFor(zip, to, copy);
            }
            else
            {
                SetGameOutfit(zip, to, copy);
                data.SavePlayerZipData();
            }
            if (to == zip.SelectedStyleIndex)
            {
                Swap.PutOnAgain();
            }
            return $"Copied {Slots.Label(zip, from)} into {Slots.Label(zip, to)}." +
                   (to == zip.SelectedStyleIndex ? " You're wearing it now." : "");
        }

        // The saved outfit in a slot, or null for an empty one.
        private static CustomizationDataIDs3 OutfitIn(PlayerDataZip zip, int slot)
        {
            switch (slot)
            {
                case 0: return zip.CustomizationDataIDsDict;
                case 1: return zip.CustomizationDataIDs1Dict;
                case 2: return zip.CustomizationDataIDs2Dict;
                default: return Slots.Store.Outfit(slot);
            }
        }

        private static void SetGameOutfit(PlayerDataZip zip, int slot, CustomizationDataIDs3 outfit)
        {
            switch (slot)
            {
                case 0: zip.CustomizationDataIDsDict = outfit; break;
                case 1: zip.CustomizationDataIDs1Dict = outfit; break;
                case 2: zip.CustomizationDataIDs2Dict = outfit; break;
            }
        }

        // A slot by its number (1-9) or name. Slots count from 0 inside the mod.
        private static bool TrySlot(PlayerDataZip zip, string typed, out int slot, out string why)
        {
            why = null;
            if (int.TryParse(typed, out int number))
            {
                slot = number - 1;
                if (slot >= 0 && slot < Slots.Total)
                {
                    return true;
                }
                why = $"Pick a slot from 1 to {Slots.Total}.";
                return false;
            }
            var names = Enumerable.Range(0, Slots.Total).Select(i => (Slot: i, Name: Slots.Name(zip, i).Trim())).Where(n => n.Name.Length > 0).ToList();
            foreach (Func<string, bool> rule in new Func<string, bool>[]
                     {
                         n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase),
                         n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                     })
            {
                var found = names.Where(n => rule(n.Name)).ToList();
                if (found.Count == 1)
                {
                    slot = found[0].Slot;
                    return true;
                }
                if (found.Count > 1)
                {
                    slot = -1;
                    why = $"{found.Count} outfits are called {typed}. Use the slot's number.";
                    return false;
                }
            }
            slot = -1;
            why = $"No outfit is called {typed}. outfit lists them.";
            return false;
        }

        // Swap.Wear's message, as a warning unless it worked.
        private static string Said(string message)
        {
            return message.StartsWith("Wearing", StringComparison.Ordinal) ? message + "." : "! " + message + ".";
        }

        private static bool Is(string word, string expected) => string.Equals(word, expected, StringComparison.OrdinalIgnoreCase);

        private static string[] Complete(string[] args, int index)
        {
            if (index == 0)
            {
                return new[] { "next", "copy", "list" }.Concat(Enumerable.Range(1, Slots.Total).Select(n => n.ToString())).ToArray();
            }
            if (index <= 2 && args.Length > 0 && Is(args[0], "copy"))
            {
                return Enumerable.Range(1, Slots.Total).Select(n => n.ToString()).ToArray();
            }
            return null;
        }
    }
}
