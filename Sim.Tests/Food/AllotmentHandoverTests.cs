using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Allotment from the camp before the handover and from the stalls after it (T-465, MK-30..34, CMP-20).</summary>
    public class AllotmentHandoverTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class StubMarket : IFoodMarket
        {
            public IReadOnlyList<LedgerLocation> Stocks { get; set; }
            public PathPoint Origin { get; set; }
            public double RangeM { get; set; } = 1000;
        }

        // ---- camp world (ramp map, camp at the start layout) ----

        private static SimWorld CampWorld(SimTuning t)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), t);
            StartLayoutApplier.Apply(w, new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2));
            return w;
        }

        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static Family AddCamp(SimWorld w, int id, double dx)
        {
            double x = w.Camp.Fire.XMetres + dx, z = w.Camp.Fire.ZMetres;
            Family f = w.People.AddFamily(id, "F" + id, x, z);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, x, z);
            return f;
        }

        private static int Rations(SimWorld w, Family f)
        {
            int r = 0;
            foreach (int id in f.AllotmentReservationIds)
            {
                Reservation x = w.Inventory.GetReservation(id);
                r += x.Count * Items.All.First(i => i.Id == x.ItemId).RationsPerItem;
            }
            return r;
        }

        [Test]
        public void BeforeHandover_DefaultSource_IsTheCampOnly_AndFeedsFromTheCampStore()
        {
            SimWorld w = CampWorld(new SimTuning());
            Family f = AddCamp(w, 1, 10);
            List<IFoodMarket> src = AllotmentSystem.Default(w).ToList();
            Assert.AreEqual(1, src.Count);
            Assert.IsInstanceOf<CampFoodMarket>(src[0]);
            AllotmentSystem.Run(w, src);
            Assert.GreaterOrEqual(Rations(w, f), 7);
            Assert.AreEqual(1, w.LastAllotmentServed);
            Assert.AreEqual(0, w.LastAllotmentShort);
        }

        [Test]
        public void AfterHandover_CampFoodIsIgnored_NoStalls_EveryoneIsShort()
        {
            SimWorld w = CampWorld(new SimTuning());
            Family f = AddCamp(w, 1, 10);
            w.FoodHandedOver = true;
            Assert.AreEqual(0, AllotmentSystem.Default(w).Count(m => m is CampFoodMarket));
            AllotmentSystem.Run(w, AllotmentSystem.Default(w));
            Assert.AreEqual(0, f.AllotmentReservationIds.Count);
            Assert.AreEqual(0, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, w.LastAllotmentServed);
            Assert.AreEqual(1, w.LastAllotmentShort);
        }

        [Test]
        public void HandoverOnSaturday_StallsNotBuilt_SundayAllotsNothing_CountsShort_AndHungryOnceLarderRunsOut()
        {
            SimWorld w = CampWorld(new SimTuning { StartWeekday = Weekday.Saturday });
            Family f = AddCamp(w, 1, 10);
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 1); // 3 rations: one adult eats 1 a day
            w.FoodHandedOver = true;
            bool sawShort = false;
            int hungryAfterFirstMidnight = -1;
            for (int i = 0; i < 60 * 24 * 5; i++)
            {
                w.Step(1);
                if (w.LastAllotmentShort == 1) sawShort = true;
                if (w.Now.Hour == 0 && w.Now.Minute == 0 && hungryAfterFirstMidnight < 0) hungryAfterFirstMidnight = f.IsHungry ? 1 : 0;
            }
            Assert.IsTrue(sawShort, "that Sunday the household counted as short");
            Assert.AreEqual(0, f.AllotmentReservationIds.Count, "nothing was allotted");
            Assert.AreEqual(0, w.LastAllotmentServed);
            Assert.AreEqual(0, hungryAfterFirstMidnight, "still fed by its larder at first");
            Assert.IsTrue(f.IsHungry, "hungry once the larder ran out");
        }

        [Test]
        public void StubMarket_ServesNearestFirst_FarHouseholdOutOfRangeAndShortageLeavesTheFarOneShort()
        {
            SimWorld w = CampWorld(new SimTuning { MarketRangeM = 50 });
            Family near = AddCamp(w, 1, 10), mid = AddCamp(w, 2, 20), far = AddCamp(w, 3, 80);
            LedgerLocation stall = w.Inventory.AddFoodStore();
            w.Inventory.Produce(stall, Items.Meat, 5); // 15 rations: one household's week (7 → 3 meat = 9) then 2 meat left
            var m = new StubMarket { Stocks = new[] { stall }, Origin = new PathPoint(w.Camp.Fire.XMetres, w.Camp.Fire.ZMetres), RangeM = 50 };
            AllotmentSystem.Run(w, new IFoodMarket[] { m });
            Assert.AreEqual(9, Rations(w, near), "nearest served first");
            Assert.Less(Rations(w, mid), 7, "next gets what is left");
            Assert.AreEqual(0, Rations(w, far), "out of range gets nothing (MKT-05)");
            Assert.AreEqual(1, w.LastAllotmentServed);
            Assert.AreEqual(2, w.LastAllotmentShort);
        }

        [Test]
        public void SeveralStalls_FullestFirst_AndMixedAcrossTypes()
        {
            SimWorld w = CampWorld(new SimTuning());
            Family f = AddCamp(w, 1, 10);
            LedgerLocation small = w.Inventory.AddFoodStore(), big = w.Inventory.AddFoodStore();
            w.Inventory.Produce(small, Items.Meat, 1);
            w.Inventory.Produce(big, Items.Meat, 2);
            w.Inventory.Produce(big, Items.Berries, 4);
            var m = new StubMarket { Stocks = new[] { small, big }, Origin = new PathPoint(w.Camp.Fire.XMetres, w.Camp.Fire.ZMetres) };
            AllotmentSystem.Run(w, new IFoodMarket[] { m });
            Assert.Greater(big.ReservedOf(Items.Meat.Id), 0, "fullest stall drawn first");
            Assert.AreEqual(0, small.ReservedOf(Items.Meat.Id), "the fuller stall covered the week alone");
            Assert.Greater(big.ReservedOf(Items.Berries.Id), 0, "mixed types");
            Assert.GreaterOrEqual(Rations(w, f), 7);
        }

        [Test]
        public void SaveRoundTrip_ServedAndShortCounts_AreKept()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = CampWorld(new SimTuning());
            AddCamp(w, 1, 10);
            w.FoodHandedOver = true;
            AllotmentSystem.Run(w, AllotmentSystem.Default(w));
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(w.LastAllotmentServed, back.LastAllotmentServed);
            Assert.AreEqual(1, back.LastAllotmentShort);
            Assert.IsNull(SaveDiff.FirstDifference(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }

        // ---- full pipeline: market, supplying granary, stall family, stall built, handover, allotment ----

        private static SimWorld StallWorld()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, MarketRangeM = 60 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            AddFamily(w, 2, 40, 40, 2);
            AddFamily(w, 3, 42, 40, 1);
            AddFamily(w, 6, 95, 95, 1); // too far from the market centre
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        private static void AddFamily(SimWorld w, int id, double x, double z, int adults)
        {
            w.People.AddFamily(id, "F" + id, x, z);
            for (int i = 0; i < adults; i++) w.People.AddCitizen(id * 10 + i, "C" + id + i, Sex.Male, Adult, id, x, z);
        }

        [Test]
        public void Pipeline_StallBuiltAfterHandover_InRangeFed_OutOfRangeAndGranaryStockGetNothing()
        {
            SimWorld w = StallWorld();
            var shape = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int granaryFp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(granaryFp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(granaryFp));
            int op = w.Operations.FindByModule(granaryFp).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 3)).IsAccepted);
            Market market = w.Markets.Current;
            for (int i = 0; i < 6000 && (market.StallFootprintIds.Count == 0 || !w.IsBuilt(market.StallFootprintIds[0])); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(market.StallFootprintIds[0]), "stall built through ticks");
            Assert.IsTrue(w.FoodHandedOver, "handover reached through the real systems");

            LedgerLocation stall = w.Inventory.FindFoodStore(market.StallFootprintIds[0]);
            LedgerLocation granary = w.Inventory.FindFoodStore(granaryFp);
            w.Inventory.Produce(granary, Items.Meat, 20);
            w.Nav.Sync(w);

            Family inRange = w.People.FindFamily(2), farFamily = w.People.FindFamily(6);
            AllotmentSystem.Run(w, AllotmentSystem.Default(w));
            Assert.AreEqual(0, inRange.AllotmentReservationIds.Count, "empty stall: nothing, granary stock never counts (MK-32)");
            Assert.AreEqual(0, granary.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, w.LastAllotmentServed);
            Assert.AreEqual(3, w.LastAllotmentShort);

            w.Inventory.Produce(stall, Items.Meat, 10);
            AllotmentSystem.Run(w, AllotmentSystem.Default(w));
            Assert.GreaterOrEqual(Rations(w, inRange), 14, "adult pair topped up from the stall");
            Assert.Greater(stall.ReservedOf(Items.Meat.Id), 0);
            Assert.AreEqual(0, granary.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, farFamily.AllotmentReservationIds.Count, "house out of market range gets nothing (MKT-05)");
            Assert.AreEqual(2, w.LastAllotmentServed);
            Assert.AreEqual(1, w.LastAllotmentShort);
        }
    }
}
