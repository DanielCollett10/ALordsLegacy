using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>Placement reads its refusal text from SiteMaterials (T-515, decision 0046).</summary>
    public class PlankPlacementTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static FootprintTypeDef PlankShed() =>
            new FootprintTypeDef("structure.test_plank_shed", 8000, 6000, true, true,
                recipe: new ConstructionRecipe(new Dictionary<string, int> { { Items.Log.Id, 2 }, { Items.Plank.Id, 6 } }, 60),
                yardMarginMm: 5000);

        private static SimWorld World()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 10);
            return w;
        }

        [Test]
        public void Preview_PlanksShort_RefusedWithPlankText()
        {
            SimWorld w = World();
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(25, 25)), Items.Plank, 4);
            StructurePlacementResult r = StructurePlacement.Preview(w, PlankShed(), M(50, 50), 0);
            Assert.AreEqual(StructureRefusal.NotEnoughMaterials, r.Refusal);
            Assert.AreEqual("Not enough planks (have 4, need 6)", r.Text);
        }

        [Test]
        public void Preview_PlanksAndLogsCovered_Ok()
        {
            SimWorld w = World();
            w.Inventory.Produce(w.Inventory.AddGoodsStore(M(25, 25)), Items.Plank, 6);
            Assert.IsTrue(StructurePlacement.Preview(w, PlankShed(), M(50, 50), 0).Ok);
        }
    }
}
