using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Commands
{
    /// <summary>DesignateOperation: area plus ordered modules, all or nothing (T-386, BT-15, BT-16).</summary>
    public class DesignateOperationTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() => new MapData("flat",
            new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]),
            new[] { new MapTree(new TreeId(1), 110f, 110f, 16), new MapTree(new TreeId(2), 190f, 190f, 16) });

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private static SimWorld World(MapData map, int logs)
        {
            var w = SimWorld.FromMapData(map, new SimTuning());
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            return w;
        }

        private static AreaShape Square() =>
            AreaShape.Straight(new[] { M(100, 100), M(140, 100), M(140, 120), M(100, 120) });

        private static List<ModuleSpec> Good() => new List<ModuleSpec>
        {
            new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(110, 110), 0),
            new ModuleSpec(ModuleTypes.LogStorageId, M(125, 110), 0),
        };

        private static DesignateOperation Cmd(List<ModuleSpec> m) =>
            new DesignateOperation(Square(), OperationTypes.WoodcuttingId, m);

        private static void AssertRefusedUnchanged(SimWorld w, MapData map, DesignateOperation cmd, string text)
        {
            byte[] before = Bytes(w, map);
            CommandResult r = w.Execute(cmd);
            Assert.IsTrue(r.IsRejected, "refused");
            StringAssert.Contains(text, r.Text);
            byte[] after = Bytes(w, map);
            Assert.IsNull(SaveDiff.FirstDifference(before, after));
            Assert.AreEqual(before, after);
        }

        [Test]
        public void Apply_CreatesAreaOperationFootprintsSiteAndReservationInOneCommand()
        {
            MapData map = Map();
            SimWorld w = World(map, 40);
            CommandResult r = w.Execute(Cmd(Good()));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(3, r.Created.Length);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Assert.IsNotNull(op);
            CollectionAssert.AreEqual(new[] { (int)r.Created[1], (int)r.Created[2] }, op.ModuleIds);
            Assert.IsNotNull(w.Footprints.Get((int)r.Created[1]));
            Assert.IsNotNull(w.Construction.Get((int)r.Created[1]));
            Assert.IsNull(w.Construction.Get((int)r.Created[2]), "storage is built at once");
            Assert.AreEqual(8, w.Inventory.Reservations.Sum(x => x.Count));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Apply_LogsTheCommandExactlyOnce()
        {
            SimWorld w = World(Map(), 40);
            int before = w.CommandLog.Count;
            w.Execute(Cmd(Good()));
            Assert.AreEqual(before + 1, w.CommandLog.Count);
        }

        [Test]
        public void Apply_ClearsTreesUnderAreaAndModules()
        {
            MapData map = Map();
            SimWorld w = World(map, 40);
            CommandResult r = w.Execute(Cmd(Good()));
            Assert.IsTrue(r.IsAccepted, r.Text);
            CollectionAssert.Contains(r.RemovedTreeIds, 1L);
            CollectionAssert.DoesNotContain(r.RemovedTreeIds, 2L);
        }

        [Test]
        public void Apply_ReservesNearestSourceFirstAndInListOrder()
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning());
            LedgerLocation near = w.Inventory.AddStockpile(M(120, 130));
            LedgerLocation far = w.Inventory.AddStockpile(M(10, 10));
            w.Inventory.Produce(near, Items.Log, 5);
            w.Inventory.Produce(far, Items.Log, 10);
            var mods = Good();
            mods.Add(new ModuleSpec(ModuleTypes.OxenStandId, M(135, 115), 0));
            CommandResult r = w.Execute(Cmd(mods));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(0, near.AvailableOf(Items.Log.Id), "hut (first) drains the nearer stock first");
            Assert.AreEqual(10 - 3 - 1, far.AvailableOf(Items.Log.Id));
            Assert.AreEqual(9, w.Inventory.Reservations.Sum(x => x.Count));
        }

        [Test]
        public void Validate_InvalidArea_Refuses()
        {
            MapData map = Map();
            var bowtie = AreaShape.Straight(new[] { M(100, 100), M(140, 120), M(140, 100), M(100, 120) });
            AssertRefusedUnchanged(World(map, 40), map,
                new DesignateOperation(bowtie, OperationTypes.WoodcuttingId, Good()), "");
        }

        [Test]
        public void Validate_AreaClickOrders_AreRefusedOrAcceptedAndNeverLeaveASelfCrossingArea()
        {
            MapData map = Map();
            PointMm[] pts = { M(100, 100), M(140, 100), M(140, 120), M(100, 120) };
            foreach (int[] perm in Permutations(4))
            {
                SimWorld w = World(map, 40);
                var shape = AreaShape.Straight(perm.Select(i => pts[i]).ToArray());
                CommandResult r = w.Execute(new DesignateOperation(shape, OperationTypes.WoodcuttingId, Good()));
                if (r.IsRejected) continue;
                AreaRecord a = w.Areas.Get((int)r.Created[0]);
                Assert.IsTrue(Polygons.IsSimple(a.Polygon), string.Join(",", perm));
            }
        }

        [Test]
        public void Validate_MissingHut_RefusesWithPlaceAHut()
        {
            MapData map = Map();
            var mods = new List<ModuleSpec> { Good()[1] };
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "Place a Hut");
        }

        [Test]
        public void Validate_MissingLogStorage_RefusesWithPlaceALogStorage()
        {
            MapData map = Map();
            var mods = new List<ModuleSpec> { Good()[0] };
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "Place a Log storage");
        }

        [Test]
        public void Validate_EmptyModuleList_RefusesWithPlaceAHut()
        {
            MapData map = Map();
            AssertRefusedUnchanged(World(map, 40), map, Cmd(new List<ModuleSpec>()), "Place a Hut");
        }

        [Test]
        public void Validate_ModuleOutsideArea_RefusesAndChangesNothing()
        {
            MapData map = Map();
            var mods = Good();
            mods[1] = new ModuleSpec(ModuleTypes.LogStorageId, M(300, 300), 0);
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "inside the area");
        }

        [Test]
        public void Validate_OverlappingModules_RefusesAndChangesNothing()
        {
            MapData map = Map();
            var mods = Good();
            mods[1] = new ModuleSpec(ModuleTypes.LogStorageId, M(111, 110), 0);
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "Overlaps");
        }

        [Test]
        public void Validate_SecondHut_RefusesAndChangesNothing()
        {
            MapData map = Map();
            var mods = Good();
            mods.Add(new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(132, 112), 0));
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "allowed");
        }

        [Test]
        public void Validate_Unaffordable_RefusesAndChangesNothing()
        {
            MapData map = Map();
            AssertRefusedUnchanged(World(map, 7), map, Cmd(Good()), "Not enough logs");
        }

        [Test]
        public void Validate_TotalOfSeveralGhostsUnaffordable_Refuses()
        {
            MapData map = Map();
            var mods = Good();
            mods.Add(new ModuleSpec(ModuleTypes.OxenStandId, M(135, 115), 0));
            AssertRefusedUnchanged(World(map, 8), map, Cmd(mods), "Not enough logs");
        }

        [Test]
        public void Validate_NonFiniteRotation_Refuses()
        {
            MapData map = Map();
            var mods = Good();
            mods[0] = new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(110, 110), double.NaN);
            AssertRefusedUnchanged(World(map, 40), map, Cmd(mods), "not a number");
        }

        [Test]
        public void Validate_UnknownOperationType_Refuses()
        {
            MapData map = Map();
            AssertRefusedUnchanged(World(map, 40), map, new DesignateOperation(Square(), "operation.nope", Good()), "Unknown");
        }

        [Test]
        public void Validate_ModuleOverlappingExistingBuilding_Refuses()
        {
            MapData map = Map();
            SimWorld w = World(map, 40);
            w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.LogStorage, M(110, 110), 0);
            AssertRefusedUnchanged(w, map, Cmd(Good()), "building");
        }

        [Test]
        public void RoundTrip_AfterDesignateOperation_IsIdentical()
        {
            MapData map = Map();
            SimWorld w = World(map, 40);
            Assert.IsTrue(w.Execute(Cmd(Good())).IsAccepted);
            byte[] first = Bytes(w, map);
            byte[] second = Bytes(SimSave.Read(first, map), map);
            Assert.IsNull(SaveDiff.FirstDifference(first, second));
            Assert.AreEqual(first, second);
        }

        private static IEnumerable<int[]> Permutations(int n)
        {
            var items = Enumerable.Range(0, n).ToArray();
            return Permute(items, 0);
        }

        private static IEnumerable<int[]> Permute(int[] a, int k)
        {
            if (k == a.Length) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < a.Length; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (int[] p in Permute(a, k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
    }
}
