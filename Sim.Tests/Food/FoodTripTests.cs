using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>Sunday food trip (T-418, FD-14, FOOD-A7).</summary>
    public class FoodTripTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private static SimWorld World(int meat = 100)
        {
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartWeekday = Weekday.Sunday, StartingMeat = meat });
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static Family Add(SimWorld w, int id, double dx)
        {
            double x = w.Camp.Fire.XMetres + dx, z = w.Camp.Fire.ZMetres;
            Family f = w.People.AddFamily(id, "F" + id, x, z);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, x, z);
            return f;
        }

        private static TaskRecord TripOf(SimWorld w, Family f) =>
            w.Tasks.ByOwner(AllotmentSystem.OwnerFor(f.Id)).FirstOrDefault(t => t.Def.Id == FoodTripSystem.FetchFoodTaskId);

        [Test]
        public void FetchFood_IsRegisteredInTheCatalog_AsAnOwnerFamilyTask()
        {
            Assert.IsTrue(TaskDefCatalog.TryGet("task.fetch_food", out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.OwnerFamily, e);
            Assert.AreEqual(RequiredResource.None, r);
        }

        [Test]
        public void FullPipeline_AllotmentToLarder_ThroughRealCommandsAndTicks()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 10);
            MapData map = SyntheticMap.CreateRamp();
            bool sawCarry = false, sawText = false, savedMidTrip = false;
            TaskRecord trip = null;
            for (int i = 0; i < 60 * 40 && (trip == null || trip.State != TaskState.Done); i++)
            {
                w.Step(1);
                trip = trip ?? TripOf(w, f);
                LedgerLocation carrier = w.Inventory.Get(f.TripCarrierId);
                Assert.IsNull(w.Inventory.Audit(), "audit at tick " + i);
                if (carrier != null && carrier.Total > 0)
                {
                    sawCarry = true;
                    if (!savedMidTrip)
                    {
                        savedMidTrip = true;
                        byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
                        SimWorld back = SimSave.Read(bytes, map);
                        Assert.AreEqual(carrier.Total, back.Inventory.Get(back.People.FindFamily(1).TripCarrierId).Total);
                        Assert.IsNull(SaveDiff.FirstDifference(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
                        // The saved world finishes the trip too.
                        for (int k = 0; k < 600; k++) back.Step(1);
                        Assert.Greater(Larders.ForFamily(back, back.People.FindFamily(1)).Total, 0);
                    }
                }
                if (trip != null && trip.State == TaskState.Active && CitizenTaskText.Task(w, w.People.FindCitizen(1)) == "Fetching food") sawText = true;
            }
            Assert.IsNotNull(trip);
            Assert.AreEqual(TaskState.Done, trip.State);
            Assert.IsTrue(sawCarry, "food was in transit");
            Assert.IsTrue(sawText, "citizen text is 'Fetching food'");
            Assert.AreEqual(3, Larders.ForFamily(w, f).CountOf(Items.Meat.Id));
            Assert.AreEqual(0, f.AllotmentReservationIds.Count);
            Assert.AreEqual(0, w.Camp.FoodStoreLocation.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, w.Inventory.Get(f.TripCarrierId).Total);
            Assert.AreEqual(97, w.Camp.FoodStoreLocation.CountOf(Items.Meat.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Starts_AreSpreadOverTheMorning_OnePerHousehold()
        {
            SimWorld w = World();
            for (int i = 1; i <= 4; i++) Add(w, i, 5 * i);
            var seenAt = new Dictionary<int, int>();
            for (int i = 0; i < 60 * 30; i++)
            {
                w.Step(1);
                foreach (TaskRecord t in w.Tasks.All) if (!seenAt.ContainsKey(t.Id)) seenAt[t.Id] = i;
            }
            Assert.AreEqual(4, seenAt.Count, "one task per household");
            Assert.AreEqual(4, seenAt.Values.Distinct().Count(), "starts are on different ticks");
            int span = seenAt.Values.Max() - seenAt.Values.Min();
            Assert.That(span, Is.InRange(FoodTripSystem.MorningMinutes / 2, FoodTripSystem.MorningMinutes));
        }

        [Test]
        public void NoAllotment_NoTrip()
        {
            SimWorld w = World(meat: 0);
            Add(w, 1, 10);
            w.Step(60 * 30);
            Assert.AreEqual(0, w.Tasks.Count);
        }

        [Test]
        public void SaveVersion_IsNine_VersionEightBytesAreRefused()
        {
            Assert.AreEqual(15, SaveWriter.CurrentVersion);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 8, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 8 (this build reads version 15).", e.Message);
        }
    }
}
