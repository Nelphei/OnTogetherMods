using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FocusAnywhere
{
    // Pressing the focus key opens the free focus menu wherever the game would have said no: ground
    // it doesn't count as focusable (IsFocusable), near the music hill, or next to a seat someone else
    // is using. The game's own state checks still apply (not walking stairs, not fishing, not mid-air,
    // not on the basketball court, no menu open). Seat spots that are free still open their own menu.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (Update) and TaskManager.cs.
    [HarmonyPatch(typeof(PlayerFocusController), "Update")]
    internal static class UpdatePatch
    {
        // Remember whether our player had no menu open and no focus before the game's Update ran,
        // so a key press that just closed the menu or ended a focus doesn't reopen it.
        private static void Prefix(PlayerFocusController __instance, out bool __state)
        {
            __state = __instance.isOwner && !__instance.IsSelectorOpened && !__instance.IsFocus;
        }

        private static void Postfix(PlayerFocusController __instance, bool __state)
        {
            if (!__instance.isOwner)
            {
                return;
            }
            try
            {
                // Merge the lists as soon as we're in a lobby, so EmoteSync can join any activity too.
                if (!ActivityList.Installed)
                {
                    ActivityList.Refresh();
                }
                if (__state && Plugin.Settings.AnyGround.Value)
                {
                    OpenWhereTheGameWouldNot(__instance);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        private static void OpenWhereTheGameWouldNot(PlayerFocusController focus)
        {
            if (focus.IsSelectorOpened || focus.IsFocus)
            {
                return;   // the game opened it (or something else happened this frame)
            }
            InputManager input = MonoSingleton<InputManager>.I;
            if (input == null || !input.IsFocusKeyDown)
            {
                return;
            }
            // The game skips its focus code until these exist.
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            FocusAreaManager areas = NetworkSingleton<FocusAreaManager>.I;
            if (channel == null || channel.MainPlayer == null || areas == null || areas.AreaIndexes == null)
            {
                return;
            }
            // The game's own "free to start a focus" checks, minus where you're standing.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (tasks == null || !tasks.IsReadyForAreaFocus() || !tasks.IsReadyForFreeFocus())
            {
                return;
            }

            // What the game does before opening the free focus menu.
            Game.SetCurrentFocusArea(focus, null);
            Game.IsFocusChange(focus) = false;
            Game.OpenSelector(focus);
        }
    }

    // Before the menu opens: make sure the lists are merged and the menu has enough buttons.
    [HarmonyPatch(typeof(PlayerFocusController), "OpenSelector")]
    internal static class OpenSelectorPatch
    {
        private static void Prefix(PlayerFocusController __instance)
        {
            try
            {
                ActivityList.Refresh();
                FocusAreaController area = __instance.CurrentFocusAreaController;
                List<FocusType> types = area == null
                    ? ScriptableSingleton<GameSettings>.I.FocusTypes
                    : area.FocusAreaSettings != null ? area.FocusAreaSettings.FocusTypes : null;
                MenuSlots.EnsureSlots(ActivityList.MostInOneCategory(types));
                MenuSlots.HideExtraSlots();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }

    // Clicking a category refills the activity row. Hide our extra buttons first; the game shows
    // the ones that category uses.
    [HarmonyPatch(typeof(PlayerFocusController), "ButtonFocusItem")]
    internal static class ButtonFocusItemPatch
    {
        private static void Prefix()
        {
            try
            {
                MenuSlots.HideExtraSlots();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }
    }

    // The game shows the floater or water bed only when you're in water. When you pick a floater
    // activity on land, show it there too (and hide it again when you switch to something else).
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus).
    [HarmonyPatch(typeof(PlayerFocusController), nameof(PlayerFocusController.SetFocus))]
    internal static class SetFocusPatch
    {
        // __state: the picked entry in the merged land list, or -1 when this isn't a land pick.
        private static void Prefix(PlayerFocusController __instance, int subIndex, out int __state)
        {
            __state = -1;
            try
            {
                if (!ActivityList.Installed || __instance.CurrentFocusAreaController != null)
                {
                    return;
                }
                PlayerMovementController movement = Game.Movement(__instance);
                if (movement == null || movement.IsInWater)
                {
                    return;
                }
                __state = FindLandIndex(__instance, subIndex);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        private static void Postfix(PlayerFocusController __instance, int __state)
        {
            if (__state < 0 || __state >= ActivityList.LandWaterProps.Count || !__instance.IsFocus)
            {
                return;
            }
            try
            {
                WaterPropType prop = ActivityList.LandWaterProps[__state];
                Game.SetWaterProp(__instance, prop);
                Dictionary<OutfitType, SkinnedMeshRenderer> outfit = __instance._PlayerCustomizationController.OutfitMeshRenderers;
                if (outfit.TryGetValue(OutfitType.Floater, out SkinnedMeshRenderer floater) && floater != null)
                {
                    floater.gameObject.SetActive(prop == WaterPropType.Floater);
                }
                if (outfit.TryGetValue(OutfitType.Bed, out SkinnedMeshRenderer bed) && bed != null)
                {
                    bed.gameObject.SetActive(prop == WaterPropType.Bed);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        // The same lookup SetFocus does on land: the subIndex-th entry of the clicked category.
        private static int FindLandIndex(PlayerFocusController focus, int subIndex)
        {
            List<FocusType> categories = Game.MenuCategories(focus);
            int category = Game.MenuCategoryIndex(focus);
            if (categories == null || category < 0 || category >= categories.Count)
            {
                return -1;
            }
            FocusType type = categories[category];
            List<FocusType> types = ScriptableSingleton<GameSettings>.I.FocusTypes;
            int n = 0;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != type)
                {
                    continue;
                }
                if (n == subIndex)
                {
                    return i;
                }
                n++;
            }
            return -1;
        }
    }
}
