using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Forestry;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// All tasks in ascending id order (TSK-01..06). State changes go through this class so that illegal
    /// transitions are refused and reservations are released. Every change bumps <see cref="Version"/>.
    /// A task uses its own id as the owner id when reserving trees.
    /// </summary>
    public sealed class TaskBoard
    {
        private readonly SortedDictionary<int, TaskRecord> _byId = new SortedDictionary<int, TaskRecord>();
        private readonly TreeRegistry _trees;
        private readonly HashSet<int> _busy = new HashSet<int>();
        private int _nextId = 1;

        /// <summary>True while the citizen holds an Assigned or Active task (WC-26: such a worker is not idle).</summary>
        public bool IsBusy(int citizenId) => _busy.Contains(citizenId);

        /// <summary>Builds a board that reserves trees in <paramref name="trees"/>.</summary>
        public TaskBoard(TreeRegistry trees)
        {
            _trees = trees ?? throw new ArgumentNullException(nameof(trees));
        }

        /// <summary>Increases by one on every successful change.</summary>
        public int Version { get; private set; }

        /// <summary>Number of tasks, finished ones included.</summary>
        public int Count => _byId.Count;

        /// <summary>Tasks in ascending id order.</summary>
        public IEnumerable<TaskRecord> All => _byId.Values;

        /// <summary>The task with this id, or null.</summary>
        public TaskRecord Get(int id) => _byId.TryGetValue(id, out var t) ? t : null;

        /// <summary>Creates an Open task. Throws if <paramref name="def"/> is null.</summary>
        public TaskRecord Create(TaskDef def, int ownerOperationId, TreeId? target = null, int priority = 0)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var t = new TaskRecord(_nextId++, def, ownerOperationId, target, priority) { State = TaskState.Open };
            _byId.Add(t.Id, t);
            Version++;
            return t;
        }

        /// <summary>Tasks of one owner operation, id order, finished ones included.</summary>
        public List<TaskRecord> ByOwner(int ownerOperationId)
        {
            var r = new List<TaskRecord>();
            foreach (var t in _byId.Values) if (t.OwnerOperationId == ownerOperationId) r.Add(t);
            return r;
        }

        /// <summary>Unfinished tasks assigned to one citizen, id order.</summary>
        public List<TaskRecord> ByAssignee(int citizenId)
        {
            var r = new List<TaskRecord>();
            foreach (var t in _byId.Values)
                if (!t.IsFinished && t.AssigneeId == citizenId) r.Add(t);
            return r;
        }

        /// <summary>Tasks in a state, id order.</summary>
        public List<TaskRecord> InState(TaskState state)
        {
            var r = new List<TaskRecord>();
            foreach (var t in _byId.Values) if (t.State == state) r.Add(t);
            return r;
        }

        /// <summary>Open to Assigned. False if the task is unknown or not Open.</summary>
        public bool Assign(int taskId, int citizenId)
        {
            var t = Get(taskId);
            if (t == null || t.State != TaskState.Open) return false;
            t.State = TaskState.Assigned; t.AssigneeId = citizenId;
            _busy.Add(citizenId);
            Version++;
            return true;
        }

        /// <summary>Assigned to Active. False if the task is unknown or not Assigned.</summary>
        public bool Start(int taskId)
        {
            var t = Get(taskId);
            if (t == null || t.State != TaskState.Assigned) return false;
            t.State = TaskState.Active; t.CurrentStep = 0; t.StepProgress = 0;
            Version++;
            return true;
        }

        /// <summary>Moves an Active task to its next step. Finishing the last step makes it Done and releases
        /// reservations. False if the task is unknown or not Active.</summary>
        public bool CompleteStep(int taskId)
        {
            var t = Get(taskId);
            if (t == null || t.State != TaskState.Active) return false;
            t.StepProgress = 0;
            if (++t.CurrentStep >= t.Def.Steps.Count)
            {
                t.CurrentStep = t.Def.Steps.Count;
                t.State = TaskState.Done;
                if (t.AssigneeId.HasValue) _busy.Remove(t.AssigneeId.Value);
                ReleaseAll(t);
            }
            Version++;
            return true;
        }

        /// <summary>Adds progress ticks to the current step of an Active task. False if not Active or ticks &lt; 1.</summary>
        public bool AddProgress(int taskId, int ticks)
        {
            var t = Get(taskId);
            if (t == null || t.State != TaskState.Active || ticks < 1) return false;
            t.StepProgress += ticks;
            Version++;
            return true;
        }

        /// <summary>Reserves a standing tree for the task. False if the task is finished or unknown, or the
        /// tree cannot be reserved.</summary>
        public bool Reserve(int taskId, TreeId tree)
        {
            var t = Get(taskId);
            if (t == null || t.IsFinished || !_trees.Reserve(tree, taskId)) return false;
            if (!Contains(t, tree)) t.AddReservation(tree);
            Version++;
            return true;
        }

        /// <summary>Cancels a live task and releases its reservations (TSK-05). Clears the assignee.
        /// False if the task is unknown or already Done/Cancelled.</summary>
        public bool Cancel(int taskId)
        {
            var t = Get(taskId);
            if (t == null || t.IsFinished) return false;
            t.State = TaskState.Cancelled;
            if (t.AssigneeId.HasValue) _busy.Remove(t.AssigneeId.Value);
            t.AssigneeId = null;
            ReleaseAll(t);
            Version++;
            return true;
        }

        private static bool Contains(TaskRecord t, TreeId tree)
        {
            foreach (var r in t.Reservations) if (r == tree) return true;
            return false;
        }

        private void ReleaseAll(TaskRecord t)
        {
            foreach (var tree in t.Reservations)
                if (_trees.TryGet(tree, out var rec) && rec.ReservedBy == t.Id) _trees.Release(tree);
            t.ClearReservations();
        }
    }
}
