using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Whether a snap landed on a vertex or along an edge.</summary>
    public enum AreaSnapKind
    {
        /// <summary>Snapped to a shape vertex.</summary>
        Vertex = 0,
        /// <summary>Snapped to a point along a shape edge.</summary>
        Edge = 1
    }

    /// <summary>Result of <see cref="AreaRegistry.FindNearestSnap"/>. Immutable.</summary>
    public readonly struct AreaSnapHit
    {
        /// <summary>Vertex or Edge.</summary>
        public AreaSnapKind Kind { get; }
        /// <summary>Id of the area snapped to.</summary>
        public int AreaId { get; }
        /// <summary>Shape vertex index (Vertex), or the index of the edge (Edge); edge i runs from vertex i to i+1.</summary>
        public int Index { get; }
        /// <summary>The snapped position.</summary>
        public PointMm Point { get; }
        /// <summary>Distance from the query point to <see cref="Point"/>, in millimetres.</summary>
        public double DistanceMm { get; }

        /// <summary>Creates a snap hit.</summary>
        public AreaSnapHit(AreaSnapKind kind, int areaId, int index, PointMm point, double distanceMm)
        {
            Kind = kind; AreaId = areaId; Index = index; Point = point; DistanceMm = distanceMm;
        }
    }
}
