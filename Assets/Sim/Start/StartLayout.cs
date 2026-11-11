using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Start
{
    /// <summary>One citizen in a start layout (PPL-10).</summary>
    public sealed class StartCitizen
    {
        /// <summary>Creates the data.</summary>
        public StartCitizen(string name, Sex sex, long birthTick)
        {
            Name = name; Sex = sex; BirthTick = birthTick;
        }

        /// <summary>Given name.</summary>
        public string Name { get; }
        /// <summary>Sex.</summary>
        public Sex Sex { get; }
        /// <summary>Birth tick; negative means before the game started.</summary>
        public long BirthTick { get; }
    }

    /// <summary>One family in a start layout: its tent spot (relative to the camp) and members.</summary>
    public sealed class StartFamily
    {
        /// <summary>Creates the data. Members are copied.</summary>
        public StartFamily(string surname, PointMm tentSpot, IReadOnlyList<StartCitizen> citizens)
        {
            Surname = surname ?? string.Empty;
            TentSpot = tentSpot;
            Citizens = new List<StartCitizen>(citizens ?? new StartCitizen[0]).AsReadOnly();
        }

        /// <summary>Family name.</summary>
        public string Surname { get; }
        /// <summary>Tent spot relative to the camp centre, in the camp's own (unrotated) frame, mm.</summary>
        public PointMm TentSpot { get; }
        /// <summary>The members.</summary>
        public IReadOnlyList<StartCitizen> Citizens { get; }
    }

    /// <summary>
    /// Pure data for a new world (CMP-01): camp position and rotation, fixed points relative to the camp,
    /// the trunk road control points, families and the number of oxen. Relative points are in the camp's
    /// local frame (X along its width, Z along its depth) and are rotated with the camp.
    /// Stockpile contents arrive with M9.
    /// </summary>
    public sealed class StartLayout
    {
        /// <summary>Creates a layout. Lists are copied.</summary>
        public StartLayout(PointMm campCentre, double campRotationDegrees, PointMm fire, PointMm stockpile,
            PointMm oxStand, IReadOnlyList<PointMm> trunkRoad, IReadOnlyList<StartFamily> families, int oxCount,
            string trunkRoadTypeId = Roads.RoadTypes.DirtPathId)
        {
            CampCentre = campCentre;
            CampRotationDegrees = campRotationDegrees;
            Fire = fire;
            Stockpile = stockpile;
            OxStand = oxStand;
            TrunkRoad = new List<PointMm>(trunkRoad ?? new PointMm[0]).AsReadOnly();
            Families = new List<StartFamily>(families ?? new StartFamily[0]).AsReadOnly();
            OxCount = oxCount;
            TrunkRoadTypeId = trunkRoadTypeId;
        }

        /// <summary>Camp centre in world mm.</summary>
        public PointMm CampCentre { get; }
        /// <summary>Camp rotation, degrees counter-clockwise.</summary>
        public double CampRotationDegrees { get; }
        /// <summary>Camp fire, relative to the camp centre.</summary>
        public PointMm Fire { get; }
        /// <summary>Stockpile spot, relative to the camp centre.</summary>
        public PointMm Stockpile { get; }
        /// <summary>Ox stand point, relative to the camp centre.</summary>
        public PointMm OxStand { get; }
        /// <summary>Trunk road control points in world mm.</summary>
        public IReadOnlyList<PointMm> TrunkRoad { get; }
        /// <summary>Road type id for the trunk road.</summary>
        public string TrunkRoadTypeId { get; }
        /// <summary>Families with their citizens.</summary>
        public IReadOnlyList<StartFamily> Families { get; }
        /// <summary>Number of oxen.</summary>
        public int OxCount { get; }
    }
}
