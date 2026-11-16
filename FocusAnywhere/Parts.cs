using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FocusAnywhere
{
    // The groups the objects window switches. Each activity object belongs to one, worked out from
    // where it sits in the player (read from the Player prefab, 2026-11-16):
    // - held objects are children of the hand bones (mixamorig:LeftHand / RightHand), the jump rope of a
    //   rope bone, all under the skeleton (AvatarArmature), so they move with the animation;
    // - the headphones hang off the head bone (mixamorig:Head);
    // - placed objects (laptop, tablet, easel, ironing board, treadmill...) sit under the model
    //   (MD_AvatarDefault), the player itself, or the floater or water bed, but not on the skeleton;
    // - the sleep Zzz are particle effects.
    // The desk (PlayerFocusController._desk) and the floater and water bed (outfit parts that
    // SetFocus switches on) are groups of their own.
    internal enum Part
    {
        Desk,
        Placed,
        Hands,
        Headphones,
        Floaties,
        Effects,
    }

    // Where an object sits, for the window's list of objects to add to an activity.
    internal enum Spot
    {
        LeftHand,
        RightHand,
        Head,
        SetDown,
        Other,   // the jump rope and the sleep Zzz
    }

    internal static class Parts
    {
        public static readonly Part[] All =
        {
            Part.Desk, Part.Placed, Part.Hands, Part.Headphones, Part.Floaties, Part.Effects,
        };

        public static readonly Spot[] Spots =
        {
            Spot.LeftHand, Spot.RightHand, Spot.Head, Spot.SetDown, Spot.Other,
        };

        private const string Skeleton = "AvatarArmature";
        private const string HeadBone = "mixamorig:Head";
        private const string LeftHandBone = "mixamorig:LeftHand";
        private const string RightHandBone = "mixamorig:RightHand";

        // Objects that can't be added to another activity. The easel only paints when the game's own
        // painting activity starts it.
        private static readonly HashSet<string> NotAddable = new HashSet<string> { "MD_Easle" };

        public static bool CanAdd(string objectName) => !NotAddable.Contains(objectName);

        // Which hand holds it, or whether it's on the head or set down.
        public static Spot SpotOf(GameObject obj, Transform player)
        {
            if (obj.GetComponent<ParticleSystem>() != null)
            {
                return Spot.Other;
            }
            for (Transform t = obj.transform.parent; t != null && t != player; t = t.parent)
            {
                switch (t.name)
                {
                    case LeftHandBone: return Spot.LeftHand;
                    case RightHandBone: return Spot.RightHand;
                    case HeadBone: return Spot.Head;
                    case Skeleton: return Spot.Other;
                }
            }
            return Spot.SetDown;
        }

        public static string SpotName(Spot spot)
        {
            switch (spot)
            {
                case Spot.LeftHand: return "LEFT HAND";
                case Spot.RightHand: return "RIGHT HAND";
                case Spot.Head: return "HEAD";
                case Spot.SetDown: return "SET DOWN";
                default: return "OTHER";
            }
        }

        // An object's name under its hand's heading, without the "Left" or "Right" the heading says.
        public static string ShortLabel(string label)
        {
            foreach (string side in new[] { "Left ", "Right " })
            {
                if (label.StartsWith(side) && label.Length > side.Length)
                {
                    string rest = label.Substring(side.Length);
                    return char.ToUpperInvariant(rest[0]) + rest.Substring(1);
                }
            }
            foreach (string side in new[] { " (left)", " (right)" })
            {
                if (label.EndsWith(side))
                {
                    return label.Substring(0, label.Length - side.Length);
                }
            }
            return label;
        }

        // An activity object's group. If a game update renames the bones, objects fall back to Placed.
        public static Part Classify(GameObject obj, Transform player)
        {
            if (obj.GetComponent<ParticleSystem>() != null)
            {
                return Part.Effects;
            }
            for (Transform t = obj.transform.parent; t != null && t != player; t = t.parent)
            {
                // The head bone comes before the skeleton's root on the way up.
                if (t.name == HeadBone)
                {
                    return Part.Headphones;
                }
                if (t.name == Skeleton)
                {
                    return Part.Hands;
                }
            }
            return Part.Placed;
        }

        // The group's name in the window, and a few examples after it.
        public static string Name(Part part)
        {
            switch (part)
            {
                case Part.Desk: return "Desk";
                case Part.Placed: return "Placed objects";
                case Part.Hands: return "In your hands";
                case Part.Headphones: return "Headphones";
                case Part.Floaties: return "Floater and water bed";
                default: return "Sleep Zzz";
            }
        }

        public static string Help(Part part)
        {
            switch (part)
            {
                case Part.Placed: return "laptop, easel, treadmill";
                case Part.Hands: return "book, phone, pen";
                case Part.Floaties: return "you sit lower";
                default: return "";
            }
        }

        // Short, for an object's row in the "Right now" list.
        public static string Short(Part part)
        {
            switch (part)
            {
                case Part.Placed: return "placed";
                case Part.Hands: return "in hand";
                case Part.Headphones: return "headphones";
                default: return "";
            }
        }

        // The game's object names (from the Player prefab) as the window shows them. Where the game has
        // more than one of a thing (a laptop for the desk, one for the ground...), the name says which.
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "MD_Book", "Book" },
            { "MD_Laptop", "Laptop (desk)" },
            { "MD_Tablet", "Tablet" },
            { "MD_TabletPen", "Tablet pen" },
            { "MD_Console", "Game controller" },
            { "MD_ConsoleDeck", "Handheld console" },
            { "MD_Latte", "Latte" },
            { "MD_Chopstick", "Chopsticks" },
            { "MD_RamenBowl", "Ramen bowl" },
            { "MD_Dumbbell_L", "Left dumbbell" },
            { "MD_Dumbbell_R", "Right dumbbell" },
            { "MD_Headphone", "Headphones" },
            { "MD_Notebook", "Notebook" },
            { "MD_Pencil", "Pencil" },
            { "MD_Rope", "Jump rope" },
            { "MD_Handle_L", "Left rope handle" },
            { "MD_Handle_R", "Right rope handle" },
            { "MD_PaintBrush", "Paintbrush" },
            { "MD_Easle", "Easel" },
            { "MD_WatercolorBrush", "Watercolour brush" },
            { "MD_WatercolorSetup", "Watercolour set" },
            { "MD_Drawing0Pencil", "Drawing pencil" },
            { "MD_DrawingMiniCanvas", "Sketchpad" },
            { "MD_Knit_L", "Left knitting needle" },
            { "MD_Knit_R", "Right knitting needle" },
            { "MD_Crochet_L", "Crochet work" },
            { "MD_CrochetNeedle_R", "Crochet hook" },
            { "MD_Reader", "E-reader" },
            { "MD_Manga", "Manga" },
            { "MD_Dactilo", "Typewriter" },
            { "MD_Write_NotePAd", "Notepad" },
            { "MD_Write_Tablet", "Tablet (writing)" },
            { "MD_Sweep", "Broom" },
            { "MD_VacuumCleaner", "Vacuum cleaner" },
            { "MD_IroinTable", "Ironing board" },
            { "MD_Iron", "Iron" },
            { "MD_Marshmallow", "Marshmallow stick" },
            { "MD_CharSheet", "Character sheet" },
            { "MD_CharSheetL", "Character sheet 2" },
            { "MD_Treadmill", "Treadmill" },
            { "MD_Cellphone", "Phone" },
            { "MD_CellphoneBrowse", "Phone (browsing)" },
            { "MD_LaptopGround", "Laptop (ground)" },
            { "fx_Sleep", "Zzz" },
            { "fx_SleepDesk", "Zzz (desk)" },
            { "MD_BedLaptop", "Laptop (water bed)" },
            { "MD_FloaterLaptop", "Laptop (floater)" },
            { "MD_WaterBook", "Book (floating)" },
            { "MD_WaterManga", "Manga (floating)" },
            { "MD_WaterReader", "E-reader (floating)" },
            { "MD_WaterPhone", "Phone (floating)" },
            { "MD_Coconut", "Coconut" },
            { "MD_WaterPencil", "Pencil (floating)" },
            { "MD_WaterNotebook", "Notebook (water bed)" },
            { "MD_Watermelon", "Watermelon" },
            { "MD_GroundLaptop_S", "Laptop (ground, low)" },
            { "MD_Bento", "Bento box" },
            { "MD_Onigiri", "Onigiri" },
            { "MD_LaptopBrowsingDesk", "Laptop (browsing)" },
        };

        // An object's name in the window. Objects a game update adds get a tidied-up version of their
        // own name ("MD_SomeThing_L" becomes "Some thing (left)").
        public static string Label(string objectName)
        {
            if (Labels.TryGetValue(objectName, out string label))
            {
                return label;
            }
            string name = objectName;
            if (name.StartsWith("MD_") || name.StartsWith("fx_"))
            {
                name = name.Substring(3);
            }
            string side = "";
            if (name.EndsWith("_L"))
            {
                side = " (left)";
                name = name.Substring(0, name.Length - 2);
            }
            else if (name.EndsWith("_R"))
            {
                side = " (right)";
                name = name.Substring(0, name.Length - 2);
            }
            var text = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_')
                {
                    text.Append(' ');
                    continue;
                }
                bool wordStart = i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]);
                if (wordStart)
                {
                    text.Append(' ');
                }
                text.Append(text.Length == 0 ? c : wordStart ? char.ToLowerInvariant(c) : c);
            }
            string result = text.ToString().Trim();
            return (result.Length > 0 ? result : objectName) + side;
        }
    }
}
