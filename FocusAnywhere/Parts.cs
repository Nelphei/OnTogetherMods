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

    internal static class Parts
    {
        public static readonly Part[] All =
        {
            Part.Desk, Part.Placed, Part.Hands, Part.Headphones, Part.Floaties, Part.Effects,
        };

        private const string Skeleton = "AvatarArmature";
        private const string HeadBone = "mixamorig:Head";

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

        // The game's object names (from the Player prefab) as the window shows them.
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "MD_Book", "Book" },
            { "MD_Laptop", "Laptop" },
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
            { "MD_Drawing0Pencil", "Pencil" },
            { "MD_DrawingMiniCanvas", "Sketchpad" },
            { "MD_Knit_L", "Left knitting needle" },
            { "MD_Knit_R", "Right knitting needle" },
            { "MD_Crochet_L", "Crochet work" },
            { "MD_CrochetNeedle_R", "Crochet hook" },
            { "MD_Reader", "E-reader" },
            { "MD_Manga", "Manga" },
            { "MD_Dactilo", "Typewriter" },
            { "MD_Write_NotePAd", "Notepad" },
            { "MD_Write_Tablet", "Tablet" },
            { "MD_Sweep", "Broom" },
            { "MD_VacuumCleaner", "Vacuum cleaner" },
            { "MD_IroinTable", "Ironing board" },
            { "MD_Iron", "Iron" },
            { "MD_Marshmallow", "Marshmallow stick" },
            { "MD_CharSheet", "Character sheet" },
            { "MD_CharSheetL", "Character sheet" },
            { "MD_Treadmill", "Treadmill" },
            { "MD_Cellphone", "Phone" },
            { "MD_CellphoneBrowse", "Phone" },
            { "MD_LaptopGround", "Laptop" },
            { "fx_Sleep", "Zzz" },
            { "fx_SleepDesk", "Zzz" },
            { "MD_BedLaptop", "Laptop" },
            { "MD_FloaterLaptop", "Laptop" },
            { "MD_WaterBook", "Book" },
            { "MD_WaterManga", "Manga" },
            { "MD_WaterReader", "E-reader" },
            { "MD_WaterPhone", "Phone" },
            { "MD_Coconut", "Coconut" },
            { "MD_WaterPencil", "Pencil" },
            { "MD_WaterNotebook", "Notebook" },
            { "MD_Watermelon", "Watermelon" },
            { "MD_GroundLaptop_S", "Laptop" },
            { "MD_Bento", "Bento box" },
            { "MD_Onigiri", "Onigiri" },
            { "MD_LaptopBrowsingDesk", "Laptop" },
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
