using System.Collections.Generic;

namespace CardDesigner
{
    // Undo and redo for one card line. The game's typing box has neither.
    //
    // Typing in a burst is one step: a new step starts after a pause of a second, or after a toolbar
    // change. Each step remembers the text and where the caret and selection were.
    internal sealed class History
    {
        private const int MaxSteps = 200;
        private const float BurstSeconds = 1f;

        public struct Step
        {
            public string Text;
            public int Anchor;
            public int Focus;
        }

        private readonly List<Step> _undo = new List<Step>();
        private readonly List<Step> _redo = new List<Step>();
        private float _lastTyped = -100f;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        // Call with the line as it was just before you typed.
        public void Typed(Step before, float now)
        {
            if (now - _lastTyped > BurstSeconds || _undo.Count == 0)
            {
                Push(_undo, before);
            }
            _lastTyped = now;
            _redo.Clear();
        }

        // Call with the line as it was just before a toolbar change.
        public void Changed(Step before)
        {
            Push(_undo, before);
            _lastTyped = -100f;
            _redo.Clear();
        }

        public bool Undo(Step current, out Step back)
        {
            return Move(_undo, _redo, current, out back);
        }

        public bool Redo(Step current, out Step forward)
        {
            return Move(_redo, _undo, current, out forward);
        }

        private bool Move(List<Step> from, List<Step> to, Step current, out Step step)
        {
            step = default;
            if (from.Count == 0)
            {
                return false;
            }
            step = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Push(to, current);
            _lastTyped = -100f;
            return true;
        }

        private static void Push(List<Step> list, Step step)
        {
            if (list.Count > 0 && list[list.Count - 1].Text == step.Text)
            {
                return;
            }
            list.Add(step);
            if (list.Count > MaxSteps)
            {
                list.RemoveAt(0);
            }
        }
    }
}
