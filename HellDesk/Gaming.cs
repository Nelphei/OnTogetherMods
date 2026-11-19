using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace HellDesk
{
    // Sits your character down with the handheld console (the Connect activity with the game deck)
    // when you open DOOM, the same way the game does when you pick it in the focus menu. Only from
    // standing: if you're already sitting (at your desk, a seat spot, flying with Desk Swarm...),
    // nothing changes, and closing DOOM never stands you up.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (SetFocus) and TaskManager.cs
    // (IsReadyForFreeFocus). Emote Sync's FocusJoin does the same for focus activities.
    internal static class Gaming
    {
        private const string FocusAnywhereGuid = "nelphei.ontogether.focusanywhere";

        private static readonly AccessTools.FieldRef<PlayerFocusController, bool> IsFocusChange =
            AccessTools.FieldRefAccess<PlayerFocusController, bool>("_isFocusChange");

        private static readonly AccessTools.FieldRef<PlayerFocusController, List<FocusType>> SimpledFocusTypes =
            AccessTools.FieldRefAccess<PlayerFocusController, List<FocusType>>("_simpledFocusTypes");

        private static readonly AccessTools.FieldRef<PlayerFocusController, int> FocusMainIndex =
            AccessTools.FieldRefAccess<PlayerFocusController, int>("_currentFocusMainIndex");

        private static readonly Action<PlayerFocusController, FocusAreaController> SetCurrentFocusArea =
            AccessTools.MethodDelegate<Action<PlayerFocusController, FocusAreaController>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.CurrentFocusAreaController)));

        // Private. True when the ground you're standing on allows free focus.
        private static readonly Func<PlayerFocusController, bool> IsFocusable =
            AccessTools.MethodDelegate<Func<PlayerFocusController, bool>>(AccessTools.Method(typeof(PlayerFocusController), "IsFocusable"));

        private static bool? _anyGround;

        // Sits you down if you're free to. Returns why not (for the log), or null when it did.
        public static string TrySitDown()
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (channel == null || tasks == null || channel.MainFocusController == null || channel.MainMovementController == null)
            {
                return "the game isn't ready";
            }
            PlayerFocusController focus = channel.MainFocusController;
            PlayerMovementController movement = channel.MainMovementController;

            if (focus.IsFocus)
            {
                return "you're already sitting";
            }
            string busy = Busy(tasks, focus, movement);
            if (busy != null)
            {
                return busy;
            }

            // The handheld console in the activity list for where you are (land or water). Focus
            // Anywhere may have added it to lists that don't normally have it. The game files it
            // under Connect (land: animation 8, with the personal desk; water: 110, on the floatie),
            // so take the category from the entry rather than assuming one.
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            List<FocusType> types = settings.FocusTypes;
            List<PropType> props = settings.PropTypes;
            int index = -1;
            for (int i = 0; i < types.Count && i < props.Count; i++)
            {
                if (props[i] == PropType.GameDeck && IsAllowed(i, movement, settings))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                return "the handheld console isn't in your focus menu here";
            }
            FocusType category = types[index];

            // SetFocus takes the position within the chosen category, counting only allowed entries.
            int subIndex = 0;
            for (int i = 0; i < index; i++)
            {
                if (types[i] == category && IsAllowed(i, movement, settings))
                {
                    subIndex++;
                }
            }

            // What the focus menu sets up before you click.
            if (NetworkSingleton<BasketBallManager>.I.IsBallOnMyHand)
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
            IsFocusChange(focus) = false;
            SimpledFocusTypes(focus) = categories;
            FocusMainIndex(focus) = categories.IndexOf(category);

            focus.SetFocus(subIndex);
            return focus.IsFocus ? null : "the game didn't start it";
        }

        // The game's own "free to focus" checks (other mods patch the game's to always pass).
        private static string Busy(TaskManager tasks, PlayerFocusController focus, PlayerMovementController movement)
        {
            if (tasks.IsWriting)
            {
                return "you're typing";
            }
            if (focus.IsSelectorOpened)
            {
                return "the focus menu is open";
            }
            if (tasks.LockState != LockState.Free)
            {
                return "a game window is open";
            }
            if (NetworkSingleton<BasketBallManager>.I.IsInBasketBallCourtColorful)
            {
                return "you're on the basketball court";
            }
            if (MonoSingleton<FishingManager>.I.FishingState != FishingState.None)
            {
                return "you're fishing";
            }
            if (movement.IsAnimation)
            {
                return "you're in the middle of an animation";
            }
            if (movement.IsStair)
            {
                return "you're on the stairs";
            }
            if (movement.MovementState == MovementState.Bubble)
            {
                return "you're in a bubble";
            }
            if ((movement.MovementState != MovementState.Idle && movement.MovementState != MovementState.Walk) ||
                movement.GroundState != GroundState.Grounded)
            {
                return "you're in the air";
            }

            // Where you're standing. Focus Anywhere lets you focus on any ground, so skip these with it.
            if (!AnyGround())
            {
                MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
                if (focus.FocusAreaControllers.Count > 0 && !movement.IsLake)
                {
                    return "you're at a seat spot";
                }
                if (scene != null && (scene.MusicHillTransform.position - focus.transform.position).sqrMagnitude < 12.53f)
                {
                    return "you're on the music hill";
                }
                if (!IsFocusable(focus))
                {
                    return "you can't focus on this ground";
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

        // True when Focus Anywhere is installed with its "any ground" setting on. Read once: that
        // setting only changes when the game restarts.
        private static bool AnyGround()
        {
            if (_anyGround == null)
            {
                _anyGround = false;
                if (Chainloader.PluginInfos.TryGetValue(FocusAnywhereGuid, out PluginInfo info) && info.Instance != null)
                {
                    _anyGround = !info.Instance.Config.TryGetEntry("Focus", "AnyGround", out ConfigEntry<bool> entry) || entry.Value;
                }
            }
            return _anyGround.Value;
        }
    }
}
