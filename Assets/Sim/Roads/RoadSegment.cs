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

        internal RoadSegment(int id, int nodeA, int nodeB, string typeId, CurveKind curve,
            PointMm[] controlPoints, PointMm[] samples)
        {
            Id = id; NodeA = nodeA; NodeB = nodeB; TypeId = typeId; Curve = curve;
            ControlPoints = controlPoints; Samples = samples;
            LengthM = Polylines.LengthMetres(samples);
        }
    }
}
