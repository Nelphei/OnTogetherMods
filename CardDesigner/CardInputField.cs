using TMPro;
using UnityEngine;

namespace CardDesigner
{
    // The window's typing box: TextMeshPro's own input box, which shows the styling as you type and
    // moves the caret over the hidden tags.
    //
    // The keyboard sends Ctrl+letter as a control character, and the box would type some of them:
    // Ctrl+I is a tab and Ctrl+M a new line. The window handles Ctrl+B, Ctrl+I, Ctrl+U, Ctrl+Z and
    // Ctrl+Y itself, so the box types nothing while Ctrl is held (pasting still works). AltGr counts
    // as Ctrl+Alt, so letters typed with it get through.
    internal sealed class CardInputField : TMP_InputField
    {
        private bool _pasting;

        protected override void Append(string input)
        {
            _pasting = true;
            try
            {
                base.Append(input);
            }
            finally
            {
                _pasting = false;
            }
        }

        protected override void Append(char input)
        {
            // A tab does nothing useful on the card.
            if (input == '\t' || (!_pasting && CtrlHeld))
            {
                return;
            }
            base.Append(input);
        }

        public static bool CtrlHeld
        {
            get
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || Input.GetKey(KeyCode.AltGr);
                return ctrl && !alt;
            }
        }
    }
}
