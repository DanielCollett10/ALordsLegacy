using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>
    /// Uniform-grid spatial index over axis-aligned boxes in integer millimetres (decision 0012, GEO-16).
    /// Each item is stored in every cell its bounds touch, so large items span many cells.
    /// Queries return each matching item once, in ascending insertion order (deterministic).
    /// Edges are inclusive: a box that only touches the query counts as a match.
    /// </summary>
    /// <typeparam name="T">Item type. Items are compared with <see cref="EqualityComparer{T}.Default"/>.</typeparam>
    public sealed class SpatialGrid<T>
    {
        /// <summary>One stored item: its insertion id, bounds and value.</summary>
        private sealed class Entry
        {
            public readonly long Id;
            public readonly T Item;
            public readonly BoundsMm Bounds;

            public Entry(long id, T item, BoundsMm bounds)
            {
                Id = id; Item = item; Bounds = bounds;
            }
        }

        private readonly long _cellMm;
        private readonly Dictionary<T, Entry> _items = new Dictionary<T, Entry>(EqualityComparer<T>.Default);
        private readonly Dictionary<(long, long), List<Entry>> _cells = new Dictionary<(long, long), List<Entry>>();
        private long _nextId;

        /// <summary>
        /// Creates an empty grid. <paramref name="cellSizeM"/> is the cell edge in metres (rounded to whole mm).
        /// Throws <see cref="ArgumentOutOfRangeException"/> if it is not finite and at least 1 mm.
        /// </summary>
        public SpatialGrid(double cellSizeM)
        {
            if (double.IsNaN(cellSizeM) || double.IsInfinity(cellSizeM))
                throw new ArgumentOutOfRangeException(nameof(cellSizeM), "Cell size must be finite");
            long mm = (long)Math.Round(cellSizeM * PointMm.MmPerMetre, MidpointRounding.AwayFromZero);
            if (mm < 1) throw new ArgumentOutOfRangeException(nameof(cellSizeM), "Cell size must be at least 1 mm");
            _cellMm = mm;
        }

        /// <summary>Cell edge in metres, as rounded to whole millimetres.</summary>
        public double CellSizeM => _cellMm / (double)PointMm.MmPerMetre;

        /// <summary>Number of items currently stored.</summary>
        public int Count => _items.Count;

        /// <summary>True if the item is stored.</summary>
        public bool Contains(T item) => item != null && _items.ContainsKey(item);

        /// <summary>
        /// Stores an item under its bounds. Throws <see cref="ArgumentNullException"/> for a null reference item,
        /// <see cref="ArgumentException"/> for inverted bounds or an item that is already stored.
        /// Re-inserting an item after <see cref="Remove"/> gives it a new, later insertion id.
        /// </summary>
        public void Insert(T item, BoundsMm bounds)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            ValidateBounds(bounds, nameof(bounds));
            if (_items.ContainsKey(item)) throw new ArgumentException("Item is already in the grid", nameof(item));

            var entry = new Entry(_nextId++, item, bounds);
            _items.Add(item, entry);
            ForEachCell(bounds, cell =>
            {
                if (!_cells.TryGetValue(cell, out var list)) { list = new List<Entry>(); _cells[cell] = list; }
                list.Add(entry);
            });
        }

        /// <summary>Removes an item. Returns false, and changes nothing, if it is not stored.</summary>
        public bool Remove(T item)
        {
            if (item == null) return false;
            if (!_items.TryGetValue(item, out var entry)) return false;
            _items.Remove(item);
            ForEachCell(entry.Bounds, cell =>
            {
                var list = _cells[cell];
                list.Remove(entry);
                if (list.Count == 0) _cells.Remove(cell);
            });
            return true;
        }

        /// <summary>
        /// Items whose bounds overlap or touch <paramref name="bounds"/>, each once, in ascending insertion order.
        /// Returns an empty list when nothing matches (including an empty grid).
        /// </summary>
        public List<T> QueryRect(BoundsMm bounds)
        {
            ValidateBounds(bounds, nameof(bounds));
            var hits = new List<Entry>();
            ForEachCell(bounds, cell =>
            {
                if (!_cells.TryGetValue(cell, out var list)) return;
                foreach (var entry in list)
                    if (entry.Bounds.Intersects(bounds)) hits.Add(entry);
            });
            return ToSortedItems(hits);
        }

        /// <summary>
        /// Items whose bounds contain <paramref name="point"/> (edge inclusive), each once, in ascending insertion order.
        /// A zero-size item at exactly that point matches.
        /// </summary>
        public List<T> QueryPoint(PointMm point) => QueryRect(new BoundsMm(point, point));

        /// <summary>Sorts by insertion id and drops repeats (an item sits in several cells).</summary>
        private static List<T> ToSortedItems(List<Entry> hits)
        {
            hits.Sort((a, b) => a.Id.CompareTo(b.Id));
            var result = new List<T>(hits.Count);
            long last = -1;
            foreach (var e in hits)
            {
                if (e.Id == last) continue;
                last = e.Id;
                result.Add(e.Item);
            }
            return result;
        }

        private static void ValidateBounds(BoundsMm b, string name)
        {
            if (b.Min.X > b.Max.X || b.Min.Z > b.Max.Z)
                throw new ArgumentException("Bounds minimum must not exceed maximum", name);
        }

        /// <summary>Calls <paramref name="visit"/> for every cell the box touches (inclusive edges).</summary>
        private void ForEachCell(BoundsMm b, Action<(long, long)> visit)
        {
            long x0 = FloorDiv(b.Min.X), x1 = FloorDiv(b.Max.X);
            long z0 = FloorDiv(b.Min.Z), z1 = FloorDiv(b.Max.Z);
            for (long cx = x0; cx <= x1; cx++)
                for (long cz = z0; cz <= z1; cz++)
                    visit((cx, cz));
        }

        /// <summary>Cell index of a millimetre coordinate; floors toward negative infinity.</summary>
        private long FloorDiv(long mm)
        {
            long q = mm / _cellMm;
            if (mm % _cellMm != 0 && mm < 0) q--;
            return q;
        }
    }
}
