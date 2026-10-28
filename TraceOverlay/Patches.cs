using System;
using HarmonyLib;
using UnityEngine;

namespace TraceOverlay
{
    // Remember the DrawingManager when you start drawing, so the window never has to search the
    // scene for it. See reference/Assembly-CSharp/DrawingManager.cs (ActivateDrawing).
    [HarmonyPatch(typeof(DrawingManager), nameof(DrawingManager.ActivateDrawing))]
    internal static class ActivateDrawingPatch
    {
        private static void Postfix(DrawingManager __instance)
        {
            TraceWindow.Drawing = __instance;
        }
    }

    // The board paints wherever the left mouse button is held over it, reading the mouse directly
    // (QuadPainterGPU.Update), so clicks on the window would also paint the board behind it.
    // Skip painting while the mouse is on the window, and forget the last brush point so the next
    // stroke doesn't draw a line from where the mouse left the board.
    [HarmonyPatch(typeof(QuadPainterGPU), "Update")]
    internal static class PaintPatch
    {
        private static readonly AccessTools.FieldRef<QuadPainterGPU, Vector2> PreviousUV =
            AccessTools.FieldRefAccess<QuadPainterGPU, Vector2>("previousUV");

        private static bool Prefix(QuadPainterGPU __instance)
        {
            if (__instance.IsActive && TraceWindow.BlockPainting)
            {
                PreviousUV(__instance).x = -1f;
                return false;
            }
            return true;
        }
    }

    // A click on the window shouldn't also click the world behind it (players, pets).
    // Only the press is hidden, so a click that started outside still ends.
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down =
            (Action<InputManager, bool>)Delegate.CreateDelegate(typeof(Action<InputManager, bool>),
                AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Down)));

        private static void Postfix(InputManager __instance)
        {
            if (TraceWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }
    }
}
