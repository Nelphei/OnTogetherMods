using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace BuddyUp
{
    internal enum SitResult
    {
        Sat,
        Wait,   // busy right now (typing, a menu open...); try again in a moment
        Skip,   // can't sit down in that one here at all
    }

    // Sits your own character down in a free-focus activity (or switches to it), the same way the
    // game does when you press the focus key and click an activity. Emote Sync's FocusJoin and Doom
    // Together's Gaming do the same.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (Update, OpenSelector, SetFocus) and
    // TaskManager.cs (IsReadyForFreeFocus).
    internal static class Sitting
    {
        private const string FocusAnywhereGuid = "nelphei.ontogether.focusanywhere";

        private static readonly AccessTools.FieldRef<PlayerFocusController, bool> IsFocusChange =
            AccessTools.FieldRefAccess<PlayerFocusController, bool>("_isFocusChange");

        private static readonly AccessTools.FieldRef<PlayerFocusController, List<FocusType>> SimpledFocusTypes =
            AccessTools.FieldRefAccess<PlayerFocusController, List<FocusType>>("_simpledFocusTypes");

        private static readonly AccessTools.FieldRef<PlayerFocusController, int> FocusMainIndex =
            AccessTools.FieldRefAccess<PlayerFocusController, int>("_currentFocusMainIndex");

        private static readonly System.Action<PlayerFocusController, FocusAreaController> SetCurrentFocusArea =
            AccessTools.MethodDelegate<System.Action<PlayerFocusController, FocusAreaController>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.CurrentFocusAreaController)));

        // Private. True when the ground you're standing on allows free focus.
        private static readonly System.Func<PlayerFocusController, bool> IsFocusable =
            AccessTools.MethodDelegate<System.Func<PlayerFocusController, bool>>(AccessTools.Method(typeof(PlayerFocusController), "IsFocusable"));

        private static bool? _anyGround;

        // True while the mod sits you down to ride, so you don't tell your buddies you sat down.
        public static bool ForRide { get; private set; }

        // Sits you down in the activity your buddy picked (category + animation), or switches to it.
        public static SitResult Sit(FocusType type, int anim, out string why)
        {
            if (!Ready(out PlayerFocusController focus, out PlayerMovementController movement, out why))
            {
                return SitResult.Wait;
            }
            bool switching = focus.IsFocus;
            if (switching && !Game.SitsFree(focus))
            {
                why = "you're at a seat spot";
                return SitResult.Skip;
            }
            if (switching && Game.MyAnim() == anim)
            {
                return SitResult.Sat;   // already doing it (Emote Sync may have got there first)
            }
            why = Busy(focus, movement, switching, groundRules: true, out bool wait);
            if (why != null)
            {
                return wait ? SitResult.Wait : SitResult.Skip;
            }

            // Find it in your own list (land and water differ). Prefer the same category, since a
            // few animations show up under more than one.
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            int index = Find(settings, movement, i => settings.AnimationStates[i] == anim && settings.FocusTypes[i] == type);
            if (index < 0)
            {
                index = Find(settings, movement, i => settings.AnimationStates[i] == anim);
            }
            if (index < 0)
            {
                why = "that activity isn't in your focus menu here";
                return SitResult.Skip;
            }
            return SitAt(focus, index, switching, out why);
        }

        // Sits you down to ride on someone's head, in the riding activity (RideActivity, RideChoice),
        // and says which pose that is. The ground's rules don't matter: you're moved up onto the head
        // straight away. Wait means try again in a moment (a menu open, mid-step off the ground...).
        public static SitResult SitToRide(out string why, out int anim)
        {
            anim = -1;
            if (!Ready(out PlayerFocusController focus, out PlayerMovementController movement, out why))
            {
                return SitResult.Wait;
            }
            if (focus.IsFocus)
            {
                anim = Game.MyAnim();
                return SitResult.Sat;
            }
            why = Busy(focus, movement, switching: false, groundRules: false, out bool wait);
            if (why != null)
            {
                return wait ? SitResult.Wait : SitResult.Skip;
            }

            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            FocusType kind = Plugin.Settings.RideActivity.Value;
            int choice = Plugin.Settings.RideChoice.Value;
            int index = -1;
            if (choice > 0)
            {
                // The n-th of that kind, as the menu counts them.
                int seen = 0;
                index = Find(settings, movement, i => settings.FocusTypes[i] == kind && ++seen == choice);
            }
            if (index < 0)
            {
                index = Find(settings, movement, i => settings.FocusTypes[i] == kind && Plain(settings, i, movement));
            }
            if (index < 0)
            {
                index = Find(settings, movement, i => Plain(settings, i, movement));
            }
            if (index < 0)
            {
                index = Find(settings, movement, i => settings.FocusTypes[i] == kind);
            }
            if (index < 0)
            {
                index = Find(settings, movement, i => true);
            }
            if (index < 0)
            {
                why = "your focus menu is empty here";
                return SitResult.Skip;
            }

            anim = settings.AnimationStates[index];
            ForRide = true;
            try
            {
                return SitAt(focus, index, switching: false, out why);
            }
            finally
            {
                ForRide = false;
            }
        }

        private static SitResult SitAt(PlayerFocusController focus, int index, bool switching, out string why)
        {
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            PlayerMovementController movement = NetworkSingleton<TextChannelManager>.I.MainMovementController;
            List<FocusType> types = settings.FocusTypes;

            // SetFocus takes the position within the chosen category, counting only allowed entries.
            FocusType category = types[index];
            int subIndex = 0;
            for (int i = 0; i < index; i++)
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
            why = focus.IsFocus ? null : "the game didn't start it";
            return focus.IsFocus ? SitResult.Sat : SitResult.Skip;
        }

        private static bool Ready(out PlayerFocusController focus, out PlayerMovementController movement, out string why)
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            focus = channel != null ? channel.MainFocusController : null;
            movement = channel != null ? channel.MainMovementController : null;
            if (focus == null || movement == null || MonoSingleton<TaskManager>.I == null)
            {
                why = "the game isn't ready";
                return false;
            }
            why = null;
            return true;
        }

        // The game's own "free to focus" checks, done here so we can say what's in the way (other
        // mods patch the game's checks to always pass). Returns null when you're free; `wait` says
        // whether it's worth trying again in a moment.
        private static string Busy(PlayerFocusController focus, PlayerMovementController movement, bool switching,
            bool groundRules, out bool wait)
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            wait = true;
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
            if (switching)
            {
                return null;
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
            // Mid-jump the game's own timers would soon swap your pose for falling, so not even to ride.
            if ((movement.MovementState != MovementState.Idle && movement.MovementState != MovementState.Walk) ||
                (movement.GroundState != GroundState.Grounded && !movement.IsInWater))
            {
                return "you're in the air";
            }

            // Where you're standing. Focus Anywhere lets you focus on any ground, so skip these with it.
            if (groundRules && !AnyGround())
            {
                wait = false;
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

        // The first allowed entry of your current activity list that matches.
        private static int Find(GameSettings settings, PlayerMovementController movement, System.Func<int, bool> match)
        {
            List<FocusType> types = settings.FocusTypes;
            List<int> anims = settings.AnimationStates;
            for (int i = 0; i < types.Count && i < anims.Count; i++)
            {
                if (IsAllowed(i, movement, settings) && match(i))
                {
                    return i;
                }
            }
            return -1;
        }

        // No desk, nothing in your hands and no floatie: just you, sitting.
        private static bool Plain(GameSettings settings, int i, PlayerMovementController movement)
        {
            if (i < settings.PropTypes.Count && settings.PropTypes[i] != PropType.None)
            {
                return false;
            }
            if (movement.IsInWater)
            {
                return i >= settings.WaterDlcPropTypes.Count || settings.WaterDlcPropTypes[i] == WaterPropType.None;
            }
            return i >= settings.IsDesk.Count || !settings.IsDesk[i];
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
