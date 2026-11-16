using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Food
{
    /// <summary>GranaryReport read model and confirm text (T-448).</summary>
    public class GranaryReportTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            return w;
        }

        private static int Place(SimWorld w)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        // Places a granary and builds it through Sim ticks only.
        private static int BuildThroughTicks(SimWorld w)
        {
            w.People.AddFamily(2, "Helpers", 40, 40);
            for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            int id = Place(w);
            for (int i = 0; i < 4000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");
            return id;
        }

        [Test]
        public void JustPlaced_IsDelivering_Needs8Logs_Capacity100_AllFoodListedAtZero()
        {
            SimWorld w = Make();
            int id = Place(w);
            GranaryReport r = GranaryReport.Of(w, id);
            Assert.IsNotNull(r);
            Assert.AreEqual(GranaryState.Delivering, r.State);
            Assert.IsFalse(r.IsBuilt);
            Assert.AreEqual(8, r.LogsNeeded);
            Assert.AreEqual(100, r.Capacity);
            Assert.AreEqual(3, r.Stock.Count);
            Assert.IsTrue(r.Stock.All(kv => kv.Value == 0));
            CollectionAssert.AreEqual(new[] { Items.Meat.Id, Items.Berries.Id, Items.Mushrooms.Id }, r.Stock.Select(kv => kv.Key));
        }

        [Test]
        public void BuiltThroughTicks_IsBuilt_AndStockShows40Berries3Meat0Mushrooms_Total43()
        {
            SimWorld w = Make();
            int id = BuildThroughTicks(w);
            LedgerLocation store = w.Inventory.FindFoodStore(id);
            w.Inventory.Produce(store, Items.Berries, 40);
            w.Inventory.Produce(store, Items.Meat, 3);
            GranaryReport r = GranaryReport.Of(w, id);
            Assert.AreEqual(GranaryState.Built, r.State);
            Assert.IsTrue(r.IsBuilt);
            Assert.AreEqual(100, r.BuildPercent);
            Assert.AreEqual(40, r.StockOf(Items.Berries.Id));
            Assert.AreEqual(3, r.StockOf(Items.Meat.Id));
            Assert.AreEqual(0, r.StockOf(Items.Mushrooms.Id));
            Assert.AreEqual(43, r.Total);
            Assert.AreEqual(43, r.DropsItems);
        }

        [Test]
        public void Reservation_IsReflectedInReserved_TotalUnchanged()
        {
            SimWorld w = Make();
            int id = BuildThroughTicks(w);
            LedgerLocation store = w.Inventory.FindFoodStore(id);
            w.Inventory.Produce(store, Items.Berries, 40);
            Assert.AreNotEqual(0, w.Inventory.ReserveStock(store, Items.Berries, 5, 1));
            GranaryReport r = GranaryReport.Of(w, id);
            Assert.AreEqual(5, r.Reserved);
            Assert.AreEqual(40, r.Total);
        }

        [Test]
        public void MarkedForDeconstruction_IsDeconstructing_PercentRisesFromZero()
        {
            SimWorld w = Make();
            int id = BuildThroughTicks(w);
            w.Tuning.DeconstructTimeShare = 0.5;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            GranaryReport r0 = GranaryReport.Of(w, id);
            Assert.AreEqual(GranaryState.Deconstructing, r0.State);
            Assert.IsTrue(r0.IsBuilt);
            Assert.AreEqual(0, r0.DeconstructPercent);
            int last = 0;
            for (int i = 0; i < 4000 && w.Footprints.Get(id) != null && last == 0; i++)
            {
                w.Step(1);
                GranaryReport r = GranaryReport.Of(w, id);
                if (r != null) last = r.DeconstructPercent;
            }
            Assert.Greater(last, 0, "progress rises as ticks pass");
        }

        [Test]
        public void UnknownId_AndNonGranaryFootprint_ReturnNull()
        {
            SimWorld w = Make();
            Assert.IsNull(GranaryReport.Of(w, 9999));
            Assert.IsNull(GranaryReport.ConfirmText(w, 9999));
            Footprint house = w.Footprints.Add(77, ModuleTypes.House, M(30, 30), 0);
            Assert.IsNotNull(house);
            Assert.IsNull(GranaryReport.Of(w, house.Id));
        }

        [Test]
        public void ConfirmText_UnbuiltCancel_ThenBuiltRefundWith43FoodLeftOnGround()
        {
            SimWorld w = Make();
            int id = BuildThroughTicks(w);
            // Unbuilt text is checked on a second, just-placed site.
            SimWorld w2 = Make();
            int site = Place(w2);
            Assert.AreEqual("Cancel this unbuilt granary? The site is removed at once.", GranaryReport.ConfirmText(w2, site));

            Assert.AreEqual("Deconstruct this granary?\nReturns " + ModuleTypes.Granary.ConstructionRecipe.Logs / 2 + " logs.",
                GranaryReport.ConfirmText(w, id));
            LedgerLocation store = w.Inventory.FindFoodStore(id);
            w.Inventory.Produce(store, Items.Berries, 40);
            w.Inventory.Produce(store, Items.Meat, 3);
            string text = GranaryReport.ConfirmText(w, id);
            StringAssert.StartsWith("Deconstruct this granary?\nReturns ", text);
            StringAssert.EndsWith("\n43 food items are left on the ground.", text);
        }
    }
}
