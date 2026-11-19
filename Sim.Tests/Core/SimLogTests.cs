using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Core
{
    public class SimLogTests
    {
        private static SimWorld World() => new SimWorld(new SimTuning());

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 11, 11, new float[121]), new MapTree[0]);

        [Test]
        public void Write_StoresTickCategoryAndText()
        {
            SimWorld w = World();
            w.Step(3);
            w.Log.Write(LogCategory.Haul, "hello");
            Assert.AreEqual(1, w.Log.Count);
            Assert.AreEqual(3, w.Log[0].Tick);
            Assert.AreEqual(LogCategory.Haul, w.Log[0].Category);
            Assert.AreEqual("hello", w.Log[0].Text);
        }

        [Test]
        public void Write_PastCapacity_DropsOldestKeepsNewest()
        {
            SimWorld w = World();
            for (int i = 0; i < SimLog.Capacity + 5; i++) w.Log.Write(LogCategory.Tasks, "m" + i);
            Assert.AreEqual(SimLog.Capacity, w.Log.Count);
            Assert.AreEqual("m5", w.Log[0].Text);
            Assert.AreEqual("m" + (SimLog.Capacity + 4), w.Log[SimLog.Capacity - 1].Text);
        }

        [Test]
        public void Write_DisabledCategory_StoresNothingAndOthersStillWork()
        {
            SimWorld w = World();
            w.Log.SetEnabled(LogCategory.Food, false);
            w.Log.Write(LogCategory.Food, "x");
            w.Log.Write(LogCategory.Growth, "y");
            Assert.AreEqual(1, w.Log.Count);
            Assert.AreEqual(LogCategory.Growth, w.Log[0].Category);
        }

        [Test]
        public void Write_DisabledCategory_AllocatesNothing()
        {
            SimWorld w = World();
            w.Log.SetEnabled(LogCategory.Food, false);
            w.Log.Write(LogCategory.Food, "warm");
            SimLog log = w.Log;
            System.GC.GetAllocatedBytesForCurrentThread();
            long a = System.GC.GetAllocatedBytesForCurrentThread();
            long b = System.GC.GetAllocatedBytesForCurrentThread();
            long baseline = b - a; // cost of the measuring call itself
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) log.Write(LogCategory.Food, "x");
            long used = System.GC.GetAllocatedBytesForCurrentThread() - before - baseline;
            Assert.AreEqual(0, used);
        }

        [Test]
        public void Snapshot_FiltersByCategory()
        {
            SimWorld w = World();
            w.Log.Write(LogCategory.Food, "a");
            w.Log.Write(LogCategory.Haul, "b");
            Assert.AreEqual(1, w.Log.Snapshot(LogCategory.Haul).Count);
            Assert.AreEqual(2, w.Log.Snapshot().Count);
        }

        [Test]
        public void Load_LeavesLogEmpty()
        {
            MapData map = Map();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            w.Log.Write(LogCategory.Tasks, "x");
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(0, back.Log.Count);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void LoggingOnOrOff_GivesIdenticalWorldState()
        {
            MapData map = Map();
            byte[] Run(bool logging)
            {
                SimWorld w = SimWorld.FromMapData(map, new SimTuning());
                w.Log.SetAllEnabled(logging);
                w.Step(50);
                w.Log.Write(LogCategory.Commands, "between");
                w.Step(50);
                return SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            }
            Assert.AreEqual(Run(true), Run(false));
        }
    }
}
