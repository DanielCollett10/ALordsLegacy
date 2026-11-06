using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Calendar
{
    /// <summary>
    /// Owns the selected game speed and turns real elapsed seconds into a number of Sim ticks
    /// (1 tick = 1 game minute). The host calls <see cref="Advance"/> once per frame and runs that
    /// many <c>SimWorld.Step</c> calls. Pause is not here: <c>SimWorld</c> owns pause (decision 0006);
    /// while paused the host runs nothing and calls <see cref="Reset"/>.
    /// Rate: <see cref="GameMinutesPerRealSecond"/> = <c>GameMinutesPerRealSecondAt1x</c> × <see cref="CurrentSpeed"/>
    /// (CLK-01, TIME-A2, TIME-A3). Read live from the tuning, so changing the day length needs no code change.
    /// </summary>
    public sealed class TickPacer
    {
        private readonly SimTuning _tuning;
        private readonly int[] _speeds;
        private int _speedIndex;
        private double _carry;

        /// <param name="tuning">Source of the speed list, day length and per-frame tick cap.
        /// The speed list is copied at construction; the other values are read on every call.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tuning"/> is null.</exception>
        /// <exception cref="ArgumentException">The speed list is empty or holds a value of 0 or less.</exception>
        public TickPacer(SimTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            if (tuning.Speeds == null || tuning.Speeds.Length == 0)
                throw new ArgumentException("Speeds must contain at least one entry.", nameof(tuning));
            foreach (int s in tuning.Speeds)
            {
                if (s <= 0) throw new ArgumentException("Every speed must be greater than 0.", nameof(tuning));
            }
            _tuning = tuning;
            _speeds = (int[])tuning.Speeds.Clone();
        }

        /// <summary>Selectable speed multipliers (× of 1×), in the order given by the tuning.</summary>
        public IReadOnlyList<int> Speeds => _speeds;

        /// <summary>Index into <see cref="Speeds"/> of the selected speed. Starts at 0 (1×).</summary>
        public int CurrentSpeedIndex => _speedIndex;

        /// <summary>The selected speed multiplier (× of 1×).</summary>
        public int CurrentSpeed => _speeds[_speedIndex];

        /// <summary>Game minutes per real second at the current speed. Unit: game minutes per real second.</summary>
        public double GameMinutesPerRealSecond => _tuning.GameMinutesPerRealSecondAt1x * CurrentSpeed;

        /// <summary>
        /// The carried-over part of a tick after the last <see cref="Advance"/>, in [0, 1). Use it to
        /// interpolate visuals between ticks. Zero after a capped call or after <see cref="Reset"/>.
        /// </summary>
        public double Fraction => _carry;

        /// <summary>
        /// True if the last <see cref="Advance"/> hit <see cref="SimTuning.MaxTicksPerFrame"/> and dropped
        /// the excess ticks (CLK-05): the game is running slower than real time at this speed.
        /// </summary>
        public bool SlowedDown { get; private set; }

        /// <summary>Selects a speed by index into <see cref="Speeds"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The index is not a valid entry in <see cref="Speeds"/>.</exception>
        public void SetSpeed(int index)
        {
            if (index < 0 || index >= _speeds.Length)
                throw new ArgumentOutOfRangeException(nameof(index),
                    "Speed index must be from 0 to " + (_speeds.Length - 1) + ".");
            _speedIndex = index;
        }

        /// <summary>
        /// Converts real seconds into the whole ticks due now. The fractional remainder is carried into
        /// the next call. If more than <see cref="SimTuning.MaxTicksPerFrame"/> ticks are due, returns the
        /// cap, drops the excess and the carry-over, and sets <see cref="SlowedDown"/>.
        /// </summary>
        /// <param name="realSeconds">Real seconds since the last call (e.g. <c>Time.deltaTime</c>). Must be finite and ≥ 0.</param>
        /// <returns>Number of Sim ticks to run this frame.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="realSeconds"/> is negative, NaN or infinite.</exception>
        public int Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds), "Real seconds must be finite and not negative.");

            double total = _carry + realSeconds * GameMinutesPerRealSecond;
            double whole = Math.Floor(total);
            int cap = _tuning.MaxTicksPerFrame;

            if (whole > cap)
            {
                _carry = 0;
                SlowedDown = true;
                return cap;
            }

            _carry = total - whole;
            SlowedDown = false;
            return (int)whole;
        }

        /// <summary>
        /// Clears the carry-over and <see cref="SlowedDown"/>. The host calls this while paused so that
        /// time spent paused does not turn into ticks on resume.
        /// </summary>
        public void Reset()
        {
            _carry = 0;
            SlowedDown = false;
        }
    }
}
