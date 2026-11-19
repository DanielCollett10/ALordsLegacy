using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Thatch;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Scenarios
{
    /// <summary>
    /// T-579, M19 thatch end to end: from the start layout a thatcher's hut is built by real commands, a family cuts reed,
    /// the stook cures for 7 days and the dry sheaves go to the hut store; a house is then built from that thatch and the
    /// yard's panels. Winter cuts at the full rate, summer takes 4 times as long. Audited every tick; save mid-cure.
    /// </summary>
    [TestFixture]
    public class ThatchChainScenarioTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static StartCitizen Adult(string n, Sex s) => new StartCitizen(n, s, AdultBirth);

        private static SimWorld Start(MapData map, int month)
        {
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                WorldSeed = 7, StartMonth = month, StartDay = 1, StartHour = 6, StartMinute = 0,
                BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0, ReedBedCount = 0, WorkshopDefaultReserve = 0,
            });
            var fams = new List<StartFamily>();
            for (int i = 0; i < 6; i++)
                fams.Add(new StartFamily("F" + i, M(-9 + 3 * i, 8), new[] { Adult("A" + i, Sex.Male), Adult("B" + i, Sex.Female) }));
            StartLayoutApplier.Apply(w, new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0], fams, 2,
                stock: new[] { new StartStock(Items.Log, 60) }));
            LordsLegacy.Sim.Tests.Start.NoCampTests.UseHouseGoodsUp(w); // the camp's 90 thatch and panels must not hide the chain
            return w;
        }

        private static void Check(SimWorld w)
        {
            Assert.IsNull(w.Inventory.Audit(), "ledger audit at tick " + w.Tick);
            Assert.IsNull(w.Curing.Audit(), "curing audit at tick " + w.Tick);
        }

        private static void StepAudited(SimWorld w, int ticks, System.Func<bool> stop = null)
        {
            for (int i = 0; i < ticks && (stop == null || !stop()); i++) { w.Step(1); Check(w); }
        }

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private sealed class Run { public SimWorld W; public Operation Op; public LedgerLocation Store; public long CutStart; }

        /// <summary>Builds a thatcher's hut by command, staffs it with family 1 and puts one reed bed in its work area.</summary>
        private static Run BuildHut(MapData map, int month, int bundles = 8, bool startAtMorning = false)
        {
            SimWorld w = Start(map, month);
            AreaShape area = AreaShape.Straight(new[] { M(30, 20), M(60, 20), M(60, 40), M(30, 40) });
            CommandResult c = w.Execute(new DesignateOperation(area, OperationTypes.ThatchingId,
                new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ThatcherHutId, M(45, 30), 0) }));
            Assert.IsTrue(c.IsAccepted, c.Text);
            Operation op = w.Operations.FindByArea((int)c.Created[0]);
            int hut = w.Footprints.All.First(f => f.Type.Id == ModuleTypes.ThatcherHutId).Id;
            Assert.NotNull(op);
            StepAudited(w, 6 * 1440, () => w.IsBuilt(hut));
            Assert.IsTrue(w.IsBuilt(hut), "hut built");
            if (startAtMorning) StepAudited(w, 2 * 1440, () => w.Tick % 1440 == 0); // tick 0 is 06:00
            w.Bushes.Add(40, 25, BushKind.ReedBed, bundles);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            return new Run { W = w, Op = op, Store = w.Inventory.FindGoodsStore(hut), CutStart = w.Tick };
        }

        private static long TicksToFirstStook(Run r)
        {
            StepAudited(r.W, 4 * 1440, () => r.W.Curing.Count > 0);
            Assert.Greater(r.W.Curing.Count, 0, "a stook stands");
            return r.W.Tick - r.CutStart;
        }

        /// <summary>Ticks from the first cut task going Active to the first stook: the cutting itself, after any wait for daylight.</summary>
        private static long CutTicks(Run r)
        {
            long started = -1;
            for (int i = 0; i < 4 * 1440 && r.W.Curing.Count == 0; i++)
            {
                r.W.Step(1);
                Check(r.W);
                if (started < 0 && r.W.Tasks.All.Any(t => t.Def.Id == ThatchingSystem.CutReedTaskId && t.State == TaskState.Active))
                    started = r.W.Tick;
            }
            Assert.Greater(r.W.Curing.Count, 0, "a stook stands");
            Assert.GreaterOrEqual(started, 0, "the cut task went Active");
            return r.W.Tick - started;
        }

        [Test]
        public void Summer_CutsFourTimesSlower_ThanDecember()
        {
            MapData map = Map();
            // Two beds per season, both cut from 06:00 so no night or sleep falls inside; the walk is the same for both, so the
            // difference between a 4-bundle and a 2-bundle cut is exactly two bundles of cutting.
            long wDiff = CutTicks(BuildHut(map, 12, 4, true)) - CutTicks(BuildHut(map, 12, 2, true));
            long sDiff = CutTicks(BuildHut(map, 7, 4, true)) - CutTicks(BuildHut(map, 7, 2, true));
            TestContext.Out.WriteLine("winter two bundles " + wDiff + " summer two bundles " + sDiff);
            Assert.AreEqual(2 * 30, wDiff, 2, "winter: 30 minutes a bundle");
            Assert.AreEqual(4 * wDiff, sDiff, 4, "summer cutting is 4 times slower");
        }

        [Test]
        public void December_HutFillsItsStoreAfterCuring_AndAHouseIsBuiltFromThatchAndYardPanels_AuditedEveryTick_SaveMidCureByteIdentical()
        {
            MapData map = Map();
            Run r = BuildHut(map, 12);
            SimWorld w = r.W;
            for (int i = 0; i < 4; i++) w.Bushes.Add(36 + 4 * i, 35, BushKind.ReedBed, 8);

            TicksToFirstStook(r);
            Assert.AreEqual(0, r.Store.CountOf(Items.Thatch.Id), "green reed is not thatch");
            StepAudited(w, 300);
            Assert.Greater(w.Curing.Count, 0, "still curing");

            // Save mid-cure: byte-identical round trip, and the loaded world keeps running identically.
            byte[] first = Bytes(w, map);
            SimWorld back = SimSave.Read(first, map);
            CollectionAssert.AreEqual(first, Bytes(back, map), "save mid-cure is byte-identical");
            for (int i = 0; i < 400; i++) { w.Step(1); back.Step(1); Check(w); Check(back); }
            CollectionAssert.AreEqual(Bytes(w, map), Bytes(back, map), "original and loaded stay identical");

            StepAudited(w, 12 * 1440, () => r.Store.CountOf(Items.Thatch.Id) >= 10);
            Assert.GreaterOrEqual(r.Store.CountOf(Items.Thatch.Id), 10, "dry sheaves reached the hut store");

            // Panels in a built storage yard; the camp's thatch is gone, so the house must take the hut's.
            CommandResult y = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(15, -30), 0));
            Assert.IsTrue(y.IsAccepted, y.Text);
            int yard = (int)y.Created[0];
            StepAudited(w, 6 * 1440, () => w.IsBuilt(yard));
            Assert.IsTrue(w.IsBuilt(yard), "yard built");
            LedgerLocation yardStore = w.Inventory.FindGoodsStore(yard);
            w.Inventory.Produce(yardStore, Items.WattlePanel, 10);

            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            // Stand the thatchers down so no new sheaves arrive while the house is built: the store count can only fall.
            Assert.IsTrue(w.Execute(new UnassignFamily(r.Op.Id, 1)).IsAccepted);
            int hutThatchBefore = r.Store.CountOf(Items.Thatch.Id);
            CommandResult hr = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(hr.IsAccepted, hr.Text);
            int house = (int)hr.Created[1];
            StepAudited(w, 40000, () => w.IsBuilt(house));
            Assert.IsTrue(w.IsBuilt(house), "house built");
            Assert.AreEqual(0, yardStore.CountOf(Items.WattlePanel.Id), "panels came from the yard");
            Assert.AreEqual(hutThatchBefore - 10, r.Store.CountOf(Items.Thatch.Id), "the house took its 10 thatch from the hut store");
        }
    }
}
