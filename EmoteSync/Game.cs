using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTogetherMods.Shared;
using PurrNet;
using PurrNet.Modules;
using Steamworks;
using UnityEngine;

namespace EmoteSync
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/PlayerAnimationController.cs
    // and EmoteController.cs.
    internal static class Game
    {
        // The animator int that drives every player animation (idle, walk, emotes, focus poses...).
        public static readonly int AnimStateParam = Animator.StringToHash("AnimState");

        private static readonly AccessTools.FieldRef<PlayerAnimationController, Animator> AnimatorField =
            AccessTools.FieldRefAccess<PlayerAnimationController, Animator>("_animator");

        // The last name seen on each player's name tag, so it still shows after they leave.
        private static readonly Dictionary<ulong, string> NameTags = new Dictionary<ulong, string>();

        // The name on this player's name tag, with its colours and other styling, ready to go in
        // text drawn with TextBox. Falls back to their Steam name, shown as typed.
        // The game keeps each player's Steam ID next to their name tag (PlayerPanelController).
        public static string PlayerName(CSteamID id)
        {
            try
            {
                PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
                if (panel != null && panel.PlayerSteamIDs != null && panel.PlayerControllers != null)
                {
                    int i = panel.PlayerSteamIDs.IndexOf(id.ToString());
                    string name = i >= 0 && i < panel.PlayerControllers.Count ? PlayerNames.Of(panel.PlayerControllers[i]) : null;
                    if (name != null)
                    {
                        NameTags[id.m_SteamID] = name;
                        return name;
                    }
                }
            }
            catch (Exception)
            {
                // No player list yet.
            }
            return NameTags.TryGetValue(id.m_SteamID, out string seen) ? seen : PlayerNames.Plain(LobbyChannel.NameOf(id));
        }

        private static readonly AccessTools.FieldRef<PlayerAnimationController, NetworkAnimator> NetAnimatorField =
            AccessTools.FieldRefAccess<PlayerAnimationController, NetworkAnimator>("_networkAnimator");

        private static readonly AccessTools.FieldRef<PlayerAnimationController, EmoteController> EmoteControllerField =
            AccessTools.FieldRefAccess<PlayerAnimationController, EmoteController>("_emoteController");

        // Private. Plays emote #index on the local player: face, then the networked animation.
        private static readonly Action<EmoteController, int> SetEmoteAnimMethod =
            AccessTools.MethodDelegate<Action<EmoteController, int>>(AccessTools.Method(typeof(EmoteController), "SetEmoteAnim"));

        // Focus selector state. See reference/Assembly-CSharp/PlayerFocusController.cs (OpenSelector, SetFocus).
        public static readonly AccessTools.FieldRef<PlayerFocusController, bool> IsFocusChange =
            AccessTools.FieldRefAccess<PlayerFocusController, bool>("_isFocusChange");

        public static readonly AccessTools.FieldRef<PlayerFocusController, List<FocusType>> SimpledFocusTypes =
            AccessTools.FieldRefAccess<PlayerFocusController, List<FocusType>>("_simpledFocusTypes");

        public static readonly AccessTools.FieldRef<PlayerFocusController, int> FocusMainIndex =
            AccessTools.FieldRefAccess<PlayerFocusController, int>("_currentFocusMainIndex");

        public static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        private static readonly Action<PlayerFocusController, FocusAreaController> SetCurrentFocusAreaMethod =
            AccessTools.MethodDelegate<Action<PlayerFocusController, FocusAreaController>>(
                AccessTools.PropertySetter(typeof(PlayerFocusController), nameof(PlayerFocusController.CurrentFocusAreaController)));

        // Private. True when the ground you're standing on allows free focus.
        private static readonly Func<PlayerFocusController, bool> IsFocusableMethod =
            AccessTools.MethodDelegate<Func<PlayerFocusController, bool>>(AccessTools.Method(typeof(PlayerFocusController), "IsFocusable"));

        // Free-focus animations on land and in water. The public getters pick one by whether
        // the local player is in water; we want both.
        private static readonly AccessTools.FieldRef<GameSettings, List<int>> LandFocusAnims =
            AccessTools.FieldRefAccess<GameSettings, List<int>>("_animationStates");

        private static readonly AccessTools.FieldRef<GameSettings, List<int>> WaterFocusAnims =
            AccessTools.FieldRefAccess<GameSettings, List<int>>("_animationStatesWater");

        public static void SetCurrentFocusArea(PlayerFocusController focus, FocusAreaController area) => SetCurrentFocusAreaMethod(focus, area);

        public static bool IsFocusable(PlayerFocusController focus) => IsFocusableMethod(focus);

        public static IEnumerable<int> FreeFocusAnimStates()
        {
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            return (LandFocusAnims(settings) ?? new List<int>()).Concat(WaterFocusAnims(settings) ?? new List<int>());
        }

        public static Animator GetAnimator(PlayerAnimationController player) => AnimatorField(player);

        public static NetworkAnimator GetNetAnimator(PlayerAnimationController player) => NetAnimatorField(player);

        public static EmoteController GetEmoteController(PlayerAnimationController player) => EmoteControllerField(player);

        public static void SetEmoteAnim(EmoteController controller, int emoteIndex) => SetEmoteAnimMethod(controller, emoteIndex);

        // A focus category's name as the game's menu shows it, in the player's language.
        public static string FocusName(FocusType type)
        {
            try
            {
                int index = ScriptableSingleton<GameSettings>.I.FocusTypesForNames.IndexOf(type);
                List<string> names = MonoSingleton<SettingsController>.I.FocusNamesStrings;
                if (index >= 0 && index < names.Count && !string.IsNullOrEmpty(names[index]))
                {
                    return names[index];
                }
            }
            catch (Exception)
            {
                // Fall back to the English name below.
            }
            return type.ToString();
        }

        public static int EmoteCount => ScriptableSingleton<GameSettings>.I.EmoteAnimStates.Count;

        public static int EmoteAnimState(int emoteIndex) => ScriptableSingleton<GameSettings>.I.EmoteAnimStates[emoteIndex];

        // PurrNet's tick counter, shared by everyone in the session: the host counts ticks and each
        // client keeps re-syncing its copy using its ping. Null when not connected.
        public static TickManager Ticks
        {
            get
            {
                NetworkManager nm = NetworkManager.main;
                if (nm == null || (!nm.isClient && !nm.isServer))
                {
                    return null;
                }
                return nm.tickModule;
            }
        }
    }
}
