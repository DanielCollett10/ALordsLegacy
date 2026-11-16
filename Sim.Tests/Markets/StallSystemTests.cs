using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>Stalls on market slots: creation, reuse, no room, removal (T-459, MK-20..23, MK-04).</summary>
    public class StallSystemTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            for (int fam = 2; fam <= 5; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return (w, map);
        }

        private static void DesignateMarket(SimWorld w, double size = 20)
        {
            var shape = AreaShape.Straight(new[] { M(10, 10), M(10 + size, 10), M(10 + size, 10 + size), M(10, 10 + size) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted, "market designated");
        }

        /// <summary>A built granary that supplies the market, reached only through commands and ticks.</summary>
        private static int SupplyingGranary(SimWorld w)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 60), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            int op = w.Operations.FindByModule(fp).Id;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            return op;
        }

        private static void Assign(SimWorld w, int op, int fam) => Assert.IsTrue(w.Execute(new AssignFamily(op, fam)).IsAccepted);

        [Test]
        public void AssignedFamily_GetsOneStall_OnTheSlotNearestTheCentre_AndItIsBuiltThroughTicks()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2);
            w.Step(2);
            Market m = w.Markets.Current;
            Assert.AreEqual(1, m.StallFootprintIds.Count);
            int stall = m.StallFootprintIds[0];
            Footprint fp = w.Footprints.Get(stall);
            Assert.AreEqual(ModuleTypes.MarketStallId, fp.Type.Id);
            Assert.AreEqual(3000, fp.Type.WidthMm);
            Assert.AreEqual(2000, fp.Type.DepthMm);
            Assert.IsFalse(fp.Type.BlocksMovement);
            Assert.AreEqual(1, fp.Type.ConstructionRecipe.Logs);
            Assert.AreEqual(60, fp.Type.ConstructionRecipe.BuildWorkerMinutes);
            Assert.AreEqual(2, m.FamilyOf(stall));
            var slots = StallGrid.Slots(w.Areas.Get(m.AreaId).Polygon, w.Tuning);
            Assert.AreEqual(slots[0].Centre, fp.Centre, "free slot nearest the centre");
            Assert.AreEqual(30, w.Inventory.FindFoodStore(stall).Capacity);
            Assert.IsFalse(w.IsBuilt(stall), "queued as an ordinary site");
            for (int i = 0; i < 6000 && !w.IsBuilt(stall); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(stall), "built by workers through Sim ticks only");
            Assert.AreEqual(1, m.StallFootprintIds.Count, "one stall per family, no more");
        }

        [Test]
        public void SecondFamily_TakesTheNextNearestSlot()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2); Assign(w, op, 3);
            w.Step(2);
            Market m = w.Markets.Current;
            var slots = StallGrid.Slots(w.Areas.Get(m.AreaId).Polygon, w.Tuning);
            Assert.AreEqual(2, m.StallFootprintIds.Count);
            Assert.AreEqual(slots[0].Centre, w.Footprints.Get(m.StallFootprintIds[0]).Centre);
            Assert.AreEqual(slots[1].Centre, w.Footprints.Get(m.StallFootprintIds[1]).Centre);
            Assert.AreEqual(2, m.FamilyOf(m.StallFootprintIds[0]));
            Assert.AreEqual(3, m.FamilyOf(m.StallFootprintIds[1]));
        }

        [Test]
        public void GranaryNotSwitchedOn_MakesNoStalls()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, false)).IsAccepted);
            Assign(w, op, 2);
            w.Step(3);
            Assert.AreEqual(0, w.Markets.Current.StallFootprintIds.Count);
        }

        [Test]
        public void Unassigned_LeavesStallUnstaffedWithItsFood_AndNextFamilyReusesIt()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2);
            w.Step(2);
            Market m = w.Markets.Current;
            int stall = m.StallFootprintIds[0];
            w.Inventory.Produce(w.Inventory.FindFoodStore(stall), Items.Find(Items.Berries.Id), 7);
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 2)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(0, m.FamilyOf(stall), "unstaffed");
            Assert.AreEqual(1, m.StallFootprintIds.Count);
            Assert.AreEqual(7, w.Inventory.FindFoodStore(stall).Total, "keeps its food");
            Assign(w, op, 3);
            w.Step(2);
            Assert.AreEqual(1, m.StallFootprintIds.Count, "reused before a new slot");
            Assert.AreEqual(3, m.FamilyOf(stall));
            Assert.AreEqual(7, w.Inventory.FindFoodStore(stall).Total);
        }

        [Test]
        public void SwitchingTheGranaryOff_LeavesStallsUnstaffedWithFood_AndSwitchingOnReuses()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2);
            w.Step(2);
            Market m = w.Markets.Current;
            int stall = m.StallFootprintIds[0];
            w.Inventory.Produce(w.Inventory.FindFoodStore(stall), Items.Find(Items.Berries.Id), 5);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, false)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(0, m.FamilyOf(stall));
            Assert.AreEqual(5, w.Inventory.FindFoodStore(stall).Total);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(2, m.FamilyOf(stall));
            Assert.AreEqual(1, m.StallFootprintIds.Count);
        }

        [Test]
        public void NoFreeSlot_FlagsTheFamilyNoRoomAtMarket_AndBuildsNothingMore()
        {
            var (w, _) = Make();
            DesignateMarket(w, 10); // 10 x 10 m holds a single slot
            int op = SupplyingGranary(w);
            Assert.AreEqual(1, StallGrid.Slots(w.Areas.Get(w.Markets.Current.AreaId).Polygon, w.Tuning).Count);
            Assign(w, op, 2); Assign(w, op, 3);
            w.Step(3);
            Market m = w.Markets.Current;
            Assert.AreEqual(1, m.StallFootprintIds.Count);
            CollectionAssert.AreEqual(new[] { 3 }, m.NoRoomFamilyIds);
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 2)).IsAccepted);
            w.Step(2);
            Assert.AreEqual(3, m.FamilyOf(m.StallFootprintIds[0]), "the freed stall is reused");
            Assert.AreEqual(0, m.NoRoomFamilyIds.Count);
        }

        [Test]
        public void DeletingTheMarket_DropsStallFoodAsGroundPile_AndRemovesStalls()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2);
            w.Step(2);
            Market m = w.Markets.Current;
            int stall = m.StallFootprintIds[0];
            PointMm at = w.Footprints.Get(stall).Centre;
            w.Inventory.Produce(w.Inventory.FindFoodStore(stall), Items.Find(Items.Berries.Id), 9);
            Assert.IsTrue(w.Execute(new DeleteArea(m.AreaId)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(stall));
            Assert.AreEqual(0, w.Markets.Count);
            LedgerLocation pile = w.Inventory.GetOrAddGroundPile(at, null);
            Assert.AreEqual(9, pile.Total, "stall food dropped as a ground pile at the stall");
            w.Step(3); // nothing throws with the market gone
        }

        [Test]
        public void SaveRoundTrip_KeepsStallFamilyLinks_AndStaysByteIdentical()
        {
            var (w, map) = Make();
            DesignateMarket(w);
            int op = SupplyingGranary(w);
            Assign(w, op, 2); Assign(w, op, 3);
            w.Step(2);
            Assert.IsTrue(w.Execute(new UnassignFamily(op, 3)).IsAccepted);
            w.Step(2);
            Market m = w.Markets.Current;
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Market bm = back.Markets.Current;
            Assert.AreEqual(m.StallFootprintIds, bm.StallFootprintIds);
            foreach (int s in m.StallFootprintIds) Assert.AreEqual(m.FamilyOf(s), bm.FamilyOf(s));
            Assert.AreEqual(2, bm.FamilyOf(m.StallFootprintIds[0]));
            Assert.AreEqual(0, bm.FamilyOf(m.StallFootprintIds[1]), "unstaffed stall survives the save");
            Assert.AreEqual(16, SaveWriter.CurrentVersion);
        }
    }
}
