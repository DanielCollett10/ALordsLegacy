using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>One piece of a drawn road: control points and samples for one network segment.</summary>
    internal readonly struct RoadPiece
    {
        public readonly PointMm[] ControlPoints;
        public readonly PointMm[] Samples;

        public RoadPiece(PointMm[] controlPoints, PointMm[] samples) { ControlPoints = controlPoints; Samples = samples; }
    }

    /// <summary>
    /// Cuts a drawn road's samples and control points at its junction points (decision 0013). Every original
    /// sample is kept exactly; a cut point is added as a sample and a control point on both sides of the cut.
    /// </summary>
    internal static class RoadCutter
    {
        /// <param name="samples">The sampled centre line.</param>
        /// <param name="controlPoints">The cleaned control points (all lie on the samples).</param>
        /// <param name="startNode">Node position the road start snaps to, or null. Added in front of the first sample.</param>
        /// <param name="endNode">Node position the road end snaps to, or null. Added after the last sample.</param>
        /// <param name="interior">Junction points along the road (any order).</param>
        public static List<RoadPiece> Cut(IReadOnlyList<PointMm> samples, IReadOnlyList<PointMm> controlPoints,
            PointMm? startNode, PointMm? endNode, IReadOnlyList<PointMm> interior)
        {
            var s = new List<PointMm>(samples);
            var c = new List<PointMm>(controlPoints);
            if (startNode.HasValue && startNode.Value != s[0]) { s.Insert(0, startNode.Value); c.Insert(0, startNode.Value); }
            if (endNode.HasValue && endNode.Value != s[s.Count - 1]) { s.Add(endNode.Value); c.Add(endNode.Value); }

            var cuts = new List<CutPoint>();
            foreach (PointMm p in interior)
            {
                if (p == s[0] || p == s[s.Count - 1]) continue;
                PolylineHit hit = Polylines.Nearest(s, p);
                int i = hit.SegmentIndex;
                int on = p == s[i] ? i : p == s[i + 1] ? i + 1 : -1;
                if (on == 0 || on == s.Count - 1) continue;
                cuts.Add(new CutPoint(p, hit.ArcLengthMm, i, on));
            }
            cuts.Sort((a, b) => a.Arc.CompareTo(b.Arc));
            for (int k = cuts.Count - 1; k > 0; k--)
                if (cuts[k].Point == cuts[k - 1].Point) cuts.RemoveAt(k);

            double[] cpPos = ControlPointPositions(s, c);
            var pieces = new List<RoadPiece>();
            int pointer = 0;
            double lo = -1;
            PointMm? from = null;
            foreach (CutPoint cut in cuts)
            {
                var ps = new List<PointMm>();
                if (from.HasValue) ps.Add(from.Value);
                int lastBefore = cut.OnSample >= 0 ? cut.OnSample - 1 : cut.Index;
                for (int k = pointer; k <= lastBefore; k++) ps.Add(s[k]);
                ps.Add(cut.Point);
                pieces.Add(new RoadPiece(Controls(c, cpPos, from, lo, cut.Pos, cut.Point), ps.ToArray()));
                pointer = lastBefore + 1 + (cut.OnSample >= 0 ? 1 : 0);
                lo = cut.Pos;
                from = cut.Point;
            }
            var last = new List<PointMm>();
            if (from.HasValue) last.Add(from.Value);
            for (int k = pointer; k < s.Count; k++) last.Add(s[k]);
            pieces.Add(new RoadPiece(Controls(c, cpPos, from, lo, double.MaxValue, null), last.ToArray()));
            return pieces;
        }

        private static PointMm[] Controls(List<PointMm> c, double[] pos, PointMm? from, double lo, double hi, PointMm? to)
        {
            var r = new List<PointMm>();
            if (from.HasValue) r.Add(from.Value);
            for (int k = 0; k < c.Count; k++) if (pos[k] > lo && pos[k] < hi) r.Add(c[k]);
            if (to.HasValue) r.Add(to.Value);
            return r.ToArray();
        }

        // Position of each control point along the samples: the sample index, or span index + 0.5 if it is off the samples.
        private static double[] ControlPointPositions(List<PointMm> s, List<PointMm> c)
        {
            var pos = new double[c.Count];
            int cursor = 0;
            for (int k = 0; k < c.Count; k++)
            {
                double p = -1;
                for (int j = cursor; j < s.Count; j++)
                    if (s[j] == c[k]) { p = j; cursor = j; break; }
                pos[k] = p >= 0 ? p : Polylines.Nearest(s, c[k]).SegmentIndex + 0.5;
            }
            return pos;
        }

        private readonly struct CutPoint
        {
            public readonly PointMm Point;
            public readonly double Arc;
            public readonly int Index;     // span index of the nearest hit
            public readonly int OnSample;  // sample index if the cut lies on a sample, else -1
            public double Pos => OnSample >= 0 ? OnSample : Index + 0.5;

            public CutPoint(PointMm point, double arc, int index, int onSample)
            { Point = point; Arc = arc; Index = index; OnSample = onSample; }
        }
    }
}
