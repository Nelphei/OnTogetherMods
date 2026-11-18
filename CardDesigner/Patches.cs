using System;
using HarmonyLib;
using UnityEngine;

namespace CardDesigner
{
    // Remember the customization screen (it has the game's ID card page) when it starts, in both
    // scenes, so nothing has to search the scene for it.
    [HarmonyPatch(typeof(CustomizationUIController), "Start")]
    internal static class CustomizationStartPatch
    {
        private static void Postfix(CustomizationUIController __instance)
        {
            Game.Customization = __instance;
        }
    }

    // The customization screen reads Esc straight from the keyboard (CustomizationUIController.Update)
    // and goes back from the ID card page. Esc while typing in the window only stops the typing.
    [HarmonyPatch(typeof(CustomizationUIController), "Update")]
    internal static class CustomizationUpdatePatch
    {
        private static bool Prefix()
        {
            return !(CardWindow.HoldsKeys && Input.GetKeyDown(Patches.EscapeKey()));
        }
    }

    // The main menu reads Esc and Enter straight from the keyboard (MainMenuUIController.Update), so
    // Esc would leave the session list while you type in the window. Skip it while typing.
    [HarmonyPatch(typeof(MainMenuUIController), "Update")]
    internal static class MainMenuUpdatePatch
    {
        private static bool Prefix()
        {
            return !CardWindow.HoldsKeys;
        }
    }

    // While you type in the window, tell the game "the player is typing", so its InputManager zeroes
    // every input and WASD doesn't walk you away. It's the same path chat uses.
    // See reference/Assembly-CSharp/InputManager.cs (Update) and TaskManager.cs (IsCancelInputs).
    [HarmonyPatch(typeof(TaskManager), nameof(TaskManager.IsCancelInputs))]
    internal static class CancelInputsPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (CardWindow.HoldsKeys)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetEnterKeyDown = Patches.Setter(nameof(InputManager.EnterKeyDown));
        private static readonly Action<InputManager, bool> SetEnterKeyUp = Patches.Setter(nameof(InputManager.EnterKeyUp));
        private static readonly Action<InputManager, bool> SetMouse0Down = Patches.Setter(nameof(InputManager.IsMouseButton0Down));

        private static void Postfix(InputManager __instance)
        {
            // The typing path above still reports Enter, which would open chat. In the window Enter
            // starts a new line.
            if (CardWindow.HoldsKeys)
            {
                SetEnterKeyDown(__instance, false);
                SetEnterKeyUp(__instance, false);
            }
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (CardWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }
    }

    internal static class Patches
    {
        // The game's Esc key (it can be changed in its settings).
        public static KeyCode EscapeKey()
        {
            try
            {
                DataManager data = MonoSingleton<DataManager>.I;
                return data != null ? data.SettingsData.Keys[KeyType.Escape].Item1 : KeyCode.Escape;
            }
            catch (Exception)
            {
                return KeyCode.Escape;
            }
        }

        // The properties have private setters, so bind to them once instead of reflecting every frame.
        public static Action<InputManager, bool> Setter(string property)
        {
            return (Action<InputManager, bool>)Delegate.CreateDelegate(
                typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), property));
        }
    }
}
