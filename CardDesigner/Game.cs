using System;
using System.Collections.Generic;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardDesigner
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/CustomizationUIController.cs
    // (ButtonIDCard, CloseIDCard), PlayerData.cs (GetPlayerIdInfo) and TemporaryPhoto.cs (how other
    // players' cards are shown).
    internal static class Game
    {
        // The card lines this mod edits, in the order the card shows them. The name (the first line)
        // is left to Name Designer.
        public const int Fields = 5;
        public const int DateOfBirth = 0;
        public const int HoroscopeSign = 1;
        public const int Location = 2;
        public const int AreaOfStudy = 3;
        public const int Quote = 4;

        // How much of each line the game sends to other players (PlayerData.GetPlayerIdInfo cuts the
        // rest off). Tags count too.
        public const int SendLimit = 1000;

        // The quote box shows at most 3 lines, and setting its text cuts off the rest
        // (TMP_InputField.UpdateLabel with lineLimit 3).
        public const int QuoteLines = 3;

        // The card's English labels, for when the game's own (in your language) can't be read.
        private static readonly string[] FallbackLabels =
        {
            "Name", "Date of birth", "Horoscope sign", "Location", "Area of study", "Favourite quote",
        };

        // Remembered by a patch when the customization screen starts (it's in both scenes).
        public static CustomizationUIController Customization;

        private static DataManager _data;
        private static float _nextDataLook;
        private static readonly List<Canvas> Canvases = new List<Canvas>();
        private static readonly Vector3[] Corners = new Vector3[4];

        // ---- Your card ----

        // The game's save manager. Looked up at most once a second while it's missing, since
        // MonoSingleton.I searches the scene each time.
        private static DataManager Data
        {
            get
            {
                if (_data == null && Time.unscaledTime >= _nextDataLook)
                {
                    _nextDataLook = Time.unscaledTime + 1f;
                    _data = MonoSingleton<DataManager>.I;
                }
                return _data;
            }
        }

        private static PlayerData Player => Data != null ? Data.PlayerData : null;

        // True while the game's own ID card page is open (on the main menu or in a world).
        public static bool CardPageShowing
        {
            get
            {
                CustomizationUIController card = Customization;
                return card != null && card.IDCustomizationPanel != null && card.IDCustomizationPanel.activeInHierarchy;
            }
        }

        // What your card lines say now. While the game's ID card page is open, its boxes may hold
        // changes it hasn't saved yet, so they're read instead.
        public static string[] ReadCard()
        {
            var lines = new string[Fields];
            if (CardPageShowing)
            {
                TMP_InputField[] boxes = Boxes(Customization);
                for (int i = 0; i < Fields; i++)
                {
                    lines[i] = boxes[i] != null ? boxes[i].text ?? "" : "";
                }
                return lines;
            }
            PlayerData player = Player;
            if (player == null)
            {
                return null;
            }
            lines[DateOfBirth] = player.DateOfBirth ?? "";
            lines[HoroscopeSign] = player.HoroscopeSign ?? "";
            lines[Location] = player.Location ?? "";
            lines[AreaOfStudy] = player.AreaOfStudy ?? "";
            lines[Quote] = player.FavoriteQuote ?? "";
            return lines;
        }

        // Does what closing the ID card does (CustomizationUIController.CloseIDCard) for these lines:
        // saves them and, in a world, sends your card to everyone. Returns false when you aren't in a
        // world, so it was only saved for the next time you join.
        public static bool ApplyCard(string[] lines)
        {
            DataManager data = Data;
            PlayerData player = data.PlayerData;
            player.DateOfBirth = lines[DateOfBirth];
            player.HoroscopeSign = lines[HoroscopeSign];
            player.Location = lines[Location];
            player.AreaOfStudy = lines[AreaOfStudy];
            player.FavoriteQuote = lines[Quote];
            data.SavePlayerData();

            KeepBoxesInStep(lines);

            if (MonoSingleton<MainSceneManager>.I == null)
            {
                return false;
            }
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            if (channel == null || channel.MainCustomizationController == null)
            {
                return false;
            }
            channel.MainCustomizationController.UpdatePlayerInfo(player.GetPlayerIdInfo());
            return true;
        }

        // The ID card page copies its boxes into your save when it closes. Opening it only fills a box
        // when the saved line isn't empty (ButtonIDCard), so a line you emptied here would come back.
        // The quote box is only set while the page shows: it cuts its text to 3 lines when it's set,
        // and while it's hidden it would measure the lines of the text it showed before.
        private static void KeepBoxesInStep(string[] lines)
        {
            CustomizationUIController card = Customization;
            if (card == null)
            {
                return;
            }
            bool showing = CardPageShowing;
            TMP_InputField[] boxes = Boxes(card);
            for (int i = 0; i < Fields; i++)
            {
                if (boxes[i] == null || (i == Quote && !showing && lines[i].Length > 0))
                {
                    continue;
                }
                try
                {
                    boxes[i].text = lines[i];
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Couldn't put line {i + 2} into the game's ID card: {e.Message}");
                }
            }
        }

        private static TMP_InputField[] Boxes(CustomizationUIController card)
        {
            return new[]
            {
                card.DateOfBirthInputField, card.HoroscopeSignInputField, card.LocationInputField,
                card.AreaOfStudyInputField, card.FavoriteQuoteInputField,
            };
        }

        // Your name, the card's first line. The game uses your Steam name until you set one.
        public static string Name()
        {
            PlayerData player = Player;
            string name = player != null ? player.Name : null;
            if (string.IsNullOrEmpty(name) && SteamManager.Initialized)
            {
                name = SteamFriends.GetPersonaName();
            }
            return name ?? "";
        }

        // ---- How the card looks ----

        // The card's lettering.
        public static TMP_FontAsset CardFont()
        {
            CustomizationUIController card = Customization;
            TMP_InputField box = card != null ? card.FavoriteQuoteInputField : null;
            if (box != null && box.textComponent != null && box.textComponent.font != null)
            {
                return box.textComponent.font;
            }
            return TMP_Settings.defaultFontAsset;
        }

        // The label of a line (0 is the name), in your language: the game shows it in an empty box.
        public static string Label(int row)
        {
            CustomizationUIController card = Customization;
            TMP_InputField box = null;
            if (card != null)
            {
                box = row == 0 ? card.NameInputField : Boxes(card)[row - 1];
            }
            if (box != null && box.placeholder is TMP_Text placeholder && !string.IsNullOrEmpty(placeholder.text))
            {
                return placeholder.text;
            }
            return FallbackLabels[row];
        }

        public static bool IsGold
        {
            get
            {
                PlayerData player = Player;
                return player != null && player.IsGold;
            }
        }

        // The card's colour, picked on the game's ID card page.
        public static Color CardColour()
        {
            try
            {
                List<Color> colours = ScriptableSingleton<GameSettings>.I.IDCardColors;
                PlayerData player = Player;
                int index = player != null ? player.IDCardColorIndex : 0;
                if (colours != null && colours.Count > 0)
                {
                    return colours[Mathf.Clamp(index, 0, colours.Count - 1)];
                }
            }
            catch (Exception)
            {
                // Fall through to a plain colour.
            }
            return new Color(0.55f, 0.75f, 0.95f);
        }

        public static Color BoxColour()
        {
            CustomizationUIController card = Customization;
            if (card == null)
            {
                return new Color(1f, 1f, 1f, 0.9f);
            }
            return IsGold ? card.GoldIDCardInputBgColor : card.DefaultIDCardInputBgColor;
        }

        public static Color TextColour()
        {
            CustomizationUIController card = Customization;
            if (card == null)
            {
                return new Color(0.25f, 0.2f, 0.25f);
            }
            return IsGold ? card.GoldIDCardTextColor : card.DefaultIDCardTextColor;
        }

        public static Color PlaceholderColour()
        {
            CustomizationUIController card = Customization;
            if (card == null)
            {
                return new Color(0.25f, 0.2f, 0.25f, 0.5f);
            }
            return IsGold ? card.GoldIDCardPlaceholderColor : card.DefaultIDCardPlaceholderColor;
        }

        // The rounded box behind the game's ID card lines, if it has one.
        public static Sprite BoxSprite()
        {
            CustomizationUIController card = Customization;
            Image image = card != null && card.NameInputField != null ? card.NameInputField.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        // ---- The Design card button ----

        private static int _buttonFrame = -1;
        private static bool _buttonFound;
        private static Rect _buttonBounds;

        // Where the Design card button goes on the game's ID card page, in 1080p units from the
        // top-left (like PanelStack): the spot of the page's unused Next button, right of the colours
        // and the gold star. False if the page isn't showing. Worked out once a frame.
        public static bool CardButtonBounds(out Rect bounds)
        {
            if (_buttonFrame != Time.frameCount)
            {
                _buttonFrame = Time.frameCount;
                _buttonFound = FindCardButton(out _buttonBounds);
            }
            bounds = _buttonBounds;
            return _buttonFound;
        }

        private static bool FindCardButton(out Rect bounds)
        {
            bounds = default;
            if (!CardPageShowing)
            {
                return false;
            }
            Transform page = Customization.IDCustomizationPanel.transform;
            Transform spot = page.Find("Button_Next");
            if (spot is RectTransform next && ScreenBounds(next, out bounds))
            {
                return true;
            }
            // No such button (a game update?): just below the card's lines, at their right.
            Transform lines = page.Find("Panel_InputFields");
            if (lines is RectTransform area && ScreenBounds(area, out Rect box))
            {
                bounds = new Rect(box.xMax - 180f, Mathf.Min(box.yMax + 12f, 1080f - 60f), 180f, 48f);
                return true;
            }
            return false;
        }

        // A piece of game UI's spot on screen, in 1080p units from the top-left. It works on switched-off
        // pieces too, since nothing lays them out by itself.
        private static bool ScreenBounds(RectTransform part, out Rect bounds)
        {
            bounds = default;
            part.GetComponentsInParent(true, Canvases);
            if (Canvases.Count == 0)
            {
                return false;
            }
            Canvas root = Canvases[0].rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            part.GetWorldCorners(Corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in Corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            // Screen pixels count up from the bottom-left.
            float scale = 1080f / Screen.height;
            bounds = Rect.MinMaxRect(min.x * scale, (Screen.height - max.y) * scale,
                max.x * scale, (Screen.height - min.y) * scale);
            return bounds.width > 1f && bounds.height > 1f;
        }
    }
}
