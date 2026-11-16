using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>The Sunday trip walks to the reserved stalls (T-466, MK-33).</summary>
    public class StallTripTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);

        private sealed class StubMarket : IFoodMarket
        {
            public IReadOnlyList<LedgerLocation> Stocks { get; set; }
            public PathPoint Origin { get; set; }
            public double RangeM { get; set; } = 1000;
        }

        private static SimWorld World()
        {
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartWeekday = Weekday.Saturday });
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

        // A stall store at camp + (dx, dz), bound to a real footprint so it can be removed.
        private static LedgerLocation Stall(SimWorld w, int footprintId, double dx, double dz, int meat)
        {
            PointMm at = PointMm.FromMetres(w.Camp.Fire.XMetres + dx, w.Camp.Fire.ZMetres + dz);
            w.Footprints.Add(footprintId, ModuleTypes.LogStorage, at, 0);
            LedgerLocation l = w.Inventory.AddFoodStore(int.MaxValue, footprintId);
            w.Inventory.Produce(l, Items.Meat, meat);
            return l;
        }

        private static void Allot(SimWorld w, params LedgerLocation[] stalls)
        {
            var m = new StubMarket { Stocks = stalls, Origin = new PathPoint(w.Camp.Fire.XMetres, w.Camp.Fire.ZMetres) };
            AllotmentSystem.Run(w, new IFoodMarket[] { m });
        }

        private static List<TaskStep> Steps(TaskRecord t) => t.Def.Steps.ToList();

        [Test]
        public void Trip_TwoStalls_VisitsNearestFirst_ThenOtherThenHome()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 5);
            LedgerLocation far = Stall(w, 501, 40, 0, 2), near = Stall(w, 502, 15, 0, 2);
            Allot(w, far, near);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            List<TaskStep> s = Steps(t);
            Assert.AreEqual(6, s.Count);
            Assert.AreEqual(StepKind.CollectFood, s[1].Kind);
            Assert.AreEqual(near.Id, s[1].LocationId, "nearest reserved stall first");
            Assert.AreEqual(far.Id, s[3].LocationId);
            Assert.AreEqual(w.Footprints.Get(502).Centre.XMetres, s[0].X, 1e-6);
            Assert.AreEqual(f.HomeX, s[4].X, 1e-6, "walks home last");
            Assert.AreEqual(StepKind.DepositFood, s[5].Kind);
        }

        [Test]
        public void Trip_CampSupplied_IsTheM14FourSteps()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 5);
            AllotmentSystem.Run(w, new IFoodMarket[] { new CampFoodMarket(w) });
            List<TaskStep> s = Steps(FoodTripSystem.OpenTrip(w, f));
            Assert.AreEqual(4, s.Count);
            Assert.AreEqual(StepKind.WalkTo, s[0].Kind);
            Assert.AreEqual(w.Camp.Fire.XMetres, s[0].X, 1e-6);
            Assert.AreEqual(StepKind.CollectFood, s[1].Kind);
            Assert.AreEqual(0, s[1].LocationId);
        }

        [Test]
        public void Trip_StallRemovedMidTrip_IsSkipped_ReservationsFreed_AndFoodFromOtherStallArrives()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 5);
            LedgerLocation a = Stall(w, 501, 15, 0, 2), b = Stall(w, 502, 40, 0, 2);
            Allot(w, a, b);
            int before = f.AllotmentReservationIds.Count;
            Assert.AreEqual(2, before);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            bool removed = false;
            for (int i = 0; i < 60 * 40 && t.State != TaskState.Done; i++)
            {
                w.Step(1);
                if (!removed && w.Inventory.Get(f.TripCarrierId)?.Total > 0)
                {
                    removed = true;
                    Assert.IsTrue(w.Footprints.Remove(502), "second stall taken down after the first collect");
                }
            }
            Assert.IsTrue(removed);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.AreEqual(2, Larders.ForFamily(w, f).CountOf(Items.Meat.Id), "only the first stall's food arrived");
            Assert.AreEqual(0, b.ReservedOf(Items.Meat.Id), "removed stall's reservation freed");
            Assert.AreEqual(0, f.AllotmentReservationIds.Count);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Trip_StallRemovedBeforeStart_IsSkipped_AndNothingIsLeftReserved()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 5);
            LedgerLocation a = Stall(w, 501, 15, 0, 2), b = Stall(w, 502, 40, 0, 2);
            Allot(w, a, b);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            int fromB = b.ReservedOf(Items.Meat.Id);
            Assert.Greater(fromB, 0);
            w.Footprints.Remove(501);
            for (int i = 0; i < 60 * 40 && t.State != TaskState.Done; i++) w.Step(1);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.AreEqual(fromB, Larders.ForFamily(w, f).CountOf(Items.Meat.Id));
            Assert.AreEqual(0, a.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, b.ReservedOf(Items.Meat.Id));
        }

        [Test]
        public void Trip_AllStallsRemoved_WalksHomeEmptyAndFreesEverything()
        {
            SimWorld w = World();
            Family f = Add(w, 1, 5);
            LedgerLocation a = Stall(w, 501, 15, 0, 2);
            Allot(w, a);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            w.Footprints.Remove(501);
            for (int i = 0; i < 60 * 40 && t.State != TaskState.Done; i++) w.Step(1);
            Assert.AreEqual(TaskState.Done, t.State);
            Assert.AreEqual(0, a.ReservedOf(Items.Meat.Id));
            Assert.AreEqual(0, f.AllotmentReservationIds.Count);
        }

        [Test]
        public void SaveRoundTrip_TripBetweenTwoStalls_Resumes_AndStepsKeepTheirStall()
        {
            SimWorld w = World();
            MapData map = SyntheticMap.CreateRamp();
            Family f = Add(w, 1, 5);
            LedgerLocation a = Stall(w, 501, 15, 0, 2), b = Stall(w, 502, 40, 0, 2);
            Allot(w, a, b);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            SimWorld back = null;
            for (int i = 0; i < 60 * 40 && back == null; i++)
            {
                w.Step(1);
                if (w.Inventory.Get(f.TripCarrierId)?.Total > 0 && t.CurrentStep >= 2)
                {
                    byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
                    back = SimSave.Read(bytes, map);
                    Assert.IsNull(SaveDiff.FirstDifference(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
                }
            }
            Assert.IsNotNull(back, "saved between the two stalls");
            TaskRecord bt = back.Tasks.Get(t.Id);
            Assert.AreEqual(b.Id, bt.Def.Steps[3].LocationId);
            for (int i = 0; i < 60 * 40 && bt.State != TaskState.Done; i++) back.Step(1);
            Assert.AreEqual(TaskState.Done, bt.State);
            Assert.AreEqual(3, Larders.ForFamily(back, back.People.FindFamily(1)).CountOf(Items.Meat.Id));
            Assert.IsNull(back.Inventory.Audit());
        }
    }
}
