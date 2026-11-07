using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>What a <see cref="RoadNetwork.FindNearest"/> query found.</summary>
    public enum RoadHitKind
    {
        /// <summary>Nothing within range.</summary>
        None = 0,
        /// <summary>A node (road end or junction).</summary>
        End = 1,
        /// <summary>A point on a segment's centre line.</summary>
        Line = 2,
    }

    /// <summary>Result of a nearest-road query.</summary>
    public readonly struct RoadHit
    {
        /// <summary>What was hit.</summary>
        public readonly RoadHitKind Kind;
        /// <summary>Node id for <see cref="RoadHitKind.End"/>, otherwise -1.</summary>
        public readonly int NodeId;
        /// <summary>Segment id (for End: the node's first segment), or -1 for None.</summary>
        public readonly int SegmentId;
        /// <summary>The hit point (rounded to mm); default for None.</summary>
        public readonly PointMm Point;
        /// <summary>Distance from the query point in mm; 0 for None.</summary>
        public readonly double DistanceMm;
        /// <summary>Arc length in metres along the segment from node A; 0 for None.</summary>
        public readonly double ArcPositionM;

        /// <summary>Creates a hit.</summary>
        public RoadHit(RoadHitKind kind, int nodeId, int segmentId, PointMm point, double distanceMm, double arcPositionM)
        { Kind = kind; NodeId = nodeId; SegmentId = segmentId; Point = point; DistanceMm = distanceMm; ArcPositionM = arcPositionM; }

        /// <summary>The "nothing found" hit.</summary>
        public static RoadHit None => new RoadHit(RoadHitKind.None, -1, -1, default(PointMm), 0, 0);
    }

    /// <summary>Result of <see cref="RoadNetwork.SplitSegmentAt"/>.</summary>
    public readonly struct SplitResult
    {
        /// <summary>True if the segment was cut; false if the point was at an end (no change).</summary>
        public readonly bool Split;
        /// <summary>The node at the cut (new, or the existing end/node there).</summary>
        public readonly int NodeId;
        /// <summary>First half id (the A side); -1 if not split.</summary>
        public readonly int FirstSegmentId;
        /// <summary>Second half id (the B side); -1 if not split.</summary>
        public readonly int SecondSegmentId;

        /// <summary>Creates a result.</summary>
        public SplitResult(bool split, int nodeId, int first, int second)
        { Split = split; NodeId = nodeId; FirstSegmentId = first; SecondSegmentId = second; }
    }
}
