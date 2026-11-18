using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>
    /// Baskets and panels leave the yard (T-562): sites reserve them from any goods store, and workshops find them
    /// through <see cref="InputSources"/> from a working storage yard only.
    /// </summary>
    public class YardBasketPanelOutflowTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var w = SimWorld.FromMapData(new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]),
                new SimTuning { StartHour = 8, StartingPlanks = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 20);
            for (int fam = 2; fam <= 4; fam++) // builders and yard staff
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, -4_000_000, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return w;
        }

        private static LedgerLocation BuiltYardStore(SimWorld w, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, at, 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id));
            return w.Inventory.FindGoodsStore(id);
        }

        private static LedgerLocation WorkshopOutput(SimWorld w, string type, PointMm at)
        {
            Footprint fp = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), at, 0);
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, fp.Id);
            WorkshopBuildings.OnBuilt(w, fp);
            return w.Inventory.FindGoodsStore(fp.Id);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Site_ReservesBasketsAndPanelsFromTheYard(int which)
        {
            SimWorld w = Make();
            ItemDef item = which == 0 ? Items.Basket : Items.WattlePanel;
            LedgerLocation yard = BuiltYardStore(w, M(100, 100));
            w.Inventory.Produce(yard, item, 3);

            Assert.AreEqual(3, ConstructionSources.Available(w, item), "the yard's goods count as sources for a site");
            var ids = ConstructionSources.TryReserve(w, item, 2, M(60, 60), 77);

            Assert.IsNotNull(ids, "a site can reserve 2 of the 3 in the yard");
            Assert.AreEqual(1, yard.AvailableOf(item.Id), "one left free in the yard");
            Assert.IsNull(w.Inventory.Audit());
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Workshop_FindsBasketsAndPanelsInTheYard(int which)
        {
            SimWorld w = Make();
            ItemDef item = which == 0 ? Items.Basket : Items.WattlePanel;
            LedgerLocation yard = BuiltYardStore(w, M(100, 100));
            w.Inventory.Produce(yard, item, 2);

            LedgerLocation found = InputSources.CreateDefault().Find(w, item, M(60, 60));

            Assert.AreSame(yard, found, "a workshop draws from the yard");
        }

        [Test]
        public void Workshop_IgnoresAWeaverOutputStore_OnlyTheYardCounts()
        {
            SimWorld w = Make();
            LedgerLocation weaver = WorkshopOutput(w, ModuleTypes.WeaverId, M(60, 60));
            w.Inventory.Produce(weaver, Items.Basket, 4);

            Assert.IsNull(InputSources.CreateDefault().Find(w, Items.Basket, M(60, 60)),
                "a workshop's own output is collected by the yard, not drawn as an input (ST-42b)");
        }

        [Test]
        public void Workshop_NearestYardWithStock_WinsOverEmptyOne()
        {
            SimWorld w = Make();
            LedgerLocation empty = BuiltYardStore(w, M(30, 30));
            LedgerLocation stocked = BuiltYardStore(w, M(100, 100));
            w.Inventory.Produce(stocked, Items.WattlePanel, 1);

            Assert.AreSame(stocked, InputSources.CreateDefault().Find(w, Items.WattlePanel, M(30, 30)),
                "an empty yard has nothing free, so the stocked one is used even though it is farther");
            Assert.AreEqual(0, empty.AvailableOf(Items.WattlePanel.Id));
        }

        [Test]
        public void Workshop_ThreeYardsAndWeaverNeighbours_FindsTheRightStoreForEachItem()
        {
            SimWorld w = Make();
            LedgerLocation yardA = BuiltYardStore(w, M(100, 100));
            LedgerLocation yardB = BuiltYardStore(w, M(40, 100));
            LedgerLocation weaver = WorkshopOutput(w, ModuleTypes.WeaverId, M(70, 70));
            LedgerLocation wattle = WorkshopOutput(w, ModuleTypes.WattleYardId, M(70, 40));
            w.Inventory.Produce(yardA, Items.Basket, 1);
            w.Inventory.Produce(yardB, Items.WattlePanel, 1);
            w.Inventory.Produce(weaver, Items.Basket, 3);
            w.Inventory.Produce(wattle, Items.WattlePanel, 3);
            InputSources s = InputSources.CreateDefault();

            Assert.AreSame(yardA, s.Find(w, Items.Basket, M(70, 70)), "basket: the yard, not the weaver's own store");
            Assert.AreSame(yardB, s.Find(w, Items.WattlePanel, M(70, 40)), "panel: the yard, not the wattle yard's own store");
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
