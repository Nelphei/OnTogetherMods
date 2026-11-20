using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EZhex1991.EZSoftBone;
using HarmonyLib;
using OnTogetherMods.Shared;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuddyUp
{
    // Shortcuts into the game's code. See reference/Assembly-CSharp/PlayerFocusController.cs,
    // PlayerMovementController.cs, FollowScript.cs and PlayerPanelController.cs.
    internal static class Game
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;
        private const string HeadBone = "mixamorig:Head";
        private const string DeskSnapGuid = "nelphei.ontogether.desksnap";

        // Where the game puts you back when you stand up.
        private static readonly AccessTools.FieldRef<PlayerFocusController, Vector3> PreFocusPosition =
            AccessTools.FieldRefAccess<PlayerFocusController, Vector3>("_preFocusPosition");

        private static readonly AccessTools.FieldRef<PlayerFocusController, Quaternion> PreFocusRotation =
            AccessTools.FieldRefAccess<PlayerFocusController, Quaternion>("_preFocusRotation");

        // The seat spot you're sitting at, or null for free focus.
        public static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        // Everything solid, water included, but not players, areas or invisible triggers.
        private static int _solid;

        // The Steam ID last looked up, as the player list writes it (asked every frame).
        private static ulong _lastId;
        private static string _lastIdText;

        // The last name seen on each player's name tag, so it still shows after they leave.
        private static readonly Dictionary<ulong, string> NameTags = new Dictionary<ulong, string>();

        // Each player's head bone, found once.
        private static readonly Dictionary<PlayerController, Transform> Heads = new Dictionary<PlayerController, Transform>();

        private static ConfigEntry<bool> _deskSnapOn;
        private static bool _lookedForDeskSnap;
        private static Func<bool> _deskSwarmPiloting;
        private static bool _lookedForDeskSwarm;
        private static float _standUpUntil = -1f;

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
                if (!InWorld)
                {
                    return null;
                }
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                return channel != null ? channel.MainMovementController : null;
            }
        }

        // The animation you're playing (a focus pose's number while you sit).
        public static int MyAnim()
        {
            TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
            return channel != null && channel.MainAnimationController != null ? channel.MainAnimationController.GetState() : -1;
        }

        // True while you sit in free focus (your desk, or any activity done on open ground), not at one
        // of the game's seat spots (benches, classroom desks, the swan...), which can't move.
        public static bool SitsFree(PlayerFocusController me)
        {
            return me != null && me.IsFocus && me.CurrentFocusAreaController == null && ActiveFocusArea(me) == null;
        }

        // Moves you while you sit. The game turned the movement collider off when you sat, and nothing
        // puts you back until you stand up, which happens at the stand-up spot (SetStandSpot).
        public static void PlaceSeated(PlayerFocusController me, Vector3 position, float yaw, bool moveCamera = true)
        {
            Transform t = me.transform;
            bool jump = (t.position - position).sqrMagnitude > 4f;
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (jump)
            {
                ResetTail(me);
            }

            // The camera follows a point the game moves to you every frame. Move it now too, so the
            // camera doesn't trail a frame behind and shake.
            if (moveCamera)
            {
                FollowScript follow = MonoSingleton<FollowScript>.I;
                if (follow != null && follow.FollowTarget == t)
                {
                    follow.Update();
                }
            }

            // Your game sends your new spot to everyone on the next network tick, as it does while you
            // walk. Never NetworkTransform.ForceSync: when you aren't the host, the host passes the move
            // on a second time and everyone else sees you go twice as far.
        }

        // Where standing up puts you.
        public static void SetStandSpot(PlayerFocusController me, Vector3 position, float yaw)
        {
            PreFocusPosition(me) = position;
            PreFocusRotation(me) = Quaternion.Euler(0f, yaw, 0f);
        }

        // Moves you while you stand, the way the game does when you fall off the world (and Teleport
        // Spots does). The movement collider stays on, so the game notices you leaving areas.
        public static void Hop(PlayerMovementController move, Vector3 position, float yaw)
        {
            Transform player = move.transform;
            player.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
            ResetTail(move);

            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            FollowScript follow = MonoSingleton<FollowScript>.I;
            if (follow != null && follow.FollowTarget == player)
            {
                follow.Update();
            }
            if (scene != null && scene.PlayerVCamera != null)
            {
                // Cut to the new place instead of gliding across the map.
                scene.PlayerVCamera.PreviousStateIsValid = false;
            }
        }

        // How fast you walk with a key held, in metres a second (popsicle, turbo and the basketball
        // court change it). See reference/Assembly-CSharp/PlayerMovementController.cs (MovePlayer).
        public static float WalkSpeed(PlayerMovementController move)
        {
            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            float speed = settings.WalkSpeed;
            if ((move.MovementState == MovementState.Idle || move.MovementState == MovementState.Walk) && !move.IsSwim)
            {
                TextChannelManager channel = NetworkSingleton<TextChannelManager>.I;
                BasketBallManager ball = NetworkSingleton<BasketBallManager>.I;
                if (ball != null && ball.IsInBasketBallCourt)
                {
                    speed = settings.BasketWalkSpeed;
                }
                else if (channel != null && channel.MainFXController != null && channel.MainFXController.IsPopsicleDouble)
                {
                    speed = settings.PopsicleSpeed;
                }
                else if (move.IsTurbo)
                {
                    speed = settings.TurboMovement;
                }
            }
            return speed > 0.1f ? speed : 1f;
        }

        // Stands you up the way the stand-up key does (Space unless the player changed it): the
        // input patch hands the game one press as soon as it would take it. Given up after a second
        // (a menu stayed open), so you never stand up out of the blue later.
        public static void StandUp()
        {
            _standUpUntil = Time.unscaledTime + 1f;
        }

        // For the input patch: true once, when the press should go to the game now.
        public static bool TakeStandUp()
        {
            if (_standUpUntil < 0f)
            {
                return false;
            }
            PlayerFocusController me = Me;
            if (Time.unscaledTime > _standUpUntil || me == null || !me.IsFocus || DeskSwarmPiloting())
            {
                // Too late, or already up (a press now would jump), or flying with Desk Swarm.
                _standUpUntil = -1f;
                return false;
            }
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (tasks == null || !tasks.IsReadyForFocusQuit() || me.IsSelectorOpened)
            {
                return false;   // the game wouldn't take it yet
            }
            _standUpUntil = -1f;
            return true;
        }

        // Which way the camera faces, as a turn around the vertical axis. The movement keys walk
        // relative to it.
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

        // True while the chat box is open, Photo Mode or Doom Together has the keys, or a game menu is open.
        public static bool KeysBusy()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks == null || tasks.IsCancelInputs() || tasks.IsWriting || tasks.LockState != LockState.Free;
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
            if (steamId == 0 || !InWorld)
            {
                return null;
            }
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
                PlayerController player = i >= 0 && i < panel.PlayerControllers.Count ? panel.PlayerControllers[i] : null;
                return player != null ? player : null;
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

        // The player's head bone (it moves with every animation), or their root if it can't be found.
        public static Transform Head(PlayerController player)
        {
            if (Heads.TryGetValue(player, out Transform head) && head != null)
            {
                return head;
            }
            if (Heads.Count > 64)
            {
                // Players from earlier visits to the world.
                Heads.Clear();
            }
            head = Find(player.transform, HeadBone);
            if (head == null)
            {
                Plugin.Log.LogWarning($"Couldn't find {HeadBone} on {player.name}, so riders sit on top of their feet.");
                head = player.transform;
            }
            Heads[player] = head;
            return head;
        }

        // The top of the ground or water within `depth` below `from`, not counting players or pets.
        public static bool Ground(Vector3 from, float depth, out float y)
        {
            if (_solid == 0)
            {
                _solid = ~LayerMask.GetMask("Ignore Raycast", "Player", "Area", "AreaTrigger",
                    "Customization", "IsGroundedChecker", "DeskRender");
            }
            y = 0f;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.down, depth, _solid, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < nearest && !IsPlayerOrPet(hit.collider))
                {
                    nearest = hit.distance;
                    y = hit.point.y;
                }
            }
            return nearest < float.MaxValue;
        }

        // True if something solid is on the straight line between two points.
        public static bool Blocked(Vector3 from, Vector3 to)
        {
            if (_solid == 0)
            {
                Ground(from, 0f, out _);
            }
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return false;
            }
            foreach (RaycastHit hit in Physics.RaycastAll(from, line / length, length, _solid, QueryTriggerInteraction.Ignore))
            {
                if (!IsPlayerOrPet(hit.collider))
                {
                    return true;
                }
            }
            return false;
        }

        // True when Desk Snap is installed with snapping on: it lines desks up itself when you sit down.
        public static bool DeskSnapOn()
        {
            if (!_lookedForDeskSnap)
            {
                _lookedForDeskSnap = true;
                if (Chainloader.PluginInfos.TryGetValue(DeskSnapGuid, out PluginInfo info) && info.Instance != null)
                {
                    info.Instance.Config.TryGetEntry("Snap", "Enabled", out _deskSnapOn);
                }
            }
            return _deskSnapOn != null && _deskSnapOn.Value;
        }

        // True while Desk Swarm is flying your desk with the keys.
        public static bool DeskSwarmPiloting()
        {
            if (!_lookedForDeskSwarm)
            {
                _lookedForDeskSwarm = true;
                try
                {
                    Type flight = AccessTools.TypeByName("DeskSwarm.Flight");
                    var getter = flight != null ? AccessTools.PropertyGetter(flight, "IsPiloting") : null;
                    if (getter != null)
                    {
                        _deskSwarmPiloting = AccessTools.MethodDelegate<Func<bool>>(getter);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Couldn't check Desk Swarm: {e.Message}");
                }
            }
            try
            {
                return _deskSwarmPiloting != null && _deskSwarmPiloting();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsPlayerOrPet(Collider collider)
        {
            return collider.GetComponentInParent<PlayerController>() != null ||
                   collider.GetComponentInParent<PetController>() != null;
        }

        private static Transform Find(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
                Transform found = Find(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        // The tail's floppy bones (EZSoftBone) remember where they were, so a big jump would fling
        // it. SetRestState puts each bone where it is now. Same as Teleport Spots.
        private static void ResetTail(Component player)
        {
            PlayerCustomizationController looks = player.GetComponent<PlayerCustomizationController>();
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
