namespace SoapBox
{
    // What the player is pressing, for the cart. The window fills it in every frame from the
    // keyboard, the mouse and clicks on the handheld's buttons; the runner reads it at each update.
    // Presses are kept until an update has seen them, so a quick tap between two updates still counts.
    internal sealed class Controls
    {
        public const int Left = 0, Right = 1, Up = 2, Down = 3, A = 4, B = 5, Count = 6;

        public static readonly string[] Names = { "left", "right", "up", "down", "a", "b" };

        // One bit per button.
        public int Held;
        public int Pressed;

        // The mouse, in screen pixels (outside the screen too). Bit 0 left button, bit 1 right.
        public int MouseX;
        public int MouseY;
        public int MouseHeld;
        public int MousePressed;
        public float Wheel;

        // Sets which buttons are down now, noting the ones that just went down.
        public void SetHeld(int held)
        {
            Pressed |= held & ~Held;
            Held = held;
        }

        public void SetMouse(int x, int y, int held, float wheel)
        {
            MouseX = x;
            MouseY = y;
            MousePressed |= held & ~MouseHeld;
            MouseHeld = held;
            Wheel += wheel;
        }

        // Everything let go (the window lost the keyboard).
        public void Release()
        {
            Held = 0;
            Pressed = 0;
            MouseHeld = 0;
            MousePressed = 0;
            Wheel = 0f;
        }
    }
}
