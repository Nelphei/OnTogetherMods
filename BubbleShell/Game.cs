using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EZhex1991.EZSoftBone;
using HarmonyLib;
using OnTogetherMods.Shared;
using PurrNet;
using PurrNet.Modules;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubbleShell
{
    // One player in the session, from the game's player list.
    internal struct PlayerEntry
    {
        public PlayerController Player;
        public ulong SteamId;
        public string Name;       // styled, ready for the console
        public string PlainName;  // without tags, for matching what you type
        public bool IsMe;
    }

    // Shortcuts into the game's code, mostly copied from the other mods (Teleport Spots, Buddy Up,
    // Emote Sync, Session Manager). See reference/Assembly-CSharp/PlayerMovementController.cs,
    // PlayerFocusController.cs, TaskManager.cs, EmoteController.cs and PlayerPanelController.cs.
    internal static class Game
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;
        private const string HeadBone = "mixamorig:Head";

        public static readonly int AnimStateParam = Animator.StringToHash("AnimState");

        private static readonly AccessTools.FieldRef<PlayerFocusController, Vector3> PreFocusPosition =
            AccessTools.FieldRefAccess<PlayerFocusController, Vector3>("_preFocusPosition");

        private static readonly AccessTools.FieldRef<PlayerFocusController, Quaternion> PreFocusRotation =
            AccessTools.FieldRefAccess<PlayerFocusController, Quaternion>("_preFocusRotation");

        // The seat spot you're at (a bench, a classroom desk...), or null for free focus.
        private static readonly AccessTools.FieldRef<PlayerFocusController, FocusAreaController> ActiveFocusArea =
            AccessTools.FieldRefAccess<PlayerFocusController, FocusAreaController>("_activeFocusAreaController");

        private static readonly AccessTools.FieldRef<PlayerAnimationController, Animator> AnimatorField =
            AccessTools.FieldRefAccess<PlayerAnimationController, Animator>("_animator");

        // The object the game lifts for floaties; the whole skeleton (and so the body) is under it.
        private static readonly AccessTools.FieldRef<PlayerAnimationController, Transform> ArmatureParentField =
            AccessTools.FieldRefAccess<PlayerAnimationController, Transform>("_armatureParent");

        private static readonly AccessTools.FieldRef<PlayerAnimationController, EmoteController> EmoteControllerField =
            AccessTools.FieldRefAccess<PlayerAnimationController, EmoteController>("_emoteController");

        // Private. Plays emote #index on you: face, then the networked animation (what the emote wheel does).
        private static readonly Action<EmoteController, int> SetEmoteAnimMethod =
            AccessTools.MethodDelegate<Action<EmoteController, int>>(AccessTools.Method(typeof(EmoteController), "SetEmoteAnim"));

        // Tags that change a name's size or layout, left out in the console so lines stay even.
        private static readonly Regex LayoutTags = new Regex(
            @"</?(size|voffset|line-height|font|margin|indent|line-indent|align|pos|space|width|rotate|cspace|mspace|br|page)(=[^<>]*)?>",
            RegexOptions.IgnoreCase);

        private static int _solid;
        private static float _standUpUntil = -1f;
        private static Func<bool> _deskSwarmPiloting;
        private static bool _lookedForDeskSwarm;
        private static bool? _anyGround;

        // ---- Where you are ----

        public static bool InWorld => SceneManager.GetActiveScene().buildIndex == WorldScene;

        public static TextChannelManager Channel => InWorld ? NetworkSingleton<TextChannelManager>.I : null;

        public static PlayerFocusController MyFocus => Channel != null ? Channel.MainFocusController : null;

        public static PlayerMovementController MyMovement => Channel != null ? Channel.MainMovementController : null;

        public static PlayerController MyPlayer => MyMovement != null ? MyMovement.GetComponent<PlayerController>() : null;

        // True while the chat box is open (only looked up on key press: on the main menu
        // MonoSingleton.I searches the scene).
        public static bool IsTyping()
        {
            if (!InWorld)
            {
                return false;
            }
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.IsWriting;
        }

        // True while a game menu or panel is open (pause menu, inventory, shop, focus menu...).
        // At an instrument or drawing board (Music) counts as free.
        public static bool MenuOpen()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks != null && tasks.LockState != LockState.Free && tasks.LockState != LockState.Music;
        }

        // True while the keys belong to something else: chat, Photo Mode, Doom Together, a game menu.
        public static bool KeysBusy()
        {
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            return tasks == null || tasks.IsCancelInputs() || tasks.IsWriting || tasks.LockState != LockState.Free;
        }

        // ---- Moving ----

        // Why you can't be moved right now, or null if you can. Teleport Spots' rules: standing (or
        // swimming), no menu, not fishing, not mid-step.
        public static string WhyCantMove()
        {
            PlayerMovementController move = MyMovement;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (move == null || tasks == null)
            {
                return "Join a world first.";
            }
            PlayerFocusController focus = MyFocus;
            if (focus != null && focus.IsFocus)
            {
                return "Stand up first (stand).";
            }
            if (tasks.LockState == LockState.Music)
            {
                return "Stop playing music first.";
            }
            if (tasks.LockState != LockState.Free)
            {
                return "Close the open menu first.";
            }
            FishingManager fishing = MonoSingleton<FishingManager>.I;
            if (fishing != null && fishing.FishingState != FishingState.None)
            {
                return "Stop fishing first.";
            }
            BasketBallManager ball = NetworkSingleton<BasketBallManager>.I;
            if (ball != null && ball.IsBallOnMyHand)
            {
                return "Let go of the ball first.";
            }
            if (move.IsStair)
            {
                return "Get off the stairs first.";
            }
            if (move.IsAnimation)
            {
                return "Wait for the animation to finish.";
            }
            bool walking = move.MovementState == MovementState.Idle || move.MovementState == MovementState.Walk;
            if (!walking || (move.GroundState != GroundState.Grounded && !move.IsInWater))
            {
                return "Land first.";
            }
            return null;
        }

        // Moves you the way the game does when you fall off the world (and Teleport Spots does): the
        // movement collider stays on, so the game notices you leaving areas. Your game sends the new
        // spot to everyone on the next tick. Never NetworkTransform.ForceSync (see CLAUDE.md).
        public static void Teleport(Vector3 position, float yaw)
        {
            PlayerMovementController move = MyMovement;
            Transform player = move.transform;
            player.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
            ResetTail(move);

            FollowScript follow = MonoSingleton<FollowScript>.I;
            if (follow != null && follow.FollowTarget == player)
            {
                follow.Update();
            }
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            if (scene != null && scene.PlayerVCamera != null)
            {
                // Cut to the new place instead of gliding across the map.
                scene.PlayerVCamera.PreviousStateIsValid = false;
            }
        }

        // Why you can't be moved while sitting, or null if you can. Sitting free (your desk, or any
        // activity on open ground) moves fine, the way Desk Swarm flies you; seat spots (benches,
        // classroom desks, the swan...) don't, and neither do moves another mod would undo.
        public static string WhyCantMoveSeated()
        {
            PlayerFocusController focus = MyFocus;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (focus == null || tasks == null)
            {
                return "Join a world first.";
            }
            if (focus.CurrentFocusAreaController != null || ActiveFocusArea(focus) != null)
            {
                return "You're at a seat spot (a bench, a classroom desk...). Stand up first (stand).";
            }
            if (focus.IsSelectorOpened)
            {
                return "Close the focus menu first.";
            }
            if (tasks.LockState != LockState.Free)
            {
                return "Close the open menu first.";
            }
            if (BuddyUpRiding())
            {
                return "You're riding on a buddy's head. Hop off first (buddy stop).";
            }
            if (DeskSwarmFollowing())
            {
                return "Your desk is following a swarm, so it would fly straight back. Leave it first (swarm leave).";
            }
            return null;
        }

        // Moves you while you sit, the way Desk Swarm flies you: the movement collider stays off (the game
        // turned it off when you sat), and standing up happens where you are now. Your game sends the new
        // spot to everyone on the next tick.
        public static void MoveSeated(Vector3 position, float yaw)
        {
            PlayerFocusController me = MyFocus;
            Transform t = me.transform;
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            t.SetPositionAndRotation(position, facing);
            PreFocusPosition(me) = position;
            PreFocusRotation(me) = facing;
            ResetTail(me);

            FollowScript follow = MonoSingleton<FollowScript>.I;
            if (follow != null && follow.FollowTarget == t)
            {
                follow.Update();
            }
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            if (scene != null && scene.PlayerVCamera != null)
            {
                scene.PlayerVCamera.PreviousStateIsValid = false;
            }
        }

        // True while Buddy Up has you riding on someone's head (it puts you back on it every frame).
        public static bool BuddyUpRiding()
        {
            try
            {
                Type controller = AccessTools.TypeByName("BuddyUp.BuddyController");
                object instance = controller != null ? AccessTools.Property(controller, "Instance")?.GetValue(null) : null;
                object riding = instance != null ? AccessTools.Field(controller, "_riding")?.GetValue(instance) : null;
                object carrier = riding != null ? AccessTools.Property(riding.GetType(), "Carrier")?.GetValue(riding) : null;
                return carrier is ulong id && id != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // True while your desk follows someone's Desk Swarm swarm (it flies to its spot every frame).
        public static bool DeskSwarmFollowing()
        {
            try
            {
                Type controller = AccessTools.TypeByName("DeskSwarm.SwarmController");
                object instance = controller != null ? AccessTools.Property(controller, "Instance")?.GetValue(null) : null;
                object swarms = instance != null ? AccessTools.Field(controller, "_swarms")?.GetValue(instance) : null;
                object following = swarms != null ? AccessTools.Property(swarms.GetType(), "Following")?.GetValue(swarms) : null;
                return following is ulong id && id != 0;
            }
            catch (Exception)
            {
                return false;
            }
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

        // Where standing up would put you while you sit (the spot you sat down from).
        public static Vector3 StandSpot(PlayerFocusController focus) => PreFocusPosition(focus);

        // Stands you up the way the stand-up key does: the input patch hands the game one press as
        // soon as it would take it. Given up after a second, so you never stand up out of the blue later.
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
            PlayerFocusController me = MyFocus;
            if (Time.unscaledTime > _standUpUntil || me == null || !me.IsFocus || DeskSwarmPiloting())
            {
                _standUpUntil = -1f;
                return false;
            }
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (tasks == null || !tasks.IsReadyForFocusQuit() || me.IsSelectorOpened)
            {
                return false;
            }
            _standUpUntil = -1f;
            return true;
        }

        // True while Desk Swarm is flying your desk with the keys (standing up would drop you).
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

        // True when Focus Anywhere is installed with its "any ground" setting on (sit anywhere).
        public static bool AnyGround()
        {
            if (_anyGround == null)
            {
                _anyGround = false;
                if (Chainloader.PluginInfos.TryGetValue("nelphei.ontogether.focusanywhere", out PluginInfo info) && info.Instance != null)
                {
                    _anyGround = !info.Instance.Config.TryGetEntry("Focus", "AnyGround", out ConfigEntry<bool> entry) || entry.Value;
                }
            }
            return _anyGround.Value;
        }

        // ---- Emotes ----

        public static int EmoteCount => ScriptableSingleton<GameSettings>.I.EmoteAnimStates.Count;

        // Plays emote #index (from 0) like the emote wheel does. Null when it did, else why not.
        public static string Emote(int index)
        {
            TextChannelManager channel = Channel;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (channel == null || tasks == null || channel.MainAnimationController == null)
            {
                return "Join a world first.";
            }
            if (MyFocus != null && MyFocus.IsFocus)
            {
                return "You can't emote while sitting. Stand up first (stand).";
            }
            // The wheel's own check: standing on the ground, no menu, not fishing, not typing.
            if (!tasks.IsReadyForEmotePanel())
            {
                return "You can't emote right now (in the water, a menu open, mid-jump...).";
            }
            EmoteController emotes = EmoteControllerField(channel.MainAnimationController);
            if (emotes == null)
            {
                return "The game isn't ready.";
            }
            channel.MainMovementController.ClosePropsOnHand();
            SetEmoteAnimMethod(emotes, index);
            return null;
        }

        // ---- Players ----

        // Everyone in the session your game has a character for, you included, in the player list's order.
        public static List<PlayerEntry> Players()
        {
            var players = new List<PlayerEntry>();
            if (!InWorld)
            {
                return players;
            }
            PlayerController me = MyPlayer;
            try
            {
                PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
                if (panel == null || panel.PlayerControllers == null)
                {
                    return players;
                }
                for (int i = 0; i < panel.PlayerControllers.Count; i++)
                {
                    PlayerController player = panel.PlayerControllers[i];
                    if (player == null)
                    {
                        continue;
                    }
                    ulong.TryParse(panel.PlayerSteamIDs != null && i < panel.PlayerSteamIDs.Count ? panel.PlayerSteamIDs[i] : "", out ulong id);
                    string name = PlayerNames.Of(player) ?? (id != 0 ? PlayerNames.Plain(SteamFriends.GetFriendPersonaName(new CSteamID(id))) : "someone");
                    name = ConsoleName(name);
                    players.Add(new PlayerEntry
                    {
                        Player = player,
                        SteamId = id,
                        Name = name,
                        PlainName = PlayerNames.WithoutTags(name).Trim(),
                        IsMe = player == me,
                    });
                }
            }
            catch (Exception)
            {
                // No player list yet.
            }
            return players;
        }

        // A styled name (a player's, a session's) for a console line: its colours and bold kept, but
        // not sizes or other layout, and every tag it opens closed after it.
        public static string ConsoleName(string name) => PlayerNames.ForText(LayoutTags.Replace(name ?? "", ""));

        // The player whose name starts with (or contains) what you typed, not you. Null and a reason if
        // none or several match.
        public static PlayerEntry? FindPlayer(string typed, out string why)
        {
            why = null;
            List<PlayerEntry> others = Players().FindAll(p => !p.IsMe);
            if (others.Count == 0)
            {
                why = "Nobody else is here.";
                return null;
            }
            foreach (Func<PlayerEntry, bool> rule in new Func<PlayerEntry, bool>[]
                     {
                         p => string.Equals(p.PlainName, typed, StringComparison.OrdinalIgnoreCase),
                         p => p.PlainName.StartsWith(typed, StringComparison.OrdinalIgnoreCase),
                         p => p.PlainName.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0,
                     })
            {
                List<PlayerEntry> matches = others.FindAll(p => rule(p));
                if (matches.Count == 1)
                {
                    return matches[0];
                }
                if (matches.Count > 1)
                {
                    why = $"{matches.Count} players match {typed}. Type more of the name.";
                    return null;
                }
            }
            why = $"Nobody here is called {typed}. Try players.";
            return null;
        }

        // The animation a player is playing (focus poses and emotes have their own numbers).
        public static int AnimOf(PlayerController player)
        {
            PlayerAnimationController anim = player != null ? player.GetComponent<PlayerAnimationController>() : null;
            Animator animator = anim != null ? AnimatorField(anim) : null;
            return animator != null ? animator.GetInteger(AnimStateParam) : -1;
        }

        public static Transform ArmatureParent(PlayerController player)
        {
            PlayerAnimationController anim = player != null ? player.GetComponent<PlayerAnimationController>() : null;
            return anim != null ? ArmatureParentField(anim) : null;
        }

        // The player's head bone, or null.
        public static Transform Head(PlayerController player) => Find(player.transform, HeadBone);

        // ---- The session ----

        // The Steam lobby of the session you're in, or Nil.
        public static CSteamID Lobby
        {
            get
            {
                if (!InWorld)
                {
                    return CSteamID.Nil;
                }
                try
                {
                    MultiplayerManager multiplayer = MonoSingleton<MultiplayerManager>.I;
                    return multiplayer != null && ulong.TryParse(multiplayer.LobbyCode, out ulong id) && id != 0
                        ? new CSteamID(id)
                        : CSteamID.Nil;
                }
                catch (Exception)
                {
                    return CSteamID.Nil;
                }
            }
        }

        public static bool IsHost => Channel != null && Channel.isHost;

        // PurrNet's tick counter (shared by everyone in the session), or null when not connected.
        public static TickManager Ticks
        {
            get
            {
                NetworkManager nm = NetworkManager.main;
                return nm != null && (nm.isClient || nm.isServer) ? nm.tickModule : null;
            }
        }

        // ---- Helpers ----

        private static bool IsPlayerOrPet(Collider collider)
        {
            return collider.GetComponentInParent<PlayerController>() != null ||
                   collider.GetComponentInParent<PetController>() != null;
        }

        public static Transform Find(Transform parent, string name)
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

        // The tail's floppy bones remember where they were, so a big jump would fling it. Same as
        // Teleport Spots.
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
