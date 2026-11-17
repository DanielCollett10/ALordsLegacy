using System.Collections.Generic;

namespace LordsLegacy.Sim.Footprints
{
    /// <summary>
    /// What it takes to build a footprint type (construction.md CN-02): item counts and build worker-minutes.
    /// Immutable. An empty recipe means the footprint is built at placement.
    /// </summary>
    public sealed class ConstructionRecipe
    {
        /// <summary>The recipe with no items and no minutes: built at once.</summary>
        public static readonly ConstructionRecipe Empty = new ConstructionRecipe(0, 0);

        private readonly SortedDictionary<string, int> _items = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

        /// <summary>Build work needed, in worker-minutes.</summary>
        public int BuildWorkerMinutes { get; }

        /// <summary>Creates a recipe needing only logs.</summary>
        public ConstructionRecipe(int logs, int buildWorkerMinutes)
        {
            if (logs < 0) throw new System.ArgumentOutOfRangeException(nameof(logs));
            if (buildWorkerMinutes < 0) throw new System.ArgumentOutOfRangeException(nameof(buildWorkerMinutes));
            if (logs > 0) _items[Inventory.Items.Log.Id] = logs;
            BuildWorkerMinutes = buildWorkerMinutes;
        }

        /// <summary>Creates a recipe needing the given item counts (item id to count); non-positive counts are ignored.</summary>
        public ConstructionRecipe(IEnumerable<KeyValuePair<string, int>> items, int buildWorkerMinutes)
        {
            if (items == null) throw new System.ArgumentNullException(nameof(items));
            if (buildWorkerMinutes < 0) throw new System.ArgumentOutOfRangeException(nameof(buildWorkerMinutes));
            foreach (var kv in items)
            {
                if (string.IsNullOrEmpty(kv.Key)) throw new System.ArgumentException("Item id missing.", nameof(items));
                if (kv.Value > 0) _items[kv.Key] = kv.Value;
            }
            BuildWorkerMinutes = buildWorkerMinutes;
        }

        /// <summary>Item id to count, in id order. Only positive counts.</summary>
        public IReadOnlyDictionary<string, int> Items => _items;

        /// <summary>Logs needed.</summary>
        public int Logs => _items.TryGetValue(Inventory.Items.Log.Id, out int n) ? n : 0;

        /// <summary>True when nothing is needed: the footprint is built at placement.</summary>
        public bool IsEmpty => _items.Count == 0 && BuildWorkerMinutes == 0;
    }
}
