using UnityEngine;

namespace NetworkCrashFix
{
    // A few log lines that show the fix at work.
    internal sealed class Reporter : MonoBehaviour
    {
        private const float ReportSeconds = 300f;

        private bool _loggedFirst;
        private long _reported;
        private float _nextReport = ReportSeconds;

        private void Update()
        {
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
                if (Plugin.Settings.DebugLog.Value && count > _reported)
                {
                    Plugin.Log.LogInfo($"Made {count - _reported} reads at the end of a packet safe in the last " +
                        $"{ReportSeconds / 60f:0} minutes.");
                }
                _reported = count;
            }
        }
    }
}
