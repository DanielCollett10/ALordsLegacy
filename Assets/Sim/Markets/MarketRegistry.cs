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
