using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>
    /// Decides whether a drawn road is allowed and where its junctions are (RD-05..RD-10). Used by the tool
    /// preview and by the Sim command. Pure: it never changes the world.
    /// </summary>
    public static class RoadValidator
    {
        private const double ExemptRiseMm = 5;   // a sample counts as "moving away" from a junction if D rises by more than this
        private const double DedupeMm = 10;

        /// <summary>
        /// Validates a road. Reasons are checked in order: outside the map, too steep, crosses itself, overlaps road.
        /// Junctions are planned whatever the result.
        /// </summary>
        /// <param name="world">The world (terrain, tuning, road network). Not changed.</param>
        /// <param name="controlPoints">The clicked points; cleaned and sampled as in RD-02/RD-03.</param>
        /// <param name="typeId">Road type id.</param>
        /// <param name="ignoreSegmentId">A segment to treat as absent (for reshaping); -1 for none.</param>
        public static RoadPreview Validate(SimWorld world, IReadOnlyList<PointMm> controlPoints, string typeId,
            int ignoreSegmentId = -1)
        {
            var ignored = new HashSet<int>();
            if (ignoreSegmentId >= 0) ignored.Add(ignoreSegmentId);
            return ValidateCore(world, controlPoints, typeId, ignored, false, null, null, null);
        }

        /// <summary>
        /// Validates a re-shaped road (RD-17): the segments in <paramref name="ignoreSegmentIds"/> are treated as absent,
        /// no tangent is worked out from neighbours (only the ones passed in are used), and any junction other than a
        /// join at one of <paramref name="ownNodeIds"/> is refused as <see cref="RoadRefusal.WouldJoinRoad"/> (checked last).
        /// </summary>
        public static RoadPreview ValidateMove(SimWorld world, IReadOnlyList<PointMm> controlPoints, string typeId,
            IReadOnlyCollection<int> ignoreSegmentIds, IReadOnlyCollection<int> ownNodeIds,
            RoadTangent? startTangent, RoadTangent? endTangent)
        {
            return ValidateCore(world, controlPoints, typeId, new HashSet<int>(ignoreSegmentIds ?? new int[0]), true,
                startTangent, endTangent, ownNodeIds);
        }

        /// <summary>
        /// Previews moving a node (RD-17) without changing anything: one <see cref="RoadPreview"/> per attached segment.
        /// </summary>
        public static NodeMovePreview PreviewMove(SimWorld world, int nodeId, PointMm position) =>
            NodeMovePlanner.Plan(world, nodeId, position);

        private static RoadPreview ValidateCore(SimWorld world, IReadOnlyList<PointMm> controlPoints, string typeId,
            HashSet<int> ignored, bool moveMode, RoadTangent? forcedStart, RoadTangent? forcedEnd,
            IReadOnlyCollection<int> ownNodeIds)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var noRanges = new List<InvalidRange>();
            var noJunctions = new List<PlannedJunction>();
            var none = new List<PointMm>();

            RoadTypeDef type = typeId == null ? null : RoadTypes.Find(typeId);
            if (type == null) return new RoadPreview(none, noRanges, noJunctions, 0, 0, RoadRefusal.UnknownType);
            if (controlPoints == null) return new RoadPreview(none, noRanges, noJunctions, 0, 0, RoadRefusal.NotEnoughPoints);

            var sampler = new CatmullRomSampler(world.Tuning);
            IReadOnlyList<PointMm> samples = sampler.Sample(controlPoints);
            int n = samples.Count;
            if (n < 2) return new RoadPreview(samples, noRanges, noJunctions, 0, 0, RoadRefusal.NotEnoughPoints);

            var others = new List<RoadSegment>();
            foreach (RoadSegment s in world.Roads.Segments) if (!ignored.Contains(s.Id)) others.Add(s);

            double widthMm = type.WidthM * PointMm.MmPerMetre;
            double lengthM = Polylines.LengthMetres(samples);

            // RD-16: an end joining a dead-end node leaves flush with the old road. The ends are the same with or
            // without tangents, so find the joins on the free curve, then sample again with the tangents.
            RoadTangent? startTan = null, endTan = null;
            if (moveMode)
            {
                startTan = forcedStart; endTan = forcedEnd;
            }
            else
            {
                foreach (PlannedJunction j in PlanJunctions(samples, others, widthMm, lengthM, new List<(int Lo, int Hi)>()))
                {
                    if (j.Kind != JunctionKind.JoinNode) continue;
                    bool atStart = j.NewArcM <= 0, atEnd = j.NewArcM >= lengthM;
                    if (!atStart && !atEnd) continue;
                    RoadTangent? outward = DeadEndDirection(world.Roads, j.NodeId, ignored);
                    if (!outward.HasValue) continue;
                    if (atStart) startTan = outward; else endTan = outward.Value.Reversed();
                }
            }
            if (startTan.HasValue || endTan.HasValue)
            {
                samples = sampler.Sample(controlPoints, startTan, endTan);
                n = samples.Count;
                lengthM = Polylines.LengthMetres(samples);
            }

            bool outside = false;
            for (int i = 0; i < n && !outside; i++)
                if (!world.Terrain.Contains((float)samples[i].XMetres, (float)samples[i].ZMetres)) outside = true;

            List<InvalidRange> ranges = CheckGrade(world, samples, type.MaxGrade, out double maxGrade);
            bool selfCross = CrossesItself(samples);

            var cuts = new List<(int Lo, int Hi)>();
            List<PlannedJunction> junctions = PlanJunctions(samples, others, widthMm, lengthM, cuts);

            RoadRefusal reason = RoadRefusal.Valid;
            string crossedName = "";
            if (outside) reason = RoadRefusal.OutsideMap;
            else if (ranges.Count > 0) reason = RoadRefusal.TooSteep;
            else if (selfCross) reason = RoadRefusal.CrossesItself;
            else if (Overlaps(samples, others, junctions, cuts, widthMm)) reason = RoadRefusal.OverlapsRoad;
            else
            {
                Areas.AreaRecord crossed = FindCrossedBuildingArea(world, samples, typeId);
                if (crossed != null) { reason = RoadRefusal.CrossesArea; crossedName = crossed.Name; }
            }
            if (moveMode && reason == RoadRefusal.Valid)
            {
                foreach (PlannedJunction j in junctions)
                {
                    bool own = j.Kind == JunctionKind.JoinNode && ownNodeIds != null && Contains(ownNodeIds, j.NodeId);
                    if (!own) { reason = RoadRefusal.WouldJoinRoad; break; }
                }
            }

            return new RoadPreview(samples, ranges, junctions, lengthM, maxGrade, reason, crossedName, startTan, endTan);
        }

        private static bool Contains(IReadOnlyCollection<int> ids, int id)
        {
            foreach (int x in ids) if (x == id) return true;
            return false;
        }

        // Direction pointing away from the old road at a node that has exactly one (non-looping) segment, else null.
        private static RoadTangent? DeadEndDirection(RoadNetwork net, int nodeId, HashSet<int> ignored)
        {
            RoadNode node = net.GetNode(nodeId);
            if (node == null) return null;
            RoadSegment only = null;
            foreach (int id in node.SegmentIdList)
            {
                if (ignored.Contains(id)) continue;
                if (only != null) return null;
                only = net.GetSegment(id);
            }
            if (only == null || only.NodeA == only.NodeB) return null;

            IReadOnlyList<PointMm> s = only.Samples;
            bool atA = only.NodeA == nodeId;
            PointMm at = atA ? s[0] : s[s.Count - 1];
            for (int k = 1; k < s.Count; k++)
            {
                PointMm nb = atA ? s[k] : s[s.Count - 1 - k];
                if (nb != at) return new RoadTangent(at.X - nb.X, at.Z - nb.Z);
            }
            return null;
        }

        // RD-13: the lowest-id building area whose overlap with the new corridor exceeds the tolerance, or null.
        // Work areas are never in the building grid, so they never block roads.
        private static Areas.AreaRecord FindCrossedBuildingArea(SimWorld world, IReadOnlyList<PointMm> samples, string typeId)
        {
            if (world.Areas.Count == 0) return null;
            PointMm[] corridor = new RoadSegment(0, 0, 0, typeId, CurveKind.CatmullRomCentripetal,
                new PointMm[0], ToArray(samples)).CorridorPolygon();
            if (corridor.Length < 3) return null;
            BoundsMm b = Polygons.Bounds(corridor);
            Areas.AreaRecord best = null;
            foreach (Areas.AreaRecord a in world.Areas.QueryOverlapCandidates(b))
            {
                if (best != null && a.Id >= best.Id) continue;
                if (!a.Bounds.Intersects(b)) continue;
                if (PolygonOverlap.Overlaps(corridor, a.Polygon, world.Tuning.OverlapToleranceM2)) best = a;
            }
            return best;
        }

        private static PointMm[] ToArray(IReadOnlyList<PointMm> pts)
        {
            var a = new PointMm[pts.Count];
            for (int i = 0; i < a.Length; i++) a[i] = pts[i];
            return a;
        }

        private static List<InvalidRange> CheckGrade(SimWorld world, IReadOnlyList<PointMm> samples, double maxGrade, out double worst)
        {
            var ranges = new List<InvalidRange>();
            worst = 0;
            int start = -1, end = -1;
            float prevH = world.Terrain.HeightAt((float)samples[0].XMetres, (float)samples[0].ZMetres);
            for (int i = 1; i < samples.Count; i++)
            {
                float h = world.Terrain.HeightAt((float)samples[i].XMetres, (float)samples[i].ZMetres);
                double run = samples[i - 1].DistanceTo(samples[i]) / PointMm.MmPerMetre;
                bool bad = false;
                if (run > 0)
                {
                    double grade = Math.Abs(h - prevH) / run;
                    if (grade > worst) worst = grade;
                    bad = grade > maxGrade;
                }
                if (bad)
                {
                    if (start < 0) start = i - 1;
                    end = i;
                }
                else if (start >= 0)
                {
                    ranges.Add(new InvalidRange(start, end));
                    start = -1;
                }
                prevH = h;
            }
            if (start >= 0) ranges.Add(new InvalidRange(start, end));
            return ranges;
        }

        private static bool CrossesItself(IReadOnlyList<PointMm> s)
        {
            int n = s.Count;
            for (int i = 0; i + 1 < n; i++)
            {
                PointMm a1 = s[i], a2 = s[i + 1];
                long minX = Math.Min(a1.X, a2.X), maxX = Math.Max(a1.X, a2.X);
                long minZ = Math.Min(a1.Z, a2.Z), maxZ = Math.Max(a1.Z, a2.Z);
                for (int j = i + 2; j + 1 < n; j++)
                {
                    PointMm b1 = s[j], b2 = s[j + 1];
                    if (Math.Max(b1.X, b2.X) < minX || Math.Min(b1.X, b2.X) > maxX
                        || Math.Max(b1.Z, b2.Z) < minZ || Math.Min(b1.Z, b2.Z) > maxZ) continue;
                    if (Segments.Intersects(a1, a2, b1, b2)) return true;
                }
            }
            return false;
        }

        private static List<PlannedJunction> PlanJunctions(IReadOnlyList<PointMm> samples, List<RoadSegment> others,
            double widthMm, double lengthM, List<(int Lo, int Hi)> cuts)
        {
            int n = samples.Count;
            var list = new List<PlannedJunction>();
            var ends = new List<PlannedJunction>();

            for (int e = 0; e < 2; e++)
            {
                bool atStart = e == 0;
                PointMm end = atStart ? samples[0] : samples[n - 1];
                RoadSegment best = null;
                PolylineHit bestHit = default(PolylineHit);
                foreach (RoadSegment seg in others)
                {
                    PolylineHit h = Polylines.Nearest(seg.Samples, end);
                    if (h.DistanceMm <= widthMm / 2 && (best == null || h.DistanceMm < bestHit.DistanceMm)) { best = seg; bestHit = h; }
                }
                if (best == null) continue;

                PointMm p = Round(bestHit.X, bestHit.Z);
                PlannedJunction j;
                double newArc = atStart ? 0 : lengthM;
                if (p == best.Samples[0])
                    j = new PlannedJunction(JunctionKind.JoinNode, best.Id, best.NodeA, p, 0, newArc);
                else if (p == best.Samples[best.Samples.Count - 1])
                    j = new PlannedJunction(JunctionKind.JoinNode, best.Id, best.NodeB, p, best.LengthM, newArc);
                else
                    j = new PlannedJunction(JunctionKind.T, best.Id, -1, p, bestHit.ArcLengthMm / PointMm.MmPerMetre, newArc);
                list.Add(j);
                ends.Add(j);
                cuts.Add(atStart ? (-1, 0) : (n - 1, n));
            }

            foreach (RoadSegment seg in others)
            {
                IReadOnlyList<PointMm> b = seg.Samples;
                for (int i = 0; i + 1 < n; i++)
                {
                    PointMm a1 = samples[i], a2 = samples[i + 1];
                    for (int k = 0; k + 1 < b.Count; k++)
                    {
                        SegmentRelation rel = Segments.Classify(a1, a2, b[k], b[k + 1]);
                        if (rel != SegmentRelation.Crossing && rel != SegmentRelation.Touching) continue;
                        // Parallel pieces only touch where they run along each other: that is overlap, not a junction.
                        if ((a2.X - a1.X) * (b[k + 1].Z - b[k].Z) - (a2.Z - a1.Z) * (b[k + 1].X - b[k].X) == 0) continue;
                        PointMm p = rel == SegmentRelation.Crossing ? CrossingPoint(a1, a2, b[k], b[k + 1]) : TouchPoint(a1, a2, b[k], b[k + 1]);
                        if (Near(list, p)) continue;

                        double newArc = Polylines.Nearest(samples, p).ArcLengthMm / PointMm.MmPerMetre;
                        if (NearEndJunction(ends, seg.Id, newArc, widthMm / 2 / PointMm.MmPerMetre)) continue;
                        double oldArc = Polylines.Nearest(b, p).ArcLengthMm / PointMm.MmPerMetre;
                        int node = p == b[0] ? seg.NodeA : p == b[b.Count - 1] ? seg.NodeB : -1;
                        list.Add(new PlannedJunction(JunctionKind.X, seg.Id, node, p, oldArc, newArc));
                        cuts.Add((i, i + 1));
                    }
                }
            }

            // Sort by position along the new road, keeping cuts in step.
            var order = new List<int>();
            for (int i = 0; i < list.Count; i++) order.Add(i);
            order.Sort((x, y) =>
            {
                int c = list[x].NewArcM.CompareTo(list[y].NewArcM);
                if (c != 0) return c;
                c = list[x].SegmentId.CompareTo(list[y].SegmentId);
                return c != 0 ? c : x.CompareTo(y);
            });
            var sorted = new List<PlannedJunction>();
            var sortedCuts = new List<(int Lo, int Hi)>();
            foreach (int i in order) { sorted.Add(list[i]); sortedCuts.Add(cuts[i]); }
            cuts.Clear();
            cuts.AddRange(sortedCuts);
            return sorted;
        }

        private static bool Near(List<PlannedJunction> list, PointMm p)
        {
            foreach (PlannedJunction j in list) if (j.Point.DistanceTo(p) <= DedupeMm) return true;
            return false;
        }

        private static bool NearEndJunction(List<PlannedJunction> ends, int segId, double newArcM, double withinM)
        {
            foreach (PlannedJunction j in ends)
                if (j.SegmentId == segId && Math.Abs(j.NewArcM - newArcM) < withinM) return true;
            return false;
        }

        private static PointMm Round(double x, double z) =>
            new PointMm((long)Math.Round(x, MidpointRounding.AwayFromZero), (long)Math.Round(z, MidpointRounding.AwayFromZero));

        private static PointMm CrossingPoint(PointMm a1, PointMm a2, PointMm b1, PointMm b2)
        {
            double rx = a2.X - a1.X, rz = a2.Z - a1.Z, sx = b2.X - b1.X, sz = b2.Z - b1.Z;
            double denom = rx * sz - rz * sx;
            double t = ((b1.X - a1.X) * sz - (b1.Z - a1.Z) * sx) / denom;
            return Round(a1.X + t * rx, a1.Z + t * rz);
        }

        private static PointMm TouchPoint(PointMm a1, PointMm a2, PointMm b1, PointMm b2)
        {
            if (Segments.ContainsPoint(b1, b2, a1)) return a1;
            if (Segments.ContainsPoint(b1, b2, a2)) return a2;
            if (Segments.ContainsPoint(a1, a2, b1)) return b1;
            return b2;
        }

        private static bool Overlaps(IReadOnlyList<PointMm> samples, List<RoadSegment> others, List<PlannedJunction> junctions,
            List<(int Lo, int Hi)> cuts, double widthMm)
        {
            if (others.Count == 0) return false;
            int n = samples.Count;
            var d = new double[n];
            for (int i = 0; i < n; i++)
            {
                double best = double.MaxValue;
                foreach (RoadSegment seg in others)
                    best = Math.Min(best, Polylines.Nearest(seg.Samples, samples[i]).DistanceMm);
                d[i] = best;
            }

            var exempt = new bool[n];
            for (int q = 0; q < junctions.Count; q++)
            {
                for (int i = 0; i < n; i++)
                    if (samples[i].DistanceTo(junctions[q].Point) <= widthMm) exempt[i] = true;
                int lo = cuts[q].Lo, hi = cuts[q].Hi;
                if (lo >= 0) { exempt[lo] = true; Walk(exempt, d, lo, -1, widthMm); }
                if (hi < n) { exempt[hi] = true; Walk(exempt, d, hi, 1, widthMm); }
            }

            for (int i = 0; i < n; i++)
                if (!exempt[i] && d[i] < widthMm) return true;
            return false;
        }

        // Exempts samples moving away from a junction while they get steadily further from the road, until a full width away.
        private static void Walk(bool[] exempt, double[] d, int from, int dir, double widthMm)
        {
            int cur = from;
            while (d[cur] < widthMm)
            {
                int next = cur + dir;
                if (next < 0 || next >= d.Length || !(d[next] > d[cur] + ExemptRiseMm)) break;
                exempt[next] = true;
                cur = next;
            }
        }
    }
}
