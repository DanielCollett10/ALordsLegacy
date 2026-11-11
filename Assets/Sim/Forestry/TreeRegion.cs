using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>A circle or polygon on the ground used to query trees. Edges count as inside.</summary>
    public sealed class TreeRegion
    {
        private const double MarginM = 0.01;
        private readonly float _cx, _cz, _r;
        private readonly IReadOnlyList<PointMm> _poly;

        private TreeRegion(float cx, float cz, float r, IReadOnlyList<PointMm> poly)
        {
            _cx = cx; _cz = cz; _r = r; _poly = poly;
        }

        /// <summary>A disc. Throws <see cref="ArgumentOutOfRangeException"/> for a negative, NaN or infinite radius.</summary>
        public static TreeRegion Circle(float x, float z, float radiusM)
        {
            if (!(radiusM >= 0f) || float.IsInfinity(radiusM)) throw new ArgumentOutOfRangeException(nameof(radiusM));
            return new TreeRegion(x, z, radiusM, null);
        }

        /// <summary>A polygon (no repeated closing vertex). Throws <see cref="ArgumentException"/> for fewer than 3 vertices.</summary>
        public static TreeRegion Polygon(IReadOnlyList<PointMm> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (vertices.Count < 3) throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));
            return new TreeRegion(0f, 0f, 0f, new List<PointMm>(vertices));
        }

        /// <summary>True when (x,z) is inside or on the boundary.</summary>
        public bool Contains(float x, float z)
        {
            if (_poly != null) return Polygons.Contains(_poly, PointMm.FromMetres(x, z));
            double dx = x - _cx, dz = z - _cz;
            return dx * dx + dz * dz <= (double)_r * _r;
        }

        /// <summary>A rectangle (slightly padded) that covers the region.</summary>
        public Bounds2 Bounds()
        {
            if (_poly != null)
            {
                var b = Polygons.Bounds(_poly);
                return new Bounds2(
                    (float)(b.Min.XMetres - MarginM), (float)(b.Min.ZMetres - MarginM),
                    (float)(b.Max.XMetres + MarginM), (float)(b.Max.ZMetres + MarginM));
            }
            return new Bounds2(_cx - _r, _cz - _r, _cx + _r, _cz + _r);
        }
    }
}
