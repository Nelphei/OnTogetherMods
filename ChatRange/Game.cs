using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ChatRange
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/TextChannelManager.cs,
    // TextBubbleController.cs and UIManager.cs.
    internal static class Game
    {
        // Local chat messages and chat bubbles only show for players this close to the sender.
        // Each player's own game checks it when a message arrives, using where the sender stood
        // when they sent it (TextChannelManager.OnChannelMessageReceived and
        // TextBubbleController.ShowTextBubble skip it when the squared distance is over 25).
        // It's measured in 3D, so height counts too.
        public const float LocalRange = 5f;

        // The parts of the chat box. It isn't its own canvas: these sit on the game's main UI
        // canvas, inside the draggable _messagePanelParent.
        private static readonly AccessTools.FieldRef<UIManager, RectTransform> MessagePanel =
            AccessTools.FieldRefAccess<UIManager, RectTransform>("_messagePanelTransform");

        private static readonly AccessTools.FieldRef<UIManager, RectTransform> LocalTab =
            AccessTools.FieldRefAccess<UIManager, RectTransform>("_localButtonTransform");

        private static readonly AccessTools.FieldRef<UIManager, RectTransform> GlobalTab =
            AccessTools.FieldRefAccess<UIManager, RectTransform>("_globalButtonTransform");

        private static readonly AccessTools.FieldRef<UIManager, RectTransform> HideButton =
            AccessTools.FieldRefAccess<UIManager, RectTransform>("_hideButtonOnPanelTransform");

        private static readonly List<Canvas> Canvases = new List<Canvas>();
        private static readonly Vector3[] Corners = new Vector3[4];

        // Where the chat box is on screen, in 1080p units from the top-left (like TextBox and
        // PanelStack), tabs and typing box included. False while it isn't showing.
        public static bool ChatBounds(UIManager ui, out Rect bounds)
        {
            bounds = default;
            RectTransform panel = MessagePanel(ui);
            if (!IsShowing(panel, out _))
            {
                return false;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            Add(panel, ref min, ref max);
            Add(LocalTab(ui), ref min, ref max);
            Add(GlobalTab(ui), ref min, ref max);
            Add(HideButton(ui), ref min, ref max);
            Add(ui.MessageInputField != null ? ui.MessageInputField.transform as RectTransform : null, ref min, ref max);

            // Screen pixels count up from the bottom-left. TextBox is laid out from the top-left.
            float scale = 1080f / Screen.height;
            bounds = Rect.MinMaxRect(min.x * scale, (Screen.height - max.y) * scale,
                max.x * scale, (Screen.height - min.y) * scale);
            return true;
        }

        private static void Add(RectTransform part, ref Vector2 min, ref Vector2 max)
        {
            if (part == null || !IsShowing(part, out Camera camera))
            {
                return;
            }
            part.GetWorldCorners(Corners);
            foreach (Vector3 corner in Corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        // True if this part of the UI is on screen. The game minimises the chat box by switching
        // it off, and Photo Mode hides the game's UI by switching canvases off.
        private static bool IsShowing(RectTransform part, out Camera camera)
        {
            camera = null;
            if (part == null || !part.gameObject.activeInHierarchy)
            {
                return false;
            }
            part.GetComponentsInParent(true, Canvases);
            if (Canvases.Count == 0)
            {
                return false;
            }
            foreach (Canvas canvas in Canvases)
            {
                if (!canvas.enabled)
                {
                    return false;
                }
            }
            Canvas root = Canvases[0].rootCanvas;
            camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return true;
        }
    }
}
