using System.Collections.Generic;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>A built module marked for deconstruction (CN-52). It no longer works.</summary>
    public sealed class DeconstructionEntry
    {
        /// <summary>The footprint being taken down.</summary>
        public int FootprintId { get; }
        /// <summary>Deconstruction progress in worker-minutes (thrown away on cancel, CN-57).</summary>
        public int ProgressMinutes { get; set; }

        /// <summary>Creates an entry.</summary>
        public DeconstructionEntry(int footprintId) { FootprintId = footprintId; }
    }

    /// <summary>Built modules marked for deconstruction, keyed by footprint id (CN-52).</summary>
    public sealed class DeconstructionRegistry
    {
        private readonly SortedDictionary<int, DeconstructionEntry> _entries = new SortedDictionary<int, DeconstructionEntry>();

        /// <summary>All entries in footprint id order.</summary>
        public IEnumerable<DeconstructionEntry> Entries => _entries.Values;

        private readonly SortedSet<int> _pendingOperations = new SortedSet<int>();

        /// <summary>Operations whose whole area goes once their last module is gone (CN-58), in id order.</summary>
        public IEnumerable<int> PendingOperationIds => _pendingOperations;

        /// <summary>True if the operation is marked for removal.</summary>
        public bool IsOperationPending(int operationId) => _pendingOperations.Contains(operationId);

        /// <summary>Marks an operation for removal after its last module goes.</summary>
        public void AddPendingOperation(int operationId) => _pendingOperations.Add(operationId);

        /// <summary>Clears the mark. False if there was none.</summary>
        public bool RemovePendingOperation(int operationId) => _pendingOperations.Remove(operationId);

        /// <summary>Number of entries.</summary>
        public int Count => _entries.Count;

        /// <summary>Adds an entry. Throws if the footprint already has one.</summary>
        public DeconstructionEntry Add(DeconstructionEntry e)
        {
            if (e == null) throw new System.ArgumentNullException(nameof(e));
            if (_entries.ContainsKey(e.FootprintId))
                throw new System.InvalidOperationException("Footprint " + e.FootprintId + " is already being deconstructed.");
            _entries.Add(e.FootprintId, e);
            return e;
        }

        /// <summary>The entry of a footprint, or null.</summary>
        public DeconstructionEntry Get(int footprintId) => _entries.TryGetValue(footprintId, out var e) ? e : null;

        /// <summary>Removes an entry. False if there was none.</summary>
        public bool Remove(int footprintId) => _entries.Remove(footprintId);
    }
}
