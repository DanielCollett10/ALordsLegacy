using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>Deconstructing an unstaffed stall (T-460, MK-23b, CN-50..57).</summary>
    public class DeconstructStallTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(20, 22)), Items.Plank, 10); // stalls cost 2 planks (PL-10)
            for (int fam = 2; fam <= 5; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        // Market, a supplying granary, family 2 assigned and its stall built, all through commands and ticks.
        private static (int op, int stall) Staffed(SimWorld w)
        {
            var shape = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int g = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(g); i++) w.Step(1);
            int op = w.Operations.FindByModule(g).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 2)).IsAccepted);
            w.Step(2);
            int stall = w.Markets.Current.StallFootprintIds[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(stall); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(stall));
            return (op, stall);
        }

        [Test]
        public void StaffedStall_IsRefused_NamingTheFamily()
        {
            SimWorld w = Make();
            var (_, stall) = Staffed(w);
            CommandResult r = w.Execute(new DeconstructStall(stall));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Staffed by F2: unassign them first", r.Text);
            Assert.IsNull(w.Deconstruction.Get(stall));
        }

        [Test]
        public void UnknownStall_IsNotFound()
        {
            SimWorld w = Make();
            Staffed(w);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new DeconstructStall(9999)).Reason);
        }

        [Test]
        public void UnstaffedStall_FullDeconstruct_DropsFoodPile_NoLogs_AndFreesSlot_ThroughTicks()
        {
            SimWorld w = Make();
            var (op, stall) = Staffed(w);
            Market m = w.Markets.Current;
            PointMm centre = w.Footprints.Get(stall).Centre;
            w.Inventory.Produce(w.Inventory.FindFoodStore(stall), Items.Find(Items.Berries.Id), 7);
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 2)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(0, m.FamilyOf(stall));
            Assert.IsTrue(w.Execute(new DeconstructStall(stall)).IsAccepted);
            Assert.IsNotNull(w.Deconstruction.Get(stall));
            Assert.IsTrue(w.Execute(new DeconstructStall(stall)).IsRejected, "already marked");
            int logsBefore = TotalLogs(w);
            for (int i = 0; i < 3000 && w.Footprints.Get(stall) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(stall), "removed by odd-jobbers");
            w.Step(2);
            Assert.AreEqual(0, m.StallFootprintIds.Count, "slot is free");
            Assert.IsNull(w.Deconstruction.Get(stall));
            Assert.AreEqual(logsBefore, TotalLogs(w), "refund is 0 logs");
            LedgerLocation pile = w.Inventory.GetOrAddGroundPile(centre, null);
            Assert.AreEqual(7, pile.CountOf(Items.Berries.Id), "food drops as a pile at the stall");
            Assert.AreEqual(1, pile.CountOf(Items.Plank.Id), "refund: half of the stall's 2 planks (PL-10, DeconstructRefundShare)");
            Assert.AreEqual(9, w.Inventory.TotalOf(Items.Plank), "9 planks: 8 left in the camp plus the 1 refunded to the pile");
            Assert.IsNull(pile.OwnerOperationId, "unowned");
        }

        [Test]
        public void MarkedStall_IsNotRebuilt_AndNotReusedWhileMarked_ButNextFamilyGetsTheFreedSlot()
        {
            SimWorld w = Make();
            var (op, stall) = Staffed(w);
            Market m = w.Markets.Current;
            PointMm centre = w.Footprints.Get(stall).Centre;
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 2)).IsAccepted);
            w.Step(2);
            Assert.IsTrue(w.Execute(new DeconstructStall(stall)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(op, 3)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(0, m.FamilyOf(stall), "a marked stall is not reused");
            Assert.AreEqual(2, m.StallFootprintIds.Count, "next family gets a new stall on another slot");
            for (int i = 0; i < 3000 && w.Footprints.Get(stall) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(stall));
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 3)).IsAccepted);
            w.Step(5);
            Assert.AreEqual(1, m.StallFootprintIds.Count, "nobody needs a stall: the removed one is not rebuilt");
            Assert.IsTrue(w.Execute(new AssignFamily(op, 4)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(1, m.StallFootprintIds.Count, "family 4 reuses family 3's unstaffed stall");
            Assert.AreNotEqual(0, m.StallOf(4));
            Assert.AreNotEqual(centre, w.Footprints.Get(m.StallOf(4)).Centre, "the removed stall's slot was not rebuilt");
        }

        [Test]
        public void Cancel_RestoresTheStall_AndItCanBeReusedAgain()
        {
            SimWorld w = Make();
            var (op, stall) = Staffed(w);
            Market m = w.Markets.Current;
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 2)).IsAccepted);
            w.Step(2);
            Assert.IsTrue(w.Execute(new DeconstructStall(stall)).IsAccepted);
            w.Step(3);
            Assert.IsTrue(w.Execute(new CancelDeconstruction(stall)).IsAccepted);
            Assert.IsNull(w.Deconstruction.Get(stall));
            Assert.IsTrue(w.Execute(new AssignFamily(op, 3)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(3, m.FamilyOf(stall), "reused after cancel");
            Assert.AreEqual(1, m.StallFootprintIds.Count);
        }

        private static int TotalLogs(SimWorld w)
        {
            int n = 0;
            foreach (LedgerLocation l in w.Inventory.Locations) n += l.CountOf(Items.Log.Id);
            return n;
        }
    }
}
