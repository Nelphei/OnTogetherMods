using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SessionManager
{
    // While you type in the window, tell the game "the player is typing", so its InputManager zeroes
    // every input and WASD doesn't walk you away. It's the same path chat uses.
    // See reference/Assembly-CSharp/InputManager.cs (Update) and TaskManager.cs (IsCancelInputs).
    [HarmonyPatch(typeof(TaskManager), nameof(TaskManager.IsCancelInputs))]
    internal static class CancelInputsPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (ManagerWindow.HoldsKeys)
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
            // The typing path above still reports Enter, which would open chat. Enter renames the
            // session (or saves the note) instead.
            if (ManagerWindow.HoldsKeys)
            {
                SetEnterKeyDown(__instance, false);
                SetEnterKeyUp(__instance, false);
            }
            // A click on the window shouldn't also click the world behind it (pets, players, the
            // fishing rod). Only the press is hidden, so a click that started outside still ends.
            if (ManagerWindow.IsMouseOver)
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

    // The arrows beside the player count on the main menu's Create Session panel. The game's own stop
    // at 16; these go up to MaxPlayers, and Shift-click jumps by SizeStep. Left to the other mod when
    // PlayerLimitLift is installed.
    [HarmonyPatch(typeof(MultiplayerManager), nameof(MultiplayerManager.ChangeFilterPlayer))]
    internal static class PlayerPickerPatch
    {
        private static bool Prefix(MultiplayerManager __instance, int value, ref int __result)
        {
            try
            {
                if (!Lift.OwnsPicker)
                {
                    return true;
                }
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                int next = Lift.Next(__instance.FilterPlayerValue, value == 1 ? 1 : -1, shift);
                __instance.FilterPlayerValue = next;
                __result = next;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                return true;
            }
        }
    }

    // The game reads its ban file once, when it starts (DataManager.Start). Until then its ban list
    // may be empty, which would look like every ban had been lifted.
    [HarmonyPatch]
    internal static class BanFileLoadedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DataManager), "LoadBanData");
        }

        // Skipped, rather than stopping the whole mod, if a game update renames it.
        private static bool Prepare()
        {
            if (TargetMethod() != null)
            {
                return true;
            }
            Plugin.Log.LogWarning("Couldn't find where the game loads its ban list, so timed bans won't end by " +
                                  "themselves. Unban players in the Bans tab instead.");
            return false;
        }

        private static void Postfix()
        {
            Game.BansLoaded = true;
        }
    }

    // Join requests: see JoinRequests.cs.
    [HarmonyPatch(typeof(LobbyRequestManager), "Update")]
    internal static class RequestUpdatePatch
    {
        private static void Postfix(LobbyRequestManager __instance)
        {
            try
            {
                JoinRequests.AfterUpdate(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(UIManager), nameof(UIManager.OpenRequestUI))]
    internal static class RequestPopupPatch
    {
        private static void Prefix(string requester)
        {
            try
            {
                LobbyRequestManager manager = MonoSingleton<LobbyRequestManager>.I;
                if (manager != null)
                {
                    JoinRequests.PopupOpening(manager, requester);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }
}
