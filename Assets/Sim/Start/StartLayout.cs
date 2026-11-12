using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
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

    /// <summary>One kind and count of items placed in the camp stockpile at the start (CON-08, INV-10).</summary>
    public sealed class StartStock
    {
        /// <summary>Creates the stock entry.</summary>
        public StartStock(ItemDef item, int count)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Must not be negative.");
            Count = count;
        }

        /// <summary>The item.</summary>
        public ItemDef Item { get; }
        /// <summary>How many; zero or more.</summary>
        public int Count { get; }
    }

    /// <summary>
    /// Pure data for a new world (CMP-01): camp position and rotation, fixed points relative to the camp,
    /// the trunk road control points, families, the number of oxen and the starting stockpile stock.
    /// Relative points are in the camp's local frame (X along its width, Z along its depth) and are rotated with the camp.
    /// </summary>
    public sealed class StartLayout
    {
        /// <summary>The starting stockpile stock: 20 logs (CON-08).</summary>
        public static readonly IReadOnlyList<StartStock> DefaultStock =
            new[] { new StartStock(Items.Log, 20) };

        /// <summary>Creates a layout. Lists are copied. A null <paramref name="stock"/> gives <see cref="DefaultStock"/>.</summary>
        public StartLayout(PointMm campCentre, double campRotationDegrees, PointMm fire, PointMm stockpile,
            PointMm oxStand, IReadOnlyList<PointMm> trunkRoad, IReadOnlyList<StartFamily> families, int oxCount,
            string trunkRoadTypeId = Roads.RoadTypes.DirtPathId, IReadOnlyList<StartStock> stock = null)
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
            Stock = new List<StartStock>(stock ?? DefaultStock).AsReadOnly();
        }

        /// <summary>Items placed in the camp stockpile when the world starts.</summary>
        public IReadOnlyList<StartStock> Stock { get; }

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
