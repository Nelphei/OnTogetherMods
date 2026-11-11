using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FocusAnywhere
{
    // "Objects off" (HideObjects, right-click the icon): focus activities without their desk or
    // objects (laptop, book, easel, treadmill...), so the pose plays on its own. Effects like the
    // sleep Zzz stay.
    //
    // The game switches the desk and each object on in SetFocus, and each of them carries PurrNet's
    // NetworkReflection, which sends its on/off state from its owner to everyone. So switching them
    // off here shows for every player, with or without the mod. Standing up switches them all off as
    // usual. IsDesk is cleared too, so your pet doesn't sit on the hidden desk.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus, Update) and PetManager.cs (UpdatePetState).
    internal static class NoObjects
    {
        // What this mod switched off for the current activity, to bring back when the switch goes off.
        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static GameObject _hiddenDesk;
        private static bool _clearedIsDesk;
        private static PlayerFocusController _player;
        private static bool _applied;   // objects are off for the current activity

        public static bool Wanted => Plugin.Settings.Enabled.Value && Plugin.Settings.HideObjects.Value;

        // Right after SetFocus started an activity: the game has just shown its desk and objects.
        public static void OnActivityStarted(PlayerFocusController focus)
        {
            Forget();
            if (Wanted && focus.IsFocus)
            {
                Hide(focus);
            }
        }

        // Every frame, for your own player: follows the switch while you're focusing.
        public static void Follow(PlayerFocusController focus)
        {
            if (!focus.IsFocus || !ReferenceEquals(focus, _player) && _applied)
            {
                // Standing up switched everything off (or this is a new player object).
                Forget();
                if (!focus.IsFocus)
                {
                    return;
                }
            }
            bool wanted = Wanted;
            if (wanted && !_applied)
            {
                Hide(focus);
            }
            else if (!wanted && _applied)
            {
                Show(focus);
            }
        }

        private static void Hide(PlayerFocusController focus)
        {
            _player = focus;
            _applied = true;

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
                        if (obj != null && obj.activeSelf && !IsEffect(obj) && !Hidden.Contains(obj))
                        {
                            obj.SetActive(false);
                            Hidden.Add(obj);
                        }
                    }
                }
            }

            GameObject desk = Game.Desk(focus);
            if (desk != null && desk.activeSelf)
            {
                desk.SetActive(false);
                _hiddenDesk = desk;
            }
            if (focus.IsDesk)
            {
                Game.SetIsDesk(focus, false);
                _clearedIsDesk = true;
                UpdatePet();
            }

            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Objects off: hid {Hidden.Count} objects{(_hiddenDesk != null ? " and the desk" : "")}.");
            }
        }

        private static void Show(PlayerFocusController focus)
        {
            foreach (GameObject obj in Hidden)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
            if (_hiddenDesk != null)
            {
                _hiddenDesk.SetActive(true);
            }
            if (_clearedIsDesk)
            {
                Game.SetIsDesk(focus, true);
                UpdatePet();
            }
            Forget();
        }

        private static void Forget()
        {
            Hidden.Clear();
            _hiddenDesk = null;
            _clearedIsDesk = false;
            _player = null;
            _applied = false;
        }

        // The sleep Zzz are particle effects, not objects. They stay.
        private static bool IsEffect(GameObject obj)
        {
            return obj.GetComponent<ParticleSystem>() != null;
        }

        // Moves the pet between the desk and your head, as the game does when an activity starts.
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
    // activity as it'll stay. __state: when the last activity started, to tell a new one from a pick
    // the game turned down (a taken seat), which changes nothing.
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
