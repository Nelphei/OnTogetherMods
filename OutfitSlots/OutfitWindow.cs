using System;
using OnTogetherMods.Shared;
using UnityEngine;

namespace OutfitSlots
{
    // The quick-swap window: all 9 outfit slots, click one to put it on. It sits in the bottom window
    // stack (Shared/PanelStack.cs) and is drawn with Unity's immediate-mode GUI, laid out at 1080p
    // and scaled to the screen. It steps aside while the outfit screen is open, which has its own list.
    internal sealed class OutfitWindow : MonoBehaviour
    {
        private const string PanelId = "OutfitSlots";
        private const float Width = 300f;
        private const float Pad = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 28f;
        private const float Gap = 3f;
        private const float StatusHeight = 22f;
        private const float NumberWidth = 26f;
        private const float TagWidth = 62f;
        private const float StatusSeconds = 4f;

        private static OutfitWindow _instance;

        // Read by the patches. True while the mouse is over the window, so clicks don't reach the world.
        internal static bool IsMouseOver { get; private set; }

        private static Settings S => Plugin.Settings;

        private bool _open;
        private bool _iconClicked;
        private bool _iconRightClicked;
        private int _picked = -1;
        private bool _screenOpen;

        private string _status;
        private float _statusUntil;

        // Where the window was last drawn, in screen pixels from the top-left.
        private Rect _screenRect;
        private int _drawnFrame = -100;

        // An invisible piece of game UI under the window (see Shared/ClickBlocker.cs).
        private ClickBlocker _blocker;

        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _wearingStyle;
        private GUIStyle _numberStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _dimStyle;
        private GUIStyle _tagStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tipStyle;

        private void Awake()
        {
            _instance = this;
            _blocker = new ClickBlocker(PanelStack.CanvasPrefix + "OutfitSlots ClickBlocker");
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                IsMouseOver = false;
            }
            _blocker?.Destroy();
        }

        private void Update()
        {
            try
            {
                _screenOpen = Game.ScreenOpen;

                // Clicks on the icon (Shared/IconBar.cs) and the window from the last OnGUI.
                bool clicked = _iconClicked;
                bool next = _iconRightClicked;
                int picked = _picked;
                _iconClicked = false;
                _iconRightClicked = false;
                _picked = -1;

                if ((Input.GetKeyDown(S.ToggleKey.Value) && !IsChatOpen()) || clicked)
                {
                    _open = !_open;
                    _status = null;
                }
                if (next && Slots.Active)
                {
                    Wear(Swap.NextSlot());
                }
                if (picked >= 0)
                {
                    Wear(picked);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
            UpdateMouseOver();
        }

        private void Wear(int slot)
        {
            string message;
            try
            {
                message = Swap.Wear(slot);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
                message = "Couldn't change outfit. See the BepInEx log.";
            }
            Status(message);
            if (_open && S.CloseAfterSwap.Value && message.StartsWith("Wearing", StringComparison.Ordinal))
            {
                _open = false;
            }
        }

        private void UpdateMouseOver()
        {
            bool shown = _open && Time.frameCount - _drawnFrame <= 2;
            IsMouseOver = shown && ClickBlocker.MouseIn(_screenRect);
            _blocker?.Update(shown, _screenRect);
        }

        private static bool IsChatOpen()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        private void Status(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + StatusSeconds;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons.
            if (S.ShowIcon.Value)
            {
                IconBar.State state = !Slots.Active ? IconBar.State.Off : _open ? IconBar.State.Open : IconBar.State.Plain;
                IconBar.Click click = IconBar.Draw("OutfitSlots", IconBar.OutfitSlotsOrder, Icon.Pixels, state,
                    "Outfit Slots", S.ToggleKey.Value, Slots.Active ? "Right-click: next outfit" : null);
                if (click == IconBar.Click.Left)
                {
                    _iconClicked = true;   // acted on in Update
                }
                else if (click == IconBar.Click.Right)
                {
                    _iconRightClicked = true;
                }
            }

            // Hidden while Photo Mode takes a photo, while the game's eye button hides the UI, and
            // while the outfit screen is open.
            if (!_open || PanelStack.Hidden || _screenOpen)
            {
                return;
            }
            if (_boxStyle == null)
            {
                MakeStyles();
            }

            Matrix4x4 previous = PanelStack.Begin(out _);
            try
            {
                DrawWindow();
            }
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Plugin.Log.LogError(e);
            }
            finally
            {
                PanelStack.End(previous);
            }
        }

        private void DrawWindow()
        {
            float listHeight = Slots.Total * RowHeight + (Slots.Total - 1) * Gap;
            float height = Pad + HeaderHeight + Gap + listHeight + Gap + StatusHeight + Pad;

            Rect panel = PanelStack.Place(PanelId, PanelStack.OutfitSlotsOrder, new Vector2(Width, height));
            float scale = Screen.height / 1080f;
            _screenRect = new Rect(panel.x * scale, panel.y * scale, panel.width * scale, panel.height * scale);
            _drawnFrame = Time.frameCount;

            GUI.Box(panel, GUIContent.none, _boxStyle);
            float x = panel.x + Pad;
            float w = panel.width - 2 * Pad;
            float y = panel.y + Pad;

            GUI.Label(new Rect(x, y, w, HeaderHeight), "<b>OUTFIT SLOTS</b>", _titleStyle);
            GUI.Label(new Rect(x, y, w, HeaderHeight), $"{S.ToggleKey.Value} to close", _keyStyle);
            y += HeaderHeight + Gap;

            var list = new Rect(x, y, w, listHeight);
            if (Slots.Active)
            {
                DrawList(list);
            }
            else
            {
                GUI.Label(list, "Outfit Slots is off, because BlueSage QoL's own style slots 4-9 are on and both " +
                    "change the same part of the game.\n\nTo use this mod, set EnableExtendedAvatarStyles = false in " +
                    "BepInEx\\config\\com.bluesage.ontogether.qoltweaks.beta.cfg (or switch BlueSage's avatar styles " +
                    "off in its menu), then restart the game.", _messageStyle);
            }
            y += listHeight + Gap;

            DrawStatus(new Rect(x, y, w, StatusHeight));
        }

        private void DrawList(Rect area)
        {
            PlayerDataZip zip = Game.Data != null ? Game.Data.PlayerDataZip : null;
            int wearing = zip != null ? zip.SelectedStyleIndex : -1;
            for (int slot = 0; slot < Slots.Total; slot++)
            {
                var row = new Rect(area.x, area.y + slot * (RowHeight + Gap), area.width, RowHeight);
                bool on = slot == wearing;
                if (GUI.Button(row, GUIContent.none, on ? _wearingStyle : _rowStyle))
                {
                    _picked = slot;   // acted on in Update
                }

                // Drawn over the button: labels don't take clicks.
                GUI.Label(new Rect(row.x + 8f, row.y, NumberWidth, row.height), (slot + 1).ToString(), _numberStyle);
                string name = Slots.Name(zip, slot).Trim();
                var nameRect = new Rect(row.x + 8f + NumberWidth, row.y, row.width - 8f - NumberWidth - TagWidth, row.height);
                if (name.Length > 0)
                {
                    GUI.Label(nameRect, name, _nameStyle);
                }
                else
                {
                    GUI.Label(nameRect, Slots.IsEmpty(slot) ? "empty" : "no name", _dimStyle);
                }
                if (on)
                {
                    GUI.Label(new Rect(row.xMax - TagWidth - 6f, row.y, TagWidth, row.height), "wearing", _tagStyle);
                }
            }
        }

        private void DrawStatus(Rect row)
        {
            if (_status != null && Time.unscaledTime < _statusUntil)
            {
                GUI.Label(row, _status, _statusStyle);
            }
            else if (Slots.Active)
            {
                GUI.Label(row, "Click an outfit to put it on", _tipStyle);
            }
        }

        private void MakeStyles()
        {
            var dim = new Color(1f, 1f, 1f, 0.55f);

            _boxStyle = PanelStack.MakeStyle(TextAnchor.UpperLeft);
            _titleStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _titleStyle.richText = true;
            _keyStyle = Label(13, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleRight);
            _numberStyle = Label(15, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleLeft);
            _nameStyle = Label(15, Color.white, TextAnchor.MiddleLeft);
            _dimStyle = Label(15, dim, TextAnchor.MiddleLeft);
            _tagStyle = Label(13, new Color32(0x8C, 0xDC, 0x78, 0xFF), TextAnchor.MiddleRight);
            _tipStyle = Label(14, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleLeft);
            _statusStyle = Label(14, new Color32(0xFF, 0xD9, 0x66, 0xFF), TextAnchor.MiddleLeft);
            _messageStyle = Label(14, Color.white, TextAnchor.UpperLeft);
            _messageStyle.wordWrap = true;

            _rowStyle = new GUIStyle(GUI.skin.button);
            // The slot you're wearing looks pressed in.
            _wearingStyle = new GUIStyle(GUI.skin.button);
            _wearingStyle.normal = GUI.skin.button.onNormal;
            _wearingStyle.hover = GUI.skin.button.onHover;
            _wearingStyle.active = GUI.skin.button.onActive;
        }

        private static GUIStyle Label(int size, Color color, TextAnchor alignment)
        {
            // Names are shown as typed, never read as formatting tags.
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                alignment = alignment,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
