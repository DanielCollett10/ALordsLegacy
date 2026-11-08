using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>One stretch of road between two nodes (docs/design/roads.md, decision 0013). Immutable.</summary>
    public sealed class RoadSegment
    {
        /// <summary>Segment id, unique in its network and never reused.</summary>
        public int Id { get; }
        /// <summary>Node at the start of the centre line.</summary>
        public int NodeA { get; }
        /// <summary>Node at the end of the centre line.</summary>
        public int NodeB { get; }
        /// <summary>Road type id, e.g. <c>road.dirt_path</c>.</summary>
        public string TypeId { get; }
        /// <summary>How the control points make the centre line.</summary>
        public CurveKind Curve { get; }
        /// <summary>Control points; first and last equal the node positions.</summary>
        public IReadOnlyList<PointMm> ControlPoints { get; }
        /// <summary>Sampled centre line, first and last equal the node positions.</summary>
        public IReadOnlyList<PointMm> Samples { get; }
        /// <summary>Length of the sampled centre line in metres.</summary>
        public double LengthM { get; }

        /// <summary>Direction pinned at the first control point when the curve was sampled (RD-16), or null for a free end.</summary>
        public RoadTangent? StartTangent { get; }
        /// <summary>Direction pinned at the last control point when the curve was sampled (RD-16), or null for a free end.</summary>
        public RoadTangent? EndTangent { get; }

        internal RoadSegment(int id, int nodeA, int nodeB, string typeId, CurveKind curve,
            PointMm[] controlPoints, PointMm[] samples, RoadTangent? startTangent = null, RoadTangent? endTangent = null)
        {
            StartTangent = startTangent; EndTangent = endTangent;
            Id = id; NodeA = nodeA; NodeB = nodeB; TypeId = typeId; Curve = curve;
            ControlPoints = controlPoints; Samples = samples;
            LengthM = Polylines.LengthMetres(samples);
        }

        /// <summary>
        /// The edge of the road corridor on one side, as a polyline running in the same direction as
        /// <see cref="Samples"/> (NodeA to NodeB). Each sample is moved sideways by half the road type's width along
        /// the averaged normal of its neighbouring pieces (mitred, clamped at 2x). An unknown road type gives width 0.
        /// Consecutive duplicate points are dropped.
        /// </summary>
        public PointMm[] SideLine(RoadSide side)
        {
            RoadTypeDef type = RoadTypes.Find(TypeId);
            double half = type == null ? 0 : type.WidthM * PointMm.MmPerMetre / 2.0;
            double sign = side == RoadSide.Left ? 1 : -1;
            var pts = Samples;
            var result = new List<PointMm>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                (double nx0, double nz0) = i > 0 ? Normal(pts[i - 1], pts[i]) : (0.0, 0.0);
                (double nx1, double nz1) = i < pts.Count - 1 ? Normal(pts[i], pts[i + 1]) : (0.0, 0.0);
                double nx = nx0 + nx1, nz = nz0 + nz1;
                double len = Math.Sqrt(nx * nx + nz * nz);
                double scale = half;
                if (len > 1e-9)
                {
                    nx /= len; nz /= len;
                    if (i > 0 && i < pts.Count - 1) scale = half / Math.Max(nx * nx0 + nz * nz0, 0.5);
                }
                var p = new PointMm(
                    pts[i].X + (long)Math.Round(sign * nx * scale, MidpointRounding.AwayFromZero),
                    pts[i].Z + (long)Math.Round(sign * nz * scale, MidpointRounding.AwayFromZero));
                if (result.Count == 0 || result[result.Count - 1] != p) result.Add(p);
            }
            return result.ToArray();
        }

        /// <summary>
        /// The road corridor as a counter-clockwise polygon: the right side line forward, then the left side line
        /// backward. Ends are cut square.
        /// </summary>
        public PointMm[] CorridorPolygon()
        {
            PointMm[] right = SideLine(RoadSide.Right), left = SideLine(RoadSide.Left);
            var poly = new List<PointMm>(right.Length + left.Length);
            poly.AddRange(right);
            for (int i = left.Length - 1; i >= 0; i--) poly.Add(left[i]);
            return poly.ToArray();
        }

        private static (double, double) Normal(PointMm a, PointMm b)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            return len < 1e-9 ? (0.0, 0.0) : (-dz / len, dx / len);
        }
    }
}
