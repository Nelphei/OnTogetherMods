using System.Collections.Generic;
using HarmonyLib;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.UI;

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

        // The chat box isn't its own canvas. It's these objects, sitting on the game's main UI
        // canvas next to everything else. See UIManager.cs (ButtonHide, MessageDrag).
        private static readonly string[] ChatFields =
        {
            "_messagePanelParent", "_messagePanel", "_messagePanelTransform", "_messageInputField",
            "_globalPanel", "_localPanel", "_globalMessagePanel", "_localMessagePanel",
        };

        private readonly List<Canvas> _hiddenScreenCanvases = new List<Canvas>();
        private readonly List<Canvas> _hiddenBubbleCanvases = new List<Canvas>();

        // While chat is shown, we turn its canvases back on, then hide everything else on them
        // by adding a switched-off Canvas to each non-chat object (a disabled Canvas hides
        // everything under it). Chat is read-only: its raycasters are off, so clicks and
        // scrolling can't focus the chat box or start typing.
        private readonly List<Canvas> _chatCanvases = new List<Canvas>();
        private readonly List<Canvas> _blockers = new List<Canvas>();
        private readonly List<GraphicRaycaster> _disabledRaycasters = new List<GraphicRaycaster>();
        private readonly List<Transform> _chatRoots = new List<Transform>();
        private readonly HashSet<Transform> _chatPath = new HashSet<Transform>();

        public bool NameTagsHidden { get; private set; }
        public bool ChatShown { get; private set; }

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
            if (ChatShown)
            {
                HideAllButChat();
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

        // Returns false if the chat box couldn't be found (for example after a game update).
        public bool SetChatShown(bool shown)
        {
            if (shown == ChatShown)
            {
                return true;
            }

            if (shown)
            {
                if (!FindChat())
                {
                    return false;
                }
                ChatShown = true;

                for (int i = _hiddenScreenCanvases.Count - 1; i >= 0; i--)
                {
                    Canvas canvas = _hiddenScreenCanvases[i];
                    if (canvas != null && IsChat(canvas.transform))
                    {
                        canvas.enabled = true;
                        _chatCanvases.Add(canvas);
                        _hiddenScreenCanvases.RemoveAt(i);
                        DisableRaycaster(canvas);
                    }
                }
                HideAllButChat();
            }
            else
            {
                ChatShown = false;
                StopShowingChat();
                foreach (Canvas canvas in _chatCanvases)
                {
                    if (canvas != null && canvas.enabled)
                    {
                        canvas.enabled = false;
                        _hiddenScreenCanvases.Add(canvas);
                    }
                }
                _chatCanvases.Clear();
                HideScreenCanvases();
            }
            return true;
        }

        public void RestoreAll()
        {
            // Chat canvases were on before photo mode, and they're on now, so just forget them.
            ChatShown = false;
            StopShowingChat();
            _chatCanvases.Clear();

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
                // Other mods' text boxes stay, like their windows, and hide themselves for the photo.
                if (PanelStack.IsModCanvas(canvas))
                {
                    continue;
                }
                if (ChatShown && IsChat(canvas.transform))
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

        private bool FindChat()
        {
            _chatRoots.Clear();
            _chatPath.Clear();

            UIManager ui = MonoSingleton<UIManager>.I;
            if (ui == null)
            {
                return false;
            }

            foreach (string name in ChatFields)
            {
                Transform root = AsTransform(AccessTools.Field(typeof(UIManager), name)?.GetValue(ui));
                if (root == null)
                {
                    Plugin.Log.LogWarning($"Couldn't find UIManager.{name}. The game may have changed its chat box.");
                    continue;
                }
                _chatRoots.Add(root);
                for (Transform t = root; t != null; t = t.parent)
                {
                    _chatPath.Add(t);
                }
            }
            return _chatRoots.Count > 0;
        }

        // Walk from each chat object up to its outermost canvas, hiding every sibling along
        // the way that isn't itself part of the chat or on the way to it.
        private void HideAllButChat()
        {
            foreach (Transform root in _chatRoots)
            {
                // Nothing to hide if the chat object isn't on a canvas, or is a whole canvas itself.
                Canvas topCanvas = root != null ? OutermostCanvas(root) : null;
                if (topCanvas == null || topCanvas.transform == root)
                {
                    continue;
                }
                Transform top = topCanvas.transform;
                for (Transform node = root.parent; node != null; node = node.parent)
                {
                    if (!IsInsideChat(node))
                    {
                        HideNonChatChildren(node);
                    }
                    if (node == top)
                    {
                        break;
                    }
                }
            }
        }

        private void HideNonChatChildren(Transform node)
        {
            foreach (Transform child in node)
            {
                // Children with their own canvas are already handled by HideScreenCanvases.
                // Plain Transforms can't take a Canvas without being converted, so leave them.
                if (_chatPath.Contains(child) || !(child is RectTransform) || child.GetComponent<Canvas>() != null)
                {
                    continue;
                }
                Canvas blocker = child.gameObject.AddComponent<Canvas>();
                if (blocker != null)
                {
                    blocker.enabled = false;
                    _blockers.Add(blocker);
                }
            }
        }

        private void DisableRaycaster(Canvas canvas)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null && raycaster.enabled)
            {
                raycaster.enabled = false;
                _disabledRaycasters.Add(raycaster);
            }
        }

        private void StopShowingChat()
        {
            foreach (Canvas blocker in _blockers)
            {
                if (blocker != null)
                {
                    Object.Destroy(blocker);
                }
            }
            _blockers.Clear();

            foreach (GraphicRaycaster raycaster in _disabledRaycasters)
            {
                if (raycaster != null)
                {
                    raycaster.enabled = true;
                }
            }
            _disabledRaycasters.Clear();

            _chatRoots.Clear();
            _chatPath.Clear();
        }

        // True for chat objects, anything inside them, and the parents above them.
        private bool IsChat(Transform t)
        {
            return _chatPath.Contains(t) || IsInsideChat(t);
        }

        private bool IsInsideChat(Transform t)
        {
            foreach (Transform root in _chatRoots)
            {
                if (root != null && t.IsChildOf(root))
                {
                    return true;
                }
            }
            return false;
        }

        private static Transform AsTransform(object value)
        {
            if (value is GameObject go && go != null)
            {
                return go.transform;
            }
            if (value is Component component && component != null)
            {
                return component.transform;
            }
            return null;
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

        private static bool IsInWorld(Canvas canvas)
        {
            Canvas outermost = OutermostCanvas(canvas.transform);
            return outermost != null && outermost.renderMode == RenderMode.WorldSpace;
        }

        // A nested canvas follows the render mode of the outermost canvas above it. Unity only
        // reports rootCanvas for active objects, so walk up the parents ourselves.
        private static Canvas OutermostCanvas(Transform t)
        {
            Canvas[] chain = t.GetComponentsInParent<Canvas>(includeInactive: true);
            return chain.Length > 0 ? chain[chain.Length - 1] : null;
        }
    }
}
