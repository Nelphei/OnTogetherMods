using System.Collections.Generic;
using ManagedDoom;
using ManagedDoom.UserInput;
using UnityEngine;

namespace HellDesk
{
    // The keyboard, for DOOM. DoomWindow passes keys on only while DOOM has the keyboard; this keeps
    // which ones are down and turns them into DOOM's movement each tic. Keyboard only, like the
    // original's default controls: no mouse look. Ported from Managed Doom's SilkUserInput (GPLv2).
    internal sealed class UnityUserInput : IUserInput
    {
        // Unity's keys and DOOM's. F keys are left out: other mods use them, and DOOM's menu
        // (Esc) has everything they do.
        public static readonly KeyValuePair<KeyCode, DoomKey>[] Keys = BuildKeys();

        private readonly Config _config;
        private readonly HashSet<DoomKey> _pressed = new HashSet<DoomKey>();
        private readonly bool[] _weaponKeys = new bool[7];
        private int _turnHeld;

        public UnityUserInput(Config config)
        {
            _config = config;
        }

        public void SetPressed(DoomKey key, bool down)
        {
            if (down)
            {
                _pressed.Add(key);
            }
            else
            {
                _pressed.Remove(key);
            }
        }

        public List<DoomKey> Held()
        {
            return new List<DoomKey>(_pressed);
        }

        // Every key let go, e.g. when DOOM loses the keyboard with keys still held.
        public void ReleaseAll()
        {
            _pressed.Clear();
        }

        public void BuildTicCmd(TicCmd cmd)
        {
            bool keyForward = IsPressed(_config.key_forward);
            bool keyBackward = IsPressed(_config.key_backward);
            bool keyStrafeLeft = IsPressed(_config.key_strafeleft);
            bool keyStrafeRight = IsPressed(_config.key_straferight);
            bool keyTurnLeft = IsPressed(_config.key_turnleft);
            bool keyTurnRight = IsPressed(_config.key_turnright);
            bool keyFire = IsPressed(_config.key_fire);
            bool keyUse = IsPressed(_config.key_use);
            bool keyRun = IsPressed(_config.key_run);
            bool keyStrafe = IsPressed(_config.key_strafe);

            _weaponKeys[0] = _pressed.Contains(DoomKey.Num1);
            _weaponKeys[1] = _pressed.Contains(DoomKey.Num2);
            _weaponKeys[2] = _pressed.Contains(DoomKey.Num3);
            _weaponKeys[3] = _pressed.Contains(DoomKey.Num4);
            _weaponKeys[4] = _pressed.Contains(DoomKey.Num5);
            _weaponKeys[5] = _pressed.Contains(DoomKey.Num6);
            _weaponKeys[6] = _pressed.Contains(DoomKey.Num7);

            cmd.Clear();

            int speed = keyRun ? 1 : 0;
            int forward = 0;
            int side = 0;
            if (_config.game_alwaysrun)
            {
                speed = 1 - speed;
            }

            _turnHeld = keyTurnLeft || keyTurnRight ? _turnHeld + 1 : 0;
            int turnSpeed = _turnHeld < PlayerBehavior.SlowTurnTics ? 2 : speed;

            if (keyStrafe)
            {
                if (keyTurnRight)
                {
                    side += PlayerBehavior.SideMove[speed];
                }
                if (keyTurnLeft)
                {
                    side -= PlayerBehavior.SideMove[speed];
                }
            }
            else
            {
                if (keyTurnRight)
                {
                    cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
                if (keyTurnLeft)
                {
                    cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
            }

            if (keyForward)
            {
                forward += PlayerBehavior.ForwardMove[speed];
            }
            if (keyBackward)
            {
                forward -= PlayerBehavior.ForwardMove[speed];
            }
            if (keyStrafeLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
            if (keyStrafeRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }

            if (keyFire)
            {
                cmd.Buttons |= TicCmdButtons.Attack;
            }
            if (keyUse)
            {
                cmd.Buttons |= TicCmdButtons.Use;
            }

            for (int i = 0; i < _weaponKeys.Length; i++)
            {
                if (_weaponKeys[i])
                {
                    cmd.Buttons |= TicCmdButtons.Change;
                    cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                    break;
                }
            }

            forward = Mathf.Clamp(forward, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
            side = Mathf.Clamp(side, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
            cmd.ForwardMove += (sbyte)forward;
            cmd.SideMove += (sbyte)side;
        }

        private bool IsPressed(KeyBinding binding)
        {
            foreach (DoomKey key in binding.Keys)
            {
                if (_pressed.Contains(key))
                {
                    return true;
                }
            }
            return false;
        }

        public void Reset()
        {
        }

        public void GrabMouse()
        {
        }

        public void ReleaseMouse()
        {
        }

        public int MaxMouseSensitivity => 15;

        public int MouseSensitivity
        {
            get => _config.mouse_sensitivity;
            set => _config.mouse_sensitivity = value;
        }

        private static KeyValuePair<KeyCode, DoomKey>[] BuildKeys()
        {
            var keys = new List<KeyValuePair<KeyCode, DoomKey>>();
            void Add(KeyCode unity, DoomKey doom) => keys.Add(new KeyValuePair<KeyCode, DoomKey>(unity, doom));

            for (int i = 0; i < 26; i++)
            {
                Add(KeyCode.A + i, DoomKey.A + i);
            }
            for (int i = 0; i < 10; i++)
            {
                Add(KeyCode.Alpha0 + i, DoomKey.Num0 + i);
                Add(KeyCode.Keypad0 + i, DoomKey.Numpad0 + i);
            }
            Add(KeyCode.Escape, DoomKey.Escape);
            Add(KeyCode.LeftControl, DoomKey.LControl);
            Add(KeyCode.RightControl, DoomKey.RControl);
            Add(KeyCode.LeftShift, DoomKey.LShift);
            Add(KeyCode.RightShift, DoomKey.RShift);
            Add(KeyCode.LeftAlt, DoomKey.LAlt);
            Add(KeyCode.RightAlt, DoomKey.RAlt);
            Add(KeyCode.LeftBracket, DoomKey.LBracket);
            Add(KeyCode.RightBracket, DoomKey.RBracket);
            Add(KeyCode.Semicolon, DoomKey.Semicolon);
            Add(KeyCode.Comma, DoomKey.Comma);
            Add(KeyCode.Period, DoomKey.Period);
            Add(KeyCode.Quote, DoomKey.Quote);
            Add(KeyCode.Slash, DoomKey.Slash);
            Add(KeyCode.Backslash, DoomKey.Backslash);
            Add(KeyCode.BackQuote, DoomKey.Tilde);
            Add(KeyCode.Equals, DoomKey.Equal);
            Add(KeyCode.Minus, DoomKey.Hyphen);
            Add(KeyCode.Space, DoomKey.Space);
            Add(KeyCode.Return, DoomKey.Enter);
            Add(KeyCode.KeypadEnter, DoomKey.Enter);
            Add(KeyCode.Backspace, DoomKey.Backspace);
            Add(KeyCode.Tab, DoomKey.Tab);
            Add(KeyCode.PageUp, DoomKey.PageUp);
            Add(KeyCode.PageDown, DoomKey.PageDown);
            Add(KeyCode.End, DoomKey.End);
            Add(KeyCode.Home, DoomKey.Home);
            Add(KeyCode.Insert, DoomKey.Insert);
            Add(KeyCode.Delete, DoomKey.Delete);
            Add(KeyCode.KeypadPlus, DoomKey.Add);
            Add(KeyCode.KeypadMinus, DoomKey.Subtract);
            Add(KeyCode.KeypadMultiply, DoomKey.Multiply);
            Add(KeyCode.KeypadDivide, DoomKey.Divide);
            Add(KeyCode.LeftArrow, DoomKey.Left);
            Add(KeyCode.RightArrow, DoomKey.Right);
            Add(KeyCode.UpArrow, DoomKey.Up);
            Add(KeyCode.DownArrow, DoomKey.Down);
            Add(KeyCode.Pause, DoomKey.Pause);
            return keys.ToArray();
        }
    }
}
