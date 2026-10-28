using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FocusAnywhere
{
    // The focus menu's second row (the activities in one category) has 11 buttons, and with every
    // activity merged the biggest category needs 12. This adds copies of the last button.
    // Each game button calls UIManager.ButtonSetFocus(n) with its own fixed n, so a copy has that
    // call switched off and calls ButtonSetFocus with its own number instead.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (OnSpawned, ButtonFocusItem) and UIManager.cs.
    internal static class MenuSlots
    {
        // How many buttons the game's menu has and fills itself.
        private const int GameSlots = 11;

        public static void EnsureSlots(int needed)
        {
            UIManager ui = MonoSingleton<UIManager>.I;
            RectTransform parent = ui != null ? ui.FocusSubItemParent : null;
            if (parent == null || parent.childCount < GameSlots)
            {
                return;
            }
            while (parent.childCount < needed)
            {
                AddSlot(parent);
            }
        }

        // The game only hides the buttons it knows about, so ours start hidden and the game's
        // fill loop shows the ones a category uses.
        public static void HideExtraSlots()
        {
            UIManager ui = MonoSingleton<UIManager>.I;
            RectTransform parent = ui != null ? ui.FocusSubItemParent : null;
            if (parent == null)
            {
                return;
            }
            for (int i = GameSlots; i < parent.childCount; i++)
            {
                parent.GetChild(i).gameObject.SetActive(false);
            }
        }

        private static void AddSlot(RectTransform parent)
        {
            Transform template = parent.GetChild(GameSlots - 1);
            GameObject slot = Object.Instantiate(template.gameObject, parent, false);
            int index = parent.childCount - 1;
            slot.name = $"{template.name} (Focus Anywhere {index})";
            slot.SetActive(false);

            int switchedOff = 0;
            string wiring = "";
            Button button = slot.GetComponent<Button>();
            if (button != null)
            {
                switchedOff += SwitchOffSetFocus(button.onClick, ref wiring);
                button.onClick.AddListener(() => Pick(index));
            }

            // Hover: light this button and no other. The game's own hover code only knows 11 buttons.
            EventTrigger trigger = slot.GetComponent<EventTrigger>() ?? slot.AddComponent<EventTrigger>();
            foreach (EventTrigger.Entry entry in trigger.triggers)
            {
                switchedOff += SwitchOffSetFocus(entry.callback, ref wiring);
            }
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => Highlight(parent, index));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetHighlight(parent.GetChild(index), false));

            if (switchedOff == 0)
            {
                Plugin.Log.LogWarning($"Added focus menu button {index + 1}, but couldn't find its copied click call " +
                    $"(saved calls:{wiring}). It may start the wrong activity.");
            }
            else
            {
                Plugin.Log.LogInfo($"Added focus menu button {index + 1}." +
                    (Plugin.Settings.DebugLog.Value ? $" Saved calls:{wiring}" : ""));
            }
        }

        private static void Pick(int index)
        {
            try
            {
                MonoSingleton<UIManager>.I.ButtonSetFocus(index);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError(e);
            }
        }

        private static void Highlight(RectTransform parent, int index)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                SetHighlight(parent.GetChild(i), i == index);
            }
        }

        // Child 1 of a button is its hover outline.
        private static void SetHighlight(Transform slot, bool on)
        {
            if (slot.childCount > 1)
            {
                slot.GetChild(1).gameObject.SetActive(on);
            }
        }

        // Turns off the copied "ButtonSetFocus(n)" call, keeping anything else (like a click sound).
        private static int SwitchOffSetFocus(UnityEventBase unityEvent, ref string wiring)
        {
            int count = 0;
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                string method = unityEvent.GetPersistentMethodName(i) ?? "";
                Object target = unityEvent.GetPersistentTarget(i);
                wiring += $" {(target != null ? target.GetType().Name : "null")}.{method}";
                if (method.Contains("SetFocus"))
                {
                    unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
                    count++;
                }
            }
            return count;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ =>
            {
                try
                {
                    action();
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError(e);
                }
            });
            trigger.triggers.Add(entry);
        }
    }
}
