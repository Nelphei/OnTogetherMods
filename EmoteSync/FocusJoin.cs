using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EmoteSync
{
    internal enum JoinResult
    {
        Joined,
        Wait,   // busy right now; try again when free
        Skip,   // can't join this one at all
    }

    // Starts (or switches to) a free-focus activity on our own character, the same way the game
    // does when you press the focus key and click an activity. Only free focus: players sitting at
    // a seat spot (bench, classroom desk...) are skipped.
    // See reference/Assembly-CSharp/PlayerFocusController.cs (Update, OpenSelector, SetFocus)
    // and TaskManager.cs (IsReadyForAreaFocus, IsReadyForFreeFocus, IsReadyForFocusQuit).
    internal static class FocusJoin
    {
        private const string FocusAnywhereGuid = "nelphei.ontogether.focusanywhere";

        private static bool? _anyGround;

        // True while we're starting a focus for the group, so the SetFocus patch doesn't send it again.
        public static bool Joining { get; private set; }

        // For Wait, `why` finishes the sentence "Joining ... when ___" (e.g. "you finish typing"),
        // and canClear says whether ClearWay() can deal with everything in the way.
        // For Skip, `why` says why not (e.g. "you're at a seat spot").
        public static JoinResult TryJoin(int animState, FocusType focusType, out string why, out bool canClear)
        {
            why = null;
            canClear = false;
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (channel == null || tasks == null || channel.MainFocusController == null ||
                channel.MainMovementController == null || channel.MainAnimationController == null)
            {
                why = "the game is ready";
                return JoinResult.Wait;
            }
            PlayerFocusController focus = channel.MainFocusController;
            PlayerMovementController movement = channel.MainMovementController;

            bool switching = focus.IsFocus;
            if (switching && Game.ActiveFocusArea(focus) != null)
            {
                why = "you're at a seat spot";
                return JoinResult.Skip;
            }

            why = WaitingFor(tasks, focus, movement, switching, out canClear);
            if (why != null)
            {
                return JoinResult.Wait;
            }

            if (switching && channel.MainAnimationController.GetState() == animState)
            {
                return JoinResult.Joined;   // already doing it
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
                why = "that activity isn't in your menu here";
                return JoinResult.Skip;
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
            if (!focus.IsFocus)
            {
                why = "the game didn't start it";
                return JoinResult.Skip;
            }
            return JoinResult.Joined;
        }

        // "Join now": clears what's in the way, which is only what the player could clear with one key:
        // leaves the chat box, closes an open game window (the focus menu is one) and stops an emote.
        // Call it only when TryJoin said canClear. Fishing is left alone, since cancelling a catch
        // can lose the fish.
        public static void ClearWay()
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            InputManager input = MonoSingleton<InputManager>.I;
            if (channel == null || tasks == null || input == null)
            {
                return;
            }

            // Leave the chat box the way clicking anywhere else does. Typed text stays in it.
            if (tasks.IsWriting && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            // Close an open window the way the game does when you start typing.
            // See reference/Assembly-CSharp/InputManager.cs (CloseOnWriting).
            if (IsClosableWindow(tasks.LockState) && tasks.CloseUIAction != null)
            {
                input.CloseOnWriting();
            }

            // Stop an emote or getting up, the way moving does.
            // See reference/Assembly-CSharp/PlayerMovementController.cs (AnimationUpdate).
            PlayerMovementController movement = channel.MainMovementController;
            if (movement != null && movement.IsAnimation && CanStopAnimation(movement))
            {
                if (movement.PlayerAnimationState == AnimationState.Emote)
                {
                    channel.MainAnimationController.StopEmote();
                }
                else if (movement.GetUpRoutine != null)
                {
                    movement.StopCoroutine(movement.GetUpRoutine);
                }
                movement.IsAnimation = false;
                movement.PlayerAnimationState = AnimationState.None;
            }
        }

        // The game's own "free to focus" checks, done here so we can say what we're waiting for.
        // (Other mods patch the game's checks to always pass.) Nothing is closed or stopped here.
        // Returns null when you're free. Otherwise returns the first thing in the way, and sets
        // canClear when ClearWay() can deal with all of it.
        private static string WaitingFor(TaskManager tasks, PlayerFocusController focus,
            PlayerMovementController movement, bool switching, out bool canClear)
        {
            string first = null;
            bool stuck = false;
            void Add(string why, bool clearable)
            {
                first = first ?? why;
                stuck |= !clearable;
            }

            if (tasks.IsWriting)
            {
                Add("you finish typing", true);
            }
            if (focus.IsSelectorOpened)
            {
                Add("you close the focus menu", true);
            }
            if (tasks.LockState == LockState.Music)
            {
                DrawingManager drawing = MonoSingleton<DrawingManager>.I;
                Add(drawing != null && drawing.IsActive ? "you stop drawing" : "you leave the instrument", false);
            }
            else if (tasks.LockState != LockState.Free)
            {
                // The menu and the emote wheel aren't closed for you.
                Add("you close the window you have open", IsClosableWindow(tasks.LockState) && tasks.CloseUIAction != null);
            }

            // Swapping activities only needs the checks above.
            if (!switching)
            {
                if (NetworkSingleton<BasketBallManager>.I.IsInBasketBallCourtColorful)
                {
                    Add("you leave the basketball court", false);
                }
                if (MonoSingleton<FishingManager>.I.FishingState != FishingState.None)
                {
                    Add("you stop fishing", false);
                }
                if (movement.IsAnimation)
                {
                    switch (movement.PlayerAnimationState)
                    {
                        case AnimationState.Emote:
                            Add("your emote finishes", true);
                            break;
                        case AnimationState.UseItem:
                            Add("you finish using the item", false);
                            break;
                        default:
                            Add("you're back on your feet", CanStopAnimation(movement));
                            break;
                    }
                }
                if (movement.IsStair)
                {
                    Add("you're off the stairs", false);
                }
                if (movement.MovementState == MovementState.Bubble)
                {
                    Add("you're out of the bubble", false);
                }
                else if ((movement.MovementState != MovementState.Idle && movement.MovementState != MovementState.Walk) ||
                         movement.GroundState != GroundState.Grounded)
                {
                    Add("you land", false);
                }

                // Where you're standing. Focus Anywhere lets you focus on any ground, so skip these with it.
                if (!AnyGround())
                {
                    MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
                    if (focus.FocusAreaControllers.Count > 0 && !movement.IsLake)
                    {
                        Add("you step away from the seat spot", false);
                    }
                    else if (scene != null && (scene.MusicHillTransform.position - focus.transform.position).sqrMagnitude < 12.53f)
                    {
                        Add("you move away from the music hill", false);
                    }
                    else if (!Game.IsFocusable(focus))
                    {
                        Add("you stand somewhere you can focus", false);
                    }
                }
            }

            canClear = first != null && !stuck;
            return first;
        }

        // Windows the game itself closes when you start typing (InputManager.CloseOnWriting).
        private static bool IsClosableWindow(LockState state)
        {
            return state == LockState.UIInventory || state == LockState.UIFishUI || state == LockState.UIWalkable;
        }

        // Moving cuts an emote short, and getting up after a fall once it's half a second in
        // (PlayerMovementController.AnimationUpdate). Using an item always plays out.
        private static bool CanStopAnimation(PlayerMovementController movement)
        {
            switch (movement.PlayerAnimationState)
            {
                case AnimationState.Emote:
                case AnimationState.SoftGetUp:
                    return true;
                case AnimationState.GetUp:
                    return movement.AnimationTimer >= 0.5f;
                default:
                    return false;
            }
        }

        // True when Focus Anywhere is installed with its "any ground" setting on. Read once:
        // that setting only changes when the game restarts.
        private static bool AnyGround()
        {
            if (_anyGround == null)
            {
                _anyGround = false;
                if (Chainloader.PluginInfos.TryGetValue(FocusAnywhereGuid, out PluginInfo info) && info.Instance != null)
                {
                    _anyGround = !info.Instance.Config.TryGetEntry("Focus", "AnyGround", out ConfigEntry<bool> entry) || entry.Value;
                }
                Plugin.Log.LogInfo(_anyGround.Value
                    ? "Focus Anywhere is installed, so you can join focus on any ground."
                    : "Joining focus follows the game's ground rules (Focus Anywhere isn't installed or its AnyGround is off).");
            }
            return _anyGround.Value;
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
