using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Why a drawn area is refused, in the order the shape checks run (docs/design/areas.md).</summary>
    public enum AreaRefusal
    {
        /// <summary>The area passed every check that has run.</summary>
        Valid = 0,
        /// <summary>The area type id is not known.</summary>
        UnknownType = 1,
        /// <summary>Fewer points than the type's minimum (AR-10).</summary>
        TooFewPoints = 2,
        /// <summary>More points than the type's maximum (AR-10).</summary>
        TooManyPoints = 3,
        /// <summary>A FollowsRoad edge's vertices are not on that road side.</summary>
        NotOnRoad = 4,
        /// <summary>Self-intersection or zero-length edge (AR-11).</summary>
        EdgesCross = 5,
        /// <summary>A point is outside the map (AR-12).</summary>
        OutsideMap = 6,
        /// <summary>Smaller than the type's minimum m² (AR-13).</summary>
        TooSmall = 7,
        /// <summary>Larger than the type's maximum m² (AR-13).</summary>
        TooLarge = 8,
        /// <summary>A slope sample inside the area is steeper than the type's limit (AR-14).</summary>
        TooSteep = 9,
        /// <summary>Overlaps another building area (AR-15); see <see cref="AreaPreview.OverlapName"/>.</summary>
        OverlapsArea = 10,
        /// <summary>Overlaps a road corridor (AR-16).</summary>
        CrossesRoad = 11,
    }

    /// <summary>Result of <see cref="AreaValidator.Validate"/>. Immutable.</summary>
    public sealed class AreaPreview
    {
        /// <summary>The flattened outline (counter-clockwise); empty if the shape has no points.</summary>
        public IReadOnlyList<PointMm> Polygon { get; }
        /// <summary>Area of the flattened outline in m².</summary>
        public double AreaM2 { get; }
        /// <summary>Number of player points (shape vertices).</summary>
        public int PointCount { get; }
        /// <summary>The first failing reason, or <see cref="AreaRefusal.Valid"/>.</summary>
        public AreaRefusal Reason { get; }

        /// <summary>True when the area passed every check that has run.</summary>
        public bool IsValid => Reason == AreaRefusal.Valid;

        /// <summary>Player-facing refusal text, or an empty string when valid.</summary>
        public string ReasonText
        {
            get
            {
                switch (Reason)
                {
                    case AreaRefusal.UnknownType: return "unknown area type";
                    case AreaRefusal.TooFewPoints: return "too few points";
                    case AreaRefusal.TooManyPoints: return "too many points";
                    case AreaRefusal.NotOnRoad: return "not on road";
                    case AreaRefusal.EdgesCross: return "edges cross";
                    case AreaRefusal.OutsideMap: return "outside the map";
                    case AreaRefusal.TooSmall: return "too small";
                    case AreaRefusal.TooLarge: return "too large";
                    case AreaRefusal.TooSteep: return "too steep";
                    case AreaRefusal.OverlapsArea: return "overlaps " + OverlapName;
                    case AreaRefusal.CrossesRoad: return "crosses road";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>Name of the area overlapped when <see cref="Reason"/> is OverlapsArea; otherwise empty.</summary>
        public string OverlapName { get; }

        internal AreaPreview(IReadOnlyList<PointMm> polygon, double areaM2, int pointCount, AreaRefusal reason,
            string overlapName = "")
        {
            Polygon = polygon; AreaM2 = areaM2; PointCount = pointCount; Reason = reason;
            OverlapName = overlapName ?? string.Empty;
        }
    }
}
