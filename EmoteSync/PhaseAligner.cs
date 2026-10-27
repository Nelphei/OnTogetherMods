using System.Collections.Generic;
using PurrNet.Modules;
using UnityEngine;

namespace EmoteSync
{
    // Everyone starts a synced emote on the same tick, but each player's animation reaches you a
    // little late (their ping). After a synced emote, this watches the other players. When one of
    // them starts the emote, it jumps their animation on your screen to where it should be by now.
    // Only your view changes; nothing is sent.
    internal sealed class PhaseAligner
    {
        private const float WatchSeconds = 4f;

        private sealed class Watch
        {
            public PlayerAnimationController Player;
            public Animator Animator;
            public int Hash;
            public float Time;
            public bool Done;
        }

        private readonly List<Watch> _watches = new List<Watch>();
        private int _animState;
        private uint _startTick;
        private double _endTick;
        private bool _active;

        public void Begin(int animState, uint startTick, TickManager ticks)
        {
            _watches.Clear();
            _active = false;
            if (!Plugin.Settings.AlignOthers.Value || ticks == null)
            {
                return;
            }

            PlayerAnimationController mine = NetworkSingleton<TextChannelManager>.I?.MainAnimationController;
            foreach (PlayerAnimationController player in Object.FindObjectsByType<PlayerAnimationController>(FindObjectsSortMode.None))
            {
                Animator animator = player == mine ? null : Game.GetAnimator(player);
                if (animator == null)
                {
                    continue;
                }
                var watch = new Watch { Player = player, Animator = animator };
                Read(animator, out watch.Hash, out watch.Time);
                _watches.Add(watch);
            }

            _animState = animState;
            _startTick = startTick;
            _endTick = startTick + WatchSeconds * ticks.tickRate;
            _active = _watches.Count > 0;
        }

        public void Stop()
        {
            _watches.Clear();
            _active = false;
        }

        // Call from LateUpdate, after the animators have updated this frame.
        public void Update(TickManager ticks)
        {
            if (!_active)
            {
                return;
            }
            if (ticks == null || ticks.syncedPreciseTick > _endTick)
            {
                Stop();
                return;
            }

            double elapsed = (ticks.syncedPreciseTick - _startTick) * ticks.tickDeltaDouble;
            foreach (Watch watch in _watches)
            {
                if (watch.Done || watch.Player == null || watch.Animator == null || !watch.Animator.isActiveAndEnabled)
                {
                    continue;
                }

                AnimatorStateInfo info = Read(watch.Animator, out int hash, out float time);
                // A new state, or the same state jumping back to the start (a restarted emote).
                bool started = hash != watch.Hash || time + 0.1f < watch.Time;
                watch.Hash = hash;
                watch.Time = time;
                if (!started || watch.Animator.GetInteger(Game.AnimStateParam) != _animState)
                {
                    continue;
                }

                watch.Done = true;
                if (elapsed < 0.05 || info.length <= 0f)
                {
                    continue;
                }

                float target = (float)(elapsed / info.length);
                if (target >= 1f)
                {
                    if (!info.loop)
                    {
                        continue;
                    }
                    target -= Mathf.Floor(target);
                }

                watch.Animator.Play(hash, 0, target);
                if (Plugin.Settings.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"Aligned {watch.Player.name}: {elapsed:0.000}s late, jumped to {target:0.000} of {info.length:0.00}s.");
                }
            }
        }

        // The state the animator is in, or heading into if it's mid-blend.
        private static AnimatorStateInfo Read(Animator animator, out int hash, out float time)
        {
            AnimatorStateInfo info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);
            hash = info.fullPathHash;
            time = info.normalizedTime;
            return info;
        }
    }
}
