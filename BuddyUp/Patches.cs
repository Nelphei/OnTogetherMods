using System;
using HarmonyLib;
using UnityEngine;

namespace BuddyUp
{
    // After the game reads the keys each frame. See reference/Assembly-CSharp/InputManager.cs (Update).
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down = Setter<bool>(nameof(InputManager.IsMouseButton0Down));
        private static readonly Action<InputManager, float> SetHorizontal = Setter<float>(nameof(InputManager.Horizontal));
        private static readonly Action<InputManager, float> SetVertical = Setter<float>(nameof(InputManager.Vertical));

        private static void Postfix(InputManager __instance)
        {
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (BuddyController.IsMouseOverWindow)
            {
                SetMouse0Down(__instance, false);
            }

            if (!Following.Steering)
            {
                return;
            }
            // Your own movement keys always win.
            if (Mathf.Abs(__instance.Horizontal) > 0.001f || Mathf.Abs(__instance.Vertical) > 0.001f)
            {
                Following.OwnKeys();
                return;
            }
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (tasks == null || tasks.IsCancelInputs() || tasks.IsWriting || tasks.LockState != LockState.Free)
            {
                return;
            }
            // Walking with your buddy: as if you held the movement keys this much.
            SetHorizontal(__instance, Following.Horizontal);
            SetVertical(__instance, Following.Vertical);
            if (Following.TakeJump())
            {
                __instance.IsSpaceKeyDown = true;
            }
        }

        // The properties have private setters, so bind to them once instead of reflecting every frame.
        private static Action<InputManager, T> Setter<T>(string property)
        {
            return (Action<InputManager, T>)Delegate.CreateDelegate(
                typeof(Action<InputManager, T>), AccessTools.PropertySetter(typeof(InputManager), property));
        }
    }

    // After any focus activity is picked (by you, or by another mod for you): tells your buddies, so
    // anyone walking with you can sit down beside you.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus).
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class SetFocusPatch
    {
        private static void Postfix(PlayerFocusController __instance)
        {
            try
            {
                if (__instance.isOwner && __instance.IsFocus && !Sitting.ForRide)
                {
                    BuddyController.Instance?.OnSatDown(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
