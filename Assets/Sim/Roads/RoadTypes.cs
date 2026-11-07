using System.Collections.Generic;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>The road type definitions, built in code until the definition pipeline exists (decision 0013).</summary>
    public static class RoadTypes
    {
        /// <summary>Id of the dirt path.</summary>
        public const string DirtPathId = "road.dirt_path";

        /// <summary>The dirt path: 2.5 m wide, 15% max grade, 1.5x speed (tuning.md "Roads and areas").</summary>
        public static readonly RoadTypeDef DirtPath =
            new RoadTypeDef(DirtPathId, 2.5, 0.15, 1.5, "visual.road.dirt_path");

        /// <summary>All road types.</summary>
        public static readonly IReadOnlyList<RoadTypeDef> All = new[] { DirtPath };

        /// <summary>Finds a type by id, or null if unknown.</summary>
        public static RoadTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
