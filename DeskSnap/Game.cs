using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using EZhex1991.EZSoftBone;
using HarmonyLib;
using PurrNet;
using TMPro;
using UnityEngine;

namespace DeskSnap
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/PlayerFocusController.cs,
    // FocusAreaManager.cs and PlayerAnimationController.cs.
    internal static class Game
    {
        private const string EmoteSyncGuid = "nelphei.ontogether.emotesync";

        // The animator int that drives every player animation, focus poses included. Synced to everyone.
        public static readonly int AnimStateParam = Animator.StringToHash("AnimState");

        // The personal desk. It's part of the player, so it sits at a fixed spot in front of them.
        public static readonly AccessTools.FieldRef<PlayerFocusController, GameObject> Desk =
            AccessTools.FieldRefAccess<PlayerFocusController, GameObject>("_desk");

        // Where the game puts you back when you stand up.
        public static readonly AccessTools.FieldRef<PlayerFocusController, Vector3> PreFocusPosition =
            AccessTools.FieldRefAccess<PlayerFocusController, Vector3>("_preFocusPosition");

        public static readonly AccessTools.FieldRef<PlayerFocusController, Quaternion> PreFocusRotation =
            AccessTools.FieldRefAccess<PlayerFocusController, Quaternion>("_preFocusRotation");

        // The seat spot you're sitting at, or null for free focus.
        public static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        private static readonly AccessTools.FieldRef<PlayerAnimationController, Animator> AnimatorField =
            AccessTools.FieldRefAccess<PlayerAnimationController, Animator>("_animator");

        // Free-focus activities on land, as parallel lists. (Focus Anywhere adds to these.)
        public static readonly AccessTools.FieldRef<GameSettings, List<int>> LandAnims =
            AccessTools.FieldRefAccess<GameSettings, List<int>>("_animationStates");

        public static readonly AccessTools.FieldRef<GameSettings, List<bool>> LandIsDesk =
            AccessTools.FieldRefAccess<GameSettings, List<bool>>("_isDesk");

        private static Func<bool> _emoteSyncJoining;
        private static bool _lookedForEmoteSync;

        // True while you're sitting in free focus (not at a seat spot) with your desk out.
        public static bool IsAtOwnDesk(PlayerFocusController me)
        {
            return me != null && me.IsFocus && me.IsDesk && IsFreeFocus(me);
        }

        public static bool IsFreeFocus(PlayerFocusController me)
        {
            return me.CurrentFocusAreaController == null && ActiveFocusArea(me) == null;
        }

        // True while Emote Sync is starting a focus activity because someone in your group picked it.
        // Read through reflection, so this mod works with or without Emote Sync.
        public static bool EmoteSyncJoining()
        {
            if (!_lookedForEmoteSync)
            {
                _lookedForEmoteSync = true;
                if (Chainloader.PluginInfos.TryGetValue(EmoteSyncGuid, out PluginInfo info) && info.Instance != null)
                {
                    Type join = info.Instance.GetType().Assembly.GetType("EmoteSync.FocusJoin");
                    MethodInfo getter = join != null ? AccessTools.PropertyGetter(join, "Joining") : null;
                    if (getter != null)
                    {
                        _emoteSyncJoining = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                    }
                }
                if (Plugin.Settings.DebugLog.Value)
                {
                    Plugin.Log.LogInfo(_emoteSyncJoining != null
                        ? "Found Emote Sync: desks snap after its focus joins too."
                        : "Emote Sync isn't installed (or is too old), so only your own picks are checked for joins.");
                }
            }
            return _emoteSyncJoining != null && _emoteSyncJoining();
        }

        // True when this player is in one of the game's seat spots (bench, classroom desk, swan...).
        public static bool IsAtSeatSpot(PlayerFocusController player)
        {
            FocusAreaManager areas = NetworkSingleton<FocusAreaManager>.I;
            NetworkTransform net = player.GetComponent<NetworkTransform>();
            if (areas == null || net == null || areas.NetworkTransforms == null || areas.AreaIndexes == null)
            {
                return false;
            }
            int i = areas.NetworkTransforms.IndexOf(net);
            return i >= 0 && i < areas.AreaIndexes.Count && areas.AreaIndexes[i] >= 0;
        }

        public static Animator AnimatorOf(PlayerFocusController player)
        {
            PlayerAnimationController anim = player.GetComponent<PlayerAnimationController>()
                ?? player.GetComponentInChildren<PlayerAnimationController>()
                ?? player.GetComponentInParent<PlayerAnimationController>();
            return anim != null ? AnimatorField(anim) : null;
        }

        // The name on the player's name tag.
        public static string NameOf(PlayerFocusController player)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            string name = pc != null && pc.PlayerNameText != null ? pc.PlayerNameText.text : null;
            return string.IsNullOrWhiteSpace(name) ? "your friend" : name.Trim();
        }

        // The font the player's name tag is drawn with (null if there's no name tag).
        public static TMP_FontAsset NameTagFont(PlayerFocusController player)
        {
            if (player == null)
            {
                return null;
            }
            PlayerController pc = player.GetComponent<PlayerController>();
            return pc != null && pc.PlayerNameText != null ? pc.PlayerNameText.font : null;
        }

        // PurrNet's player number. Every game in the lobby sees the same number for the same player.
        public static ulong IdOf(PlayerFocusController player)
        {
            PlayerID? owner = player.owner;
            return owner.HasValue ? owner.Value.id.value : 0UL;
        }

        // Moves you while you're sitting. The game turned the movement collider off when you sat,
        // and turns it back on at the stand-up spot, which is set to here so you get up by your desk.
        public static void MoveSeated(PlayerFocusController me, Vector3 position, Quaternion rotation)
        {
            me.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            PreFocusPosition(me) = position;
            PreFocusRotation(me) = rotation;
            ResetTail(me);

            // Your game decides where your character is and sends it to everyone. ForceSync sends it
            // now and tells the others to jump straight there instead of sliding.
            NetworkTransform net = me.GetComponent<NetworkTransform>();
            if (net != null)
            {
                net.ForceSync();
            }
        }

        // The tail's floppy bones (EZSoftBone) remember where they were, so a jump would fling it.
        // SetRestState puts each bone where it is now. Same as Teleport Spots.
        private static void ResetTail(PlayerFocusController me)
        {
            PlayerCustomizationController looks = me.GetComponent<PlayerCustomizationController>();
            if (looks == null || !looks.OutfitMeshRenderers.TryGetValue(OutfitType.Tail, out SkinnedMeshRenderer tail)
                || tail == null || tail.transform.parent == null)
            {
                return;
            }
            EZSoftBone bones = tail.transform.parent.GetComponent<EZSoftBone>();
            if (bones != null)
            {
                bones.SetRestState();
            }
        }
    }
}
