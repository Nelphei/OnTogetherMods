using OnTogetherMods.Shared;
using UnityEngine;

namespace NetworkCrashFix
{
    // The mod's icon (a switch for the fix), and a few log lines that show the fix at work.
    internal sealed class IconButton : MonoBehaviour
    {
        private const float ReportSeconds = 300f;

        private static Settings S => Plugin.Settings;

        private bool _clicked;
        private bool _loggedFirst;
        private long _reported;
        private float _nextReport = ReportSeconds;

        private void Update()
        {
            if (_clicked)
            {
                _clicked = false;
                S.Enabled.Value = !S.Enabled.Value;
            }

            long count = SafeReads.Count;
            if (!_loggedFirst && count > 0)
            {
                _loggedFirst = true;
                Plugin.Log.LogInfo("Made the first read at the end of a network packet safe. Without the fix, " +
                    "reads like this could crash the game.");
            }
            if (Time.unscaledTime >= _nextReport)
            {
                _nextReport = Time.unscaledTime + ReportSeconds;
                if (S.DebugLog.Value && count > _reported)
                {
                    Plugin.Log.LogInfo($"Made {count - _reported} reads at the end of a packet safe in the last " +
                        $"{ReportSeconds / 60f:0} minutes.");
                }
                _reported = count;
            }
        }

        private void OnGUI()
        {
            // First, so it lines up with the other mods' icons.
            if (S.ShowIcon.Value &&
                IconBar.Draw("NetworkCrashFix", IconBar.NetworkCrashFixOrder, Icon.Pixels,
                    S.Enabled.Value ? IconBar.State.On : IconBar.State.Off, Plugin.PluginName, KeyCode.None,
                    "Stops a crash in the game's networking code") == IconBar.Click.Left)
            {
                _clicked = true;   // acted on in Update
            }
        }
    }
}
