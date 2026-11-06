using System;
using Kirurobo;
using OnTogetherMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuickHide
{
    // Hides the whole game window in the split-screen and mini (Desk) window modes, and brings it back.
    //
    // In those modes the game is a see-through, always-on-top window over the desktop, run by the
    // UniWindowController plugin. Its alphaValue fades the whole window (world, characters, UI and mod
    // windows together), so 0 hides everything at once while the game keeps running. The plugin also
    // makes the window catch clicks wherever something is drawn, so that check is switched off while
    // hidden and every click goes through to the apps behind.
    internal sealed class HideController : MonoBehaviour
    {
        // The game's scenes by build index: 1 is the main menu, 2 is the world.
        private const int WorldScene = 2;

        private static Settings S => Plugin.Settings;

        private bool _hidden;
        private ResolutionMode _hiddenMode;
        private float _savedAlpha = 1f;
        private bool _savedHitTest = true;

        // The window mode this frame, or null outside the world.
        private ResolutionMode? _mode;
        private bool _wasFocused;
        private bool _iconClicked;

        // On Windows the key is read straight from the keyboard, so it works from other apps too.
        private bool _windows;
        private KeyCode _keyFor = KeyCode.None;
        private int _virtualKey;
        private bool _keyWasDown;

        private bool CanHide => _mode.HasValue && _mode.Value != ResolutionMode.Normal;

        private void Awake()
        {
            _windows = Application.platform == RuntimePlatform.WindowsPlayer;
            _wasFocused = Application.isFocused;
        }

        private void OnDestroy()
        {
            if (_hidden)
            {
                Show("Quick Hide was unloaded");
            }
        }

        private void Update()
        {
            _mode = CurrentMode();

            // A click on the icon (Shared/IconBar.cs) from the last OnGUI.
            bool clicked = _iconClicked;
            _iconClicked = false;
            bool pressed = KeyPressed();
            bool focused = Application.isFocused;
            bool focusGained = focused && !_wasFocused;
            _wasFocused = focused;

            if (_hidden)
            {
                // Leaving the world puts the game back in the Normal mode first, so this covers that too.
                if (_mode != _hiddenMode)
                {
                    Show("the window mode changed");
                }
                else if (pressed)
                {
                    Show("the key was pressed");
                }
                else if (focusGained)
                {
                    // Clicked in the taskbar, or picked with Alt+Tab.
                    Show("the game window was picked");
                }
                return;
            }

            if ((pressed || clicked) && CanHide)
            {
                Hide(_mode.Value);
            }
        }

        // Only the icon. Nothing else is drawn, so it's always first.
        private void OnGUI()
        {
            if (!S.ShowIcon.Value)
            {
                return;
            }
            KeyCode key = S.HideKey.Value;
            string hint = !CanHide ? "Works in split screen and mini mode (keys 2-4)"
                : key != KeyCode.None ? $"Hides the whole game. {key} brings it back"
                : "Hides the whole game. Click On-Together in the taskbar to bring it back";
            if (IconBar.Draw("QuickHide", IconBar.QuickHideOrder, Icon.Pixels,
                    CanHide ? IconBar.State.Plain : IconBar.State.Off, "Quick Hide", key, hint) == IconBar.Click.Left)
            {
                _iconClicked = true;   // acted on in Update
            }
        }

        private static ResolutionMode? CurrentMode()
        {
            // On the main menu MonoSingleton.I searches the scene, so only ask in the world.
            if (SceneManager.GetActiveScene().buildIndex != WorldScene)
            {
                return null;
            }
            OverlayManager overlay = MonoSingleton<OverlayManager>.I;
            return overlay != null ? overlay.ResolutionMode : (ResolutionMode?)null;
        }

        // ---- Hiding ----

        private void Hide(ResolutionMode mode)
        {
            UniWindowController window = UniWindowController.current;
            if (window == null)
            {
                Plugin.Log.LogWarning("Couldn't find the game's window, so nothing was hidden.");
                return;
            }

            _savedAlpha = window.alphaValue > 0f ? window.alphaValue : 1f;
            _savedHitTest = window.isHitTestEnabled;
            window.isHitTestEnabled = false;
            window.isClickThrough = true;
            window.alphaValue = 0f;
            _hidden = true;
            _hiddenMode = mode;

            // If you were in the game, typing would still go to it, hidden: chat, walking, the window
            // mode keys. Hand the keyboard to the app behind instead.
            string keyboardTo = null;
            if (_windows && Application.isFocused)
            {
                try
                {
                    keyboardTo = Win32.FocusWindowBehind();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Couldn't give the keyboard to the window behind the game: {e.Message}");
                }
            }
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Hid the game in {mode} mode." +
                    (keyboardTo != null ? $" Keyboard handed to {keyboardTo}." : ""));
            }
        }

        private void Show(string why)
        {
            _hidden = false;
            UniWindowController window = UniWindowController.current;
            if (window == null)
            {
                return;
            }
            window.alphaValue = _savedAlpha;
            // From the next frame the plugin's own check lets clicks through only where nothing is drawn.
            window.isHitTestEnabled = _savedHitTest;
            if (!window.isTransparent)
            {
                window.isClickThrough = false;
            }
            if (S.DebugLog.Value)
            {
                Plugin.Log.LogInfo($"Showed the game: {why}.");
            }
        }

        // ---- The key ----

        // True on the frame the key goes down.
        private bool KeyPressed()
        {
            KeyCode key = S.HideKey.Value;
            if (key == KeyCode.None)
            {
                _keyWasDown = false;
                return false;
            }
            if (_windows)
            {
                if (key != _keyFor)
                {
                    _keyFor = key;
                    _virtualKey = Win32.VirtualKey(key);
                    _keyWasDown = false;
                    if (_virtualKey == 0)
                    {
                        Plugin.Log.LogWarning($"{key} can't be read while another app is focused, so it only " +
                            "works while the game is. F keys, letters, numbers, Pause and Scroll Lock work everywhere.");
                    }
                }
                if (_virtualKey != 0)
                {
                    try
                    {
                        bool down = Win32.IsKeyDown(_virtualKey);
                        bool pressed = down && !_keyWasDown;
                        _keyWasDown = down;
                        return pressed;
                    }
                    catch (Exception e)
                    {
                        _windows = false;
                        Plugin.Log.LogWarning($"Couldn't read the keyboard directly, so {key} only works while the " +
                            $"game is focused: {e.Message}");
                    }
                }
            }
            return Input.GetKeyDown(key);
        }
    }
}
