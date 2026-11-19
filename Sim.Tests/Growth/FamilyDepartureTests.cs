using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>FamilyDeparture.Begin: unassign, fail tasks, drop loads, free the house (T-501, GR-10..12, GR-14).</summary>
    public class FamilyDepartureTests
    {
        private const long Adult = -4_000_000;
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Flat() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static int Ground(SimWorld w) => w.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.Total);

        // ---- shared unassign helper: command still works ----

        [Test]
        public void Begin_FamilyOnAnOperation_IsUnassigned_PassageLeaving_HungryDaysKept_LogWritten()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8, BushCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Family f = w.People.AddFamily(1, "Smith", 100, 100);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            f.HungryDays = 30;
            FamilyDeparture.Begin(w, 1);
            Assert.IsNull(f.WorkplaceId);
            CollectionAssert.DoesNotContain(op.FamilyIds, 1);
            Assert.AreEqual(FamilyPassage.Leaving, f.Passage);
            Assert.AreEqual(30, f.HungryDays);
            Assert.AreEqual("The Smith family is leaving: hungry for 30 days", w.SettlementLog.Entries.Last().Text);
        }

        [Test]
        public void Begin_WritesADepartureEntryUnderGrowth()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning());
            Family f = w.People.AddFamily(1, "Smith", 10, 10);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 10, 10);
            f.HungryDays = 30;
            FamilyDeparture.Begin(w, 1);
            var growth = w.Log.Snapshot(LogCategory.Growth);
            Assert.AreEqual(1, growth.Count);
            StringAssert.Contains("departure: family 1 Smith leaving", growth[0].Text);
        }

        // ---- house ----

        [Test]
        public void Begin_FamilyInAHouse_FreesTheHouse_LarderFoodStaysInIt()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning());
            Family f = w.People.AddFamily(1, "F", 10, 10);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 10, 10);
            w.Homes.Add(7, 70);
            LordsLegacy.Sim.Homes.HomeRecord home =w.Homes.Get(7);
            Assert.IsTrue(w.Homes.MoveIn(home, f));
            w.Inventory.Produce(Larders.ForHome(w, home), Items.Meat, 6);
            FamilyDeparture.Begin(w, 1);
            Assert.AreEqual(0, home.FamilyId);
            Assert.IsTrue(f.IsHomeless);
            Assert.AreEqual(6, Larders.ForHome(w, home).CountOf(Items.Meat.Id), "food stays in the house");
            Assert.AreEqual(0, Larders.ForFamily(w, f).Total);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Begin_HomelessFamily_CampLarderStaysWhereItIs()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning());
            Family f = w.People.AddFamily(1, "F", 10, 10);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 10, 10);
            LedgerLocation larder = Larders.ForFamily(w, f);
            w.Inventory.Produce(larder, Items.Meat, 4);
            FamilyDeparture.Begin(w, 1);
            Assert.AreSame(larder, w.Inventory.Get(f.LarderLocationId));
            Assert.AreEqual(4, larder.CountOf(Items.Meat.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        // ---- forager mid basket ----

        [Test]
        public void Begin_ForagerMidBasket_DropsBasketsAtTheForager_BushFreed_AuditPasses()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8, BushCount = 0 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.ForagerHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Inventory.AddFoodStore(w.Tuning.ForagerHutCapacity, 1);
            w.People.AddFamily(1, "F", 100, 100);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            w.Bushes.Add(105, 95, BushKind.Berries, 3);
            TaskRecord t = null;
            for (int i = 0; i < 600 && (t == null || !t.CarrierId.HasValue || w.Inventory.Get(t.CarrierId.Value).Total == 0); i++)
            {
                w.Step(1);
                t = w.Tasks.All.FirstOrDefault(x => x.Def.Id == ForagingSystem.ForageTaskId && !x.IsFinished);
            }
            Assert.IsNotNull(t);
            int carried = w.Inventory.Get(t.CarrierId.Value).Total;
            Assert.Greater(carried, 0);
            FamilyDeparture.Begin(w, 1);
            Assert.AreEqual(0, w.Inventory.Get(t.CarrierId.Value).Total);
            Assert.AreEqual(carried, Ground(w));
            Assert.IsTrue(t.IsFinished);
            Assert.IsNull(w.Inventory.Audit());
        }

        // ---- food trip ----

        [Test]
        public void Begin_FamilyMidFoodTrip_DropsCarriedFood_ReleasesAllotment_AuditPasses()
        {
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0], new StartFamily[0], 2);
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning { StartWeekday = Weekday.Sunday, StartingMeat = 100 });
            StartLayoutApplier.Apply(w, layout);
            double x = w.Camp.Fire.XMetres + 5, z = w.Camp.Fire.ZMetres;
            Family f = w.People.AddFamily(1, "F", x, z);
            w.People.AddCitizen(1, "C", Sex.Male, Adult, 1, x, z);
            AllotmentSystem.Run(w, new IFoodMarket[] { new CampFoodMarket(w) });
            Assert.Greater(f.AllotmentReservationIds.Count, 0);
            TaskRecord t = FoodTripSystem.OpenTrip(w, f);
            for (int i = 0; i < 60 * 40 && !(w.Inventory.Get(f.TripCarrierId)?.Total > 0); i++) w.Step(1);
            int carried = w.Inventory.Get(f.TripCarrierId).Total;
            Assert.Greater(carried, 0, "carrying food");
            FamilyDeparture.Begin(w, 1);
            Assert.IsTrue(t.IsFinished);
            Assert.AreEqual(0, w.Inventory.Get(f.TripCarrierId).Total);
            Assert.AreEqual(carried, Ground(w));
            Assert.AreEqual(0, f.AllotmentReservationIds.Count);
            Assert.IsFalse(w.Inventory.Reservations.Any(r => r.Owner == AllotmentSystem.OwnerFor(1)));
            Assert.IsNull(w.Inventory.Audit());
        }

        // ---- stock stall ----

        [Test]
        public void Begin_FamilyMidStockStall_CarryingFood_DropsIt_ReleasesReservations_AuditPasses()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8, BushCount = 0, StartWeekday = Weekday.Monday });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10); // stalls cost 2 planks (PL-10)
            w.People.AddFamily(2, "F2", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(20 + i, "C" + i, Sex.Male, Adult, 2, 40, 40);
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "H" + i, Sex.Male, Adult, 3, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            Assert.IsTrue(w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            int op = w.Operations.FindByModule(fp).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            LedgerLocation granary = w.Inventory.FindFoodStore(fp);
            granary.Capacity = 500;
            w.Inventory.Produce(granary, Items.Meat, 10);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 2)).IsAccepted);
            w.Step(2);
            int stall = w.Markets.Current.StallOf(2);
            for (int i = 0; i < 6000 && !w.IsBuilt(stall); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(stall));

            TaskRecord t = null;
            for (int i = 0; i < 3000; i++)
            {
                w.Step(1);
                t = w.Tasks.All.FirstOrDefault(x => x.Def.Id == StoreFamilySystem.StockStallTaskId && !x.IsFinished && x.CarrierId.HasValue);
                if (t != null && w.Inventory.Get(t.CarrierId.Value).Total > 0) break;
            }
            Assert.IsNotNull(t);
            Assert.AreEqual(10, w.Inventory.Get(t.CarrierId.Value).Total);
            int taskId = t.Id;
            FamilyDeparture.Begin(w, 2);
            Assert.IsTrue(t.IsFinished);
            Assert.AreEqual(0, w.Inventory.Get(t.CarrierId.Value).Total);
            Assert.AreEqual(10, Ground(w));
            Assert.IsFalse(w.Inventory.Reservations.Any(x => x.Owner == StoreFamilySystem.OwnerFor(taskId)));
            Assert.IsNull(w.People.FindFamily(2).WorkplaceId, "no longer on the operation");
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
