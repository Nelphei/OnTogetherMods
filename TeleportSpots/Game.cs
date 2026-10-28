using PurrNet;
using UnityEngine;

namespace TeleportSpots
{
    // Reading and moving the local player. See reference/Assembly-CSharp/PlayerMovementController.cs,
    // TaskManager.cs (IsReadyForFishing) and FollowScript.cs.
    internal static class Game
    {
        // Why the player can't save a spot or teleport right now, or null if they can.
        // Based on the game's own "free to fish" check, but swimming is allowed: the game works
        // out whether you're in water from your position every frame.
        public static string WhyBusy(TextChannelManager channel)
        {
            PlayerMovementController move = channel != null ? channel.MainMovementController : null;
            TaskManager tasks = MonoSingleton<TaskManager>.I;
            if (move == null || tasks == null)
            {
                return "Join a world first";
            }

            // Covers seats, the swan and every focus activity. Leaving focus puts you back where you
            // started it, so teleporting from there wouldn't stick.
            PlayerFocusController focus = channel.MainFocusController;
            if (focus != null && focus.IsFocus)
            {
                return "Stand up first";
            }
            if (tasks.LockState == LockState.Music)
            {
                return "Stop playing music first";
            }
            if (tasks.LockState != LockState.Free)
            {
                return "Close the open menu first";
            }
            if (tasks.IsWriting)
            {
                return "Close the chat first";
            }

            FishingManager fishing = MonoSingleton<FishingManager>.I;
            if (fishing != null && fishing.FishingState != FishingState.None)
            {
                return "Stop fishing first";
            }
            BasketBallManager basketball = NetworkSingleton<BasketBallManager>.I;
            if (basketball != null && basketball.IsBallOnMyHand)
            {
                return "Let go of the ball first";
            }

            if (move.IsStair)
            {
                return "Get off the stairs first";
            }
            if (move.IsAnimation)
            {
                return "Wait for the animation to finish";
            }
            bool walking = move.MovementState == MovementState.Idle || move.MovementState == MovementState.Walk;
            if (!walking || (move.GroundState != GroundState.Grounded && !move.IsInWater))
            {
                return "Land first";
            }
            return null;
        }

        public static Spot Capture(PlayerMovementController move, string name)
        {
            Transform player = move.transform;
            Transform follower = Follower();
            return new Spot
            {
                Name = name,
                Position = player.position,
                Facing = player.eulerAngles.y,
                CameraYaw = follower != null ? follower.eulerAngles.y : player.eulerAngles.y,
            };
        }

        public static void Teleport(PlayerMovementController move, Spot spot, bool restoreView)
        {
            // The game moves you the same way when you fall off the world. The movement collider
            // stays on, so the game notices you leaving areas like the stairs or the lemonade stand.
            // SyncTransforms tells the physics engine right away, so the next move starts from here.
            Transform player = move.transform;
            player.SetPositionAndRotation(spot.Position, Quaternion.Euler(0f, spot.Facing, 0f));
            Physics.SyncTransforms();

            // The camera follows an invisible "follower" object that copies your position every frame
            // and holds the camera's turn. Move it now, then tell Cinemachine to cut to the new place
            // instead of gliding across the map.
            Transform follower = Follower();
            if (follower != null)
            {
                if (restoreView)
                {
                    follower.rotation = Quaternion.Euler(0f, spot.CameraYaw, 0f);
                }
                FollowScript follow = follower.GetComponent<FollowScript>();
                if (follow != null)
                {
                    follow.Update();
                }
            }
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            if (scene != null && scene.PlayerVCamera != null)
            {
                scene.PlayerVCamera.PreviousStateIsValid = false;
            }

            // Your game decides where your character is and sends it to everyone. ForceSync sends it
            // right now and tells the others to jump straight there instead of sliding (the game does
            // this for the basketball).
            NetworkTransform net = move.GetComponent<NetworkTransform>();
            if (net != null)
            {
                net.ForceSync();
            }
        }

        private static Transform Follower()
        {
            MainSceneManager scene = MonoSingleton<MainSceneManager>.I;
            return scene != null ? scene.PlayerFollower : null;
        }
    }
}
