using System;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Real-time countdown for the autosave (saving.md SV-11). Plain C#, no Unity types, so it can be tested without an
    /// Editor. The caller feeds it real seconds while the Game scene runs and the menu is closed; it is not affected by
    /// the Sim pause, so paused time counts. <see cref="Restart"/> is called after every save.
    /// </summary>
    public sealed class AutosaveTimer
    {
        /// <summary>Real seconds between autosaves.</summary>
        public double IntervalSeconds { get; }

        /// <summary>Real seconds since the last save or start.</summary>
        public double ElapsedSeconds { get; private set; }

        /// <summary>True once the elapsed time has reached the interval.</summary>
        public bool IsDue => ElapsedSeconds >= IntervalSeconds;

        /// <summary>Creates a timer. The interval must be positive.</summary>
        public AutosaveTimer(double intervalSeconds)
        {
            if (!(intervalSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "The autosave interval must be positive.");
            IntervalSeconds = intervalSeconds;
        }

        /// <summary>Adds real seconds. Negative or non-finite values are ignored.</summary>
        public void Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds <= 0) return;
            ElapsedSeconds += realSeconds;
        }

        /// <summary>Starts the count again from zero (after any save).</summary>
        public void Restart() => ElapsedSeconds = 0;
    }
}
