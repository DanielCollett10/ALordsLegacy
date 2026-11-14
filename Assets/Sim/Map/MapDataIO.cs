using System;
using System.IO;

namespace LordsLegacy.Sim.Map
{
    public static class MapDataIO
    {
        public static void Read(Stream stream, MapData mapData)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                mapData.ContentHash = reader.ReadInt64();
            }
        }
    }
}