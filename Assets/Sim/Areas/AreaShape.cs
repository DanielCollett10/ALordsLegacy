using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>How an area edge runs between its two vertices.</summary>
    public enum AreaEdgeKind
    {
        /// <summary>A straight line.</summary>
        Straight = 0,
        /// <summary>A quadratic curve passing through <see cref="AreaEdge.Handle"/>.</summary>
        Bent = 1,
        /// <summary>Copies one side line of a road segment.</summary>
        FollowsRoad = 2
    }

    /// <summary>One edge of an area shape. Edge i runs from vertex i to vertex i+1. Immutable.</summary>
    public readonly struct AreaEdge
    {
        /// <summary>Edge kind.</summary>
        public AreaEdgeKind Kind { get; }
        /// <summary>For Bent: a point on the curve at its middle.</summary>
        public PointMm Handle { get; }
        /// <summary>For FollowsRoad: the road segment id.</summary>
        public int SegmentId { get; }
        /// <summary>For FollowsRoad: which side of the road.</summary>
        public RoadSide Side { get; }

        private AreaEdge(AreaEdgeKind kind, PointMm handle, int segmentId, RoadSide side)
        {
            Kind = kind; Handle = handle; SegmentId = segmentId; Side = side;
        }

        /// <summary>A straight edge.</summary>
        public static AreaEdge Straight() => new AreaEdge(AreaEdgeKind.Straight, default, 0, RoadSide.Left);
        /// <summary>A bent edge whose curve passes through <paramref name="handle"/> at its middle.</summary>
        public static AreaEdge Bent(PointMm handle) => new AreaEdge(AreaEdgeKind.Bent, handle, 0, RoadSide.Left);
        /// <summary>An edge that follows a road side line.</summary>
        public static AreaEdge FollowsRoad(int segmentId, RoadSide side) =>
            new AreaEdge(AreaEdgeKind.FollowsRoad, default, segmentId, side);
    }

    /// <summary>The player's area: ordered vertices plus one edge per vertex (decision 0014). Immutable.</summary>
    public sealed class AreaShape
    {
        /// <summary>Ordered vertices.</summary>
        public IReadOnlyList<PointMm> Vertices { get; }
        /// <summary>One edge per vertex; edge i joins vertex i to vertex (i+1) mod count.</summary>
        public IReadOnlyList<AreaEdge> Edges { get; }

        /// <summary>Creates a shape. Both lists are copied and must have equal length.</summary>
        public AreaShape(IReadOnlyList<PointMm> vertices, IReadOnlyList<AreaEdge> edges)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            if (vertices.Count != edges.Count) throw new ArgumentException("Need one edge per vertex.", nameof(edges));
            var v = new PointMm[vertices.Count];
            var e = new AreaEdge[edges.Count];
            for (int i = 0; i < v.Length; i++) { v[i] = vertices[i]; e[i] = edges[i]; }
            Vertices = v; Edges = e;
        }

        /// <summary>A shape whose edges are all straight.</summary>
        public static AreaShape Straight(IReadOnlyList<PointMm> vertices)
        {
            var e = new AreaEdge[vertices.Count];
            for (int i = 0; i < e.Length; i++) e[i] = AreaEdge.Straight();
            return new AreaShape(vertices, e);
        }
    }
}
