using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Navigation
{
    /// <summary>A point on the ground in metres.</summary>
    public readonly struct PathPoint : IEquatable<PathPoint>
    {
        /// <summary>World x in metres.</summary>
        public double X { get; }
        /// <summary>World z in metres.</summary>
        public double Z { get; }

        /// <summary>Creates a point.</summary>
        public PathPoint(double x, double z) { X = x; Z = z; }

        /// <inheritdoc />
        public bool Equals(PathPoint other) => X == other.X && Z == other.Z;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PathPoint p && Equals(p);
        /// <inheritdoc />
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Z.GetHashCode());
        /// <inheritdoc />
        public override string ToString() => "(" + X + ", " + Z + ")";
    }

    /// <summary>Outcome of a path query: a smoothed polyline and its travel time, or no route.</summary>
    public sealed class PathResult
    {
        private static readonly PathPoint[] None = new PathPoint[0];

        /// <summary>True if a route was found.</summary>
        public bool Ok { get; }
        /// <summary>Polyline in metres from start to goal (empty when not <see cref="Ok"/>).</summary>
        public IReadOnlyList<PathPoint> Points { get; }
        /// <summary>Travel time in game minutes at speed 1 m/min (0 when not <see cref="Ok"/>).</summary>
        public double TravelMinutes { get; }

        private PathResult(bool ok, IReadOnlyList<PathPoint> points, double minutes)
        {
            Ok = ok; Points = points; TravelMinutes = minutes;
        }

        /// <summary>The "no route" result.</summary>
        public static PathResult NoRoute() => new PathResult(false, None, 0);

        /// <summary>A found route.</summary>
        public static PathResult Found(IReadOnlyList<PathPoint> points, double minutes) =>
            new PathResult(true, points, minutes);
    }
}
