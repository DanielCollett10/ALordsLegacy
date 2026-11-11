using System;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>One step of a task definition.</summary>
    public readonly struct TaskStep
    {
        /// <summary>What the step does.</summary>
        public readonly StepKind Kind;
        /// <summary>Work duration in ticks (Work only; 0 for MoveTo).</summary>
        public readonly int DurationTicks;
        /// <summary>True if the step is heavy work (POP-A3).</summary>
        public readonly bool Heavy;

        private TaskStep(StepKind kind, int durationTicks, bool heavy)
        {
            Kind = kind; DurationTicks = durationTicks; Heavy = heavy;
        }

        /// <summary>A walk to the task target.</summary>
        public static TaskStep MoveTo(bool heavy = false) => new TaskStep(StepKind.MoveTo, 0, heavy);

        /// <summary>Work for <paramref name="durationTicks"/> ticks (at least 1).</summary>
        public static TaskStep Work(int durationTicks, bool heavy = false)
        {
            if (durationTicks < 1) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            return new TaskStep(StepKind.Work, durationTicks, heavy);
        }
    }
}
