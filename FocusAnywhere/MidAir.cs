using System;
using HarmonyLib;

namespace FocusAnywhere
{
    // Focusing in the air: you stop where you are when the menu opens, and the activity starts right
    // there, floating. The game already stops gravity while you focus (SetFocus turns the
    // CharacterController off, and PlayerMovementController.Update skips Fall while IsFocus), so only
    // the time the menu is open needs holding. Closing the menu without picking lets you fall again,
    // and so does standing up. This works whoever opened the menu: our UpdatePatch, or the game
    // itself when BlueSage QoL's Focus Anywhere is on.
    // See reference/Assembly-CSharp/PlayerMovementController.cs (Update, Fall, SetUmbrella).
    internal static class MidAir
    {
        // Set when the menu opens in the air, cleared once it closes.
        private static bool _holding;

        // Any height counts, even a small hop (GroundState Low is under about the umbrella height).
        public static bool IsAirborne(PlayerMovementController movement)
        {
            MovementState state = movement.MovementState;
            return movement.GroundState != GroundState.Grounded ||
                   state == MovementState.Fall || state == MovementState.Umbrella;
        }

        // Called after the focus menu opens.
        public static void OnMenuOpened(PlayerFocusController focus)
        {
            _holding = false;
            if (!Plugin.Settings.MidAir.Value || focus.IsFocus)
            {
                return;   // switching activity while already focusing: you're already held
            }
            PlayerMovementController movement = Game.Movement(focus);
            if (movement == null || movement.MovementState == MovementState.Bubble || !IsAirborne(movement))
            {
                return;   // a bubble moves you itself
            }
            _holding = true;
            movement.SpeedY = 0f;
            // Put the umbrella away, so it isn't still open while you sit.
            if (movement.MovementState == MovementState.Umbrella)
            {
                movement.SetUmbrella(false);
            }
            if (Plugin.Settings.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Focus menu opened in the air ({movement.GroundState}, {movement.MovementState}): holding you here.");
            }
        }

        // Fall is the gravity step the game runs while a menu is open. Skip it while we hold you.
        [HarmonyPatch(typeof(PlayerMovementController), "Fall")]
        internal static class FallPatch
        {
            private static bool Prefix(PlayerMovementController __instance)
            {
                if (!_holding)
                {
                    return true;
                }
                try
                {
                    PlayerFocusController focus = NetworkSingleton<TextChannelManager>.I.MainFocusController;
                    if (focus == null || !focus.IsSelectorOpened)
                    {
                        _holding = false;   // the menu closed without a pick: fall as normal
                        return true;
                    }
                    __instance.SpeedY = 0f;
                    return false;
                }
                catch (Exception e)
                {
                    _holding = false;
                    Plugin.Log.LogError(e);
                    return true;
                }
            }
        }
    }
}
