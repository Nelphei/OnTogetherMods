using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FocusAnywhere
{
    // Shortcuts into the game's private focus code.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (Update, OpenSelector, ButtonFocusItem, SetFocus).
    internal static class Game
    {
        public static readonly AccessTools.FieldRef<PlayerFocusController, bool> IsFocusChange =
            AccessTools.FieldRefAccess<PlayerFocusController, bool>("_isFocusChange");

        // The categories shown in the open menu, and which one was clicked.
        public static readonly AccessTools.FieldRef<PlayerFocusController, List<FocusType>> MenuCategories =
            AccessTools.FieldRefAccess<PlayerFocusController, List<FocusType>>("_simpledFocusTypes");

        public static readonly AccessTools.FieldRef<PlayerFocusController, int> MenuCategoryIndex =
            AccessTools.FieldRefAccess<PlayerFocusController, int>("_currentFocusMainIndex");

        public static readonly AccessTools.FieldRef<PlayerFocusController, PlayerMovementController> Movement =
            AccessTools.FieldRefAccess<PlayerFocusController, PlayerMovementController>("_playerMovementController");

        private static readonly Action<PlayerFocusController> OpenSelectorMethod =
            AccessTools.MethodDelegate<Action<PlayerFocusController>>(AccessTools.Method(typeof(PlayerFocusController), "OpenSelector"));

        private static readonly Action<PlayerFocusController, FocusAreaController> SetCurrentFocusAreaMethod =
            AccessTools.MethodDelegate<Action<PlayerFocusController, FocusAreaController>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.CurrentFocusAreaController)));

        private static readonly Action<PlayerFocusController, WaterPropType> SetWaterPropMethod =
            AccessTools.MethodDelegate<Action<PlayerFocusController, WaterPropType>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.WaterProp)));

        // Opens the focus menu, the same call the game makes when you press the focus key.
        public static void OpenSelector(PlayerFocusController focus) => OpenSelectorMethod(focus);

        public static void SetCurrentFocusArea(PlayerFocusController focus, FocusAreaController area) => SetCurrentFocusAreaMethod(focus, area);

        public static void SetWaterProp(PlayerFocusController focus, WaterPropType prop) => SetWaterPropMethod(focus, prop);
    }
}
