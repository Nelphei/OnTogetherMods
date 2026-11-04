using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutfitSlots
{
    // The game objects this mod works with, remembered as they start so nothing searches the scene
    // every frame. See reference/Assembly-CSharp/CustomizationUIController.cs and DataManager.cs.
    internal static class Game
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        private static DataManager _data;

        private static readonly AccessTools.FieldRef<CustomizationUIController, GameObject> PanelField =
            AccessTools.FieldRefAccess<CustomizationUIController, GameObject>("_customizationPanel");

        // Holds your save. It lives for the whole game (DontDestroyOnLoad), and MonoSingleton.I only
        // searches the scene the first time.
        public static DataManager Data
        {
            get
            {
                if (_data == null)
                {
                    _data = MonoSingleton<DataManager>.I;
                }
                return _data;
            }
            set => _data = value;
        }

        // The outfit screen of the current scene (main menu or world), set once its Start has run.
        public static CustomizationUIController Customization;

        public static bool InWorld => SceneManager.GetActiveScene().buildIndex == WorldScene;

        // True while the outfit screen (avatar, pet, desk or ID card page) is showing.
        public static bool IsOpen(CustomizationUIController screen)
        {
            GameObject panel = PanelField(screen);
            return panel != null && panel.activeInHierarchy;
        }

        public static bool ScreenOpen
        {
            get
            {
                CustomizationUIController screen = Customization;
                return screen != null && IsOpen(screen);
            }
        }
    }
}
