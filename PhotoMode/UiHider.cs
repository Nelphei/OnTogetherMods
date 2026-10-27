using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PhotoMode
{
    // Hides the game's UI by switching off Canvas components, and remembers exactly which
    // ones it switched off so it can turn back on only those. Nothing is destroyed or moved.
    internal sealed class UiHider
    {
        // Name tags and chat bubbles live on this canvas above each player.
        // See reference/Assembly-CSharp/TextBubbleController.cs.
        private static readonly AccessTools.FieldRef<TextBubbleController, Canvas> BubbleCanvas =
            AccessTools.FieldRefAccess<TextBubbleController, Canvas>("_textBubbleCanvas");

        private readonly List<Canvas> _hiddenScreenCanvases = new List<Canvas>();
        private readonly List<Canvas> _hiddenBubbleCanvases = new List<Canvas>();

        public bool NameTagsHidden { get; private set; }

        public void Hide(bool hideNameTags)
        {
            NameTagsHidden = hideNameTags;
            Refresh();
        }

        // Catches UI that appeared after photo mode started, like a notification popup
        // or a player who just joined. Cheap enough to call a couple of times a second.
        public void Refresh()
        {
            HideScreenCanvases();
            if (NameTagsHidden)
            {
                HideBubbleCanvases();
            }
        }

        public void SetNameTagsHidden(bool hidden)
        {
            NameTagsHidden = hidden;
            if (hidden)
            {
                HideBubbleCanvases();
            }
            else
            {
                Restore(_hiddenBubbleCanvases);
            }
        }

        public void RestoreAll()
        {
            Restore(_hiddenScreenCanvases);
            Restore(_hiddenBubbleCanvases);
        }

        private void HideScreenCanvases()
        {
            // Include inactive objects so panels that open later stay hidden too.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                // Skip canvases the game has already switched off (the settings menu uses this
                // to mean "closed"), and in-world canvases like chalkboards and signs.
                if (!canvas.enabled || IsInWorld(canvas))
                {
                    continue;
                }
                canvas.enabled = false;
                _hiddenScreenCanvases.Add(canvas);
            }
        }

        private void HideBubbleCanvases()
        {
            TextBubbleController[] bubbles = Object.FindObjectsByType<TextBubbleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TextBubbleController bubble in bubbles)
            {
                Canvas canvas = BubbleCanvas(bubble);
                if (canvas == null || !canvas.enabled)
                {
                    continue;
                }
                canvas.enabled = false;
                _hiddenBubbleCanvases.Add(canvas);
            }
        }

        private static void Restore(List<Canvas> canvases)
        {
            foreach (Canvas canvas in canvases)
            {
                // Unity's == null is also true for objects destroyed while we were hiding them.
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }
            canvases.Clear();
        }

        // A nested canvas follows the render mode of the outermost canvas above it. Unity only
        // reports rootCanvas for active objects, so walk up the parents ourselves.
        private static bool IsInWorld(Canvas canvas)
        {
            Canvas[] chain = canvas.GetComponentsInParent<Canvas>(includeInactive: true);
            Canvas outermost = chain.Length > 0 ? chain[chain.Length - 1] : canvas;
            return outermost.renderMode == RenderMode.WorldSpace;
        }
    }
}
