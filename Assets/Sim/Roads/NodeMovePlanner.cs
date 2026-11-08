using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>The re-shaped version of one segment attached to a moved node (RD-17).</summary>
    public sealed class SegmentMovePreview
    {
        /// <summary>Id of the segment that would be replaced.</summary>
        public int OldSegmentId { get; }
        /// <summary>The new (cleaned) control points; the end on the moved node is the new position.</summary>
        public IReadOnlyList<PointMm> ControlPoints { get; }
        /// <summary>Validation result for the new curve: samples, junctions and the refusal, if any.</summary>
        public RoadPreview Preview { get; }

        internal SegmentMovePreview(int oldId, IReadOnlyList<PointMm> controlPoints, RoadPreview preview)
        { OldSegmentId = oldId; ControlPoints = controlPoints; Preview = preview; }
    }

    /// <summary>Result of <see cref="RoadValidator.PreviewMove"/>. Immutable.</summary>
    public sealed class NodeMovePreview
    {
        /// <summary>False when the node id is unknown.</summary>
        public bool NodeFound { get; }
        /// <summary>One entry per attached segment, in ascending old id order. Empty when the node is unknown.</summary>
        public IReadOnlyList<SegmentMovePreview> Segments { get; }
        /// <summary>The first refusal over the segments in order, or Valid.</summary>
        public RoadRefusal Reason { get; }
        /// <summary>Player-facing text of the first refusal, empty when valid.</summary>
        public string ReasonText { get; }
        /// <summary>True when the node exists and the move is allowed.</summary>
        public bool IsValid => NodeFound && Reason == RoadRefusal.Valid;

        internal NodeMovePreview(bool found, IReadOnlyList<SegmentMovePreview> segments, RoadRefusal reason, string text)
        { NodeFound = found; Segments = segments; Reason = reason; ReasonText = text ?? string.Empty; }
    }

    // Builds the re-shaped control points and tangents for every segment at a node and validates each one.
    internal static class NodeMovePlanner
    {
        public static NodeMovePreview Plan(SimWorld world, int nodeId, PointMm position)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            RoadNode node = world.Roads.GetNode(nodeId);
            var list = new List<SegmentMovePreview>();
            if (node == null) return new NodeMovePreview(false, list, RoadRefusal.Valid, string.Empty);

            var ids = new List<int>(node.SegmentIds);
            ids.Sort();
            var segs = new List<RoadSegment>();
            foreach (int id in ids) segs.Add(world.Roads.GetSegment(id));

            var cps = new List<List<PointMm>>();
            foreach (RoadSegment s in segs) cps.Add(Reshape(s, nodeId, position, world.Tuning.RoadMinPointSpacingM));

            // At a plain through-joint (two segments, no loops) both curves leave along one shared line.
            bool shared = segs.Count == 2 && segs[0].NodeA != segs[0].NodeB && segs[1].NodeA != segs[1].NodeB
                && cps[0].Count >= 2 && cps[1].Count >= 2;
            RoadTangent? outward0 = null, outward1 = null;
            if (shared)
            {
                double ux, uz, vx, vz;
                Outward(segs[0], nodeId, cps[0], out ux, out uz);
                Outward(segs[1], nodeId, cps[1], out vx, out vz);
                double tx = ux - vx, tz = uz - vz;
                if (tx * tx + tz * tz < 1e-12) { tx = ux; tz = uz; }
                if (tx * tx + tz * tz > 1e-12)
                {
                    Refine(world, segs, nodeId, cps, tx, tz, out outward0, out outward1);
                }
            }

            var ignored = new List<int>(ids);
            RoadRefusal first = RoadRefusal.Valid;
            string firstText = string.Empty;
            for (int i = 0; i < segs.Count; i++)
            {
                RoadSegment s = segs[i];
                RoadTangent? outward = i == 0 ? outward0 : outward1;
                bool loop = s.NodeA == s.NodeB;
                RoadTangent? st = null, et = null;
                if (!loop)
                {
                    if (s.NodeA == nodeId) { st = outward; et = s.EndTangent; }
                    else { st = s.StartTangent; et = outward.HasValue ? outward.Value.Reversed() : (RoadTangent?)null; }
                }
                RoadPreview p = RoadValidator.ValidateMove(world, cps[i], s.TypeId, ignored,
                    new[] { s.NodeA, s.NodeB }, st, et);
                list.Add(new SegmentMovePreview(s.Id, cps[i], p));
                if (first == RoadRefusal.Valid && !p.IsValid) { first = p.Reason; firstText = p.ReasonText; }
            }
            return new NodeMovePreview(true, list, first, firstText);
        }

        // A Hermite span bends away from its pinned tangent within the first metre, so pinning exactly (tx, tz) can leave
        // the first sample steps a few degrees off the shared line. Nudge each pinned direction by the error until the
        // steps agree with the shared line (RD-17: within 1 degree).
        private static void Refine(SimWorld world, List<RoadSegment> segs, int nodeId, List<List<PointMm>> cps,
            double tx, double tz, out RoadTangent? out0, out RoadTangent? out1)
        {
            var sampler = new CatmullRomSampler(world.Tuning);
            double len = Math.Sqrt(tx * tx + tz * tz);
            tx /= len; tz /= len;
            double[] px = { tx, -tx }, pz = { tz, -tz };      // pinned outward directions
            double[] gx = { tx, -tx }, gz = { tz, -tz };      // targets
            for (int iter = 0; iter < 8; iter++)
            {
                double worst = 0;
                for (int k = 0; k < 2; k++)
                {
                    RoadSegment s = segs[k];
                    bool atA = s.NodeA == nodeId;
                    var outward = new RoadTangent(px[k], pz[k]);
                    IReadOnlyList<PointMm> samples = atA
                        ? sampler.Sample(cps[k], outward, s.EndTangent)
                        : sampler.Sample(cps[k], s.StartTangent, outward.Reversed());
                    int n = samples.Count;
                    PointMm at = atA ? samples[0] : samples[n - 1];
                    PointMm nb = at;
                    for (int j = 1; j < n && nb == at; j++) nb = atA ? samples[j] : samples[n - 1 - j];
                    double dx = nb.X - at.X, dz = nb.Z - at.Z, d = Math.Sqrt(dx * dx + dz * dz);
                    if (!(d > 0)) continue;
                    dx /= d; dz /= d;
                    double ex = gx[k] - dx, ez = gz[k] - dz;
                    worst = Math.Max(worst, Math.Sqrt(ex * ex + ez * ez));
                    double nx = px[k] + ex, nz = pz[k] + ez, nl = Math.Sqrt(nx * nx + nz * nz);
                    if (nl > 0) { px[k] = nx / nl; pz[k] = nz / nl; }
                }
                if (worst < 0.002) break;     // about 0.1 degree
            }
            out0 = new RoadTangent(px[0], pz[0]);
            out1 = new RoadTangent(px[1], pz[1]);
        }

        // The segment's control points with its end(s) on the node replaced, cleaned by RD-02.
        private static List<PointMm> Reshape(RoadSegment s, int nodeId, PointMm position, double minSpacingM)
        {
            var pts = new List<PointMm>(s.ControlPoints);
            if (s.NodeA == nodeId) pts[0] = position;
            if (s.NodeB == nodeId) pts[pts.Count - 1] = position;
            return ControlPointFilter.Clean(pts, minSpacingM);
        }

        // Unit direction from the node along the (cleaned) segment, towards its neighbouring control point.
        private static void Outward(RoadSegment s, int nodeId, List<PointMm> cps, out double x, out double z)
        {
            PointMm at = s.NodeA == nodeId ? cps[0] : cps[cps.Count - 1];
            PointMm nb = s.NodeA == nodeId ? cps[1] : cps[cps.Count - 2];
            double dx = nb.X - at.X, dz = nb.Z - at.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len > 0) { x = dx / len; z = dz / len; } else { x = 0; z = 0; }
        }
    }
}
