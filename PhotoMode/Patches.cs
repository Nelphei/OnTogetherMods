using System;
using HarmonyLib;

namespace PhotoMode
{
    // While photo mode is on, tell the game "the player is typing", so its InputManager zeroes
    // every input (movement, mouse, hotkeys). It's the same path chat uses, so the character
    // stands still and WASD only moves our camera.
    // See reference/Assembly-CSharp/InputManager.cs (Update) and TaskManager.cs (IsCancelInputs).
    [HarmonyPatch(typeof(TaskManager), nameof(TaskManager.IsCancelInputs))]
    internal static class CancelInputsPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (PhotoModeController.IsActive)
            {
                __result = true;
            }
        }
    }

    // The cancelled-input path above still reports Enter, which would open the hidden chat box.
    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class EnterKeyPatch
    {
        private static readonly Action<InputManager, bool> SetEnterKeyDown = Setter(nameof(InputManager.EnterKeyDown));
        private static readonly Action<InputManager, bool> SetEnterKeyUp = Setter(nameof(InputManager.EnterKeyUp));

        private static void Postfix(InputManager __instance)
        {
            if (PhotoModeController.IsActive)
            {
                SetEnterKeyDown(__instance, false);
                SetEnterKeyUp(__instance, false);
            }
        }

        // The properties have private setters, so bind to them once instead of reflecting every frame.
        private static Action<InputManager, bool> Setter(string property)
        {
            return (Action<InputManager, bool>)Delegate.CreateDelegate(
                typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), property));
        }
    }

    // This method turns the normal camera with right-click, zooms it with the scroll wheel, and
    // unlocks the cursor every frame you aren't right-clicking. Skip it so it doesn't fight the
    // free camera. See reference/Assembly-CSharp/PlayerMovementController.cs.
    [HarmonyPatch(typeof(PlayerMovementController), "SetScreenCameraRotation")]
    internal static class PlayerCameraPatch
    {
        private static bool Prefix()
        {
            return !PhotoModeController.IsActive;
        }
    }
}
