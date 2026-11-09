using System;
using System.Collections.Generic;
using EZhex1991.EZSoftBone;
using HarmonyLib;
using OnTogetherMods.Shared;
using PurrNet;
using PurrNet.Modules;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeskSwarm
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/PlayerFocusController.cs,
    // PlayerMovementController.cs, FollowScript.cs and PlayerPanelController.cs.
    internal static class Game
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        // Where the game puts you back when you stand up.
        private static readonly AccessTools.FieldRef<PlayerFocusController, Vector3> PreFocusPosition =
            AccessTools.FieldRefAccess<PlayerFocusController, Vector3>("_preFocusPosition");

        private static readonly AccessTools.FieldRef<PlayerFocusController, Quaternion> PreFocusRotation =
            AccessTools.FieldRefAccess<PlayerFocusController, Quaternion>("_preFocusRotation");

        // The seat spot you're sitting at, or null for free focus.
        private static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        // The Steam ID last looked up, as the player list writes it (the follower asks every frame).
        private static ulong _lastId;
        private static string _lastIdText;

        // The last name seen on each player's name tag, so it still shows after they leave.
        private static readonly Dictionary<ulong, string> NameTags = new Dictionary<ulong, string>();

        // Cheap to check every frame. Singletons are only looked up in the world: on the main menu
        // MonoSingleton.I searches the whole scene each time.
        public static bool InWorld => SceneManager.GetActiveScene().buildIndex == WorldScene;

        // You, or null outside the world.
        public static PlayerFocusController Me
        {
            get
            {
                if (!InWorld)
                {
                    return null;
                }
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                return channel != null ? channel.MainFocusController : null;
            }
        }

        public static PlayerMovementController MyMovement
        {
            get
            {
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                return channel != null ? channel.MainMovementController : null;
            }
        }

        // True while you're sitting in free focus (your desk, or any activity done on open ground),
        // not at one of the game's seat spots (benches, classroom desks, the swan...), which are part
        // of the world and can't move.
        public static bool CanFly(PlayerFocusController me)
        {
            return me != null && me.IsFocus && me.CurrentFocusAreaController == null && ActiveFocusArea(me) == null;
        }

        // Moves you while you're sitting. The game turned the movement collider off when you sat,
        // and turns it back on at the stand-up spot, which follows you so you get up where you are.
        public static void MoveSeated(PlayerFocusController me, Vector3 position, Quaternion rotation, float yaw)
        {
            Transform t = me.transform;
            bool jump = (t.position - position).sqrMagnitude > 4f;
            t.SetPositionAndRotation(position, rotation);
            PreFocusPosition(me) = position;
            PreFocusRotation(me) = Quaternion.Euler(0f, yaw, 0f);
            if (jump)
            {
                ResetTail(me);
            }

            // The camera follows a point the game moves to you every frame. Move it now too, so the
            // camera doesn't trail a frame behind and shake.
            FollowScript follow = MonoSingleton<FollowScript>.I;
            if (follow != null && follow.FollowTarget == t)
            {
                follow.Update();
            }

            // Your game sends your new spot to everyone on the next network tick, as it does while you
            // walk. Never NetworkTransform.ForceSync: when you aren't the host, the host passes the move
            // on a second time and everyone else sees you go twice as far.
        }

        // Which way the camera faces, as a turn around the vertical axis.
        public static float CameraYaw()
        {
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            Transform follower = scene != null ? scene.PlayerFollower : null;
            if (follower != null)
            {
                return follower.eulerAngles.y;
            }
            Camera camera = Camera.main;
            return camera != null ? camera.transform.eulerAngles.y : 0f;
        }

        // While you sit, the game turns the camera slowly by itself. That's off while you fly, so the
        // movement keys keep pointing where you looked.
        public static void CameraDrift(bool on)
        {
            PlayerMovementController movement = MyMovement;
            if (movement == null)
            {
                return;
            }
            if (on)
            {
                movement.StartCameraRotationRoutine();
            }
            else
            {
                movement.StopCameraRotationRoutine();
                movement.CameraFocusRoutine = null;
            }
        }

        // The water you'd be swimming in below this height (the sea).
        public static float SeaLevel
        {
            get
            {
                GameSettings settings = ScriptableSingleton<GameSettings>.I;
                return settings != null ? settings.SwimPosY : float.MinValue;
            }
        }

        // True while the chat box is open, Photo Mode has the keys, or a game menu is open.
        public static bool KeysBusy()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks == null || tasks.IsCancelInputs() || tasks.LockState != LockState.Free;
        }

        public static bool IsTyping()
        {
            // Only looked up on key press: on the main menu MonoSingleton.I searches the scene.
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // The key the game stands you up with (Space unless the player changed it).
        public static KeyCode StandUpKey()
        {
            try
            {
                DataManager data = MonoSingleton<DataManager>.I;
                return data.SettingsData.Keys[KeyType.Space].Item1;
            }
            catch (Exception)
            {
                return KeyCode.Space;
            }
        }

        // This player's character, found through the player list, which keeps each player's Steam ID
        // next to them. Null if they aren't in the world on your screen.
        public static PlayerController PlayerOf(ulong steamId)
        {
            try
            {
                PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
                if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerControllers == null)
                {
                    return null;
                }
                if (steamId != _lastId)
                {
                    _lastId = steamId;
                    _lastIdText = steamId.ToString();
                }
                int i = panel.PlayerSteamIDs.IndexOf(_lastIdText);
                return i >= 0 && i < panel.PlayerControllers.Count ? panel.PlayerControllers[i] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The name on this player's name tag, with its colours and other styling, ready to go in
        // text drawn with TextMeshPro. Falls back to their Steam name, shown as typed.
        public static string PlayerName(ulong steamId)
        {
            string name = PlayerNames.Of(PlayerOf(steamId));
            if (name != null)
            {
                NameTags[steamId] = name;
                return name;
            }
            return NameTags.TryGetValue(steamId, out string seen) ? seen : PlayerNames.Plain(LobbyChannel.NameOf(new CSteamID(steamId)));
        }

        // Seconds on the clock everyone in the session shares (PurrNet's tick counter: the host counts
        // ticks and each client keeps re-syncing its copy using its ping), so orbiting desks stay in
        // step on every screen. Your own clock when not connected.
        public static double SharedTime
        {
            get
            {
                NetworkManager nm = NetworkManager.main;
                if (nm != null && (nm.isClient || nm.isServer))
                {
                    TickManager ticks = nm.tickModule;
                    if (ticks != null)
                    {
                        return ticks.syncedPreciseTick * ticks.tickDeltaDouble;
                    }
                }
                return Time.timeAsDouble;
            }
        }

        // The tail's floppy bones (EZSoftBone) remember where they were, so a big jump would fling
        // it. SetRestState puts each bone where it is now. Same as Teleport Spots.
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
