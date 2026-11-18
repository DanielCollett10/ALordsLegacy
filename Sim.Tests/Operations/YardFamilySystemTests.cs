using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>YardFamilySystem and task.haul_by_hand with a fake job source (T-542, ST-43, ST-44).</summary>
    public class YardFamilySystemTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class FakeSource : IYardJobSource
        {
            public int Budget;
            public int Calls;
            public LedgerLocation From, To;
            public ItemDef Item = Items.Plank;
            public int Count = 20;
            public YardTrip NextTrip(SimWorld world, Operation yard)
            {
                Calls++;
                if (Budget <= 0) return null;
                Budget--;
                return new YardTrip(From, To, Item, Count);
            }
        }

        private sealed class Fixture
        {
            public SimWorld W; public FakeSource Fake; public YardJobs Jobs; public int Yard, OpId;
            public LedgerLocation YardStore, Dest;
            public IEnumerable<TaskRecord> Hauls => W.Tasks.All.Where(t => t.Def.Id == HandHaulTaskDefs.HaulId);
        }

        // Real pipeline: place the yard, build it through ticks, assign family 2. The fake source starts with no budget.
        private static Fixture Make(bool withSource = true)
        {
            var jobs = new YardJobs();
            var fake = new FakeSource();
            if (withSource) jobs.Add(fake);
            var systems = SimSystems.CreateDefault().Select(s => s is YardFamilySystem ? new YardFamilySystem(jobs) : s).ToList();
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 }, null, null, systems);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            for (int fam = 2; fam <= 3; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            int opId = w.Operations.FindByModule(id).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 2)).IsAccepted);
            var f = new Fixture { W = w, Fake = fake, Jobs = jobs, Yard = id, OpId = opId };
            f.YardStore = w.Inventory.FindGoodsStore(id);
            Assert.IsNotNull(f.YardStore);
            w.Inventory.Produce(f.YardStore, Items.Plank, 6);
            f.Dest = w.Inventory.AddGoodsStore(M(30, 30));
            fake.From = f.YardStore; fake.To = f.Dest;
            foreach (TaskRecord t in w.Tasks.All.ToList()) if (!t.IsFinished) w.Tasks.Cancel(t.Id);
            return f;
        }

        [Test]
        public void EmptySourceList_PlansNothing()
        {
            Fixture f = Make(false);
            f.W.Step(5);
            Assert.AreEqual(0, f.Hauls.Count());
        }

        [Test]
        public void Pipeline_OneTripPlannedReservedAndCompleted_MovesTheHandLoad()
        {
            Fixture f = Make();
            f.Fake.Budget = 1;
            f.W.Step(1);
            TaskRecord t = f.Hauls.Single();
            Assert.AreEqual(f.OpId, t.OwnerOperationId);
            Assert.AreEqual(new[] { StepKind.WalkTo, StepKind.Work, StepKind.CollectItems, StepKind.WalkTo, StepKind.Work, StepKind.DepositItems },
                t.Def.Steps.Select(s => s.Kind).ToArray());
            Assert.AreEqual(2, t.Def.Steps[2].Count, "hand load of planks, not the 20 asked for");
            Assert.AreEqual(2, f.Dest.IncomingOf(Items.Plank.Id), "room reserved before leaving (ST-44)");
            Assert.AreEqual(2, f.YardStore.ReservedOf(Items.Plank.Id), "stock reserved so nobody else takes it");
            for (int i = 0; i < 3000 && !f.Hauls.Single().IsFinished; i++) f.W.Step(1);
            Assert.AreEqual(TaskState.Done, f.Hauls.Single().State);
            Assert.AreEqual(2, f.Dest.CountOf(Items.Plank.Id));
            Assert.AreEqual(4, f.YardStore.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, f.Dest.IncomingTotal);
            Assert.AreEqual(0, f.YardStore.ReservedOf(Items.Plank.Id));
            Assert.AreEqual(1, f.Hauls.Count(), "one budgeted trip, one task");
            Assert.IsNull(f.W.Inventory.Audit());
        }

        [Test]
        public void FullDestination_MeansNoTrip()
        {
            Fixture f = Make();
            f.Dest.Capacity = 3;
            f.W.Inventory.Produce(f.Dest, Items.Plank, 3);
            f.Fake.Budget = 5;
            f.W.Step(3);
            Assert.AreEqual(0, f.Hauls.Count());
            Assert.AreEqual(0, f.YardStore.ReservedOf(Items.Plank.Id));
        }

        [Test]
        public void DestinationWithOneFreeSlot_CarriesOnlyOne()
        {
            Fixture f = Make();
            f.Dest.Capacity = 1;
            f.Fake.Budget = 1;
            f.W.Step(1);
            Assert.AreEqual(1, f.Hauls.Single().Def.Steps[2].Count);
        }

        [Test]
        public void TaskEndingMidCarry_DropsTheLoadAsAnUnownedPile()
        {
            Fixture f = Make();
            f.Fake.Budget = 1;
            f.W.Step(1);
            int id = f.Hauls.Single().Id;
            for (int i = 0; i < 3000 && f.Hauls.Single().CurrentStep < 3; i++) f.W.Step(1);
            TaskRecord t = f.Hauls.Single();
            Assert.GreaterOrEqual(t.CurrentStep, 3, "loaded and on the way");
            LedgerLocation carrier = f.W.Inventory.Get(t.CarrierId.Value);
            Assert.AreEqual(2, carrier.CountOf(Items.Plank.Id));
            f.W.Tasks.Cancel(id);
            f.W.Step(1);
            Assert.AreEqual(0, carrier.Total);
            LedgerLocation pile = f.W.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Plank.Id) > 0);
            Assert.IsNull(pile.OwnerOperationId);
            Assert.AreEqual(2, pile.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, f.Dest.IncomingTotal, "destination room released");
            Assert.AreEqual(0, f.Dest.CountOf(Items.Plank.Id));
        }

        [Test]
        public void FamilyInPassage_IsSkipped()
        {
            Fixture f = Make();
            f.W.People.FindFamily(2).Passage = FamilyPassage.Arriving;
            f.Fake.Budget = 5;
            f.W.Step(3);
            Assert.AreEqual(0, f.Hauls.Count());
        }

        [Test]
        public void SourcesAreAskedInOrder_AndFirstUsableWins()
        {
            Fixture f = Make();
            var first = new FakeSource { Budget = 0 };
            f.Fake.Budget = 1;
            var jobs = new YardJobs();
            jobs.Add(first); jobs.Add(f.Fake);
            new YardFamilySystem(jobs).Step(f.W);
            Assert.GreaterOrEqual(first.Calls, 1, "asked first");
            Assert.GreaterOrEqual(f.Fake.Calls, 1, "asked after the first had nothing");
            Assert.AreEqual(1, f.Hauls.Count());
        }

        [Test]
        public void BeamHasNoHandLoad_SoNoHandTrip()
        {
            Fixture f = Make();
            f.Fake.Item = Items.Beam;
            f.W.Inventory.Produce(f.YardStore, Items.Beam, 2);
            f.Fake.Budget = 1;
            f.W.Step(2);
            Assert.AreEqual(0, f.Hauls.Count());
        }

        [Test]
        public void HaulTaskDef_RebuildsFromCatalog_AsAssignedFamilyOnly()
        {
            Assert.IsTrue(TaskDefCatalog.TryGet(HandHaulTaskDefs.HaulId, out EligibilityPolicy e, out RequiredResource r));
            Assert.AreEqual(EligibilityPolicy.AssignedFamilyOnly, e);
            Assert.AreEqual(RequiredResource.None, r);
        }
    }
}
