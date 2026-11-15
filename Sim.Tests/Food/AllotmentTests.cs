using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Camp market and Sunday allotment (T-417, FD-11..13, MKT-05/06).</summary>
    public class AllotmentTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static StartLayout EmptyLayout() => new StartLayout(
            P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2);

        private static SimWorld World(SimTuning t)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), t);
            StartLayoutApplier.Apply(w, EmptyLayout());
            return w;
        }

        /// <summary>Adds a one-adult family dx metres east of the camp fire.</summary>
        private static Family Add(SimWorld w, int id, double dx)
        {
            double x = w.Camp.Fire.XMetres + dx, z = w.Camp.Fire.ZMetres;
            Family f = w.People.AddFamily(id, "F" + id, x, z);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, x, z);
            return f;
        }

        private static void Allot(SimWorld w) => AllotmentSystem.Run(w, new IFoodMarket[] { new CampFoodMarket(w) });

        private static int ReservedRations(SimWorld w, Family f)
        {
            int r = 0;
            foreach (int id in f.AllotmentReservationIds) { Reservation x = w.Inventory.GetReservation(id); r += x.Count * Items.All.First(i => i.Id == x.ItemId).RationsPerItem; }
            return r;
        }

        [Test]
        public void MarketRange_DefaultIs630()
        {
            Assert.AreEqual(630, new SimTuning().MarketRangeM);
        }

        [Test]
        public void Allot_HouseholdOutsideRange_GetsNothing_InsideGetsAWeek()
        {
            SimWorld w = World(new SimTuning { MarketRangeM = 50 });
            Family near = Add(w, 1, 20), far = Add(w, 2, 80);
            Allot(w);
            Assert.GreaterOrEqual(ReservedRations(w, near), 7);
            Assert.AreEqual(0, far.AllotmentReservationIds.Count);
        }

        [Test]
        public void Allot_ReservesOnCampFoodStoreOnly_NotGranaryOrOtherStores()
        {
            SimWorld w = World(new SimTuning { StartingMeat = 0 });
            LedgerLocation granary = w.Inventory.AddFoodStore();
            w.Inventory.Produce(granary, Items.Meat, 50);
            Family f = Add(w, 1, 10);
            Allot(w);
            Assert.AreEqual(0, f.AllotmentReservationIds.Count, "empty camp store gives nothing even with a full granary");
            Assert.AreEqual(0, granary.ReservedOf(Items.Meat.Id));

            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Meat, 5);
            Allot(w);
            Assert.AreEqual(3, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, granary.ReservedOf(Items.Meat.Id));
            foreach (int id in f.AllotmentReservationIds) Assert.AreEqual(w.Camp.FoodStoreLocation.Id, w.Inventory.GetReservation(id).LocationId);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Shortage_NearestFirst_FarHouseholdsLeftShort_RegardlessOfFamilyIdOrder()
        {
            SimWorld w = World(new SimTuning { StartingMeat = 10 });
            Family far2 = Add(w, 1, 50), far1 = Add(w, 2, 40), mid = Add(w, 3, 30), near = Add(w, 4, 20), nearest = Add(w, 5, 10);
            Allot(w);
            // 7 rations needs 3 meat (9 rations); 10 meat feeds three households fully and a fourth with the last one.
            Assert.AreEqual(9, ReservedRations(w, nearest));
            Assert.AreEqual(9, ReservedRations(w, near));
            Assert.AreEqual(9, ReservedRations(w, mid));
            Assert.AreEqual(3, ReservedRations(w, far1));
            Assert.AreEqual(0, ReservedRations(w, far2));
            Assert.AreEqual(10, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
        }

        [Test]
        public void Allot_MixesTypesInProportionToStock()
        {
            SimWorld w = World(new SimTuning { StartingMeat = 20 });
            w.Inventory.Produce(w.Camp.FoodStoreLocation, Items.Berries, 20);
            Family f = Add(w, 1, 10);
            Allot(w);
            LedgerLocation store = w.Camp.FoodStoreLocation;
            Assert.AreEqual(2, store.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(1, store.ReservedOf(Items.Berries.Id));
            Assert.AreEqual(0, store.ReservedOf(Items.Mushrooms.Id));
            Assert.AreEqual(7, ReservedRations(w, f));
        }

        [Test]
        public void Allot_TopsUpOnlyTheMissingPart_OfAHalfStockedLarder()
        {
            SimWorld w = World(new SimTuning { StartingMeat = 20 });
            Family f = Add(w, 1, 10);
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 2); // 6 of 7 rations
            Allot(w);
            Assert.AreEqual(3, ReservedRations(w, f));
        }

        [Test]
        public void Allot_RunningAgain_ReplacesLastWeeksReservations()
        {
            SimWorld w = World(new SimTuning { StartingMeat = 20 });
            Family f = Add(w, 1, 10);
            Allot(w); Allot(w);
            Assert.AreEqual(3, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(9, ReservedRations(w, f));
        }

        [Test]
        public void SundayDawn_ThroughRealTicks_ReservesOnlyOnSunday()
        {
            SimWorld mon = World(new SimTuning());
            Family fm = Add(mon, 1, 10);
            mon.Step(60 * 24);
            Assert.AreEqual(Weekday.Monday, mon.Tuning.StartWeekday);
            Assert.AreEqual(0, fm.AllotmentReservationIds.Count);

            SimWorld sun = World(new SimTuning { StartWeekday = Weekday.Sunday });
            Family fs = Add(sun, 1, 10);
            Assert.AreEqual(0, fs.AllotmentReservationIds.Count);
            for (int i = 0; i < 60 * 24 && fs.AllotmentReservationIds.Count == 0; i++) sun.Step(1); // the trip (T-418) later collects it
            Assert.AreEqual(3, ReservedRations(sun, fs) / 3);
            Assert.AreEqual(3, sun.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
        }

        [Test]
        public void SaveRoundTrip_AllotmentAndMarketRange_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = World(new SimTuning { MarketRangeM = 321 });
            Family f = Add(w, 1, 10);
            Allot(w);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(321, back.Tuning.MarketRangeM);
            CollectionAssert.AreEqual(f.AllotmentReservationIds.ToList(), back.People.FindFamily(1).AllotmentReservationIds.ToList());
            Assert.AreEqual(3, back.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
        }
    }
}
