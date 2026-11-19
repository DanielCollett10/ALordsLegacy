using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>The house recipe is 10 logs + 10 thatch + 10 panels + 10 daub (T-577, MAT-40, MAT-40b, MAT-43, HL-04); old sites keep logs only.</summary>
    public class HouseMaterialsTests
    {
        private const long Adult = -4_000_000;
        private const int MaxTicks = 40000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(out LedgerLocation goods)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            Support.HouseStock.Add(w, M(60, 40), 10);
            goods = w.Inventory.Locations.First(l => l.Kind == LocationKind.GoodsStore);
            w.People.AddFamily(1, "F", 10, 10);
            w.People.AddCitizen(10, "C", Sex.Male, Adult, 1, 10, 10);
            w.People.AddOx(50, "Ox", 40, 45);
            return w;
        }

        private static int Place(SimWorld w)
        {
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[1];
        }

        private static int Reserved(SimWorld w, int house, ItemDef item) =>
            w.Construction.Get(house).ReservationIds.Select(id => w.Inventory.GetReservation(id))
                .Where(r => r.ItemId == item.Id).Sum(r => r.Count);

        // HL-04, acceptance: "New house sites need 10 logs + 10 thatch + 10 panels + 10 daub; older sites keep their recipe".
        [Test]
        public void HouseRecipe_IsTenLogsTenThatchTenPanelsTenDaub_In720Minutes()
        {
            ConstructionRecipe r = ModuleTypes.House.ConstructionRecipe;
            Assert.AreEqual(10, r.Items[Items.Log.Id]);
            Assert.AreEqual(10, r.Items[Items.Thatch.Id]);
            Assert.AreEqual(10, r.Items[Items.WattlePanel.Id]);
            Assert.AreEqual(10, r.Items[Items.Daub.Id]);
            Assert.AreEqual(4, r.Items.Count);
            Assert.AreEqual(720, r.BuildWorkerMinutes);
        }

        [Test]
        public void NewHouseSite_ReservesLogsThatchPanelsAndDaub_AndUsesTheNewRecipe()
        {
            SimWorld w = Make(out _);
            int house = Place(w);
            Assert.AreEqual(10, Reserved(w, house, Items.Log));
            Assert.AreEqual(10, Reserved(w, house, Items.Thatch));
            Assert.AreEqual(10, Reserved(w, house, Items.WattlePanel));
            Assert.AreEqual(10, Reserved(w, house, Items.Daub));
            Assert.AreSame(ModuleTypes.House.ConstructionRecipe, SiteRecipe.Of(w, w.Footprints.Get(house)));
        }

        // HL-04 refusal: with no daub in stock the placement is refused and the text names daub.
        [Test]
        public void Placement_WithoutDaub_IsRefusedNamingDaub()
        {
            SimWorld w = Make(out LedgerLocation goods);
            w.Inventory.Consume(goods, Items.Daub, 10);
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Not enough daub (have 0, need 10)", r.Text);
        }

        [Test]
        public void Placement_WithoutThatch_IsRefusedWithTheShortfall()
        {
            SimWorld w = Make(out LedgerLocation goods);
            w.Inventory.Consume(goods, Items.Thatch, 10);
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(30, 51.25), M(50, 51.25), M(50, 61.25), M(30, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsRejected);
            StringAssert.Contains("thatch", r.Text);
        }

        [Test]
        public void ThatchAndPanels_AreHandCarried_OneAndTwoATrip_ByOddJobbers()
        {
            SimWorld w = Make(out _);
            int house = Place(w);
            w.Step(1);
            var carries = w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.CarryId && t.SiteFootprintId == house).ToList();
            int[] Counts(ItemDef item) => carries.Select(t => t.Def.Steps.First(s => s.Kind == StepKind.DepositItems))
                .Where(s => s.ItemId == item.Id).Select(s => s.Count).ToArray();
            Assert.AreEqual(10, Counts(Items.Thatch).Length, "ten thatch trips");
            Assert.IsTrue(Counts(Items.Thatch).All(n => n == 1), "one thatch a trip (MAT-43)");
            Assert.AreEqual(5, Counts(Items.WattlePanel).Length, "five panel trips");
            Assert.IsTrue(Counts(Items.WattlePanel).All(n => n == 2), "two panels a trip (MAT-43)");
            Assert.IsTrue(carries.All(t => t.OwnerOperationId == DeconstructionWork.HouseOwnerId), "odd-jobbers carry");
        }

        // HL-04: daub is hand-carried too, at HandCarryDaub (2) a trip, so 10 daub is five trips.
        [Test]
        public void Daub_IsHandCarried_TwoATrip_FiveTrips()
        {
            SimWorld w = Make(out _);
            int house = Place(w);
            w.Step(1);
            var carries = w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.CarryId && t.SiteFootprintId == house).ToList();
            int[] daubTrips = carries.Select(t => t.Def.Steps.First(s => s.Kind == StepKind.DepositItems))
                .Where(s => s.ItemId == Items.Daub.Id).Select(s => s.Count).ToArray();
            Assert.AreEqual(5, daubTrips.Length, "five daub trips");
            Assert.IsTrue(daubTrips.All(n => n == w.Tuning.HandCarryDaub), "two daub a trip");
        }

        // Pipeline: only commands and ticks. Thatch, panels and daub leave the General store by hand carry and are built in.
        [Test]
        public void Pipeline_PlaceHouse_ThroughTicksOnly_ConsumesAllFourMaterialsAndBuilds()
        {
            SimWorld w = Make(out LedgerLocation goods);
            int house = Place(w);
            for (int i = 0; i < MaxTicks && w.Construction.Get(house) != null; i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(house), "the house was built");
            Assert.AreEqual(0, goods.CountOf(Items.Thatch.Id));
            Assert.AreEqual(0, goods.CountOf(Items.WattlePanel.Id));
            Assert.AreEqual(0, goods.CountOf(Items.Daub.Id));
            Assert.IsNull(w.Inventory.Audit());
        }

        // A site placed before the switch holds logs only: it keeps the 10-log recipe and never asks for thatch or panels.
        [Test]
        public void OldSite_HoldingOnlyLogs_KeepsItsLogRecipe_AndIsBuiltWithoutThatchOrPanels()
        {
            SimWorld w = Make(out LedgerLocation goods);
            int house = Place(w);
            ConstructionSite site = w.Construction.Get(house);
            foreach (int id in site.ReservationIds.ToList())
            {
                Reservation r = w.Inventory.GetReservation(id);
                if (r.ItemId == Items.Log.Id) continue;
                w.Inventory.Release(id);
                site.ReservationIds.Remove(id);
            }
            Assert.AreSame(ModuleTypes.HouseLogRecipe, SiteRecipe.Of(w, w.Footprints.Get(house)));
            for (int i = 0; i < MaxTicks && w.Construction.Get(house) != null; i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(house), "built from logs alone");
            Assert.AreEqual(10, goods.CountOf(Items.Thatch.Id), "thatch untouched");
            Assert.AreEqual(10, goods.CountOf(Items.WattlePanel.Id), "panels untouched");
            Assert.AreEqual(10, goods.CountOf(Items.Daub.Id), "daub untouched");
            Assert.AreEqual(0, w.Tasks.All.Count(t => t.Def.Id == ConstructionTaskDefs.CarryId && t.State != TaskState.Done && t.State != TaskState.Cancelled));
        }

        [Test]
        public void OldSiteRecipe_IsAHouseOnly_NewSiteWithNothingYetDelivered_UsesTheNewRecipe()
        {
            SimWorld w = Make(out _);
            Assert.AreEqual(10, ModuleTypes.HouseLogRecipe.Logs);
            Assert.AreEqual(1, ModuleTypes.HouseLogRecipe.Items.Count);
            Assert.AreEqual(720, ModuleTypes.HouseLogRecipe.BuildWorkerMinutes);
            int house = Place(w);
            Assert.AreSame(ModuleTypes.House.ConstructionRecipe, SiteRecipe.Of(w, w.Footprints.Get(house)));
        }
    }
}
