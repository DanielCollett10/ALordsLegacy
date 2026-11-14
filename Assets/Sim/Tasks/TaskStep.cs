using System;
using LordsLegacy.Sim.Navigation;

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

        /// <summary>LeadTo: destination x in metres.</summary>
        public readonly double X;
        /// <summary>LeadTo: destination z in metres.</summary>
        public readonly double Z;
        /// <summary>LoadOx / UnloadOx: ledger location id the logs come from / go to.</summary>
        public readonly int LocationId;
        /// <summary>LoadOx / UnloadOx: number of logs moved.</summary>
        public readonly int Count;

        private TaskStep(StepKind kind, int durationTicks, bool heavy, string stageId,
            double x = 0, double z = 0, int locationId = 0, int count = 0)
        {
            Kind = kind; DurationTicks = durationTicks; Heavy = heavy; StageId = stageId;
            X = x; Z = z; LocationId = locationId; Count = count;
        }

        /// <summary>Walk to the task's ox and attach it (ox state Led).</summary>
        public static TaskStep FetchOx() => new TaskStep(StepKind.FetchOx, 0, false, null);

        /// <summary>Lead the attached ox to a point (the handler's move order; the ox follows).</summary>
        public static TaskStep LeadTo(PathPoint point) => new TaskStep(StepKind.LeadTo, 0, false, null, point.X, point.Z);

        /// <summary>Load <paramref name="count"/> logs from ledger location <paramref name="pileLocationId"/> onto the ox,
        /// working <paramref name="durationTicks"/> minutes first.</summary>
        public static TaskStep LoadOx(int pileLocationId, int count, int durationTicks, bool heavy = false)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (durationTicks < 1) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            return new TaskStep(StepKind.LoadOx, durationTicks, heavy, null, 0, 0, pileLocationId, count);
        }

        /// <summary>Unload <paramref name="count"/> logs from the ox into ledger location <paramref name="locationId"/>,
        /// working <paramref name="durationTicks"/> minutes first.</summary>
        public static TaskStep UnloadOx(int locationId, int count, int durationTicks, bool heavy = false)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (durationTicks < 1) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            return new TaskStep(StepKind.UnloadOx, durationTicks, heavy, null, 0, 0, locationId, count);
        }

        /// <summary>Lead the ox to its rest spot and detach it.</summary>
        public static TaskStep ReturnOx() => new TaskStep(StepKind.ReturnOx, 0, false, null);

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
