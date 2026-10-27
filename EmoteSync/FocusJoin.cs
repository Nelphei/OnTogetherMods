using System.Collections.Generic;
using UnityEngine;

namespace EmoteSync
{
    // Starts (or switches to) a free-focus activity on our own character, the same way the game
    // does when you press the focus key and click an activity. Only free focus: focus spots like
    // classroom desks have their own seats and activity lists, so players at one are skipped.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (Update, OpenSelector, SetFocus).
    internal static class FocusJoin
    {
        // True while we're starting a focus for the group, so the SetFocus patch doesn't send it again.
        public static bool Joining { get; private set; }

        // Returns null when we're now doing the activity, or a short reason why not.
        public static string TryJoin(int animState, FocusType focusType)
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (channel == null || tasks == null || channel.MainFocusController == null || channel.MainMovementController == null)
            {
                return "not ready";
            }
            PlayerFocusController focus = channel.MainFocusController;
            PlayerMovementController movement = channel.MainMovementController;

            if (focus.IsSelectorOpened || tasks.LockState != LockState.Free)
            {
                return "you're busy";
            }

            bool switching = focus.IsFocus;
            if (switching)
            {
                // Same as pressing the focus key while focusing, then picking another activity.
                if (Game.ActiveFocusArea(focus) != null)
                {
                    return "you're at a focus spot";
                }
                if (!tasks.IsReadyForFocusQuit())
                {
                    return "you're busy";
                }
                if (channel.MainAnimationController.GetState() == animState)
                {
                    return null;
                }
            }
            else
            {
                // The checks the game runs before it opens the free-focus selector.
                if (!tasks.IsReadyForAreaFocus() || !tasks.IsReadyForFreeFocus())
                {
                    return "you're busy";
                }
                if (focus.FocusAreaControllers.Count > 0 && !movement.IsLake)
                {
                    return "you're at a focus spot";
                }
                MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
                bool nearMusicHill = scene != null &&
                    (scene.MusicHillTransform.position - focus.transform.position).sqrMagnitude < 12.53f;
                if (nearMusicHill || !Game.IsFocusable(focus))
                {
                    return "you can't focus here";
                }
            }

            // Find the activity in our own list (it differs on land and in water). Prefer the
            // same category, since a few animations appear under more than one.
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            List<FocusType> types = settings.FocusTypes;
            List<int> anims = settings.AnimationStates;
            int index = Find(types, anims, animState, focusType, movement, settings, sameType: true);
            if (index < 0)
            {
                index = Find(types, anims, animState, focusType, movement, settings, sameType: false);
            }
            if (index < 0)
            {
                return "that activity isn't available here";
            }

            // SetFocus takes the position within the chosen category, counting only allowed entries.
            FocusType chosenType = types[index];
            int subIndex = 0;
            for (int i = 0; i < index; i++)
            {
                if (types[i] == chosenType && IsAllowed(i, movement, settings))
                {
                    subIndex++;
                }
            }

            // What the selector sets up before you click.
            if (!switching && NetworkSingleton<BasketBallManager>.I.IsBallOnMyHand)
            {
                NetworkSingleton<BasketBallManager>.I.DropBall();
            }
            var categories = new List<FocusType>();
            foreach (FocusType type in types)
            {
                if (!categories.Contains(type))
                {
                    categories.Add(type);
                }
            }
            Game.SetCurrentFocusArea(focus, null);
            Game.IsFocusChange(focus) = switching;
            Game.SimpledFocusTypes(focus) = categories;
            Game.FocusMainIndex(focus) = categories.IndexOf(chosenType);

            Joining = true;
            try
            {
                focus.SetFocus(subIndex);
            }
            finally
            {
                Joining = false;
            }
            return focus.IsFocus ? null : "the game didn't start it";
        }

        private static int Find(List<FocusType> types, List<int> anims, int animState, FocusType focusType,
            PlayerMovementController movement, GameSettings settings, bool sameType)
        {
            for (int i = 0; i < types.Count && i < anims.Count; i++)
            {
                if (anims[i] == animState && (!sameType || types[i] == focusType) && IsAllowed(i, movement, settings))
                {
                    return i;
                }
            }
            return -1;
        }

        // Summer DLC activities are hidden in water if you don't own the DLC.
        private static bool IsAllowed(int i, PlayerMovementController movement, GameSettings settings)
        {
            return !movement.IsInWater || !settings.FocusIsSummerDLC[i] || MonoSingleton<DataManager>.I.IsHaveSummerDLC;
        }
    }
}
