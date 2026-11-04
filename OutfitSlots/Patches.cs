using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace OutfitSlots
{
    // ---- Loading and saving the game's save (reference/Assembly-CSharp/DataManager.cs) ----

    // The game reads its save and two backups, then picks one. Each gets all 9 names, and goes back
    // to the slot you had on.
    [HarmonyPatch(typeof(DataManager), "LoadZipDataStatus")]
    internal static class LoadSavePatch
    {
        private static void Postfix(ref PlayerDataZip playerData)
        {
            Slots.Inflate(playerData);
        }
    }

    [HarmonyPatch(typeof(DataManager), "LoadPlayerData")]
    internal static class LoadedPatch
    {
        private static void Postfix(DataManager __instance)
        {
            Game.Data = __instance;
            PlayerDataZip zip = __instance.PlayerDataZip;
            if (Slots.Active && zip != null)
            {
                Plugin.Log.LogInfo("Wearing outfit " + Slots.Label(zip, zip.SelectedStyleIndex) + ".");
            }
        }
    }

    // While the save is written to disk it looks like the game's own: 3 slots, one of them selected.
    [HarmonyPatch]
    internal static class WriteSavePatch
    {
        private static Slots.Saved _saved;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DataManager), nameof(DataManager.SavePlayerZipData));
            yield return AccessTools.Method(typeof(DataManager), nameof(DataManager.SaveOldPlayerZipData));
        }

        private static void Prefix(DataManager __instance)
        {
            _saved = Slots.Deflate(__instance.PlayerDataZip);
        }

        // Runs even if saving fails, so the save in memory always goes back to 9 slots.
        private static void Finalizer(DataManager __instance, MethodBase __originalMethod, Exception __exception)
        {
            Slots.Restore(_saved);
            _saved = null;
            if (__exception == null && __originalMethod.Name == nameof(DataManager.SavePlayerZipData))
            {
                Slots.SaveStore(__instance.PlayerDataZip);
            }
        }
    }

    // The backup copy is written a frame later, by a coroutine.
    [HarmonyPatch]
    internal static class WriteBackupPatch
    {
        private static Slots.Saved _saved;

        private static MethodBase TargetMethod()
        {
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(DataManager), nameof(DataManager.SaveBackupZipData)));
        }

        private static void Prefix()
        {
            DataManager data = Game.Data;
            _saved = data != null ? Slots.Deflate(data.PlayerDataZip) : null;
        }

        private static void Finalizer()
        {
            Slots.Restore(_saved);
            _saved = null;
        }
    }

    // ---- The selected slot's outfit (reference/Assembly-CSharp/PlayerDataZip.cs) ----

    // Every read and write of the outfit in your selected slot goes through this property: loading
    // your look at startup, closing the outfit screen, switching slots, the lemonade hat, pets.
    [HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Getter)]
    internal static class GetOutfitPatch
    {
        private static bool Prefix(PlayerDataZip __instance, ref CustomizationDataIDs3 __result)
        {
            int slot = __instance.SelectedStyleIndex;
            if (!Slots.Active || !Slots.IsModSlot(slot))
            {
                return true;
            }
            __result = Slots.OutfitFor(__instance, slot);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Setter)]
    internal static class SetOutfitPatch
    {
        private static bool Prefix(PlayerDataZip __instance, CustomizationDataIDs3 value)
        {
            int slot = __instance.SelectedStyleIndex;
            if (!Slots.Active || !Slots.IsModSlot(slot))
            {
                return true;
            }
            Slots.SetOutfitFor(__instance, slot, value);
            return false;
        }
    }

    // ---- The outfit screen (reference/Assembly-CSharp/CustomizationUIController.cs) ----

    [HarmonyPatch(typeof(CustomizationUIController), "Start")]
    internal static class CustomizationStartPatch
    {
        private static void Postfix(CustomizationUIController __instance)
        {
            Game.Customization = __instance;
        }
    }

    // Rows 4-9 go in before the game fills in the style list.
    [HarmonyPatch(typeof(CustomizationUIController), "InitStyleDropdown")]
    internal static class StyleListPatch
    {
        private static void Prefix(CustomizationUIController __instance)
        {
            if (!Slots.Active)
            {
                return;
            }
            try
            {
                Dropdown.Prepare(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.ButtonOpenStyleDropdown))]
    internal static class OpenStyleListPatch
    {
        private static void Postfix(CustomizationUIController __instance)
        {
            if (!Slots.Active)
            {
                return;
            }
            try
            {
                Dropdown.Opened(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }

    // ---- The quick-swap window ----

    [HarmonyPatch(typeof(InputManager), "Update")]
    internal static class InputPatch
    {
        private static readonly Action<InputManager, bool> SetMouse0Down = (Action<InputManager, bool>)Delegate.CreateDelegate(
            typeof(Action<InputManager, bool>), AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Down)));

        // A click on the window shouldn't also click the world behind it (pets, players, the fishing
        // rod). Only the press is hidden, so a click that started outside still ends.
        private static void Postfix(InputManager __instance)
        {
            if (OutfitWindow.IsMouseOver)
            {
                SetMouse0Down(__instance, false);
            }
        }
    }
}
