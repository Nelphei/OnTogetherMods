using System;
using HarmonyLib;

namespace NameDesigner
{
    // Remember the main menu and the customization screen when they start, so the window never
    // has to search the scene for them.
    [HarmonyPatch(typeof(MainMenuUIController), "Start")]
    internal static class MainMenuStartPatch
    {
        private static void Postfix(MainMenuUIController __instance)
        {
            Game.Menu = __instance;
        }
    }

    [HarmonyPatch(typeof(CustomizationUIController), "Start")]
    internal static class CustomizationStartPatch
    {
        private static void Postfix(CustomizationUIController __instance)
        {
            Game.Customization = __instance;
        }
    }

    // The main menu reads Esc and Enter straight from the keyboard (MainMenuUIController.Update),
    // so Esc would leave the session list while you type in the window. Skip it while typing.
    [HarmonyPatch(typeof(MainMenuUIController), "Update")]
    internal static class MainMenuUpdatePatch
    {
        private static bool Prefix()
        {
            return !DesignerWindow.IsTyping;
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
            if (DesignerWindow.IsTyping)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetEnterKeyDown = Setter(nameof(InputManager.EnterKeyDown));
        private static readonly Action<InputManager, bool> SetEnterKeyUp = Setter(nameof(InputManager.EnterKeyUp));
        private static readonly Action<InputManager, bool> SetMouse0Down = Setter(nameof(InputManager.IsMouseButton0Down));

        private static void Postfix(InputManager __instance)
        {
            // The typing path above still reports Enter, which would open chat.
            if (DesignerWindow.IsTyping)
            {
                SetEnterKeyDown(__instance, false);
                SetEnterKeyUp(__instance, false);
            }
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (DesignerWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }

        // The properties have private setters, so bind to them once instead of reflecting every frame.
        private static Action<InputManager, bool> Setter(string property)
        {
            return (Action<InputManager, bool>)Delegate.CreateDelegate(
                typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), property));
        }
    }
}
