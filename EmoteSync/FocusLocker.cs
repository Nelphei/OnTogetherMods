using System;
using System.Collections.Generic;
using PurrNet.Modules;
using UnityEngine;

namespace EmoteSync
{
    // Keeps looping focus animations in step by tying each loop to the shared PurrNet clock:
    // at any moment, every loop of the same length is at the same point. Two people doing the
    // same activity then move together, no matter when or how each one started.
    // Only changes your own screen (nothing is sent), so it also hides other players' ping, and
    // players without the mod see the game exactly as before. Every modded player in the group
    // runs the same clock, so they all see the same thing.
    internal sealed class FocusLocker
    {
        // Smaller differences than this (in seconds) are left alone.
        private const double Tolerance = 0.08;
        // Corrections per animation start, so an animation the game speeds up or slows down
        // can't make us correct it every frame.
        private const int MaxFixes = 3;

        private sealed class Entry
        {
            public PlayerFocusController Focus;
            public PlayerAnimationController Player;
            public Animator Animator;
            public int Hash;
            public int Fixes;
        }

        private readonly Dictionary<PlayerFocusController, Entry> _entries = new Dictionary<PlayerFocusController, Entry>();
        private readonly HashSet<int> _focusAnims = new HashSet<int>();
        private readonly List<PlayerFocusController> _gone = new List<PlayerFocusController>();
        private float _nextScan;

        public void Reset()
        {
            _entries.Clear();
            _nextScan = 0f;
        }

        // Call from LateUpdate, after the animators have updated this frame.
        public void Update(TickManager ticks)
        {
            if (ticks == null)
            {
                return;
            }
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 2f;
                Scan();
            }

            PlayerAnimationController mine = NetworkSingleton<TextChannelManager>.I?.MainAnimationController;
            double clock = ticks.syncedPreciseTick * ticks.tickDeltaDouble;

            foreach (Entry entry in _entries.Values)
            {
                if (entry.Focus == null || entry.Player == null || entry.Animator == null || !entry.Animator.isActiveAndEnabled)
                {
                    continue;
                }

                bool focusing = entry.Player == mine
                    ? entry.Focus.IsFocus
                    : entry.Focus.IsFocus || _focusAnims.Contains(entry.Animator.GetInteger(Game.AnimStateParam));
                if (!focusing || entry.Animator.IsInTransition(0))
                {
                    entry.Hash = 0;
                    continue;
                }

                AnimatorStateInfo info = entry.Animator.GetCurrentAnimatorStateInfo(0);
                if (info.fullPathHash != entry.Hash)
                {
                    entry.Hash = info.fullPathHash;
                    entry.Fixes = 0;
                }
                if (!info.loop || info.length < 0.2f || entry.Fixes >= MaxFixes)
                {
                    continue;
                }

                double target = Fraction(clock / info.length);
                double offset = target - Fraction(info.normalizedTime);
                if (offset > 0.5)
                {
                    offset -= 1.0;
                }
                else if (offset < -0.5)
                {
                    offset += 1.0;
                }
                if (Math.Abs(offset) * info.length < Tolerance)
                {
                    continue;
                }

                entry.Fixes++;
                entry.Animator.Play(info.fullPathHash, 0, (float)target);
                MoveProps(entry, info.length, (float)target);

                if (Plugin.Settings.DebugLog.Value)
                {
                    Plugin.Log.LogInfo($"Focus loop of {entry.Player.name} moved {offset * info.length:+0.000;-0.000}s " +
                        $"to stay in step ({info.length:0.00}s loop).");
                }
            }
        }

        private void Scan()
        {
            _focusAnims.Clear();
            foreach (int anim in Game.FreeFocusAnimStates())
            {
                _focusAnims.Add(anim);
            }
            foreach (FocusAreaController area in UnityEngine.Object.FindObjectsByType<FocusAreaController>(FindObjectsSortMode.None))
            {
                if (area.FocusAreaSettings != null && area.FocusAreaSettings.AnimationStates != null)
                {
                    _focusAnims.UnionWith(area.FocusAreaSettings.AnimationStates);
                }
            }

            _gone.Clear();
            foreach (PlayerFocusController focus in _entries.Keys)
            {
                if (focus == null)
                {
                    _gone.Add(focus);
                }
            }
            foreach (PlayerFocusController focus in _gone)
            {
                _entries.Remove(focus);
            }

            foreach (PlayerFocusController focus in UnityEngine.Object.FindObjectsByType<PlayerFocusController>(FindObjectsSortMode.None))
            {
                if (_entries.ContainsKey(focus))
                {
                    continue;
                }
                PlayerAnimationController player = focus.GetComponent<PlayerAnimationController>()
                    ?? focus.GetComponentInParent<PlayerAnimationController>()
                    ?? focus.GetComponentInChildren<PlayerAnimationController>();
                Animator animator = player != null ? Game.GetAnimator(player) : null;
                if (animator != null)
                {
                    _entries[focus] = new Entry { Focus = focus, Player = player, Animator = animator };
                }
            }
        }

        // A prop (like a jump rope) may have its own animation that loops alongside the body.
        // Move any with the same loop length too, so they stay together.
        private static void MoveProps(Entry entry, float length, float target)
        {
            foreach (Animator prop in entry.Player.GetComponentsInChildren<Animator>())
            {
                if (prop == entry.Animator || !prop.isActiveAndEnabled || prop.runtimeAnimatorController == null ||
                    prop.layerCount == 0 || prop.IsInTransition(0))
                {
                    continue;
                }
                AnimatorStateInfo info = prop.GetCurrentAnimatorStateInfo(0);
                if (info.loop && Mathf.Abs(info.length - length) < length * 0.05f)
                {
                    prop.Play(info.fullPathHash, 0, target);
                }
            }
        }

        private static double Fraction(double value) => value - Math.Floor(value);
    }
}
