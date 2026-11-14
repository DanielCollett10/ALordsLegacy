using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Save.Sections;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class SimSaveTests
    {
        private sealed class Probe : ISaveSection
        {
            private readonly int _id;
            private readonly List<string> _log;
            public Probe(int id, List<string> log) { _id = id; _log = log; }
            public int Id => _id;
            public void Write(SimWorld world, SaveWriter w) { _log.Add("W" + _id); w.WriteInt32(_id * 10); }
            public void Read(SimWorld world, SaveReader r) { _log.Add("R" + _id); Assert.AreEqual(_id * 10, r.ReadInt32()); }
        }

        private static byte[] Save(SimWorld w, MapData m) => SimSave.Write(w, "s", 5, m.MapId, m.ContentHash);

        [Test]
        public void FreshWorld_RoundTrips_ToIdenticalBytes()
        {
            var map = SyntheticMap.CreateRamp();
            var world = SimWorld.FromMapData(map, new SimTuning());
            var bytes = Save(world, map);
            var back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, Save(back, map));
        }

        [Test]
        public void SteppedPausedWorld_RestoresTickPausedRandomAndTrees()
        {
            var map = SyntheticMap.CreateRamp();
            var world = SimWorld.FromMapData(map, new SimTuning { WorldSeed = 9 });
            world.Step(30);
            world.Random.NextUInt();
            world.Pause();
            var back = SimSave.Read(Save(world, map), map);
            Assert.AreEqual(30, back.Tick);
            Assert.IsTrue(back.Paused);
            Assert.AreEqual(world.Random.State, back.Random.State);
            Assert.AreEqual(9, back.Tuning.WorldSeed);
            Assert.AreEqual(map.Trees.Count, back.Trees.Count);
        }

        [Test]
        public void ForRestore_NeverAppliesStartLayout_AndNeverAdvancesTick()
        {
            var map = SyntheticMap.CreateRamp();
            var core = new CoreState
            {
                Tick = 777, Paused = false, RandomState = 42, Tuning = new SimTuning(),
                RoadTypes = LordsLegacy.Sim.Roads.RoadTypes.DefaultCatalog,
                AreaTypes = LordsLegacy.Sim.Areas.AreaTypes.DefaultCatalog
            };
            var world = SimWorld.ForRestore(map, core);
            Assert.AreEqual(777, world.Tick);
            Assert.IsNull(world.Camp, "no start layout applied");
            Assert.AreEqual(42UL, world.Random.State, "no system drew random numbers");
            var back = SimSave.Read(Save(world, map), map);
            Assert.AreEqual(777, back.Tick);
            Assert.IsNull(back.Camp);
        }

        [Test]
        public void Sections_WriteAndRead_InListOrder()
        {
            var map = SyntheticMap.CreateRamp();
            var world = SimWorld.FromMapData(map, new SimTuning());
            var log = new List<string>();
            var sections = new ISaveSection[] { new Probe(30, log), new Probe(10, log), new Probe(20, log) };
            var bytes = SimSave.Write(world, "s", 0, map.MapId, map.ContentHash, sections);
            SimSave.Read(bytes, map, sections);
            Assert.AreEqual(new[] { "W30", "W10", "W20", "R30", "R10", "R20" }, log);
        }

        [Test]
        public void Read_SectionOutOfOrder_Throws()
        {
            var map = SyntheticMap.CreateRamp();
            var world = SimWorld.FromMapData(map, new SimTuning());
            var log = new List<string>();
            var bytes = SimSave.Write(world, "s", 0, map.MapId, map.ContentHash, new ISaveSection[] { new Probe(1, log), new Probe(2, log) });
            Assert.Throws<SaveFormatException>(() =>
                SimSave.Read(bytes, map, new ISaveSection[] { new Probe(2, log), new Probe(1, log) }));
        }

        [Test]
        public void Read_WrongMap_Throws()
        {
            var map = SyntheticMap.CreateRamp("a");
            var world = SimWorld.FromMapData(map, new SimTuning());
            var bytes = Save(world, map);
            Assert.Throws<SaveMapMismatchException>(() => SimSave.Read(bytes, SyntheticMap.CreateRamp("b")));
        }

        [Test]
        public void Read_BuildsNavGrid()
        {
            var map = SyntheticMap.CreateRamp();
            var world = SimWorld.FromMapData(map, new SimTuning());
            var back = SimSave.Read(Save(world, map), map);
            Assert.IsNotNull(back.Nav);
        }
    }
}
