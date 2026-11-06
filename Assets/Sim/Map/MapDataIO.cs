using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Map
{
    /// <summary>
    /// Binary reader/writer for <see cref="MapData"/>. Little-endian layout: magic "LLMP", int32 version,
    /// string mapId, float32 originX, originZ, cellSize, int32 width, depth, width*depth float32 heights
    /// (row-major), int32 tree count, then per tree int32 id, float32 x, float32 z, float64 trunk length.
    /// </summary>
    public static class MapDataIO
    {
        private static readonly byte[] Magic = { (byte)'L', (byte)'L', (byte)'M', (byte)'P' };

        /// <summary>Writes the map to a stream (left open).</summary>
        public static void Write(Stream stream, MapData map)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (map == null) throw new ArgumentNullException(nameof(map));
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                w.Write(Magic);
                w.Write(map.FormatVersion);
                w.Write(map.MapId);
                var g = map.Heights;
                w.Write(g.OriginX); w.Write(g.OriginZ); w.Write(g.CellSize);
                w.Write(g.Width); w.Write(g.Depth);
                for (int iz = 0; iz < g.Depth; iz++)
                    for (int ix = 0; ix < g.Width; ix++)
                        w.Write(g.NodeHeight(ix, iz));
                w.Write(map.Trees.Count);
                foreach (var t in map.Trees)
                {
                    w.Write(t.Id.Value); w.Write(t.X); w.Write(t.Z); w.Write(t.TrunkLengthM);
                }
            }
        }

        /// <summary>Writes the map to a new byte array.</summary>
        public static byte[] ToBytes(MapData map)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, map);
                return ms.ToArray();
            }
        }

        /// <summary>Reads a map from a byte array. Throws <see cref="MapDataFormatException"/> on bad data.</summary>
        public static MapData Read(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var ms = new MemoryStream(bytes, false)) return Read(ms);
        }

        /// <summary>Reads a map from a stream (left open). Throws <see cref="MapDataFormatException"/> on bad data.</summary>
        public static MapData Read(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            try
            {
                using (var r = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    var magic = r.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                        throw new MapDataFormatException("Not a map data file (bad magic).");
                    int version = r.ReadInt32();
                    if (version != MapData.CurrentVersion)
                        throw new MapDataFormatException("Unsupported map format version " + version
                            + " (this build reads version " + MapData.CurrentVersion + ").");
                    string id = r.ReadString();
                    float ox = r.ReadSingle(), oz = r.ReadSingle(), cell = r.ReadSingle();
                    int width = r.ReadInt32(), depth = r.ReadInt32();
                    if (!(cell > 0f) || width < 2 || depth < 2 || (long)width * depth > int.MaxValue / 4)
                        throw new MapDataFormatException("Invalid height grid dimensions.");
                    long remaining = stream.CanSeek ? stream.Length - stream.Position : long.MaxValue;
                    if ((long)width * depth * 4 > remaining)
                        throw new MapDataFormatException("Map data is truncated.");
                    var heights = new float[width * depth];
                    for (int i = 0; i < heights.Length; i++) heights[i] = r.ReadSingle();
                    int count = r.ReadInt32();
                    if (count < 0) throw new MapDataFormatException("Invalid tree count.");
                    var trees = new List<MapTree>();
                    var seen = new HashSet<int>();
                    for (int i = 0; i < count; i++)
                    {
                        int tid = r.ReadInt32();
                        if (!seen.Add(tid)) throw new MapDataFormatException("Duplicate tree id " + tid + ".");
                        trees.Add(new MapTree(new TreeId(tid), r.ReadSingle(), r.ReadSingle(), r.ReadDouble()));
                    }
                    return new MapData(version, id, new HeightGrid(ox, oz, cell, width, depth, heights), trees);
                }
            }
            catch (EndOfStreamException)
            {
                throw new MapDataFormatException("Map data is truncated.");
            }
        }
    }
}
