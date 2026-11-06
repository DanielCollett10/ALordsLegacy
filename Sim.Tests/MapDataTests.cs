using System;
using System.IO;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class MapDataTests
    {
        [Test]
        public void RoundTripKeepsHeightsAndTrees()
        {
            var src = SyntheticMap.CreateRamp("m1");
            var back = MapDataIO.Read(MapDataIO.ToBytes(src));
            Assert.That(back.MapId, Is.EqualTo("m1"));
            Assert.That(back.FormatVersion, Is.EqualTo(MapData.CurrentVersion));
            Assert.That(back.Heights.Width, Is.EqualTo(src.Heights.Width));
            Assert.That(back.Heights.CellSize, Is.EqualTo(src.Heights.CellSize));
            for (int z = 0; z < src.Heights.Depth; z++)
                for (int x = 0; x < src.Heights.Width; x++)
                    Assert.That(back.Heights.NodeHeight(x, z), Is.EqualTo(src.Heights.NodeHeight(x, z)));
            Assert.That(back.Trees.Count, Is.EqualTo(src.Trees.Count));
            for (int i = 0; i < src.Trees.Count; i++)
            {
                Assert.That(back.Trees[i].Id, Is.EqualTo(src.Trees[i].Id));
                Assert.That(back.Trees[i].X, Is.EqualTo(src.Trees[i].X));
                Assert.That(back.Trees[i].Z, Is.EqualTo(src.Trees[i].Z));
                Assert.That(back.Trees[i].TrunkLengthM, Is.EqualTo(src.Trees[i].TrunkLengthM));
            }
        }

        [Test]
        public void ReadsFromStream()
        {
            var bytes = MapDataIO.ToBytes(SyntheticMap.CreateRamp());
            using (var ms = new MemoryStream(bytes))
                Assert.That(MapDataIO.Read(ms).Trees.Count, Is.EqualTo(5));
        }

        [Test]
        public void UnknownVersionFailsClearly()
        {
            var src = SyntheticMap.CreateRamp();
            var bad = new MapData(99, src.MapId, src.Heights, src.Trees);
            var ex = Assert.Throws<MapDataFormatException>(() => MapDataIO.Read(MapDataIO.ToBytes(bad)));
            StringAssert.Contains("version 99", ex.Message);
        }

        [Test]
        public void BadMagicAndTruncationFail()
        {
            Assert.Throws<MapDataFormatException>(() => MapDataIO.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            var bytes = MapDataIO.ToBytes(SyntheticMap.CreateRamp());
            Assert.Throws<MapDataFormatException>(() => MapDataIO.Read(bytes.AsSpan(0, bytes.Length - 5).ToArray()));
            Assert.Throws<MapDataFormatException>(() => MapDataIO.Read(new byte[0]));
        }

        [Test]
        public void BareWorldHasFlatTerrainAndNoTrees()
        {
            var w = new SimWorld(new SimTuning());
            Assert.That(w.Terrain, Is.InstanceOf<FlatHeightField>());
            Assert.That(w.Trees, Is.Not.Null);
            Assert.That(w.Trees.Count, Is.EqualTo(0));
        }

        [Test]
        public void FromMapDataMatchesMap()
        {
            var map = SyntheticMap.CreateRamp();
            var w = SimWorld.FromMapData(map, new SimTuning());
            Assert.That(w.Terrain.HeightAt(0f, 50f), Is.EqualTo(0f).Within(1e-4));
            Assert.That(w.Terrain.HeightAt(100f, 50f), Is.EqualTo(10f).Within(1e-4));
            Assert.That(w.Terrain.HeightAt(200f, 0f), Is.EqualTo(20f).Within(1e-4));
            Assert.That(w.Trees.Count, Is.EqualTo(5));
            Assert.That(w.Trees.Get(new TreeId(3)).X, Is.EqualTo(100f));
            for (int id = 1; id <= 5; id++) Assert.That(w.Trees.TryGet(new TreeId(id), out _), Is.True);
            Assert.Throws<ArgumentNullException>(() => SimWorld.FromMapData(null, new SimTuning()));
        }
    }
}
