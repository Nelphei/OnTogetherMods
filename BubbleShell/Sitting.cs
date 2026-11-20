using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BubbleShell
{
    // One free-focus activity in your focus menu where you are (land or water).
    internal struct Activity
    {
        public int Index;        // into GameSettings' activity lists
        public FocusType Type;
        public int Number;       // 1, 2, 3... within its category, as the menu lists them
        public string Details;   // what comes with it: desk, book, floatie...
    }

    // Sits your character down in a free-focus activity, the same way the game does when you press
    // the focus key and click one. Copied from Buddy Up's Sitting (and Emote Sync's FocusJoin).
    // See reference/Assembly-CSharp/PlayerFocusController.cs (OpenSelector, SetFocus).
    internal static class Sitting
    {
        private static readonly AccessTools.FieldRef<PlayerFocusController, bool> IsFocusChange =
            AccessTools.FieldRefAccess<PlayerFocusController, bool>("_isFocusChange");

        private static readonly AccessTools.FieldRef<PlayerFocusController, List<FocusType>> SimpledFocusTypes =
            AccessTools.FieldRefAccess<PlayerFocusController, List<FocusType>>("_simpledFocusTypes");

        private static readonly AccessTools.FieldRef<PlayerFocusController, int> FocusMainIndex =
            AccessTools.FieldRefAccess<PlayerFocusController, int>("_currentFocusMainIndex");

        private static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        private static readonly Action<PlayerFocusController, FocusAreaController> SetCurrentFocusArea =
            AccessTools.MethodDelegate<Action<PlayerFocusController, FocusAreaController>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.CurrentFocusAreaController)));

        // Private. True when the ground you're standing on allows free focus.
        private static readonly Func<PlayerFocusController, bool> IsFocusable =
            AccessTools.MethodDelegate<Func<PlayerFocusController, bool>>(AccessTools.Method(typeof(PlayerFocusController), "IsFocusable"));

        // Your focus menu's activities where you are, in the menu's order.
        public static List<Activity> Activities()
        {
            var list = new List<Activity>();
            PlayerMovementController movement = Game.MyMovement;
            if (movement == null)
            {
                return list;
            }
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            List<FocusType> types = settings.FocusTypes;
            var counts = new Dictionary<FocusType, int>();
            for (int i = 0; i < types.Count && i < settings.AnimationStates.Count; i++)
            {
                if (!IsAllowed(i, movement, settings))
                {
                    continue;
                }
                counts.TryGetValue(types[i], out int seen);
                counts[types[i]] = seen + 1;
                list.Add(new Activity { Index = i, Type = types[i], Number = seen + 1, Details = Details(settings, i, movement) });
            }
            return list;
        }

        // Sits you down in it, or switches to it if you're already sitting free. Null when it did, else why not.
        public static string Sit(Activity activity)
        {
            PlayerFocusController focus = Game.MyFocus;
            PlayerMovementController movement = Game.MyMovement;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (focus == null || movement == null || tasks == null)
            {
                return "Join a world first.";
            }
            bool switching = focus.IsFocus;
            if (switching && (focus.CurrentFocusAreaController != null || ActiveFocusArea(focus) != null))
            {
                return "You're at a seat spot. Stand up first (stand).";
            }
            string busy = Busy(focus, movement, tasks, switching);
            if (busy != null)
            {
                return busy;
            }

            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            List<FocusType> types = settings.FocusTypes;
            FocusType category = types[activity.Index];

            // SetFocus takes the position within the chosen category, counting only allowed entries.
            int subIndex = 0;
            for (int i = 0; i < activity.Index; i++)
            {
                if (types[i] == category && IsAllowed(i, movement, settings))
                {
                    subIndex++;
                }
            }

            // What the focus menu sets up before you click.
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
            SetCurrentFocusArea(focus, null);
            IsFocusChange(focus) = switching;
            SimpledFocusTypes(focus) = categories;
            FocusMainIndex(focus) = categories.IndexOf(category);

            focus.SetFocus(subIndex);
            return focus.IsFocus ? null : "The game didn't start it.";
        }

        // The game's own "free to focus" checks, done here so we can say what's in the way (other mods
        // patch the game's checks to always pass).
        private static string Busy(PlayerFocusController focus, PlayerMovementController movement, TaskManager tasks, bool switching)
        {
            if (tasks.IsWriting)
            {
                return "Close the chat first.";
            }
            if (focus.IsSelectorOpened)
            {
                return "Close the focus menu first.";
            }
            if (tasks.LockState != LockState.Free)
            {
                return "Close the open menu first.";
            }
            if (switching)
            {
                return null;
            }
            if (NetworkSingleton<BasketBallManager>.I.IsInBasketBallCourtColorful)
            {
                return "Not on the basketball court.";
            }
            if (MonoSingleton<FishingManager>.I.FishingState != FishingState.None)
            {
                return "Stop fishing first.";
            }
            if (movement.IsAnimation)
            {
                return "Wait for the animation to finish.";
            }
            if (movement.IsStair)
            {
                return "Get off the stairs first.";
            }
            if (movement.MovementState == MovementState.Bubble)
            {
                return "Not while you're in a bubble.";
            }
            if ((movement.MovementState != MovementState.Idle && movement.MovementState != MovementState.Walk) ||
                (movement.GroundState != GroundState.Grounded && !movement.IsInWater))
            {
                return "Land first.";
            }
            // Where you're standing. Focus Anywhere lets you focus on any ground, so skip these with it.
            if (!Game.AnyGround())
            {
                MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
                if (focus.FocusAreaControllers.Count > 0 && !movement.IsLake)
                {
                    return "You're at a seat spot: use the focus key there.";
                }
                if (scene != null && (scene.MusicHillTransform.position - focus.transform.position).sqrMagnitude < 12.53f)
                {
                    return "Not on the music hill.";
                }
                if (!IsFocusable(focus))
                {
                    return "You can't focus on this ground.";
                }
            }
            return null;
        }

        // Summer DLC activities are hidden in water if you don't own the DLC.
        private static bool IsAllowed(int i, PlayerMovementController movement, GameSettings settings)
        {
            return !movement.IsInWater || i >= settings.FocusIsSummerDLC.Count || !settings.FocusIsSummerDLC[i] ||
                   MonoSingleton<DataManager>.I.IsHaveSummerDLC;
        }

        private static string Details(GameSettings settings, int i, PlayerMovementController movement)
        {
            var parts = new List<string>();
            if (i < settings.PropTypes.Count && settings.PropTypes[i] != PropType.None)
            {
                parts.Add(Words(settings.PropTypes[i].ToString()));
            }
            if (movement.IsInWater)
            {
                if (i < settings.WaterDlcPropTypes.Count && settings.WaterDlcPropTypes[i] != WaterPropType.None)
                {
                    parts.Add(Words(settings.WaterDlcPropTypes[i].ToString()));
                }
            }
            else if (i < settings.IsDesk.Count && settings.IsDesk[i])
            {
                parts.Add("desk");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "just you";
        }

        // "GameDeck" -> "game deck".
        private static string Words(string name)
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    text.Append(' ');
                }
                text.Append(char.ToLowerInvariant(name[i]));
            }
            return text.ToString();
        }
    }
}
