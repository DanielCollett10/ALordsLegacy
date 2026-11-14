using System;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>
    /// A unit direction in the ground plane (x east, z north) in the direction of travel along a road (RD-16).
    /// Used to make a road leave or arrive at a dead end flush with the road already there.
    /// </summary>
    public readonly struct RoadTangent : IEquatable<RoadTangent>
    {
        /// <summary>X component of the unit direction.</summary>
        public double X { get; }
        /// <summary>Z component of the unit direction.</summary>
        public double Z { get; }

        /// <summary>Creates a tangent from any non-zero direction; it is normalised.</summary>
        /// <exception cref="ArgumentException">The direction has zero length.</exception>
        public RoadTangent(double dx, double dz)
        {
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (!(len > 0)) throw new ArgumentException("Tangent direction must not be zero.");
            X = dx / len; Z = dz / len;
        }

        private RoadTangent(double x, double z, bool alreadyUnit) { X = x; Z = z; }

        /// <summary>Restore: rebuilds a tangent from saved components without renormalising, so the bits match exactly.</summary>
        internal static RoadTangent FromSaved(double x, double z)
        {
            if (!(Math.Abs(x * x + z * z - 1.0) < 1e-6)) throw new InvalidOperationException("Saved tangent is not a unit vector.");
            return new RoadTangent(x, z, true);
        }

        /// <summary>The opposite direction.</summary>
        public RoadTangent Reversed() => new RoadTangent(-X, -Z);

        /// <inheritdoc />
        public bool Equals(RoadTangent other) => X == other.X && Z == other.Z;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is RoadTangent t && Equals(t);
        /// <inheritdoc />
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Z.GetHashCode());
        /// <summary>Equality.</summary>
        public static bool operator ==(RoadTangent a, RoadTangent b) => a.Equals(b);
        /// <summary>Inequality.</summary>
        public static bool operator !=(RoadTangent a, RoadTangent b) => !a.Equals(b);
    }
}
