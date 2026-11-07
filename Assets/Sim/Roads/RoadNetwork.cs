using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>
    /// The road graph: nodes (road ends and junctions) and segments (stretches between two nodes), each
    /// segment keeping its control points and sampled centre line (decision 0013, docs/design/roads.md).
    /// Ids are never reused. Iteration is in ascending id order, so results are deterministic.
    /// </summary>
    public sealed class RoadNetwork
    {
        private readonly SortedDictionary<int, RoadNode> _nodes = new SortedDictionary<int, RoadNode>();
        private readonly SortedDictionary<int, RoadSegment> _segments = new SortedDictionary<int, RoadSegment>();
        private readonly Dictionary<PointMm, int> _nodeAt = new Dictionary<PointMm, int>();
        private int _nextNodeId = 1;
        private int _nextSegmentId = 1;

        /// <summary>Increases on every change (add, split, remove). Views rebuild when it changes.</summary>
        public int Version { get; private set; }

        /// <summary>Number of nodes.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>Number of segments.</summary>
        public int SegmentCount => _segments.Count;

        /// <summary>All nodes in id order.</summary>
        public IEnumerable<RoadNode> Nodes => _nodes.Values;

        /// <summary>All segments in id order.</summary>
        public IEnumerable<RoadSegment> Segments => _segments.Values;

        /// <summary>Finds a node, or null.</summary>
        public RoadNode GetNode(int id) => _nodes.TryGetValue(id, out RoadNode n) ? n : null;

        /// <summary>Finds a segment, or null.</summary>
        public RoadSegment GetSegment(int id) => _segments.TryGetValue(id, out RoadSegment s) ? s : null;

        /// <summary>
        /// Adds a segment. Its end nodes sit at the first and last control points; an existing node at exactly
        /// that position is reused, otherwise one is created. The first and last samples must equal those
        /// control points. Both lists are copied.
        /// </summary>
        /// <returns>The new segment.</returns>
        /// <exception cref="ArgumentException">Fewer than 2 control points or samples, or ends that do not match.</exception>
        public RoadSegment AddSegment(string typeId, CurveKind curve,
            IReadOnlyList<PointMm> controlPoints, IReadOnlyList<PointMm> samples)
        {
            if (typeId == null) throw new ArgumentNullException(nameof(typeId));
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (controlPoints.Count < 2) throw new ArgumentException("Need at least 2 control points.", nameof(controlPoints));
            if (samples.Count < 2) throw new ArgumentException("Need at least 2 samples.", nameof(samples));
            if (controlPoints[0] != samples[0] || controlPoints[controlPoints.Count - 1] != samples[samples.Count - 1])
                throw new ArgumentException("First and last samples must equal the first and last control points.", nameof(samples));

            int a = NodeAtOrNew(controlPoints[0]);
            int b = NodeAtOrNew(controlPoints[controlPoints.Count - 1]);
            RoadSegment seg = Attach(a, b, typeId, curve, Copy(controlPoints), Copy(samples));
            Version++;
            return seg;
        }

        /// <summary>
        /// Cuts a segment at the point on its centre line nearest to <paramref name="point"/> (rounded to mm).
        /// Both halves keep the original samples exactly; the cut point becomes the last control point of the
        /// first half and the first of the second (decision 0013). The original segment id is removed and two
        /// new ids are issued. If the cut point is at one of the segment's ends nothing changes and
        /// <see cref="SplitResult.Split"/> is false.
        /// </summary>
        /// <exception cref="ArgumentException">Unknown segment id.</exception>
        public SplitResult SplitSegmentAt(int segmentId, PointMm point)
        {
            RoadSegment seg = GetSegment(segmentId);
            if (seg == null) throw new ArgumentException("Unknown segment " + segmentId, nameof(segmentId));

            IReadOnlyList<PointMm> s = seg.Samples;
            PolylineHit hit = Polylines.Nearest(s, point);
            var p = new PointMm((long)Math.Round(hit.X, MidpointRounding.AwayFromZero),
                                (long)Math.Round(hit.Z, MidpointRounding.AwayFromZero));
            int i = hit.SegmentIndex;

            // c = sample index of the cut when it lies on a sample; mid-span otherwise.
            int c = -1;
            if (p == s[i]) c = i; else if (p == s[i + 1]) c = i + 1;
            if (c == 0) return new SplitResult(false, seg.NodeA, -1, -1);
            if (c == s.Count - 1) return new SplitResult(false, seg.NodeB, -1, -1);
            if (c < 0 && (p == s[0])) return new SplitResult(false, seg.NodeA, -1, -1);
            if (c < 0 && (p == s[s.Count - 1])) return new SplitResult(false, seg.NodeB, -1, -1);

            bool mid = c < 0;
            double cutPos = mid ? i + 0.5 : c;

            var firstSamples = new List<PointMm>();
            var secondSamples = new List<PointMm>();
            int lastBefore = mid ? i : c - 1;
            for (int k = 0; k <= lastBefore; k++) firstSamples.Add(s[k]);
            firstSamples.Add(p);
            secondSamples.Add(p);
            for (int k = lastBefore + 1 + (mid ? 0 : 1); k < s.Count; k++) secondSamples.Add(s[k]);

            var firstCps = new List<PointMm>();
            var secondCps = new List<PointMm>();
            int cursor = 0;
            foreach (PointMm cp in seg.ControlPoints)
            {
                double pos = -1;
                for (int k = cursor; k < s.Count; k++)
                    if (s[k] == cp) { pos = k; cursor = k; break; }
                if (pos < 0) pos = Polylines.Nearest(s, cp).SegmentIndex + 0.5;
                if (pos < cutPos) firstCps.Add(cp);
                else if (pos > cutPos) secondCps.Add(cp);
            }
            firstCps.Add(p);
            secondCps.Insert(0, p);

            int cutNode = NodeAtOrNew(p);
            int nodeA = seg.NodeA, nodeB = seg.NodeB;
            // Attach the halves before removing the original so its end nodes are never left empty and dropped.
            RoadSegment first = Attach(nodeA, cutNode, seg.TypeId, seg.Curve, firstCps.ToArray(), firstSamples.ToArray());
            RoadSegment second = Attach(cutNode, nodeB, seg.TypeId, seg.Curve, secondCps.ToArray(), secondSamples.ToArray());
            RemoveSegmentCore(seg);
            Version++;
            return new SplitResult(true, cutNode, first.Id, second.Id);
        }

        /// <summary>Removes a segment, and any end node left with no segments. False if the id is unknown.</summary>
        public bool RemoveSegment(int segmentId)
        {
            RoadSegment seg = GetSegment(segmentId);
            if (seg == null) return false;
            RemoveSegmentCore(seg);
            Version++;
            return true;
        }

        /// <summary>True if a chain of segments links the two nodes (a node is connected to itself). False for unknown ids.</summary>
        public bool AreConnected(int nodeA, int nodeB)
        {
            if (!_nodes.ContainsKey(nodeA) || !_nodes.ContainsKey(nodeB)) return false;
            if (nodeA == nodeB) return true;
            return Flood(nodeA, null).Contains(nodeB);
        }

        /// <summary>Number of separate connected networks (0 when empty).</summary>
        public int ComponentCount
        {
            get
            {
                var seen = new HashSet<int>();
                int count = 0;
                foreach (int id in _nodes.Keys)
                {
                    if (seen.Contains(id)) continue;
                    count++;
                    Flood(id, seen);
                }
                return count;
            }
        }

        /// <summary>
        /// Number of segments in the same connected network as the given one, including itself.
        /// 0 for an unknown id.
        /// </summary>
        public int ReachableSegmentCount(int segmentId)
        {
            RoadSegment seg = GetSegment(segmentId);
            if (seg == null) return 0;
            HashSet<int> nodes = Flood(seg.NodeA, null);
            int n = 0;
            foreach (RoadSegment s in _segments.Values)
                if (nodes.Contains(s.NodeA)) n++;
            return n;
        }

        /// <summary>
        /// Finds the closest road feature within <paramref name="maxDistanceMm"/>. A node within range is
        /// preferred over any line point, even a nearer one. Ties go to the lowest id.
        /// </summary>
        public RoadHit FindNearest(PointMm point, double maxDistanceMm)
        {
            RoadNode bestNode = null;
            double bestD = double.MaxValue;
            foreach (RoadNode n in _nodes.Values)
            {
                double d = n.Position.DistanceTo(point);
                if (d <= maxDistanceMm && d < bestD) { bestD = d; bestNode = n; }
            }
            if (bestNode != null)
            {
                int segId = bestNode.SegmentIdList[0];
                RoadSegment seg = _segments[segId];
                double arc = seg.NodeA == bestNode.Id ? 0 : seg.LengthM;
                return new RoadHit(RoadHitKind.End, bestNode.Id, segId, bestNode.Position, bestD, arc);
            }

            RoadSegment bestSeg = null;
            PolylineHit bestHit = default(PolylineHit);
            bestD = double.MaxValue;
            foreach (RoadSegment seg in _segments.Values)
            {
                PolylineHit h = Polylines.Nearest(seg.Samples, point);
                if (h.DistanceMm <= maxDistanceMm && h.DistanceMm < bestD) { bestD = h.DistanceMm; bestSeg = seg; bestHit = h; }
            }
            if (bestSeg == null) return RoadHit.None;
            var p = new PointMm((long)Math.Round(bestHit.X, MidpointRounding.AwayFromZero),
                                (long)Math.Round(bestHit.Z, MidpointRounding.AwayFromZero));
            return new RoadHit(RoadHitKind.Line, -1, bestSeg.Id, p, bestD, bestHit.ArcLengthMm / PointMm.MmPerMetre);
        }

        private static PointMm[] Copy(IReadOnlyList<PointMm> list)
        {
            var a = new PointMm[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
        }

        private int NodeAtOrNew(PointMm p)
        {
            if (_nodeAt.TryGetValue(p, out int id)) return id;
            id = _nextNodeId++;
            _nodes[id] = new RoadNode(id, p);
            _nodeAt[p] = id;
            return id;
        }

        private RoadSegment Attach(int a, int b, string typeId, CurveKind curve, PointMm[] cps, PointMm[] samples)
        {
            var seg = new RoadSegment(_nextSegmentId++, a, b, typeId, curve, cps, samples);
            _segments[seg.Id] = seg;
            _nodes[a].SegmentIdList.Add(seg.Id);
            if (b != a) _nodes[b].SegmentIdList.Add(seg.Id);
            return seg;
        }

        private void RemoveSegmentCore(RoadSegment seg)
        {
            _segments.Remove(seg.Id);
            DetachFromNode(seg.NodeA, seg.Id);
            if (seg.NodeB != seg.NodeA) DetachFromNode(seg.NodeB, seg.Id);
        }

        private void DetachFromNode(int nodeId, int segId)
        {
            RoadNode n = _nodes[nodeId];
            n.SegmentIdList.Remove(segId);
            if (n.SegmentIdList.Count == 0)
            {
                _nodes.Remove(nodeId);
                _nodeAt.Remove(n.Position);
            }
        }

        // Nodes reachable from start. If 'into' is given it receives them (and is the visited set).
        private HashSet<int> Flood(int start, HashSet<int> into)
        {
            HashSet<int> seen = into ?? new HashSet<int>();
            var stack = new Stack<int>();
            seen.Add(start);
            stack.Push(start);
            while (stack.Count > 0)
            {
                RoadNode n = _nodes[stack.Pop()];
                foreach (int segId in n.SegmentIdList)
                {
                    RoadSegment s = _segments[segId];
                    int other = s.NodeA == n.Id ? s.NodeB : s.NodeA;
                    if (seen.Add(other)) stack.Push(other);
                }
            }
            return seen;
        }
    }
}
