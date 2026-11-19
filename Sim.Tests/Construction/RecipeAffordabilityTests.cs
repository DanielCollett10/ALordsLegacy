using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Construction
{
    /// <summary>House affordability query (<see cref="BuildCost.Check"/>) and the DesignatePlot refusal text (T-578).</summary>
    public class RecipeAffordabilityTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static ConstructionRecipe House => ModuleTypes.House.ConstructionRecipe;

        // Logs in the camp stockpile; thatch and wattle panels in a goods store (General items come from goods stores).
        private static SimWorld Stocked(int logs, int thatch, int panels)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(0, 0)), Items.Log, logs);
            LedgerLocation store = w.Inventory.AddGoodsStore(M(5, 0));
            if (thatch > 0) w.Inventory.Produce(store, Items.Thatch, thatch);
            if (panels > 0) w.Inventory.Produce(store, Items.WattlePanel, panels);
            return w;
        }

        [Test]
        public void Check_AllItemsPresent_IsAffordable()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(10, 10, 10), House);
            Assert.IsTrue(r.IsAffordable);
            Assert.IsNull(r.ItemId);
            Assert.IsNull(r.Text);
        }

        [Test]
        public void Check_EmptyRecipe_IsAffordable()
        {
            Assert.IsTrue(BuildCost.Check(Stocked(0, 0, 0), ConstructionRecipe.Empty).IsAffordable);
        }

        [Test]
        public void Check_NothingPresent_NamesTheFirstItemInIdOrder_Logs()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(0, 0, 0), House);
            Assert.IsFalse(r.IsAffordable);
            Assert.AreEqual(Items.Log.Id, r.ItemId);
            Assert.AreEqual(0, r.Have);
            Assert.AreEqual(10, r.Need);
            Assert.AreEqual("Not enough logs (have 0, need 10)", r.Text);
        }

        [Test]
        public void Check_LogsShortOnly_NamesLogs()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(9, 10, 10), House);
            Assert.AreEqual("Not enough logs (have 9, need 10)", r.Text);
        }

        [Test]
        public void Check_ThatchShortOnly_NamesThatch()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(10, 4, 10), House);
            Assert.AreEqual(Items.Thatch.Id, r.ItemId);
            Assert.AreEqual(4, r.Have);
            Assert.AreEqual(10, r.Need);
            Assert.AreEqual("Not enough thatch (have 4, need 10)", r.Text);
        }

        [Test]
        public void Check_PanelsShortOnly_NamesWattlePanels()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(10, 10, 3), House);
            Assert.AreEqual(Items.WattlePanel.Id, r.ItemId);
            Assert.AreEqual("Not enough wattle panels (have 3, need 10)", r.Text);
        }

        [Test]
        public void Check_ThatchAndPanelsShort_NamesThatchFirst()
        {
            RecipeAffordability r = BuildCost.Check(Stocked(10, 2, 2), House);
            Assert.AreEqual("Not enough thatch (have 2, need 10)", r.Text);
        }

        [Test]
        public void Check_ReservedThatchIsNotAvailable()
        {
            SimWorld w = Stocked(10, 10, 10);
            Assert.IsNotNull(ConstructionSources.TryReserve(w, Items.Thatch, 7, M(0, 0), 1));
            RecipeAffordability r = BuildCost.Check(w, House);
            Assert.AreEqual("Not enough thatch (have 3, need 10)", r.Text);
        }

        [Test]
        public void Check_ReservedLogsAreNotAvailable()
        {
            SimWorld w = Stocked(12, 10, 10);
            Assert.IsNotNull(ConstructionSources.TryReserve(w, Items.Log, 5, M(0, 0), 1));
            Assert.AreEqual("Not enough logs (have 7, need 10)", BuildCost.Check(w, House).Text);
        }

        [Test]
        public void Check_Null_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => BuildCost.Check(null, House));
            Assert.Throws<System.ArgumentNullException>(() => BuildCost.Check(Stocked(0, 0, 0), null));
        }

        // Pipeline through the real command: a designated house reserves its materials, so the next house is short.
        [Test]
        public void Pipeline_DesignatePlot_ThenCheck_ReservedMaterialsAreNotCounted()
        {
            SimWorld w = Stocked(20, 10, 10);
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            int roadId = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(roadId, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

            CommandResult first = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(first.IsAccepted, first.Text);
            Assert.AreEqual("Not enough thatch (have 0, need 10)", BuildCost.Check(w, House).Text);
        }

        // DesignatePlot refusal text comes from the same query (SiteMaterials.Shortfall delegates to BuildCost.Check).
        [Test]
        public void DesignatePlot_ThatchShort_RefusedWithExactText()
        {
            SimWorld w = Stocked(10, 4, 10);
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            int roadId = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(roadId, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });

            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Not enough thatch (have 4, need 10)", r.Text);
            Assert.AreEqual(0, w.Areas.All.Count());
            Assert.AreEqual(0, w.Footprints.Count);
        }
    }
}
