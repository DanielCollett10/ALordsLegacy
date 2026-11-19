using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>
    /// The derived line of a house fence (decision 0053, HL-10..12c): the plot outline minus its road frontage edges.
    /// Pure and never saved; recomputed from the plot. The frontage is always left open. Curved edges are measured along
    /// their flattened outline.
    /// </summary>
    public sealed class FenceLine
    {
        /// <summary>Side of the square marker footprint in mm.</summary>
        public const long MarkerSizeMm = 1000;

        /// <summary>The fenced runs, each a polyline in outline order. Adjacent non-frontage edges share one run.</summary>
        public IReadOnlyList<IReadOnlyList<PointMm>> Runs { get; }
        /// <summary>Total length of all runs in mm.</summary>
        public double LengthMm { get; }
        /// <summary>Middle (by length) of the longest frontage edge, where the marker sits. Equals the plot's first vertex if there is no frontage.</summary>
        public PointMm AccessPoint { get; }
        /// <summary>True if the plot has at least one road frontage edge.</summary>
        public bool HasFrontage { get; }

        private FenceLine(List<IReadOnlyList<PointMm>> runs, double length, PointMm access, bool frontage)
        {
            Runs = runs; LengthMm = length; AccessPoint = access; HasFrontage = frontage;
        }

        /// <summary>The fence line of a plot. Degenerate plots (fewer than 3 outline points) give no runs and length 0.</summary>
        public static FenceLine Of(AreaRecord plot)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            var runs = new List<IReadOnlyList<PointMm>>();
            if (plot.Polygon.Count < 3 || plot.Shape.Vertices.Count < 3) return new FenceLine(runs, 0, default, false);

            PointMm[] outline = plot.ShapeOrderOutline(out int[] starts);
            int n = starts.Length;
            bool frontage = false;
            int best = -1;
            double bestLen = -1;
            List<PointMm> cur = null;
            bool firstIsOpen = false;
            for (int i = 0; i < n; i++)
            {
                List<PointMm> pts = EdgePoints(outline, starts, i);
                if (plot.Shape.Edges[i].Kind == AreaEdgeKind.FollowsRoad)
                {
                    frontage = true;
                    double len = Length(pts);
                    if (len > bestLen) { bestLen = len; best = i; }
                    if (cur != null) { runs.Add(cur); cur = null; }
                    continue;
                }
                if (i == 0) firstIsOpen = true;
                if (cur == null) cur = pts;
                else cur.AddRange(pts.GetRange(1, pts.Count - 1));
            }
            if (cur != null)
            {
                // The last run continues into the first when the plot's edge n-1 and edge 0 are both fenced.
                if (frontage && firstIsOpen && runs.Count > 0)
                {
                    var first = new List<PointMm>(cur);
                    var head = runs[0];
                    for (int k = 1; k < head.Count; k++) first.Add(head[k]);
                    runs[0] = first;
                }
                else runs.Add(cur);
            }

            double total = 0;
            foreach (var r in runs) total += Length(r);
            PointMm access = best >= 0 ? Midpoint(EdgePoints(outline, starts, best), bestLen) : outline[0];
            return new FenceLine(runs, total, access, frontage);
        }

        /// <summary>Panels needed: <c>ceil(length / FencePanelLengthM)</c>; 0 for a zero-length line.</summary>
        public int PanelCount(SimTuning tuning)
        {
            if (tuning == null) throw new ArgumentNullException(nameof(tuning));
            double panelMm = tuning.FencePanelLengthM * 1000.0;
            if (panelMm <= 0 || LengthMm <= 0) return 0;
            // Tolerance keeps a line of exactly N panels at N despite floating-point noise.
            return (int)Math.Ceiling(LengthMm / panelMm - 1e-9);
        }

        /// <summary>The fence recipe: wattle panels, <c>FenceMinutesPerPanel</c> worker-minutes each.</summary>
        public ConstructionRecipe Recipe(SimTuning tuning)
        {
            int panels = PanelCount(tuning);
            return new ConstructionRecipe(new[] { new KeyValuePair<string, int>(Items.WattlePanel.Id, panels) },
                panels * tuning.FenceMinutesPerPanel);
        }

        // Points of edge i, including its end point (the last edge wraps to point 0).
        private static List<PointMm> EdgePoints(PointMm[] outline, int[] starts, int i)
        {
            int end = i + 1 < starts.Length ? starts[i + 1] : outline.Length;
            var pts = new List<PointMm>();
            for (int k = starts[i]; k <= end; k++) pts.Add(outline[k % outline.Length]);
            return pts;
        }

        private static double Length(IReadOnlyList<PointMm> pts)
        {
            double sum = 0;
            for (int k = 0; k + 1 < pts.Count; k++) sum += pts[k].DistanceTo(pts[k + 1]);
            return sum;
        }

        private static PointMm Midpoint(List<PointMm> pts, double total)
        {
            double half = total / 2.0, walked = 0;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                double len = pts[k].DistanceTo(pts[k + 1]);
                if (len <= 0) continue;
                if (walked + len >= half)
                {
                    double t = (half - walked) / len;
                    return new PointMm((long)Math.Round(pts[k].X + (pts[k + 1].X - pts[k].X) * t, MidpointRounding.AwayFromZero),
                                       (long)Math.Round(pts[k].Z + (pts[k + 1].Z - pts[k].Z) * t, MidpointRounding.AwayFromZero));
                }
                walked += len;
            }
            return pts[0];
        }
    }
}
