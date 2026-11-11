using OnTogetherMods.Shared;
using UnityEngine;

namespace FocusAnywhere
{
    // The mod's icon in the row of mod icons (Shared/IconBar.cs). Clicking it switches the whole mod
    // on or off. The patches read Settings.Enabled as they run, so it takes effect right away; the
    // focus menu's extra activities go away the next time you're not focusing (UpdatePatch).
    // Right-clicking it (while the mod is on) switches HideObjects, which NoObjects follows at once.
    internal sealed class IconButton : MonoBehaviour
    {
        private static Settings S => Plugin.Settings;

        private const string Hint = "Every activity, on any ground or in the air";
        private const string HintObjectsShown = Hint + "\nRight-click: hide the desk and objects";
        private const string HintObjectsHidden = Hint + "\nDesk and objects hidden. Right-click: show them";

        private bool _clicked;
        private bool _rightClicked;

        private void Update()
        {
            if (_clicked)
            {
                _clicked = false;
                S.Enabled.Value = !S.Enabled.Value;
                Plugin.Log.LogInfo(S.Enabled.Value ? "Focus Anywhere on." : "Focus Anywhere off.");
            }
            if (_rightClicked)
            {
                _rightClicked = false;
                if (S.Enabled.Value)
                {
                    S.HideObjects.Value = !S.HideObjects.Value;
                    Plugin.Log.LogInfo(S.HideObjects.Value ? "Desk and objects off." : "Desk and objects back on.");
                }
            }
        }

        private void OnGUI()
        {
            if (!S.ShowIcon.Value)
            {
                return;
            }
            string hint = !S.Enabled.Value ? Hint : S.HideObjects.Value ? HintObjectsHidden : HintObjectsShown;
            IconBar.Click click = IconBar.Draw("FocusAnywhere", IconBar.FocusAnywhereOrder, Icon.Pixels,
                S.Enabled.Value ? IconBar.State.On : IconBar.State.Off, "Focus Anywhere", KeyCode.None, hint);
            if (click == IconBar.Click.Left)
            {
                _clicked = true;   // acted on in Update
            }
            else if (click == IconBar.Click.Right)
            {
                _rightClicked = true;
            }
        }
    }
}
