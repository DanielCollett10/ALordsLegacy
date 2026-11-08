using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// All areas in the world (decision 0014). Building-kind areas are also kept in a spatial grid so the
    /// validator can find overlap candidates. Changes come only through the internal Add/Remove.
    /// </summary>
    public sealed class AreaRegistry
    {
        private readonly SortedDictionary<int, AreaRecord> _byId = new SortedDictionary<int, AreaRecord>();
        private readonly SpatialGrid<AreaRecord> _buildingGrid;
        private readonly Dictionary<string, int> _nameCounters = new Dictionary<string, int>();
        private int _nextId = 1;

        private readonly AreaTypeCatalog _types;

        /// <summary>
        /// Creates an empty registry whose spatial grid uses the given cell size in metres. Types are looked up in
        /// <paramref name="types"/> (the default definitions when null).
        /// </summary>
        public AreaRegistry(double cellSizeM, AreaTypeCatalog types = null)
        {
            _types = types ?? AreaTypes.DefaultCatalog;
            _buildingGrid = new SpatialGrid<AreaRecord>(cellSizeM);
        }

        /// <summary>Increases on every Add or Remove.</summary>
        public int Version { get; private set; }

        /// <summary>Number of areas.</summary>
        public int Count => _byId.Count;

        /// <summary>All areas in ascending id order.</summary>
        public IEnumerable<AreaRecord> All => _byId.Values;

        /// <summary>The area with this id, or null.</summary>
        public AreaRecord Get(int id) => _byId.TryGetValue(id, out var r) ? r : null;

        /// <summary>Takes the next unused area id.</summary>
        internal int NextId() => _nextId++;

        /// <summary>
        /// Takes the next name for a type: "&lt;prefix&gt; N". Numbers per type only go up in a session,
        /// so a removed area's name is never handed out again.
        /// </summary>
        internal string NextName(AreaTypeDef type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            _nameCounters.TryGetValue(type.Id, out int n);
            n++;
            _nameCounters[type.Id] = n;
            return type.NamePrefix + " " + n;
        }

        /// <summary>Adds a record. Throws if its id is taken or its type is unknown.</summary>
        internal void Add(AreaRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            AreaTypeDef type = _types.Find(record.TypeId);
            if (type == null) throw new ArgumentException("Unknown area type " + record.TypeId, nameof(record));
            if (_byId.ContainsKey(record.Id)) throw new ArgumentException("Area id already used", nameof(record));
            _byId.Add(record.Id, record);
            if (record.Id >= _nextId) _nextId = record.Id + 1;
            if (type.Kind == AreaKind.Building) _buildingGrid.Insert(record, record.Bounds);
            Version++;
        }

        /// <summary>Removes an area. Returns false, and changes nothing, if the id is unknown.</summary>
        internal bool Remove(int id)
        {
            if (!_byId.TryGetValue(id, out var record)) return false;
            _byId.Remove(id);
            _buildingGrid.Remove(record);
            Version++;
            return true;
        }

        /// <summary>Building-kind areas whose bounds overlap or touch <paramref name="bounds"/>, in insertion order.</summary>
        public List<AreaRecord> QueryOverlapCandidates(BoundsMm bounds) => _buildingGrid.QueryRect(bounds);

        /// <summary>
        /// The lowest-id area of the given kind whose flattened outline contains the point (boundary counts),
        /// or null.
        /// </summary>
        public AreaRecord FindAt(PointMm point, AreaKind kind)
        {
            foreach (var r in _byId.Values)
            {
                AreaTypeDef type = _types.Find(r.TypeId);
                if (type == null || type.Kind != kind) continue;
                if (!r.Bounds.Contains(point)) continue;
                if (Polygons.Contains(r.Polygon, point)) return r;
            }
            return null;
        }

        /// <summary>
        /// The closest snap target within <paramref name="maxDistanceMm"/> across all areas. Shape vertices win over
        /// edges: an edge hit is returned only if no vertex is in range. Edges are measured on the flattened outline
        /// and reported as the shape edge they belong to. Ties go to the lowest area id, then lowest index.
        /// Returns null when nothing is in range.
        /// </summary>
        public AreaSnapHit? FindNearestSnap(PointMm point, double maxDistanceMm)
        {
            if (double.IsNaN(maxDistanceMm) || maxDistanceMm < 0) return null;
            AreaSnapHit? bestVertex = null, bestEdge = null;
            foreach (var r in _byId.Values)
            {
                var vs = r.Shape.Vertices;
                for (int i = 0; i < vs.Count; i++)
                {
                    double d = vs[i].DistanceTo(point);
                    if (d <= maxDistanceMm && (bestVertex == null || d < bestVertex.Value.DistanceMm))
                        bestVertex = new AreaSnapHit(AreaSnapKind.Vertex, r.Id, i, vs[i], d);
                }
                if (bestVertex != null) continue; // vertices preferred: skip edge work once one is found
                PointMm[] outline = r.ShapeOrderOutline(out int[] starts);
                int m = outline.Length;
                for (int e = 0; e < starts.Length; e++)
                {
                    int from = starts[e];
                    int to = e + 1 < starts.Length ? starts[e + 1] : m; // last run ends back at the start
                    for (int k = from; k < to; k++)
                    {
                        PointMm a = outline[k], b = outline[(k + 1) % m];
                        double d = Segments.DistanceToPoint(a, b, point, out double t);
                        if (d > maxDistanceMm) continue;
                        if (bestEdge != null && d >= bestEdge.Value.DistanceMm) continue;
                        var p = new PointMm(a.X + (long)Math.Round(t * (b.X - a.X), MidpointRounding.AwayFromZero),
                                            a.Z + (long)Math.Round(t * (b.Z - a.Z), MidpointRounding.AwayFromZero));
                        bestEdge = new AreaSnapHit(AreaSnapKind.Edge, r.Id, e, p, d);
                    }
                }
            }
            return bestVertex ?? bestEdge;
        }
    }
}
