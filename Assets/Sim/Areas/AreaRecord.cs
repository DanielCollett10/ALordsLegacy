using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>One area held by the <see cref="AreaRegistry"/>. Immutable.</summary>
    public sealed class AreaRecord
    {
        /// <summary>Unique id within the session.</summary>
        public int Id { get; }
        /// <summary>Area type id, e.g. <see cref="AreaTypes.TestId"/>.</summary>
        public string TypeId { get; }
        /// <summary>Display name, e.g. "Test area 1".</summary>
        public string Name { get; }
        /// <summary>The player's shape (vertices and edge kinds).</summary>
        public AreaShape Shape { get; }
        /// <summary>The shape flattened to a counter-clockwise polygon.</summary>
        public IReadOnlyList<PointMm> Polygon { get; }
        /// <summary>Bounding box of <see cref="Polygon"/>.</summary>
        public BoundsMm Bounds { get; }

        /// <summary>Creates a record. The polygon is copied and must have at least one point.</summary>
        public AreaRecord(int id, string typeId, string name, AreaShape shape, IReadOnlyList<PointMm> polygon)
        {
            if (typeId == null) throw new ArgumentNullException(nameof(typeId));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            var copy = new PointMm[polygon.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = polygon[i];
            Id = id; TypeId = typeId; Name = name; Shape = shape; Polygon = copy;
            Bounds = Polygons.Bounds(copy);
        }

        /// <summary>
        /// The flattened outline in the same winding as <see cref="Shape"/> (the stored polygon is counter-clockwise,
        /// so this reverses it for a clockwise shape), plus for each shape edge the index where its run starts.
        /// If the vertices cannot be found in order, edge starts fall back to an even split.
        /// </summary>
        public PointMm[] ShapeOrderOutline(out int[] edgeStarts)
        {
            int n = Shape.Vertices.Count, m = Polygon.Count;
            bool reversed = Polygons.SignedArea2Mm(Shape.Vertices) < 0;
            var outline = new PointMm[m];
            for (int k = 0; k < m; k++) outline[k] = reversed ? Polygon[m - 1 - k] : Polygon[k];
            edgeStarts = new int[n];
            int cursor = 0;
            for (int i = 0; i < n; i++)
            {
                int found = -1;
                for (int k = cursor; k < m; k++)
                    if (outline[k] == Shape.Vertices[i]) { found = k; break; }
                if (found < 0)
                {
                    for (int j = 0; j < n; j++) edgeStarts[j] = (int)((long)j * m / n);
                    return outline;
                }
                edgeStarts[i] = found;
                cursor = found + 1;
            }
            return outline;
        }
    }
}
