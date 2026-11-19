using System;
using HarmonyLib;
using UnityEngine;

namespace CardTable
{
    // A click on the window shouldn't also click the world behind it (pets, players, the fishing
    // rod). Only the press is hidden, so a click that started outside still ends.
    // See reference/Assembly-CSharp/InputManager.cs.
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down = Setter(nameof(InputManager.IsMouseButton0Down));

        private static void Postfix(InputManager __instance)
        {
            if (TableWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }

        // The property has a private setter, so bind to it once instead of reflecting every frame.
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
            if (__instance.IsActive && TableWindow.IsMouseOver)
            {
                PreviousUV(__instance).x = -1f;
                return false;
            }
            return true;
        }
    }
}
