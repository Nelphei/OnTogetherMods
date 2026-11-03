using System;
using System.Collections.Generic;
using HarmonyLib;
using OnTogetherMods.Shared;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NameDesigner
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/CustomizationUIController.cs
    // (CloseIDCard), MainMenuUIController.cs, MultiplayerManager.cs (CreateLobby) and
    // PlayerCustomizationController.cs (UpdatePlayerInfo).
    internal static class Game
    {
        // How many letters a name can have when the game's own box has no limit.
        private const int FallbackLimit = 32;

        // Remembered by the patches when these start, so nothing has to search the scene.
        public static MainMenuUIController Menu;
        public static CustomizationUIController Customization;

        private static readonly AccessTools.FieldRef<MainMenuUIController, Button> CreateButton =
            AccessTools.FieldRefAccess<MainMenuUIController, Button>("_createSessionButton");

        private static readonly List<Canvas> Canvases = new List<Canvas>();
        private static readonly Vector3[] Corners = new Vector3[4];

        // ---- Your name ----

        // The name you saved on your ID card. Empty if you never set one (the game uses your Steam name).
        public static string SavedName()
        {
            DataManager data = MonoSingleton<DataManager>.I;
            return data != null && data.PlayerData != null ? data.PlayerData.Name ?? "" : "";
        }

        // The name everyone sees.
        public static string CurrentName()
        {
            string name = SavedName();
            if (string.IsNullOrEmpty(name) && SteamManager.Initialized)
            {
                name = SteamFriends.GetPersonaName();
            }
            return name ?? "";
        }

        // Does what closing the ID card does (CustomizationUIController.CloseIDCard): saves the name
        // and, in a world, sends it to everyone and updates it in chat and on your player panel.
        // Returns false when you aren't in a world, so it was only saved for the next time you join.
        public static bool ApplyName(string name)
        {
            DataManager data = MonoSingleton<DataManager>.I;
            data.PlayerData.Name = name;
            data.SavePlayerData();

            // The ID card copies its name box into your save when it closes, so they have to agree.
            CustomizationUIController card = CustomizationController();
            if (card != null && card.NameInputField != null)
            {
                card.NameInputField.text = name;
            }

            if (MonoSingleton<MainSceneManager>.I == null)
            {
                return false;
            }
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            if (channel == null || channel.MainCustomizationController == null)
            {
                return false;
            }
            channel.MainCustomizationController.UpdatePlayerInfo(data.PlayerData.GetPlayerIdInfo());
            channel.UserName = name;
            UIManager ui = MonoSingleton<UIManager>.I;
            if (ui != null && ui.PlayerText != null)
            {
                BadWordFilterManager filter = MonoSingleton<BadWordFilterManager>.I;
                ui.PlayerText.text = filter != null ? filter.FilterString(name) : name;
            }
            return true;
        }

        // How many letters a name can have: the same as the ID card's name box allows.
        public static int NameLimit()
        {
            CustomizationUIController card = Customization;
            return Limit(card != null && card.NameInputField != null ? card.NameInputField.characterLimit : 0);
        }

        // The font the name tags use. On the main menu there are no name tags, so the ID card's.
        public static TMP_FontAsset NameFont()
        {
            TMP_FontAsset font = PlayerNames.NameTagFont();
            if (font != null)
            {
                return font;
            }
            CustomizationUIController card = Customization;
            return card != null && card.NameInputField != null && card.NameInputField.textComponent != null
                ? card.NameInputField.textComponent.font
                : null;
        }

        private static CustomizationUIController CustomizationController()
        {
            // Only looked up on a click: when it's missing, MonoSingleton.I searches the scene.
            if (Customization == null)
            {
                Customization = MonoSingleton<CustomizationUIController>.I;
            }
            return Customization;
        }

        // ---- The session name ----

        // The name box on the main menu's Create Session panel. The game creates the session with
        // whatever text it holds (MultiplayerManager.CreateLobby).
        public static TMP_InputField SessionField => Menu != null ? Menu.CreateSessionNameInputField : null;

        public static bool SessionBoxShowing
        {
            get
            {
                TMP_InputField field = SessionField;
                return field != null && field.gameObject.activeInHierarchy;
            }
        }

        // Puts a name in the Create Session box. Setting the text from code isn't cut short by the
        // box's letter limit (TMP_InputField.SetText), and it switches the Create button on or off
        // like typing does (MainMenuUIController.OnCreateSessionNameInputFieldChanged).
        public static string WriteSession(string text)
        {
            TMP_InputField field = SessionField;
            if (field == null)
            {
                return null;
            }
            // Show the colours in the box instead of the tags.
            field.richText = true;
            field.text = text;
            return field.text;
        }

        public static int SessionLimit()
        {
            TMP_InputField field = SessionField;
            return Limit(field != null ? field.characterLimit : 0);
        }

        public static TMP_FontAsset SessionFont()
        {
            TMP_InputField field = SessionField;
            return field != null && field.textComponent != null ? field.textComponent.font : null;
        }

        // Where the Create Session box and its Create button are on screen, in 1080p units from
        // the top-left (like PanelStack). False if they aren't showing.
        public static bool SessionBoxBounds(out Rect bounds)
        {
            bounds = default;
            TMP_InputField field = SessionField;
            if (field == null)
            {
                return false;
            }
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            Add(field.transform as RectTransform, ref min, ref max);
            Button create = CreateButton(Menu);
            Add(create != null ? create.transform as RectTransform : null, ref min, ref max);
            if (min.x > max.x)
            {
                return false;
            }

            // Screen pixels count up from the bottom-left.
            float scale = 1080f / Screen.height;
            bounds = Rect.MinMaxRect(min.x * scale, (Screen.height - max.y) * scale,
                max.x * scale, (Screen.height - min.y) * scale);
            return true;
        }

        private static void Add(RectTransform part, ref Vector2 min, ref Vector2 max)
        {
            if (part == null || !part.gameObject.activeInHierarchy)
            {
                return;
            }
            part.GetComponentsInParent(true, Canvases);
            if (Canvases.Count == 0)
            {
                return;
            }
            Canvas root = Canvases[0].rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            part.GetWorldCorners(Corners);
            foreach (Vector3 corner in Corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        private static int Limit(int gameLimit)
        {
            return Math.Min(gameLimit > 0 ? gameLimit : FallbackLimit, Design.MaxLetters);
        }
    }
}
