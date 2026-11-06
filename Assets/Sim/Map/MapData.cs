using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Map
{
    /// <summary>One baked tree in a map file: stable id, ground position and usable trunk length.</summary>
    public readonly struct MapTree
    {
        /// <summary>Stable id from the authoring data.</summary>
        public readonly TreeId Id;
        /// <summary>World x in metres.</summary>
        public readonly float X;
        /// <summary>World z in metres.</summary>
        public readonly float Z;
        /// <summary>Usable trunk length in metres.</summary>
        public readonly double TrunkLengthM;

        /// <summary>Creates a map tree.</summary>
        public MapTree(TreeId id, float x, float z, double trunkLengthM)
        {
            Id = id; X = x; Z = z; TrunkLengthM = trunkLengthM;
        }
    }

    /// <summary>Baked map: format version, map id, height grid and trees (decision 0003).</summary>
    public sealed class MapData
    {
        /// <summary>The format version this code writes and reads.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Format version of this data.</summary>
        public int FormatVersion { get; }
        /// <summary>Identifier of the map.</summary>
        public string MapId { get; }
        /// <summary>The ground heights.</summary>
        public HeightGrid Heights { get; }
        /// <summary>The trees, in file order.</summary>
        public IReadOnlyList<MapTree> Trees { get; }

        /// <summary>Creates map data at <see cref="CurrentVersion"/>.</summary>
        public MapData(string mapId, HeightGrid heights, IEnumerable<MapTree> trees)
            : this(CurrentVersion, mapId, heights, trees)
        {
        }

        /// <summary>Creates map data with an explicit version.</summary>
        public MapData(int formatVersion, string mapId, HeightGrid heights, IEnumerable<MapTree> trees)
        {
            FormatVersion = formatVersion;
            MapId = mapId ?? throw new ArgumentNullException(nameof(mapId));
            Heights = heights ?? throw new ArgumentNullException(nameof(heights));
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            Trees = new List<MapTree>(trees).AsReadOnly();
        }
    }
}
