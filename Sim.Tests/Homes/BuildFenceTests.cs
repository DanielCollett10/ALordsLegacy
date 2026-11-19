using System;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>T-586: BuildFence, the house/fence link, its save and its removal with the house (decision 0053, HL-10..13).</summary>
    public class BuildFenceTests
    {
        private const long Adult = -4_000_000;
        private const int MaxTicks = 60000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Plot 20 x 10 m with the road frontage on one edge: fence line 10 + 20 + 10 = 40 m = 20 panels at 2 m.
        private const int PlotPanels = 20;

        private static SimWorld Make(int stock = 70)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            if (stock > 0) Support.HouseStock.Add(w, M(60, 40), stock);
            w.People.AddOx(50, "Ox", 40, 45);
            return w;
        }

        private static void AddOddJobbers(SimWorld w)
        {
            w.People.AddFamily(1, "F1", 10, 10);
            w.People.AddCitizen(10, "C1", Sex.Male, Adult, 1, 10, 10);
        }

        private static int Place(SimWorld w, double x = 30)
        {
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            CommandResult r = w.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[1];
        }

        // Shortcut: a built house with no builders needed.
        private static int BuiltHouse(SimWorld w, double x = 30)
        {
            int house = Place(w, x);
            w.Construction.Remove(house);
            return house;
        }

        private static int Fence(SimWorld w, int house)
        {
            CommandResult r = w.Execute(new BuildFence(house));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static int Panels(SimWorld w) => w.Inventory.Locations.Sum(l => l.CountOf(Items.WattlePanel.Id));

        private static int GroundPanels(SimWorld w) => w.Inventory.Locations
            .Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.CountOf(Items.WattlePanel.Id));

        [Test]
        public void Refused_UnknownHouse_NotFound()
        {
            SimWorld w = Make();
            CommandResult r = w.Execute(new BuildFence(999));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void Refused_UnbuiltHouse_NotAllowed()
        {
            SimWorld w = Make();
            int house = Place(w);
            CommandResult r = w.Execute(new BuildFence(house));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual(0, w.Homes.Get(house).FenceFootprintId);
        }

        [Test]
        public void Refused_HouseAlreadyFenced_Conflict_WhileBuildingAndWhenBuilt()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            int fence = Fence(w, house);
            CommandResult building = w.Execute(new BuildFence(house));
            Assert.IsTrue(building.IsRejected);
            Assert.AreEqual(CommandRejectCode.Conflict, building.Reason);
            w.Construction.Remove(fence);
            CommandResult built = w.Execute(new BuildFence(house));
            Assert.IsTrue(built.IsRejected);
            Assert.AreEqual(CommandRejectCode.Conflict, built.Reason);
            Assert.AreEqual(1, w.Footprints.All.Count(f => f.Type.Id == ModuleTypes.FenceId));
        }

        [Test]
        public void Refused_NotEnoughPanels_NothingChanges()
        {
            SimWorld w = Make(stock: 30); // 10 for the house, 20 left would be enough, so take 11 away
            int house = BuiltHouse(w);
            LedgerLocation goods = w.Inventory.Locations.First(l => l.CountOf(Items.WattlePanel.Id) > 0);
            w.Inventory.Consume(goods, Items.WattlePanel, goods.AvailableOf(Items.WattlePanel.Id) - (PlotPanels - 1));
            CommandResult r = w.Execute(new BuildFence(house));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            StringAssert.Contains("wattle", r.Text.ToLowerInvariant());
            Assert.AreEqual(0, w.Homes.Get(house).FenceFootprintId);
            Assert.AreEqual(0, w.Footprints.All.Count(f => f.Type.Id == ModuleTypes.FenceId));
        }

        [Test]
        public void Refused_HouseBeingDeconstructed_NotAllowed()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(house)).IsAccepted);
            Assert.IsTrue(w.Execute(new BuildFence(house)).IsRejected);
        }

        [Test]
        public void Accepted_AddsFenceFootprintAndSite_ReservesOnePanelPer2m_StateBuilding()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            Assert.AreEqual(FenceState.None, HouseFence.StateOf(w, w.Homes.Get(house)));
            int fence = Fence(w, house);
            HomeRecord h = w.Homes.Get(house);
            Assert.AreEqual(fence, h.FenceFootprintId);
            Assert.AreEqual(ModuleTypes.FenceId, w.Footprints.Get(fence).Type.Id);
            Assert.AreEqual(FenceLine.Of(w.Areas.Get(h.PlotAreaId)).AccessPoint, w.Footprints.Get(fence).Centre);
            ConstructionSite site = w.Construction.Get(fence);
            Assert.IsNotNull(site);
            Assert.AreEqual(PlotPanels, site.ReservationIds.Sum(id => w.Inventory.GetReservation(id).Count));
            Assert.AreEqual(FenceState.Building, HouseFence.StateOf(w, h));
            Assert.AreEqual(PlotPanels * w.Tuning.FenceMinutesPerPanel, SiteRecipe.Of(w, w.Footprints.Get(fence)).BuildWorkerMinutes);
            Assert.IsTrue(w.IsBuilt(house), "the house stays built");
        }

        // Pipeline test: no Build()/site shortcut. Designate, house built by odd-jobbers, BuildFence, fence built by
        // hand carry and builders, then the house is deconstructed and the fence goes with it (HL-13).
        [Test]
        public void Pipeline_HouseBuilt_FenceBuiltAfterDelivery_ThenRemovedWithHouseAndRefunded()
        {
            SimWorld w = Make();
            AddOddJobbers(w);
            int house = Place(w);
            for (int i = 0; i < MaxTicks && !w.IsBuilt(house); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(house), "the house was built");

            int fence = Fence(w, house);
            for (int i = 0; i < MaxTicks && !w.IsBuilt(fence); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fence), "the fence was built after its panels were delivered");
            Assert.AreEqual(FenceState.Built, HouseFence.StateOf(w, w.Homes.Get(house)));
            Assert.IsNull(w.Inventory.Audit());

            int before = GroundPanels(w);
            var houseRefunds = DeconstructionWork.Refunds(w, w.Footprints.Get(house));
            int plot = w.Homes.Get(house).PlotAreaId;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(house)).IsAccepted);
            for (int i = 0; i < MaxTicks && w.Footprints.Get(house) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(house));
            Assert.IsNull(w.Footprints.Get(fence), "the fence goes with its house");
            Assert.IsNull(w.Homes.Get(house));
            Assert.IsNull(w.Areas.Get(plot));
            int fencePanels = (int)Math.Floor(PlotPanels * w.Tuning.DeconstructRefundShare + 1e-9);
            int housePanels = houseRefunds.TryGetValue(Items.WattlePanel.Id, out int hp) ? hp : 0;
            Assert.Greater(fencePanels, 0);
            Assert.AreEqual(fencePanels + housePanels, GroundPanels(w) - before,
                "HL-13: normal deconstruction share of the fence panels returns, besides the house's own panels");
        }

        [Test]
        public void RemoveBuiltHouse_WithBuiltFence_RefundsShareOfPanelsAtTheMarker()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            int fence = Fence(w, house);
            PointMm marker = w.Footprints.Get(fence).Centre;
            w.Construction.Remove(fence);
            int before = GroundPanels(w);
            HouseRemoval.RemoveWithPlot(w, house);
            int expected = (int)Math.Floor(PlotPanels * w.Tuning.DeconstructRefundShare + 1e-9);
            Assert.AreEqual(expected, GroundPanels(w) - before);
            Assert.IsTrue(w.Inventory.Locations.Any(l => l.Kind == LocationKind.GroundPile && l.Position == marker && l.CountOf(Items.WattlePanel.Id) >= expected));
            Assert.IsNull(w.Footprints.Get(fence));
            Assert.IsNull(w.Homes.Get(house));
        }

        [Test]
        public void RemoveHouse_WithFenceStillBuilding_CancelsSiteAndReleasesReservations()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            int fence = Fence(w, house);
            int availableBefore = w.Inventory.Locations.Sum(l => l.AvailableOf(Items.WattlePanel.Id));
            HouseRemoval.RemoveWithPlot(w, house);
            Assert.IsNull(w.Footprints.Get(fence));
            Assert.IsNull(w.Construction.Get(fence));
            int availableAfter = w.Inventory.Locations.Sum(l => l.AvailableOf(Items.WattlePanel.Id));
            Assert.AreEqual(availableBefore + PlotPanels, availableAfter, "the reserved panels are free again");
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void HouseWithoutFence_Removal_IsUnchanged()
        {
            SimWorld w = Make();
            int house = BuiltHouse(w);
            int before = GroundPanels(w);
            HouseRemoval.RemoveWithPlot(w, house);
            Assert.AreEqual(before, GroundPanels(w));
            Assert.IsNull(w.Homes.Get(house));
        }

        [Test]
        public void SaveRoundTrip_MidBuild_KeepsLinkAndSite_AndFinishesIdentically()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make();
            AddOddJobbers(w);
            int house = BuiltHouse(w);
            int fence = Fence(w, house);
            w.Step(400); // some panels carried, fence not finished
            Assert.AreEqual(FenceState.Building, HouseFence.StateOf(w, w.Homes.Get(house)));
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 39);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(fence, back.Homes.Get(house).FenceFootprintId);
            Assert.AreEqual(FenceState.Building, HouseFence.StateOf(back, back.Homes.Get(house)));
            Assert.IsNotNull(back.Construction.Get(fence));
            Assert.AreEqual(PlotPanels * back.Tuning.FenceMinutesPerPanel, SiteRecipe.Of(back, back.Footprints.Get(fence)).BuildWorkerMinutes);
            Assert.IsNull(SaveDiff.FirstDifference(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash)));
            for (int i = 0; i < MaxTicks && !back.IsBuilt(fence); i++) { back.Step(1); w.Step(1); }
            Assert.IsTrue(back.IsBuilt(fence));
            Assert.AreEqual(FenceState.Built, HouseFence.StateOf(back, back.Homes.Get(house)));
            Assert.AreEqual(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash), SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveRoundTrip_BuiltFenceAndUnfencedHouse_BothSurvive()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make();
            int a = BuiltHouse(w, 30), b = BuiltHouse(w, 55);
            int fence = Fence(w, a);
            w.Construction.Remove(fence);
            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash), map);
            Assert.AreEqual(FenceState.Built, HouseFence.StateOf(back, back.Homes.Get(a)));
            Assert.AreEqual(FenceState.None, HouseFence.StateOf(back, back.Homes.Get(b)));
            Assert.AreEqual(0, back.Homes.Get(b).FenceFootprintId);
        }

        [Test]
        public void OldSaveVersion_IsRefused()
        {
            MapData map = SyntheticMap.CreateRamp();
            byte[] bytes = SimSave.Write(Make(), "s", 1, map.MapId, map.ContentHash);
            BitConverter.GetBytes(SaveWriter.CurrentVersion - 1).CopyTo(bytes, 4);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }
    }
}
