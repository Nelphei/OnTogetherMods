using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutfitSlots
{
    // Adds rows 4-9 to the style list in the outfit screen, copied from row 3, and adds them to the
    // screen's own lists (_styleTexts, _styleSelectedBgs). The game's code (InitStyleDropdown,
    // ButtonSelectStyle, OnDeselectStyleInputField) then handles them like rows 1-3: picking,
    // highlighting and renaming.
    internal static class Dropdown
    {
        private const string RowPrefix = "OutfitSlots Row ";

        private static readonly AccessTools.FieldRef<CustomizationUIController, List<TextMeshProUGUI>> TextsField =
            AccessTools.FieldRefAccess<CustomizationUIController, List<TextMeshProUGUI>>("_styleTexts");
        private static readonly AccessTools.FieldRef<CustomizationUIController, List<GameObject>> BgsField =
            AccessTools.FieldRefAccess<CustomizationUIController, List<GameObject>>("_styleSelectedBgs");
        private static readonly AccessTools.FieldRef<CustomizationUIController, GameObject> PanelField =
            AccessTools.FieldRefAccess<CustomizationUIController, GameObject>("_styleDropdownPanel");

        // The screen whose list was last written to the log (DebugLog).
        private static CustomizationUIController _described;

        // Before the game fills in the list (InitStyleDropdown, at the end of the screen's Start).
        public static void Prepare(CustomizationUIController screen)
        {
            List<TextMeshProUGUI> texts = TextsField(screen);
            List<GameObject> bgs = BgsField(screen);
            if (texts == null || bgs == null)
            {
                return;
            }
            if (texts.Count < Slots.Total || bgs.Count < Slots.Total)
            {
                bool added = false;
                try
                {
                    added = AddRows(screen, texts, bgs);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError(e);
                }
                if (!added)
                {
                    // Must work, or the game's code would read past the end of its lists.
                    AddStandIns(screen, texts, bgs);
                }
            }

            // Only the slot you're on is highlighted. The game switches it on next.
            foreach (GameObject bg in bgs)
            {
                if (bg != null)
                {
                    bg.SetActive(false);
                }
            }
            DataManager data = Game.Data;
            if (data != null)
            {
                Slots.EnsureNames(data.PlayerDataZip);
            }
        }

        // When the list opens: keep it on screen, now that it's taller.
        public static void Opened(CustomizationUIController screen)
        {
            GameObject panel = PanelField(screen);
            if (panel == null || !panel.activeInHierarchy || !(panel.transform is RectTransform rect))
            {
                return;
            }
            Canvas.ForceUpdateCanvases();
            KeepOnScreen(rect);
            if (Plugin.Settings.DebugLog.Value && _described != screen)
            {
                _described = screen;
                Plugin.Log.LogInfo("The outfit screen's style list:\n" + Describe(panel.transform));
            }
        }

        // ---- Copying row 3 ----

        private static bool AddRows(CustomizationUIController screen, List<TextMeshProUGUI> texts, List<GameObject> bgs)
        {
            if (texts.Count != Slots.GameSlots || bgs.Count != Slots.GameSlots ||
                texts[0] == null || texts[1] == null || texts[2] == null || bgs[1] == null || bgs[2] == null)
            {
                Plugin.Log.LogWarning($"The outfit screen's style list has {texts.Count} rows, not the 3 this mod expects, " +
                    "so slots 4-9 are only in the quick-swap window.");
                return false;
            }
            Transform row = FindRow(texts[2].transform, bgs[2].transform, texts[1].transform);
            if (row == null || row.parent == null)
            {
                Plugin.Log.LogWarning("Couldn't find row 3 of the outfit screen's style list, so slots 4-9 are only in the quick-swap window.");
                return false;
            }
            Transform parent = row.parent;
            Transform rowAbove = FindRow(texts[1].transform, bgs[1].transform, texts[0].transform);
            string textPath = PathBelow(row, texts[2].transform);
            string bgPath = PathBelow(row, bgs[2].transform);
            bool laidOut = parent.GetComponent<LayoutGroup>() != null;
            var rowRect = row as RectTransform;
            Vector2 step = RowStep(rowRect, rowAbove as RectTransform, parent);

            var newTexts = new List<TextMeshProUGUI>();
            var newBgs = new List<GameObject>();
            var clones = new List<GameObject>();
            for (int slot = Slots.GameSlots; slot < Slots.Total; slot++)
            {
                GameObject clone = UnityEngine.Object.Instantiate(row.gameObject, parent, false);
                clones.Add(clone);
                clone.name = RowPrefix + (slot + 1);
                clone.transform.SetSiblingIndex(row.GetSiblingIndex() + slot - 2);
                if (!laidOut && rowRect != null && clone.transform is RectTransform cloneRect)
                {
                    cloneRect.anchoredPosition = rowRect.anchoredPosition + step * (slot - 2);
                }

                Transform bg = Find(clone.transform, bgPath);
                TextMeshProUGUI text = Find(clone.transform, textPath)?.GetComponent<TextMeshProUGUI>();
                if (text == null || bg == null || !Rewire(clone, screen, slot))
                {
                    foreach (GameObject made in clones)
                    {
                        UnityEngine.Object.Destroy(made);
                    }
                    Plugin.Log.LogWarning("Couldn't set up the copied rows in the outfit screen's style list, " +
                        "so slots 4-9 are only in the quick-swap window.");
                    return false;
                }
                SetNumber(clone, text, slot + 1);
                bg.gameObject.SetActive(false);
                newTexts.Add(text);
                newBgs.Add(bg.gameObject);
            }
            texts.AddRange(newTexts);
            bgs.AddRange(newBgs);

            float grow = Mathf.Abs(step.y) * (Slots.Total - Slots.GameSlots);
            string grown = Grow(PanelField(screen), parent, grow);
            Plugin.Log.LogInfo($"Added rows 4-9 to the outfit screen's style list (copied '{row.name}' in '{parent.name}', " +
                $"{(laidOut ? "laid out by the game" : "placed by the mod")}, {grown}).");
            return true;
        }

        // The row holding this text and highlight: the lowest parent of both that has a button and
        // doesn't also hold the row above's text.
        private static Transform FindRow(Transform text, Transform bg, Transform otherRowText)
        {
            for (Transform t = text; t != null; t = t.parent)
            {
                if (otherRowText != null && otherRowText.IsChildOf(t))
                {
                    return null;
                }
                if (bg.IsChildOf(t) && t != bg &&
                    (t.GetComponentInChildren<Button>(true) != null || t.GetComponentInChildren<EventTrigger>(true) != null))
                {
                    return t;
                }
            }
            return null;
        }

        // How far one row is from the next.
        private static Vector2 RowStep(RectTransform row, RectTransform rowAbove, Transform parent)
        {
            if (row != null && rowAbove != null && rowAbove.parent == parent)
            {
                Vector2 apart = row.anchoredPosition - rowAbove.anchoredPosition;
                if (apart.sqrMagnitude > 1f)
                {
                    return apart;
                }
            }
            float height = row != null ? row.rect.height : 40f;
            var column = parent.GetComponent<VerticalLayoutGroup>();
            return new Vector2(0f, -(height + (column != null ? column.spacing : 0f)));
        }

        // Points the copied row's click at its own slot. Row 3's buttons call ButtonSelectStyle(2).
        private static bool Rewire(GameObject clone, CustomizationUIController screen, int slot)
        {
            bool wired = false;
            Button[] buttons = clone.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (CallsSelectStyle(button.onClick))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(() => screen.ButtonSelectStyle(slot));
                    wired = true;
                }
            }
            foreach (EventTrigger trigger in clone.GetComponentsInChildren<EventTrigger>(true))
            {
                foreach (EventTrigger.Entry entry in trigger.triggers)
                {
                    if (CallsSelectStyle(entry.callback))
                    {
                        entry.callback = new EventTrigger.TriggerEvent();
                        entry.callback.AddListener(_ => screen.ButtonSelectStyle(slot));
                        wired = true;
                    }
                }
            }
            if (!wired && buttons.Length > 0)
            {
                buttons[0].onClick = new Button.ButtonClickedEvent();
                buttons[0].onClick.AddListener(() => screen.ButtonSelectStyle(slot));
                wired = true;
            }
            return wired;
        }

        private static bool CallsSelectStyle(UnityEventBase clicked)
        {
            for (int i = 0; i < clicked.GetPersistentEventCount(); i++)
            {
                if (clicked.GetPersistentMethodName(i) == nameof(CustomizationUIController.ButtonSelectStyle))
                {
                    return true;
                }
            }
            return false;
        }

        // A row's own number ("3"), if it shows one apart from its name.
        private static void SetNumber(GameObject clone, TextMeshProUGUI nameText, int number)
        {
            foreach (TextMeshProUGUI label in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string shown = (label.text ?? "").Trim().TrimEnd('-', '.', ' ');
                if (label != nameText && shown == "3")
                {
                    label.text = label.text.Replace("3", number.ToString());
                }
            }
        }

        // Makes room for 6 more rows, unless the list sizes itself.
        private static string Grow(GameObject panelObject, Transform list, float amount)
        {
            var panel = panelObject != null ? panelObject.transform as RectTransform : null;
            var listRect = list as RectTransform;
            if (amount < 1f)
            {
                return "not resized";
            }
            var grown = new List<string>();
            if (panel != null && panel.GetComponent<ContentSizeFitter>() == null)
            {
                float before = panel.rect.height;
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, before + amount);
                grown.Add($"'{panel.name}' {before:0} to {panel.rect.height:0} tall");
            }
            // A list stretched inside the panel grows with it. One with its own height needs growing too.
            bool followsPanel = panel != null && listRect != null && listRect.IsChildOf(panel) &&
                                !Mathf.Approximately(listRect.anchorMin.y, listRect.anchorMax.y);
            if (listRect != null && listRect != panel && !followsPanel && listRect.GetComponent<ContentSizeFitter>() == null)
            {
                float before = listRect.rect.height;
                listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, before + amount);
                grown.Add($"'{listRect.name}' {before:0} to {listRect.rect.height:0} tall");
            }
            if (listRect != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(listRect);
            }
            return grown.Count > 0 ? string.Join(", ", grown) : "sizes itself";
        }

        // Moves the list up or down so all of it is on screen.
        private static void KeepOnScreen(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }
            canvas = canvas.rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float bottom = float.MaxValue;
            float top = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                float y = RectTransformUtility.WorldToScreenPoint(camera, corner).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
            const float margin = 8f;
            float shift = 0f;
            if (top - bottom > Screen.height - 2 * margin)
            {
                shift = Screen.height - margin - top;   // taller than the screen: keep the top on it
            }
            else if (bottom < margin)
            {
                shift = margin - bottom;
            }
            else if (top > Screen.height - margin)
            {
                shift = Screen.height - margin - top;
            }
            if (Mathf.Abs(shift) < 0.5f || !(rect.parent is RectTransform parent))
            {
                return;
            }
            Vector2 pivot = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, pivot + new Vector2(0f, shift), camera, out Vector3 moved))
            {
                rect.position = moved;
            }
        }

        // ---- Without the copied rows ----

        // Slots 4-9 still work from the quick-swap window. The game's code gets hidden stand-ins, so
        // it never reads past the end of its lists while you're on one of them.
        private static void AddStandIns(CustomizationUIController screen, List<TextMeshProUGUI> texts, List<GameObject> bgs)
        {
            var holder = new GameObject("OutfitSlots Hidden Rows");
            holder.SetActive(false);
            holder.transform.SetParent(screen.transform, false);
            while (texts.Count < Slots.Total)
            {
                var label = new GameObject("Name " + (texts.Count + 1));
                label.transform.SetParent(holder.transform, false);
                texts.Add(label.AddComponent<TextMeshProUGUI>());
            }
            while (bgs.Count < Slots.Total)
            {
                var bg = new GameObject("Highlight " + (bgs.Count + 1));
                bg.transform.SetParent(holder.transform, false);
                bgs.Add(bg);
            }
        }

        // ---- Helpers ----

        private static string PathBelow(Transform root, Transform child)
        {
            var names = new List<string>();
            for (Transform t = child; t != null && t != root; t = t.parent)
            {
                names.Add(t.name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static Transform Find(Transform root, string path) => path.Length == 0 ? root : root.Find(path);

        // Every object under the list, with its size and parts, for the log.
        private static string Describe(Transform root)
        {
            var text = new StringBuilder();
            Describe(root, 0, text);
            return text.ToString();
        }

        private static void Describe(Transform t, int depth, StringBuilder text)
        {
            text.Append(' ', depth * 2).Append(t.name);
            if (!t.gameObject.activeSelf)
            {
                text.Append(" (off)");
            }
            if (t is RectTransform rect)
            {
                text.Append($" {rect.rect.width:0}x{rect.rect.height:0} at {rect.anchoredPosition}");
            }
            foreach (Component part in t.GetComponents<Component>())
            {
                if (part != null && !(part is Transform))
                {
                    text.Append(" [").Append(part.GetType().Name).Append(']');
                }
            }
            text.Append('\n');
            foreach (Transform child in t)
            {
                Describe(child, depth + 1, text);
            }
        }
    }
}
