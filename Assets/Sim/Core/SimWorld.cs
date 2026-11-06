using System;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// Root of the simulation. Owns the clock and, later, all game state.
    /// Pure C#: no UnityEngine. One tick is one game minute. The Unity layer calls
    /// <see cref="Step"/> and reads state to render it.
    /// </summary>
    public sealed class SimWorld
    {
        private readonly GameCalendar _calendar;

        /// <summary>The tuning this world was built from.</summary>
        public SimTuning Tuning { get; }

        /// <summary>Number of ticks simulated since the world was created. One tick = one game minute.</summary>
        public long Tick { get; private set; }

        /// <summary>Calendar date and time for the current tick (named Now to avoid UnityEngine.Time).</summary>
        public GameTime Now => _calendar.At(Tick);

        /// <summary>True while the simulation is paused; <see cref="Step"/> does nothing.</summary>
        public bool Paused { get; private set; }

        /// <summary>Creates a world whose calendar starts at the tuning's start date, weekday and year length.</summary>
        public SimWorld(SimTuning tuning)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _calendar = new GameCalendar(tuning.StartMonth, tuning.StartDay, tuning.StartHour,
                tuning.StartMinute, tuning.StartWeekday, tuning.DaysPerYear);
        }

        /// <summary>Pause the simulation. Safe to call when already paused.</summary>
        public void Pause() => Paused = true;

        /// <summary>Resume the simulation. Safe to call when not paused.</summary>
        public void Resume() => Paused = false;

        /// <summary>Advance the simulation by <paramref name="ticks"/> game minutes; nothing while paused.</summary>
        public void Step(int ticks = 1)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (Paused) return;
            Tick += ticks;
        }
    }
}
