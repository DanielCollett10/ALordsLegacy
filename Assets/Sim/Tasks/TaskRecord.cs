using System.Collections.Generic;
using LordsLegacy.Sim.Forestry;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>One task instance on the board. Mutated only through <see cref="TaskBoard"/>.</summary>
    public sealed class TaskRecord
    {
        private readonly List<TreeId> _reservations = new List<TreeId>();

        /// <summary>Unique id, never reused.</summary>
        public int Id { get; }
        /// <summary>The definition this task was built from.</summary>
        public TaskDef Def { get; }
        /// <summary>Owning operation id.</summary>
        public int OwnerOperationId { get; }
        /// <summary>Target tree, or null when the task has no tree target.</summary>
        public TreeId? Target { get; }
        /// <summary>Dispatch priority, fixed at creation. High tasks are handed out before Normal ones.</summary>
        public TaskPriority Priority { get; }
        /// <summary>Lifecycle state.</summary>
        public TaskState State { get; internal set; }
        /// <summary>Assigned citizen id, or null.</summary>
        public int? AssigneeId { get; internal set; }
        /// <summary>Index into <c>Def.Steps</c> of the current step.</summary>
        public int CurrentStep { get; internal set; }
        /// <summary>Ticks of progress in the current step.</summary>
        public int StepProgress { get; internal set; }
        /// <summary>What the task is for, shown in the citizen panel: the module name for construction tasks, or null.</summary>
        public string Subject { get; }
        /// <summary>The ox this task uses (T-289), or null.</summary>
        public int? OxId { get; internal set; }
        /// <summary>True if the ox was assigned to the owner operation when the task took it.</summary>
        public bool OxWasAssigned { get; internal set; }
        /// <summary>For an early-fetch haul (T-291, WC-23): the Fell task whose logs it will carry. The haul waits at
        /// the pile (before LoadOx) until that task is Done; null otherwise.</summary>
        public int? AwaitsTaskId { get; internal set; }
        /// <summary>True while a haul is ending its day at sunset (T-291, WC-35): the step runner owns the worker until the
        /// ox is back at its rest spot, so the routine does not send them home yet.</summary>
        public bool WindingDown { get; internal set; }
        /// <summary>True while the task needs an ox, has none reserved and is still Open (TSK-09, WCO-06).</summary>
        public bool IsWaitingForOx => State == TaskState.Open && Def.Required == RequiredResource.Ox && !OxId.HasValue;
        /// <summary>Trees this task holds reserved. Released on cancel or completion.</summary>
        public IReadOnlyList<TreeId> Reservations => _reservations;

        /// <summary>True when Done or Cancelled.</summary>
        public bool IsFinished => State == TaskState.Done || State == TaskState.Cancelled;

        internal TaskRecord(int id, TaskDef def, int ownerOperationId, TreeId? target, TaskPriority priority,
            string subject)
        {
            Id = id; Def = def; OwnerOperationId = ownerOperationId; Target = target; Priority = priority;
            Subject = subject;
        }

        internal void AddReservation(TreeId t) => _reservations.Add(t);
        internal void ClearReservations() => _reservations.Clear();
    }
}
