using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>Why a drawn road is refused, in the order the checks run (docs/design/roads.md).</summary>
    public enum RoadRefusal
    {
        /// <summary>The road is allowed.</summary>
        Valid = 0,
        /// <summary>The road type id is not known.</summary>
        UnknownType = 1,
        /// <summary>Fewer than two usable control points (RD-02).</summary>
        NotEnoughPoints = 2,
        /// <summary>Part of the centre line is outside the map (RD-10).</summary>
        OutsideMap = 3,
        /// <summary>A 1 m step is over the type's max grade (RD-05).</summary>
        TooSteep = 4,
        /// <summary>The centre line crosses itself (RD-08).</summary>
        CrossesItself = 5,
        /// <summary>The corridor overlaps another road away from a junction (RD-09).</summary>
        OverlapsRoad = 6,
        /// <summary>The corridor overlaps a building area by more than the tolerance (RD-13); see <see cref="RoadPreview.CrossedAreaName"/>.</summary>
        CrossesArea = 7,
    }

    /// <summary>How a new road meets an existing one.</summary>
    public enum JunctionKind
    {
        /// <summary>A new road end meets the middle of an existing segment.</summary>
        T = 0,
        /// <summary>The new road's centre line crosses an existing segment.</summary>
        X = 1,
        /// <summary>A new road end meets an existing node; no split is needed.</summary>
        JoinNode = 2,
    }

    /// <summary>A stretch of samples that is too steep: sample indices <see cref="StartIndex"/>..<see cref="EndIndex"/>, inclusive.</summary>
    public readonly struct InvalidRange
    {
        /// <summary>First sample of the first bad step.</summary>
        public readonly int StartIndex;
        /// <summary>Last sample of the last bad step.</summary>
        public readonly int EndIndex;

        /// <summary>Creates a range.</summary>
        public InvalidRange(int start, int end) { StartIndex = start; EndIndex = end; }
    }

    /// <summary>A junction the new road would make with an existing segment.</summary>
    public readonly struct PlannedJunction
    {
        /// <summary>T, X or JoinNode.</summary>
        public readonly JunctionKind Kind;
        /// <summary>The existing segment that is met (the lowest id when several share the point).</summary>
        public readonly int SegmentId;
        /// <summary>The existing node at the point, or -1 when the point is mid-segment.</summary>
        public readonly int NodeId;
        /// <summary>The junction point on the existing centre line.</summary>
        public readonly PointMm Point;
        /// <summary>Arc position in metres along the existing segment, from its node A.</summary>
        public readonly double ExistingArcM;
        /// <summary>Arc position in metres along the new road, from its start.</summary>
        public readonly double NewArcM;

        /// <summary>Creates a junction.</summary>
        public PlannedJunction(JunctionKind kind, int segmentId, int nodeId, PointMm point, double existingArcM, double newArcM)
        { Kind = kind; SegmentId = segmentId; NodeId = nodeId; Point = point; ExistingArcM = existingArcM; NewArcM = newArcM; }
    }

    /// <summary>Result of <see cref="RoadValidator.Validate"/>. Immutable.</summary>
    public sealed class RoadPreview
    {
        /// <summary>The sampled centre line.</summary>
        public IReadOnlyList<PointMm> Samples { get; }
        /// <summary>Too-steep stretches, in order.</summary>
        public IReadOnlyList<InvalidRange> InvalidRanges { get; }
        /// <summary>Planned junctions, ordered by position along the new road.</summary>
        public IReadOnlyList<PlannedJunction> Junctions { get; }
        /// <summary>Centre line length in metres.</summary>
        public double LengthM { get; }
        /// <summary>Steepest 1 m step as a fraction (0.1 = 10%).</summary>
        public double MaxGrade { get; }
        /// <summary>The first refusal, or <see cref="RoadRefusal.Valid"/>.</summary>
        public RoadRefusal Reason { get; }

        /// <summary>True when the road is allowed.</summary>
        public bool IsValid => Reason == RoadRefusal.Valid;

        /// <summary>Player-facing refusal text, or an empty string when valid.</summary>
        public string ReasonText
        {
            get
            {
                switch (Reason)
                {
                    case RoadRefusal.UnknownType: return "unknown road type";
                    case RoadRefusal.NotEnoughPoints: return "needs at least two points";
                    case RoadRefusal.OutsideMap: return "outside the map";
                    case RoadRefusal.TooSteep: return "too steep";
                    case RoadRefusal.CrossesItself: return "crosses itself";
                    case RoadRefusal.OverlapsRoad: return "overlaps road";
                    case RoadRefusal.CrossesArea: return "crosses " + CrossedAreaName;
                    default: return string.Empty;
                }
            }
        }

        /// <summary>Name of the building area crossed when <see cref="Reason"/> is CrossesArea; otherwise empty.</summary>
        public string CrossedAreaName { get; }

        /// <summary>Tangent used at the first sample when the road starts on a dead-end node (RD-16), else null.</summary>
        public RoadTangent? StartTangent { get; }
        /// <summary>Tangent used at the last sample when the road ends on a dead-end node (RD-16), else null.</summary>
        public RoadTangent? EndTangent { get; }

        internal RoadPreview(IReadOnlyList<PointMm> samples, IReadOnlyList<InvalidRange> ranges,
            IReadOnlyList<PlannedJunction> junctions, double lengthM, double maxGrade, RoadRefusal reason,
            string crossedAreaName = "", RoadTangent? startTangent = null, RoadTangent? endTangent = null)
        {
            StartTangent = startTangent; EndTangent = endTangent;
            CrossedAreaName = crossedAreaName ?? string.Empty;
            Samples = samples; InvalidRanges = ranges; Junctions = junctions;
            LengthM = lengthM; MaxGrade = maxGrade; Reason = reason;
        }
    }
}
