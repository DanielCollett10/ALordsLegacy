using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Navigation
{
    /// <summary>
    /// The walk grid (decision 0022, MOV-01..03, MOV-08): square cells of <c>NavCellSizeM</c> over the terrain
    /// bounds. Each cell is <see cref="IsBlocked"/> if the slope at its centre is over the maximum walk slope or its
    /// centre is inside a movement-blocking footprint; otherwise its <see cref="SpeedMultiplier"/> is the highest
    /// multiplier among road corridors containing the centre, else 1. Slope never changes speed.
    /// </summary>
    /// <remarks>
    /// The first <see cref="Sync"/> is a full build. Later syncs diff the road segment and blocking footprint ids
    /// against what was applied; the bounds of removed and added items are the dirty rectangles, and each dirty
    /// cell is recomputed from every current road and footprint overlapping it.
    /// </remarks>
    public sealed class NavGrid
    {
        private sealed class Item
        {
            public object Source;
            public BoundsMm Bounds;
            public PointMm[] Polygon;
            public double Multiplier;
        }

        private readonly double _maxSlopeDeg;
        private readonly bool[] _slopeBlocked;
        private readonly bool[] _blocked;
        private readonly float[] _multiplier;
        private readonly Dictionary<int, Item> _roads = new Dictionary<int, Item>();
        private readonly Dictionary<int, Item> _footprints = new Dictionary<int, Item>();
        private bool _built;
        private int _roadVersion;
        private int _footprintVersion;

        /// <summary>Cell side in metres.</summary>
        public double CellSizeM { get; }
        /// <summary>World x of the grid's left edge.</summary>
        public double OriginX { get; }
        /// <summary>World z of the grid's near edge.</summary>
        public double OriginZ { get; }
        /// <summary>Number of cells along x (0 when the terrain has no bounds).</summary>
        public int Width { get; }
        /// <summary>Number of cells along z.</summary>
        public int Depth { get; }
        /// <summary>Increases by one on every sync that changed any cell (the first build counts).</summary>
        public int Version { get; private set; }

        /// <summary>Creates an unbuilt grid over the bounds. Empty bounds give a grid with no cells.</summary>
        public NavGrid(Bounds2 bounds, double cellSizeM, double maxWalkSlopeDeg)
        {
            if (!(cellSizeM > 0)) throw new ArgumentOutOfRangeException(nameof(cellSizeM));
            CellSizeM = cellSizeM;
            _maxSlopeDeg = maxWalkSlopeDeg;
            if (!bounds.IsEmpty)
            {
                OriginX = bounds.MinX; OriginZ = bounds.MinZ;
                Width = Math.Max(1, (int)Math.Ceiling(bounds.SizeX / cellSizeM - 1e-9));
                Depth = Math.Max(1, (int)Math.Ceiling(bounds.SizeZ / cellSizeM - 1e-9));
            }
            int n = Width * Depth;
            _slopeBlocked = new bool[n];
            _blocked = new bool[n];
            _multiplier = new float[n];
            for (int i = 0; i < n; i++) _multiplier[i] = 1f;
        }

        /// <summary>True if the cell cannot be walked. Cells outside the grid count as blocked.</summary>
        public bool IsBlocked(int ix, int iz) => !InGrid(ix, iz) || _blocked[iz * Width + ix];

        /// <summary>Speed multiplier of the cell (1 off-road). Cells outside the grid return 1.</summary>
        public double SpeedMultiplier(int ix, int iz) => InGrid(ix, iz) ? _multiplier[iz * Width + ix] : 1.0;

        /// <summary>True if the cell indices are inside the grid.</summary>
        public bool InGrid(int ix, int iz) => ix >= 0 && iz >= 0 && ix < Width && iz < Depth;

        /// <summary>World x of a cell's centre.</summary>
        public double CentreX(int ix) => OriginX + (ix + 0.5) * CellSizeM;

        /// <summary>World z of a cell's centre.</summary>
        public double CentreZ(int iz) => OriginZ + (iz + 0.5) * CellSizeM;

        /// <summary>Cell column containing world x (may be outside the grid).</summary>
        public int CellX(double x) => (int)Math.Floor((x - OriginX) / CellSizeM);

        /// <summary>Cell row containing world z (may be outside the grid).</summary>
        public int CellZ(double z) => (int)Math.Floor((z - OriginZ) / CellSizeM);

        /// <summary>
        /// Brings the grid up to date with the world. Does nothing if the road and footprint versions are
        /// unchanged since the last sync. Bumps <see cref="Version"/> if any cell changed (always on the first build).
        /// </summary>
        public void Sync(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (_built && world.Roads.Version == _roadVersion && world.Footprints.Version == _footprintVersion) return;

            bool changed = false;
            if (!_built)
            {
                ComputeSlope(world.Terrain);
                CollectRoads(world, null);
                CollectFootprints(world, null);
                RecomputeRect(0, 0, Width - 1, Depth - 1);
                _built = true;
                changed = true;
            }
            else
            {
                var dirty = new List<BoundsMm>();
                CollectRoads(world, dirty);
                CollectFootprints(world, dirty);
                foreach (BoundsMm b in dirty)
                {
                    int x0 = Math.Max(0, CellX(b.Min.XMetres)), x1 = Math.Min(Width - 1, CellX(b.Max.XMetres));
                    int z0 = Math.Max(0, CellZ(b.Min.ZMetres)), z1 = Math.Min(Depth - 1, CellZ(b.Max.ZMetres));
                    if (RecomputeRect(x0, z0, x1, z1)) changed = true;
                }
            }
            _roadVersion = world.Roads.Version;
            _footprintVersion = world.Footprints.Version;
            if (changed) Version++;
        }

        private void ComputeSlope(IHeightField terrain)
        {
            for (int iz = 0; iz < Depth; iz++)
                for (int ix = 0; ix < Width; ix++)
                    _slopeBlocked[iz * Width + ix] =
                        terrain.SlopeDegAt((float)CentreX(ix), (float)CentreZ(iz)) > _maxSlopeDeg;
        }

        // Makes 'applied' equal 'current' (by id and source object). Bounds of removed and added items go to 'dirty'.
        private static void Diff(Dictionary<int, Item> applied, Dictionary<int, Item> current, List<BoundsMm> dirty)
        {
            var remove = new List<int>();
            foreach (KeyValuePair<int, Item> kv in applied)
            {
                if (!current.TryGetValue(kv.Key, out Item now) || !ReferenceEquals(now, kv.Value))
                {
                    remove.Add(kv.Key);
                    dirty?.Add(kv.Value.Bounds);
                }
            }
            foreach (int id in remove) applied.Remove(id);
            foreach (KeyValuePair<int, Item> kv in current)
            {
                if (applied.ContainsKey(kv.Key)) continue;
                applied[kv.Key] = kv.Value;
                dirty?.Add(kv.Value.Bounds);
            }
        }

        private void CollectRoads(SimWorld world, List<BoundsMm> dirty)
        {
            var current = new Dictionary<int, Item>();
            foreach (RoadSegment seg in world.Roads.Segments)
            {
                if (_roads.TryGetValue(seg.Id, out Item old) && ReferenceEquals(old.Source, seg))
                {
                    current[seg.Id] = old;
                    continue;
                }
                RoadTypeDef type = world.RoadTypes.Find(seg.TypeId);
                PointMm[] poly = seg.CorridorPolygon(world.RoadTypes);
                if (type == null || poly.Length < 3) continue;
                current[seg.Id] = new Item
                {
                    Source = seg, Polygon = poly, Bounds = Polygons.Bounds(poly), Multiplier = type.SpeedMultiplier,
                };
            }
            Diff(_roads, current, dirty);
        }

        private void CollectFootprints(SimWorld world, List<BoundsMm> dirty)
        {
            var current = new Dictionary<int, Item>();
            foreach (Footprint f in world.Footprints.All)
            {
                if (!f.Type.BlocksMovement) continue;
                if (_footprints.TryGetValue(f.Id, out Item old) && ReferenceEquals(old.Source, f)) current[f.Id] = old;
                else current[f.Id] = new Item { Source = f, Polygon = f.Polygon, Bounds = f.Bounds };
            }
            Diff(_footprints, current, dirty);
        }

        // Recomputes the cells in the inclusive range from the slope flags and all applied items. True if any changed.
        private bool RecomputeRect(int x0, int z0, int x1, int z1)
        {
            if (x0 > x1 || z0 > z1) return false;
            var rect = new BoundsMm(
                new PointMm(ToMm(OriginX + x0 * CellSizeM), ToMm(OriginZ + z0 * CellSizeM)),
                new PointMm(ToMm(OriginX + (x1 + 1) * CellSizeM), ToMm(OriginZ + (z1 + 1) * CellSizeM)));
            List<Item> roads = Overlapping(_roads, rect), blockers = Overlapping(_footprints, rect);
            bool changed = false;
            for (int iz = z0; iz <= z1; iz++)
            {
                for (int ix = x0; ix <= x1; ix++)
                {
                    int idx = iz * Width + ix;
                    var c = new PointMm(ToMm(CentreX(ix)), ToMm(CentreZ(iz)));
                    bool blocked = _slopeBlocked[idx];
                    for (int i = 0; i < blockers.Count && !blocked; i++)
                        if (blockers[i].Bounds.Contains(c) && Polygons.Contains(blockers[i].Polygon, c)) blocked = true;
                    double mult = 1.0;
                    for (int i = 0; i < roads.Count; i++)
                        if (roads[i].Multiplier > mult && roads[i].Bounds.Contains(c) && Polygons.Contains(roads[i].Polygon, c))
                            mult = roads[i].Multiplier;
                    float m = (float)mult;
                    if (_blocked[idx] != blocked || _multiplier[idx] != m) changed = true;
                    _blocked[idx] = blocked;
                    _multiplier[idx] = m;
                }
            }
            return changed;
        }

        private static List<Item> Overlapping(Dictionary<int, Item> items, BoundsMm rect)
        {
            var list = new List<Item>();
            foreach (Item it in items.Values) if (it.Bounds.Intersects(rect)) list.Add(it);
            return list;
        }

        private static long ToMm(double metres) => (long)Math.Round(metres * PointMm.MmPerMetre, MidpointRounding.AwayFromZero);
    }
}
