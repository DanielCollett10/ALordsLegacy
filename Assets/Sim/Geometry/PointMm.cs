using System;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>A point on the ground plane in integer millimetres (X east, Z north).</summary>
    public readonly struct PointMm : IEquatable<PointMm>
    {
        /// <summary>Millimetres per metre.</summary>
        public const long MmPerMetre = 1000;

        /// <summary>X in millimetres.</summary>
        public readonly long X;
        /// <summary>Z in millimetres.</summary>
        public readonly long Z;

        /// <summary>Creates a point from millimetre coordinates.</summary>
        public PointMm(long x, long z) { X = x; Z = z; }

        /// <summary>Converts metres to a point, rounding each axis to the nearest mm.</summary>
        public static PointMm FromMetres(double x, double z) =>
            new PointMm((long)Math.Round(x * MmPerMetre, MidpointRounding.AwayFromZero),
                        (long)Math.Round(z * MmPerMetre, MidpointRounding.AwayFromZero));

        /// <summary>X in metres.</summary>
        public double XMetres => X / (double)MmPerMetre;
        /// <summary>Z in metres.</summary>
        public double ZMetres => Z / (double)MmPerMetre;

        /// <summary>Returns the (x, z) position in metres.</summary>
        public (double X, double Z) ToMetres() => (XMetres, ZMetres);

        /// <summary>Vector subtraction.</summary>
        public static PointMm operator -(PointMm a, PointMm b) => new PointMm(a.X - b.X, a.Z - b.Z);
        /// <summary>Vector addition.</summary>
        public static PointMm operator +(PointMm a, PointMm b) => new PointMm(a.X + b.X, a.Z + b.Z);

        /// <summary>2D cross product of (a-o) and (b-o); positive when o→a→b turns counter-clockwise. Exact.</summary>
        public static long Cross(PointMm o, PointMm a, PointMm b) =>
            (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);

        /// <summary>Euclidean distance to another point in millimetres.</summary>
        public double DistanceTo(PointMm other)
        {
            double dx = X - other.X, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <inheritdoc />
        public bool Equals(PointMm other) => X == other.X && Z == other.Z;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PointMm p && Equals(p);
        /// <inheritdoc />
        public override int GetHashCode() => unchecked((int)(X * 397) ^ (int)Z);
        /// <summary>Equality.</summary>
        public static bool operator ==(PointMm a, PointMm b) => a.Equals(b);
        /// <summary>Inequality.</summary>
        public static bool operator !=(PointMm a, PointMm b) => !a.Equals(b);
        /// <inheritdoc />
        public override string ToString() => "(" + X + "," + Z + ")mm";
    }

    /// <summary>Axis-aligned bounding box in millimetres (inclusive).</summary>
    public readonly struct BoundsMm
    {
        /// <summary>Minimum corner.</summary>
        public readonly PointMm Min;
        /// <summary>Maximum corner.</summary>
        public readonly PointMm Max;

        /// <summary>Creates a box from its corners.</summary>
        public BoundsMm(PointMm min, PointMm max) { Min = min; Max = max; }

        /// <summary>True if the point is inside or on the edge.</summary>
        public bool Contains(PointMm p) => p.X >= Min.X && p.X <= Max.X && p.Z >= Min.Z && p.Z <= Max.Z;

        /// <summary>True if the boxes overlap or touch.</summary>
        public bool Intersects(BoundsMm o) => Min.X <= o.Max.X && o.Min.X <= Max.X && Min.Z <= o.Max.Z && o.Min.Z <= Max.Z;
    }
}
