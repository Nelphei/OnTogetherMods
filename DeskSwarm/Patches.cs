using System;
using HarmonyLib;
using UnityEngine;

namespace DeskSwarm
{
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down = Setter(nameof(InputManager.IsMouseButton0Down));
        private static readonly Action<InputManager, bool> SetSpaceDown = Setter(nameof(InputManager.IsSpaceKeyDown));

        private static void Postfix(InputManager __instance)
        {
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (SwarmController.IsMouseOverWindow)
            {
                SetMouse0Down(__instance, false);
            }

            // While you fly, the stand-up key (Space) flies you up instead, so it mustn't stand you
            // up too. Menus still get it (Space closes the focus menu).
            if (Flight.IsPiloting && __instance.IsSpaceKeyDown)
            {
                KeyCode standUp = Game.StandUpKey();
                Settings s = Plugin.Settings;
                if ((s.UpKey.Value == standUp || s.DownKey.Value == standUp) &&
                    MonoSingleton<TaskManager>.I.LockState == LockState.Free)
                {
                    SetSpaceDown(__instance, false);
                }
            }
        }

        // The properties have private setters, so bind to them once instead of reflecting every frame.
        private static Action<InputManager, bool> Setter(string property)
        {
            return (Action<InputManager, bool>)Delegate.CreateDelegate(
                typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), property));
        }
    }

    // After any focus activity is picked, including a switch while flying.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus).
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class SetFocusPatch
    {
        private static void Postfix(PlayerFocusController __instance)
        {
            try
            {
                if (__instance.isOwner)
                {
                    SwarmController.Instance?.OnFocusPicked();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
