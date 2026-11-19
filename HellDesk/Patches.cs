using System;
using HarmonyLib;
using UnityEngine;

namespace HellDesk
{
    // While DOOM has the keyboard, tell the game "the player is typing", so its InputManager zeroes
    // every input: WASD doesn't walk you away, Space doesn't stand you up, Esc doesn't open the
    // menu and 1-4 don't change the window mode. It's the same path chat uses. Desk Swarm also stops
    // flying on it. See reference/Assembly-CSharp/InputManager.cs (Update) and TaskManager.cs.
    [HarmonyPatch(typeof(TaskManager), nameof(TaskManager.IsCancelInputs))]
    internal static class CancelInputsPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (DoomWindow.HoldsKeys)
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
            // The typing path above still reports Enter, which would open chat. Enter is DOOM's.
            if (DoomWindow.HoldsKeys)
            {
                SetEnterKeyDown(__instance, false);
                SetEnterKeyUp(__instance, false);
            }
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (DoomWindow.IsMouseOver)
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

    // Chalkboards and whiteboards read the mouse themselves, so a click on the window would paint the
    // board behind it (and use up chalk). Skip painting while the mouse is on the window, and forget
    // the last brush point so the next stroke doesn't draw a line from where the mouse left the board.
    [HarmonyPatch(typeof(QuadPainterGPU), "Update")]
    internal static class PaintPatch
    {
        private static readonly AccessTools.FieldRef<QuadPainterGPU, Vector2> PreviousUV =
            AccessTools.FieldRefAccess<QuadPainterGPU, Vector2>("previousUV");

        private static bool Prefix(QuadPainterGPU __instance)
        {
            if (__instance.IsActive && DoomWindow.IsMouseOver)
            {
                PreviousUV(__instance).x = -1f;
                return false;
            }
            return true;
        }
    }
}
