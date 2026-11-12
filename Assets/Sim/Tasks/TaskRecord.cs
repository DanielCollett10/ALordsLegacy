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
        /// <summary>Higher runs first.</summary>
        public int Priority { get; }
        /// <summary>Lifecycle state.</summary>
        public TaskState State { get; internal set; }
        /// <summary>Assigned citizen id, or null.</summary>
        public int? AssigneeId { get; internal set; }
        /// <summary>Index into <c>Def.Steps</c> of the current step.</summary>
        public int CurrentStep { get; internal set; }
        /// <summary>Ticks of progress in the current step.</summary>
        public int StepProgress { get; internal set; }
        /// <summary>The ox this task uses (T-289), or null.</summary>
        public int? OxId { get; internal set; }
        /// <summary>True if the ox was assigned to the owner operation when the task took it.</summary>
        public bool OxWasAssigned { get; internal set; }
        /// <summary>Trees this task holds reserved. Released on cancel or completion.</summary>
        public IReadOnlyList<TreeId> Reservations => _reservations;

        /// <summary>True when Done or Cancelled.</summary>
        public bool IsFinished => State == TaskState.Done || State == TaskState.Cancelled;

        internal TaskRecord(int id, TaskDef def, int ownerOperationId, TreeId? target, int priority)
        {
            Id = id; Def = def; OwnerOperationId = ownerOperationId; Target = target; Priority = priority;
        }

        internal void AddReservation(TreeId t) => _reservations.Add(t);
        internal void ClearReservations() => _reservations.Clear();
    }
}
