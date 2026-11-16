using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FocusAnywhere
{
    // Shows or hides the objects of the activity you're doing, following Choices (the objects window,
    // right-click the icon): the desk, placed objects, what's in your hands, headphones, the floater
    // or water bed, and the sleep Zzz.
    //
    // The game switches an activity's desk, objects and floatie on in SetFocus, and each of them
    // carries PurrNet's NetworkReflection, which sends its on/off state from its owner to everyone. So
    // switching them here shows for every player, with or without the mod. Standing up switches them
    // all off as usual. Hiding the desk also clears IsDesk, so your pet doesn't sit on the hidden desk.
    // Every player's game lifts and spins a player on a floatie only while the floatie shows
    // (PlayerAnimationController.Update), so a hidden floatie means sitting lower, without the spin.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus, Update) and PetManager.cs (UpdatePetState).
    internal static class NoObjects
    {
        internal sealed class Item
        {
            public GameObject Obj;
            public Part Part;
            public string Name;    // the game's object name, which exceptions go by
            public string Label;   // as the window shows it
        }

        // The objects the game showed for the current activity, whether they're showing now or not.
        private static readonly List<Item> Items = new List<Item>();
        private static PlayerFocusController _player;
        private static bool _captured;
        private static int _appliedVersion = -1;
        private static bool _clearedIsDesk;

        // For the window's "Right now" list. Empty while you're not focusing.
        public static IReadOnlyList<Item> Current => Items;

        // True while you're focusing (once this frame's Follow has run).
        public static bool Focusing => _captured;

        // Right after SetFocus started an activity: the game has just shown its desk and objects.
        public static void OnActivityStarted(PlayerFocusController focus)
        {
            Forget();
            if (focus.IsFocus)
            {
                Capture(focus);
                Apply(focus);
            }
        }

        // Every frame, for your own player: follows the choices while you're focusing.
        public static void Follow(PlayerFocusController focus)
        {
            if (!focus.IsFocus)
            {
                // Standing up switched everything off.
                if (_captured)
                {
                    Forget();
                }
                return;
            }
            if (!_captured || !ReferenceEquals(focus, _player))
            {
                // Already focusing when the mod loaded, or a new player object.
                Forget();
                Capture(focus);
            }
            if (_appliedVersion != Choices.Version)
            {
                Apply(focus);
            }
        }

        private static void Capture(PlayerFocusController focus)
        {
            _player = focus;
            _captured = true;
            _appliedVersion = -1;

            GameObject desk = Game.Desk(focus);
            if (desk != null && desk.activeSelf)
            {
                Add(desk, Part.Desk);
            }

            PlayerCustomizationController outfit = focus._PlayerCustomizationController;
            Dictionary<OutfitType, SkinnedMeshRenderer> renderers = outfit != null ? outfit.OutfitMeshRenderers : null;
            if (renderers != null)
            {
                foreach (OutfitType type in new[] { OutfitType.Floater, OutfitType.Bed })
                {
                    if (renderers.TryGetValue(type, out SkinnedMeshRenderer floatie) && floatie != null && floatie.gameObject.activeSelf)
                    {
                        Add(floatie.gameObject, Part.Floaties);
                    }
                }
            }

            List<FocusProps> props = Game.Props(focus);
            if (props != null)
            {
                foreach (FocusProps prop in props)
                {
                    if (prop._GameObjects == null)
                    {
                        continue;
                    }
                    foreach (GameObject obj in prop._GameObjects)
                    {
                        // Some objects belong to more than one activity (the laptop, the headphones).
                        if (obj != null && obj.activeSelf && !Items.Any(item => item.Obj == obj))
                        {
                            Add(obj, Parts.Classify(obj, focus.transform));
                        }
                    }
                }
            }

            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo("Activity objects: " + (Items.Count == 0 ? "none"
                    : string.Join(", ", Items.Select(item => $"{item.Label} ({item.Name}, {item.Part})"))) + ".");
            }
        }

        private static void Add(GameObject obj, Part part)
        {
            Items.Add(new Item
            {
                Obj = obj,
                Part = part,
                Name = obj.name,
                Label = part == Part.Desk ? "Desk" : part == Part.Floaties ? "Floatie" : Parts.Label(obj.name),
            });
        }

        // Shows and hides the captured objects as chosen. Everything shows while the mod is switched off.
        private static void Apply(PlayerFocusController focus)
        {
            _appliedVersion = Choices.Version;
            bool on = Plugin.Settings.Enabled.Value;
            bool deskHidden = false;
            int hidden = 0;
            foreach (Item item in Items)
            {
                if (item.Obj == null)
                {
                    continue;
                }
                bool show = !on || Choices.Shown(item.Part, item.Name);
                if (item.Obj.activeSelf != show)
                {
                    item.Obj.SetActive(show);
                }
                if (!show)
                {
                    hidden++;
                    deskHidden |= item.Part == Part.Desk;
                }
            }

            // Moves the pet between the desk and your head, as the game does when an activity starts.
            if (deskHidden && focus.IsDesk)
            {
                Game.SetIsDesk(focus, false);
                _clearedIsDesk = true;
                UpdatePet();
            }
            else if (!deskHidden && _clearedIsDesk)
            {
                Game.SetIsDesk(focus, true);
                _clearedIsDesk = false;
                UpdatePet();
            }

            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Activity objects: {hidden} of {Items.Count} hidden.");
            }
        }

        private static void Forget()
        {
            Items.Clear();
            _player = null;
            _captured = false;
            _clearedIsDesk = false;
            _appliedVersion = -1;
        }

        private static void UpdatePet()
        {
            PetManager pets = NetworkSingleton<PetManager>.I;
            if (pets != null)
            {
                pets.UpdatePetState();
            }
        }
    }

    // Runs before other mods' SetFocus postfixes (Desk Snap checks IsDesk there), so they see the
    // activity as it'll stay, but after this mod's floater-on-land postfix (SetFocusPatch), so a
    // floater brought out on land is counted too. __state: when the last activity started, to tell a
    // new one from a pick the game turned down (a taken seat), which changes nothing.
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class NoObjectsSetFocusPatch
    {
        private static void Prefix(PlayerFocusController __instance, out DateTime __state)
        {
            __state = Game.FocusStart(__instance);
        }

        [HarmonyPriority(Priority.First)]
        private static void Postfix(PlayerFocusController __instance, DateTime __state)
        {
            try
            {
                if (__instance.isOwner && Game.FocusStart(__instance) != __state)
                {
                    NoObjects.OnActivityStarted(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
