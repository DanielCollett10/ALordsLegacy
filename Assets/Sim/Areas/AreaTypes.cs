using System.Collections.Generic;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>Whether an area blocks other building areas (Building) or may overlap anything (Work).</summary>
    public enum AreaKind
    {
        /// <summary>A building area.</summary>
        Building = 0,
        /// <summary>A work area.</summary>
        Work = 1
    }

    /// <summary>Definition of one area type (docs/design/areas.md, decision 0014). Immutable.</summary>
    public sealed class AreaTypeDef
    {
        /// <summary>Stable id, e.g. <c>area.test</c>.</summary>
        public string Id { get; }
        /// <summary>Prefix for generated names, e.g. "Test area" gives "Test area 1".</summary>
        public string NamePrefix { get; }
        /// <summary>Building or Work.</summary>
        public AreaKind Kind { get; }
        /// <summary>Fewest vertices (AR-02).</summary>
        public int MinPoints { get; }
        /// <summary>Most vertices (AR-02).</summary>
        public int MaxPoints { get; }
        /// <summary>Smallest allowed size in m² (AR-13).</summary>
        public double MinAreaM2 { get; }
        /// <summary>Largest allowed size in m² (AR-13).</summary>
        public double MaxAreaM2 { get; }
        /// <summary>Steepest allowed slope in degrees, or null for no limit (AR-14).</summary>
        public double? MaxSlopeDegrees { get; }
        /// <summary>True if confirming the area removes trees inside it.</summary>
        public bool ClearsTrees { get; }
        /// <summary>True if roads may not cross the area (AR-16).</summary>
        public bool BlocksRoads { get; }

        /// <summary>How far (m) a NearRoad edge's vertices may be from the road side (AR-24).</summary>
        public double NearRoadReachM { get; }

        /// <summary>Creates an area type definition.</summary>
        public AreaTypeDef(string id, string namePrefix, AreaKind kind, int minPoints, int maxPoints,
            double minAreaM2, double maxAreaM2, double? maxSlopeDegrees, bool clearsTrees, bool blocksRoads,
            double nearRoadReachM = 10)
        {
            NearRoadReachM = nearRoadReachM;
            Id = id; NamePrefix = namePrefix; Kind = kind; MinPoints = minPoints; MaxPoints = maxPoints;
            MinAreaM2 = minAreaM2; MaxAreaM2 = maxAreaM2; MaxSlopeDegrees = maxSlopeDegrees;
            ClearsTrees = clearsTrees; BlocksRoads = blocksRoads;
        }
    }

    /// <summary>The area type definitions, built in code until the definition pipeline exists (decision 0014).</summary>
    public static class AreaTypes
    {
        /// <summary>Id of the test building area.</summary>
        public const string TestId = "area.test";
        /// <summary>Id of the test work area.</summary>
        public const string TestWorkId = "area.test_work";

        /// <summary>Test area: building, 4–8 points, 100–5,000 m², 10°, clears trees, blocks roads.</summary>
        public static readonly AreaTypeDef Test =
            new AreaTypeDef(TestId, "Test area", AreaKind.Building, 4, 8, 100, 5000, 10, true, true);

        /// <summary>Test work area: 4–8 points, 100–40,000 m², no slope limit, no clearing, no blocking.</summary>
        public static readonly AreaTypeDef TestWork =
            new AreaTypeDef(TestWorkId, "Test work area", AreaKind.Work, 4, 8, 100, 40000, null, false, false);

        /// <summary>All area types.</summary>
        public static readonly IReadOnlyList<AreaTypeDef> All = new[] { Test, TestWork };

        /// <summary>Finds a type by id, or null if unknown.</summary>
        public static AreaTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
