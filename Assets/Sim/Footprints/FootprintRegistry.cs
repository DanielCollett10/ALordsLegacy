using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Footprints
{
    /// <summary>All placed footprints, keyed by id and iterated in ascending id order.</summary>
    public sealed class FootprintRegistry
    {
        private readonly SortedDictionary<int, Footprint> _items = new SortedDictionary<int, Footprint>();

        private int _nextId = 1;

        /// <summary>Increases by one on every add and every successful remove.</summary>
        public int Version { get; private set; }

        /// <summary>Number of footprints.</summary>
        public int Count => _items.Count;

        /// <summary>Footprints in ascending id order.</summary>
        public IEnumerable<Footprint> All => _items.Values;

        /// <summary>Ids in ascending order.</summary>
        public IEnumerable<int> Ids => _items.Keys;

        /// <summary>Adds a footprint. Throws if the id is already in use.</summary>
        public Footprint Add(int id, FootprintTypeDef type, PointMm centre, double rotationDegrees)
        {
            if (_items.ContainsKey(id)) throw new ArgumentException("Footprint id already in use: " + id, nameof(id));
            var f = new Footprint(id, type, centre, rotationDegrees);
            _items.Add(id, f);
            if (id >= _nextId) _nextId = id + 1;
            Version++;
            return f;
        }

        /// <summary>An id above every id ever added here, so ids of removed footprints are not reused.</summary>
        public int NextId() => _nextId;

        /// <summary>Removes a footprint. Returns false (and leaves Version alone) if the id is unknown.</summary>
        public bool Remove(int id)
        {
            if (!_items.Remove(id)) return false;
            Version++;
            return true;
        }

        /// <summary>Gets a footprint by id, or null.</summary>
        public Footprint Get(int id) => _items.TryGetValue(id, out var f) ? f : null;

        /// <summary>The lowest-id footprint whose polygon contains the point (edges count), or null.</summary>
        public Footprint FindAt(PointMm p)
        {
            foreach (var f in _items.Values)
                if (f.Bounds.Contains(p) && Polygons.Contains(f.Polygon, p)) return f;
            return null;
        }

        /// <summary>Footprints whose bounding box overlaps the bounds, in id order.</summary>
        public List<Footprint> QueryRect(BoundsMm bounds)
        {
            var result = new List<Footprint>();
            foreach (var f in _items.Values)
                if (f.Bounds.Intersects(bounds)) result.Add(f);
            return result;
        }
    }
}
