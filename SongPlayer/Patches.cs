using System;
using HarmonyLib;

namespace SongPlayer
{
    // Remember the MusicManager when you sit at an instrument, so the window never has to search
    // the scene for it. See reference/Assembly-CSharp/MusicManager.cs (EquipInstrument).
    [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.EquipInstrument))]
    internal static class EquipInstrumentPatch
    {
        private static void Postfix(MusicManager __instance)
        {
            SongWindow.Music = __instance;
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
            if (SongWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }
    }
}
