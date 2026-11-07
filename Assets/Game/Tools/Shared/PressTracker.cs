using UnityEngine;

namespace LordsLegacy.Game.Tools.Shared
{
    /// <summary>Mouse buttons a tool cares about.</summary>
    public enum PressButton { Left = 0, Right = 1 }

    /// <summary>What a release turned out to be.</summary>
    public enum PressKind { None, Click, Drag, DoubleClick }

    /// <summary>Outcome of a release.</summary>
    public readonly struct PressResult
    {
        /// <summary>Click, Drag, DoubleClick, or None if the button was not pressed.</summary>
        public readonly PressKind Kind;
        /// <summary>Button released.</summary>
        public readonly PressButton Button;
        /// <summary>Screen position of the press.</summary>
        public readonly Vector2 StartPosition;
        /// <summary>Screen position of the release.</summary>
        public readonly Vector2 EndPosition;

        /// <summary>Creates a result.</summary>
        public PressResult(PressKind kind, PressButton button, Vector2 start, Vector2 end)
        {
            Kind = kind; Button = button; StartPosition = start; EndPosition = end;
        }
    }

    /// <summary>
    /// Tracks left and right buttons separately and classifies each press as click, drag or double click.
    /// A press that moves more than the drag threshold is a drag for good: a right drag never reports a Click.
    /// Time is passed in by the caller so this stays pure.
    /// </summary>
    public sealed class PressTracker
    {
        /// <summary>Pixels a press may move and still be a click.</summary>
        public const float DragThreshold = 4f;
        /// <summary>Pixels between two clicks for a double click.</summary>
        public const float DoubleClickRadius = 6f;

        private struct State
        {
            public bool Down, Dragging, HasLastClick;
            public Vector2 Start, LastClickPos;
            public float LastClickTime;
        }

        private readonly State[] _s = new State[2];
        private readonly float _doubleClickTime;

        /// <summary>Creates a tracker; pass the OS double-click time in seconds.</summary>
        public PressTracker(float doubleClickTimeSeconds = 0.5f)
        {
            _doubleClickTime = doubleClickTimeSeconds;
        }

        /// <summary>True while the button is held.</summary>
        public bool IsDown(PressButton b) { return _s[(int)b].Down; }

        /// <summary>True while the button is held and has moved past the drag threshold.</summary>
        public bool IsDragging(PressButton b) { return _s[(int)b].Dragging; }

        /// <summary>Records a button press.</summary>
        public void Press(PressButton b, Vector2 pos)
        {
            _s[(int)b].Down = true;
            _s[(int)b].Dragging = false;
            _s[(int)b].Start = pos;
        }

        /// <summary>Feeds the cursor position; marks held buttons as dragging once past the threshold.</summary>
        public void Move(Vector2 pos)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_s[i].Down && !_s[i].Dragging && (pos - _s[i].Start).magnitude > DragThreshold)
                    _s[i].Dragging = true;
            }
        }

        /// <summary>Records a release and classifies it.</summary>
        public PressResult Release(PressButton b, Vector2 pos, float time)
        {
            int i = (int)b;
            if (!_s[i].Down) return new PressResult(PressKind.None, b, pos, pos);
            Move(pos);
            Vector2 start = _s[i].Start;
            bool drag = _s[i].Dragging;
            _s[i].Down = false;
            _s[i].Dragging = false;
            if (drag)
            {
                _s[i].HasLastClick = false;
                return new PressResult(PressKind.Drag, b, start, pos);
            }
            if (_s[i].HasLastClick && time - _s[i].LastClickTime <= _doubleClickTime
                && (pos - _s[i].LastClickPos).magnitude <= DoubleClickRadius)
            {
                _s[i].HasLastClick = false;
                return new PressResult(PressKind.DoubleClick, b, start, pos);
            }
            _s[i].HasLastClick = true;
            _s[i].LastClickPos = pos;
            _s[i].LastClickTime = time;
            return new PressResult(PressKind.Click, b, start, pos);
        }

        /// <summary>Forgets all state (for example when the tool changes).</summary>
        public void Reset()
        {
            _s[0] = default;
            _s[1] = default;
        }
    }
}
