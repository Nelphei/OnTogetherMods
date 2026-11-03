using OnTogetherMods.Shared;
using UnityEngine;

namespace FocusAnywhere
{
    // The mod's icon in the row of mod icons (Shared/IconBar.cs). Clicking it switches the whole mod
    // on or off. The patches read Settings.Enabled as they run, so it takes effect right away; the
    // focus menu's extra activities go away the next time you're not focusing (UpdatePatch).
    internal sealed class IconButton : MonoBehaviour
    {
        private static Settings S => Plugin.Settings;

        private bool _clicked;

        private void Update()
        {
            if (_clicked)
            {
                _clicked = false;
                S.Enabled.Value = !S.Enabled.Value;
                Plugin.Log.LogInfo(S.Enabled.Value ? "Focus Anywhere on." : "Focus Anywhere off.");
            }
        }

        private void OnGUI()
        {
            if (S.ShowIcon.Value &&
                IconBar.Draw("FocusAnywhere", IconBar.FocusAnywhereOrder, Icon.Pixels,
                    S.Enabled.Value ? IconBar.State.On : IconBar.State.Off, "Focus Anywhere", KeyCode.None,
                    "Every activity, on any ground or in the air") == IconBar.Click.Left)
            {
                _clicked = true;   // acted on in Update
            }
        }
    }
}
