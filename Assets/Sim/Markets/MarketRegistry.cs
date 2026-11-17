using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>One market: its id, the area it stands on and its stall footprints (MK-03, MK-05). Stalls arrive in mkt-3.</summary>
    public sealed class Market
    {
        /// <summary>Market id, unique and never reused.</summary>
        public int Id { get; }
        /// <summary>Id of the <c>area.market</c> area.</summary>
        public int AreaId { get; }
        /// <summary>Footprint ids of the built or planned stalls, in creation order. Empty until mkt-3.</summary>
        public List<int> StallFootprintIds { get; } = new List<int>();

        private readonly Dictionary<int, int> _familyByStall = new Dictionary<int, int>();
        private readonly SortedSet<int> _noRoom = new SortedSet<int>();
        private readonly SortedSet<int> _noPlanks = new SortedSet<int>();

        /// <summary>Family staffing this stall (MK-20), or 0 when the stall is unstaffed (MK-23) or unknown.</summary>
        public int FamilyOf(int stallFootprintId) => _familyByStall.TryGetValue(stallFootprintId, out int f) ? f : 0;

        /// <summary>The stall staffed by this family, or 0.</summary>
        public int StallOf(int familyId)
        {
            if (familyId == 0) return 0;
            foreach (int s in StallFootprintIds) if (FamilyOf(s) == familyId) return s;
            return 0;
        }

        /// <summary>Families that need a stall but found no free slot (MK-21), ascending. Recomputed every tick, not saved.</summary>
        public IReadOnlyCollection<int> NoRoomFamilyIds => _noRoom;

        internal void SetFamily(int stallFootprintId, int familyId)
        {
            if (familyId == 0) _familyByStall.Remove(stallFootprintId); else _familyByStall[stallFootprintId] = familyId;
        }

        /// <summary>Families whose stall is waiting for planks (PL-13: fewer than the stall's 2 planks in the camp). Recomputed every tick, not saved.</summary>
        public IReadOnlyCollection<int> NoPlanksFamilyIds => _noPlanks;

        internal void ClearNoRoom() => _noRoom.Clear();
        internal void AddNoRoom(int familyId) => _noRoom.Add(familyId);
        internal void ClearNoPlanks() => _noPlanks.Clear();
        internal void AddNoPlanks(int familyId) => _noPlanks.Add(familyId);

        internal Market(int id, int areaId) { Id = id; AreaId = areaId; }
    }

    /// <summary>
    /// The town's markets (MK-04: one per town while the whole settlement is one town). Records iterate by ascending id.
    /// Saved by the Markets section.
    /// </summary>
    public sealed class MarketRegistry
    {
        private readonly SortedDictionary<int, Market> _markets = new SortedDictionary<int, Market>();
        private int _nextId = 1;

        /// <summary>Increases on every add and remove.</summary>
        public int Version { get; private set; }

        /// <summary>Number of markets.</summary>
        public int Count => _markets.Count;

        /// <summary>All markets by ascending id.</summary>
        public IEnumerable<Market> All => _markets.Values;

        /// <summary>The id the next market gets. Saved so ids are never reused.</summary>
        public int NextIdValue => _nextId;

        /// <summary>The market with this id, or null.</summary>
        public Market Get(int id) => _markets.TryGetValue(id, out var m) ? m : null;

        /// <summary>The market standing on this area, or null.</summary>
        public Market FindByArea(int areaId)
        {
            foreach (Market m in _markets.Values) if (m.AreaId == areaId) return m;
            return null;
        }

        /// <summary>The town's market (the first one), or null if none is drawn.</summary>
        public Market Current
        {
            get { foreach (Market m in _markets.Values) return m; return null; }
        }

        /// <summary>Adds a market for an area. Throws if that area already has one.</summary>
        public Market Add(int areaId)
        {
            if (FindByArea(areaId) != null) throw new ArgumentException("Area " + areaId + " already has a market");
            var m = new Market(_nextId++, areaId);
            _markets.Add(m.Id, m);
            Version++;
            return m;
        }

        /// <summary>Removes the market on this area. False if there is none.</summary>
        public bool RemoveByArea(int areaId)
        {
            Market m = FindByArea(areaId);
            if (m == null) return false;
            _markets.Remove(m.Id);
            Version++;
            return true;
        }

        internal void RestoreClear() { _markets.Clear(); _nextId = 1; Version++; }

        internal Market Restore(int id, int areaId)
        {
            if (_markets.ContainsKey(id)) throw new ArgumentException("Duplicate market " + id);
            var m = new Market(id, areaId);
            _markets.Add(id, m);
            Version++;
            return m;
        }

        internal void RestoreNextId(int next) { _nextId = next; }
    }
}
