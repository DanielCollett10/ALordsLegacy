using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>A road end or junction (docs/design/roads.md, Network).</summary>
    public sealed class RoadNode
    {
        internal readonly List<int> SegmentIdList = new List<int>();

        /// <summary>Node id, unique in its network and never reused.</summary>
        public int Id { get; }
        /// <summary>Position in mm.</summary>
        public PointMm Position { get; }
        /// <summary>Ids of the segments that end at this node, in the order they were attached.</summary>
        public IReadOnlyList<int> SegmentIds => SegmentIdList;

        internal RoadNode(int id, PointMm position) { Id = id; Position = position; }
    }
}
