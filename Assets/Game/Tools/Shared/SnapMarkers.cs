using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Game.Tools.Shared
{
    /// <summary>What a snap marker sits on (AR-22). The order is also the snap priority.</summary>
    public enum SnapMarkerKind
    {
        /// <summary>Another area's vertex (any kind of area).</summary>
        Corner = 0,
        /// <summary>A dot along a road side line, measured from the segment's node A end.</summary>
        RoadDot = 1,
        /// <summary>A dot along another area's flattened edge, measured from the edge's start vertex.</summary>
        AreaDot = 2
    }

    /// <summary>One snap marker. <see cref="SegmentId"/> and <see cref="Side"/> are set for road dots only.</summary>
    public readonly struct SnapMarker
    {
        /// <summary>The marker kind.</summary>
        public readonly SnapMarkerKind Kind;
        /// <summary>Where it is.</summary>
        public readonly PointMm Point;
        /// <summary>Road segment id for a road dot, else -1.</summary>
        public readonly int SegmentId;
        /// <summary>Road side for a road dot.</summary>
        public readonly RoadSide Side;

        /// <summary>Id of the area a corner or area dot belongs to (AR-25), else -1.</summary>
        public readonly int AreaId;

        /// <summary>Creates a marker.</summary>
        public SnapMarker(SnapMarkerKind kind, PointMm point, int segmentId, RoadSide side, int areaId = -1)
        {
            Kind = kind; Point = point; SegmentId = segmentId; Side = side; AreaId = areaId;
        }
    }

    /// <summary>
    /// Works out the snap markers near a point (areas.md AR-22). Positions depend only on the roads and areas, never on
    /// the cursor; the cursor only decides which ones are in range.
    /// </summary>
    public static class SnapMarkerBuilder
    {
        /// <summary>Corners, road-side dots and area-edge dots within <paramref name="radiusM"/> of <paramref name="centre"/>.</summary>
        public static List<SnapMarker> Build(SimWorld world, PointMm centre, double spacingM, double radiusM)
        {
            var result = new List<SnapMarker>();
            double radiusMm = radiusM * PointMm.MmPerMetre;
            var vertexSet = new HashSet<PointMm>();
            foreach (var area in world.Areas.All)
            {
                var vs = area.Shape.Vertices;
                for (int i = 0; i < vs.Count; i++)
                {
                    vertexSet.Add(vs[i]);
                    if (vs[i].DistanceTo(centre) <= radiusMm)
                        result.Add(new SnapMarker(SnapMarkerKind.Corner, vs[i], -1, RoadSide.Left, area.Id));
                }
            }
            foreach (var segment in world.Roads.Segments)
            {
                for (int k = 0; k < 2; k++)
                {
                    RoadSide side = k == 0 ? RoadSide.Left : RoadSide.Right;
                    var line = segment.SideLine(side);
                    if (line.Length < 2) continue;
                    foreach (var p in Polylines.PointsEveryWithin(line, spacingM, centre, radiusM))
                        result.Add(new SnapMarker(SnapMarkerKind.RoadDot, p, segment.Id, side));
                }
            }
            foreach (var area in world.Areas.All)
            {
                var outline = area.ShapeOrderOutline(out int[] starts);
                int m = outline.Length;
                for (int e = 0; e < starts.Length; e++)
                {
                    int from = starts[e];
                    int to = e + 1 < starts.Length ? starts[e + 1] : m;
                    var run = new List<PointMm>();
                    for (int j = from; j <= to; j++) run.Add(outline[j % m]);
                    if (run.Count < 2) continue;
                    foreach (var p in Polylines.PointsEveryWithin(run, spacingM, centre, radiusM))
                        if (!vertexSet.Contains(p)) result.Add(new SnapMarker(SnapMarkerKind.AreaDot, p, -1, RoadSide.Left, area.Id));
                }
            }
            return result;
        }
    }
}
