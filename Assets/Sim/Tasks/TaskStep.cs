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

        /// <summary>Optional id of the process stage this step runs (e.g. "stage.fell"), or null. Used to apply the
        /// stage's outcome when the step completes.</summary>
        public readonly string StageId;

        private TaskStep(StepKind kind, int durationTicks, bool heavy, string stageId)
        {
            Kind = kind; DurationTicks = durationTicks; Heavy = heavy; StageId = stageId;
        }

        /// <summary>A walk to the task target.</summary>
        public static TaskStep MoveTo(bool heavy = false) => new TaskStep(StepKind.MoveTo, 0, heavy, null);

        /// <summary>Work for <paramref name="durationTicks"/> ticks (at least 1).</summary>
        public static TaskStep Work(int durationTicks, bool heavy = false, string stageId = null)
        {
            if (durationTicks < 1) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            return new TaskStep(StepKind.Work, durationTicks, heavy, stageId);
        }
    }
}
