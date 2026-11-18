using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>One curing stook on the ground: green reed that dries at <see cref="ReadyTick"/> (MAT-32, MAT-35).</summary>
    public sealed class StookRecord
    {
        /// <summary>Creates a stook.</summary>
        public StookRecord(int id, PointMm position, int operationId, int count, long readyTick)
        {
            Id = id; Position = position; OperationId = operationId; Count = count; ReadyTick = readyTick;
        }

        /// <summary>Stook id.</summary>
        public int Id { get; }
        /// <summary>Where it stands.</summary>
        public PointMm Position { get; }
        /// <summary>The owning operation (thatcher hut).</summary>
        public int OperationId { get; }
        /// <summary>Bundles in the stook.</summary>
        public int Count { get; }
        /// <summary>Tick at which it is dry.</summary>
        public long ReadyTick { get; }
    }

    /// <summary>
    /// Curing stooks on the ground (MAT-32, MAT-39c). Green bundles are not in the ledger: they exist only here until
    /// they dry, then <see cref="Step"/> produces the count as thatch and the record goes.
    /// </summary>
    public sealed class CuringRegistry
    {
        private readonly SortedDictionary<int, StookRecord> _byId = new SortedDictionary<int, StookRecord>();
        private int _nextId = 1;

        /// <summary>Number of stooks.</summary>
        public int Count => _byId.Count;
        /// <summary>Next id to hand out (for the save).</summary>
        public int NextIdValue => _nextId;
        /// <summary>Increases on every change. Views rebuild when it moves.</summary>
        public int Version { get; private set; }
        /// <summary>All stooks in ascending id order.</summary>
        public IEnumerable<StookRecord> All => _byId.Values;

        /// <summary>Stands a new stook that dries at <paramref name="readyTick"/>.</summary>
        public StookRecord Add(PointMm position, int operationId, int count, long readyTick)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            var s = new StookRecord(_nextId++, position, operationId, count, readyTick);
            _byId.Add(s.Id, s);
            Version++;
            return s;
        }

        /// <summary>Looks up a stook by id.</summary>
        public bool TryGet(int id, out StookRecord stook) => _byId.TryGetValue(id, out stook);

        /// <summary>The stooks of one operation, ascending id.</summary>
        public List<StookRecord> ForOperation(int operationId)
        {
            var list = new List<StookRecord>();
            foreach (var s in _byId.Values) if (s.OperationId == operationId) list.Add(s);
            return list;
        }

        /// <summary>Bundles curing for one operation.</summary>
        public int CuringCount(int operationId)
        {
            int n = 0;
            foreach (var s in _byId.Values) if (s.OperationId == operationId) n += s.Count;
            return n;
        }

        /// <summary>
        /// Dries every stook whose ready tick has come: produces its count as thatch in an operation pile at the stook
        /// (journalled) and removes the record. Returns how many dried.
        /// </summary>
        public int Step(long tick, Ledger ledger)
        {
            List<StookRecord> dry = null;
            foreach (var s in _byId.Values)
                if (s.ReadyTick <= tick) (dry ?? (dry = new List<StookRecord>())).Add(s);
            if (dry == null) return 0;
            foreach (var s in dry)
            {
                ledger.ProduceOnGround(s.Position, s.OperationId, Items.Thatch, s.Count);
                _byId.Remove(s.Id);
            }
            Version++;
            return dry.Count;
        }

        /// <summary>Debug check: null when consistent, else the first fault (positive counts, ids below the counter).</summary>
        public string Audit()
        {
            foreach (var s in _byId.Values)
            {
                if (s.Count <= 0) return "Stook " + s.Id + " holds " + s.Count;
                if (s.Id >= _nextId) return "Stook " + s.Id + " is not below the id counter " + _nextId;
            }
            return null;
        }

        /// <summary>Restore: removes everything.</summary>
        internal void RestoreClear() { _byId.Clear(); _nextId = 1; Version++; }

        /// <summary>Restore: adds a stook with its saved id.</summary>
        internal void RestoreStook(int id, PointMm position, int operationId, int count, long readyTick)
        {
            if (_byId.ContainsKey(id)) throw new InvalidOperationException("Duplicate stook id " + id);
            _byId.Add(id, new StookRecord(id, position, operationId, count, readyTick));
            Version++;
        }

        /// <summary>Restore: sets the id counter.</summary>
        internal void RestoreNextId(int next) { _nextId = next; }
    }
}
