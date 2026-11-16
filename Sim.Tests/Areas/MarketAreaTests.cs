using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>area.market validation, the one-market rule, the registry and its save section (T-456, MK-01..05).</summary>
    public class MarketAreaTests
    {
        private const string Mk = AreaTypes.MarketId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld Flat() => SimWorld.FromMapData(Map(), new SimTuning());

        private static AreaShape Rect(double x, double z, double w, double h) =>
            AreaShape.Straight(new[] { M(x, z), M(x + w, z), M(x + w, z + h), M(x, z + h) });

        private static string Reason(SimWorld w, AreaShape s) => AreaValidator.Validate(w, s, Mk).ReasonText;

        [Test]
        public void Market_IsRegisteredBuildingAreaWithoutFrontage()
        {
            AreaTypeDef t = AreaTypes.Market;
            Assert.AreSame(t, new AreaTypeCatalog(AreaTypes.All).Find(Mk));
            Assert.AreEqual(AreaKind.Building, t.Kind);
            Assert.IsFalse(t.RequiresRoadFrontage);
        }

        [Test]
        public void Valid_TwentyByTwentyIsAccepted()
        {
            Assert.IsTrue(AreaValidator.Validate(Flat(), Rect(30, 30, 20, 20), Mk).IsValid);
        }

        [Test]
        public void UnderMinimumArea_RefusedTooSmallForAMarket()
        {
            Assert.AreEqual("Too small for a market", Reason(Flat(), Rect(30, 30, 9, 10))); // 90 m²
        }

        [Test]
        public void OverMaximumArea_RefusedTooLarge()
        {
            Assert.AreEqual("too large", Reason(Flat(), Rect(10, 10, 50, 41))); // 2,050 m²
        }

        [Test]
        public void ExactlyMinAndMaxArea_AreAccepted()
        {
            Assert.IsTrue(AreaValidator.Validate(Flat(), Rect(30, 30, 10, 10), Mk).IsValid); // 100 m²
            Assert.IsTrue(AreaValidator.Validate(Flat(), Rect(10, 10, 50, 40), Mk).IsValid); // 2,000 m²
        }

        [Test]
        public void ThinStripOverMinimumArea_RefusedTooSmallForAMarket()
        {
            var w = Flat();
            AreaPreview p = AreaValidator.Validate(w, Rect(10, 30, 80, 3), Mk); // 240 m² but 3 m wide: no slot
            Assert.GreaterOrEqual(p.AreaM2, 100);
            Assert.AreEqual(AreaRefusal.MarketTooSmall, p.Reason);
            Assert.AreEqual("Too small for a market", p.ReasonText);
            Assert.AreEqual(0, StallGrid.Slots(p.Polygon, w.Tuning).Count);
        }

        [Test]
        public void ZeroSlotsRefusal_UsesTheStallGrid()
        {
            var w = Flat();
            AreaShape ok = Rect(30, 30, 20, 20);
            AreaPreview p = AreaValidator.Validate(w, ok, Mk);
            Assert.IsTrue(p.IsValid);
            Assert.Greater(StallGrid.Slots(p.Polygon, w.Tuning).Count, 0);
            w.Tuning.StallCellWidthM = 100; // no cell can fit now
            Assert.AreEqual("Too small for a market", Reason(w, ok));
        }

        [Test]
        public void TuningMinAndMax_AreUsed()
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { MarketMinAreaM2 = 400, MarketMaxAreaM2 = 500 });
            Assert.AreEqual("Too small for a market", Reason(w, Rect(30, 30, 18, 20))); // 360
            Assert.AreEqual("too large", Reason(w, Rect(30, 30, 30, 20))); // 600
            Assert.IsTrue(AreaValidator.Validate(w, Rect(30, 30, 22, 20), Mk).IsValid); // 440
        }

        [Test]
        public void NoFrontageNeeded_AwayFromAnyRoad()
        {
            Assert.AreNotEqual("A plot must face a road", Reason(Flat(), Rect(30, 30, 20, 20)));
            Assert.IsTrue(AreaValidator.Validate(Flat(), Rect(30, 30, 20, 20), Mk).IsValid);
        }

        [Test]
        public void OverlapsAnotherBuildingArea_Refused()
        {
            var w = Flat();
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 20, 20), AreaTypes.TestId)).IsAccepted);
            Assert.That(Reason(w, Rect(35, 35, 20, 20)), Does.StartWith("overlaps"));
        }

        [Test]
        public void CrossesRoad_Refused()
        {
            var w = Flat();
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(10, 40), M(90, 40) }, new[] { M(10, 40), M(50, 40), M(90, 40) });
            Assert.AreEqual("crosses road", Reason(w, Rect(30, 30, 20, 20)));
        }

        [Test]
        public void SteepGround_Refused()
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRidge(), new SimTuning());
            Assert.AreEqual("too steep", Reason(w, Rect(50, 50, 20, 20)));
        }

        [Test]
        public void SecondMarket_RefusedOnlyOneMarketPerTown()
        {
            var w = Flat();
            Assert.AreEqual(string.Empty, MarketRules.CanPlaceMarket(w));
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 20, 20), Mk)).IsAccepted);
            Assert.AreEqual("Only one market per town", MarketRules.CanPlaceMarket(w));
            CommandResult r = w.Execute(new DesignateArea(Rect(60, 60, 20, 20), Mk));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Only one market per town", r.Text);
            Assert.AreEqual(1, w.Markets.Count);
        }

        [Test]
        public void CanPlaceMarket_IsPure()
        {
            var w = Flat();
            int areas = w.Areas.All.Count(), version = w.Markets.Version;
            MarketRules.CanPlaceMarket(w);
            Assert.AreEqual(areas, w.Areas.All.Count());
            Assert.AreEqual(version, w.Markets.Version);
        }

        [Test]
        public void Designate_CreatesMarketRecordForTheArea()
        {
            var w = Flat();
            CommandResult r = w.Execute(new DesignateArea(Rect(30, 30, 20, 20), Mk));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int areaId = (int)r.Created[0];
            Market m = w.Markets.FindByArea(areaId);
            Assert.NotNull(m);
            Assert.AreEqual(areaId, m.AreaId);
            Assert.AreEqual(1, m.Id);
            Assert.AreEqual(0, m.StallFootprintIds.Count);
            Assert.AreSame(m, w.Markets.Current);
            Assert.AreEqual(Mk, w.Areas.Get(areaId).TypeId);
        }

        [Test]
        public void RejectedDesignate_CreatesNoMarket()
        {
            var w = Flat();
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 9, 10), Mk)).IsRejected);
            Assert.AreEqual(0, w.Markets.Count);
        }

        [Test]
        public void MarketArea_DoesNotBlockPathing()
        {
            var w = Flat();
            w.Nav.Sync(w);
            int ix = w.Nav.CellX(40), iz = w.Nav.CellZ(40);
            Assert.IsFalse(w.Nav.IsBlocked(ix, iz));
            Assert.IsTrue(w.Execute(new DesignateArea(Rect(30, 30, 20, 20), Mk)).IsAccepted);
            w.Nav.Sync(w);
            Assert.IsFalse(w.Nav.IsBlocked(ix, iz));
        }

        [Test]
        public void DeleteArea_RemovesTheMarketRecord_AndAllowsANewMarket()
        {
            var w = Flat();
            int areaId = (int)w.Execute(new DesignateArea(Rect(30, 30, 20, 20), Mk)).Created[0];
            Assert.IsTrue(w.Execute(new DeleteArea(areaId)).IsAccepted);
            Assert.AreEqual(0, w.Markets.Count);
            Assert.IsNull(w.Markets.FindByArea(areaId));
            Assert.AreEqual(string.Empty, MarketRules.CanPlaceMarket(w));
            CommandResult again = w.Execute(new DesignateArea(Rect(60, 60, 20, 20), Mk));
            Assert.IsTrue(again.IsAccepted, again.Text);
            Assert.AreEqual(2, w.Markets.Current.Id); // ids are not reused
        }

        [Test]
        public void Registry_AddTwiceForSameAreaThrows_RemoveUnknownIsFalse()
        {
            var reg = new MarketRegistry();
            reg.Add(5);
            Assert.Throws<System.ArgumentException>(() => reg.Add(5));
            Assert.IsFalse(reg.RemoveByArea(99));
            Assert.IsNull(reg.Current == null ? null : reg.FindByArea(99));
        }

        [Test]
        public void SaveRoundTrip_MarketSurvives()
        {
            MapData map = Map();
            var w = SimWorld.FromMapData(map, new SimTuning { MarketMinAreaM2 = 150, MarketMaxAreaM2 = 1800 });
            int areaId = (int)w.Execute(new DesignateArea(Rect(30, 30, 20, 20), Mk)).Created[0];
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(1, back.Markets.Count);
            Market m = back.Markets.Current;
            Assert.AreEqual(areaId, m.AreaId);
            Assert.AreEqual(w.Markets.Current.Id, m.Id);
            Assert.AreEqual(w.Markets.NextIdValue, back.Markets.NextIdValue);
            Assert.AreEqual(150, back.Tuning.MarketMinAreaM2);
            Assert.AreEqual(1800, back.Tuning.MarketMaxAreaM2);
            Assert.AreEqual("Only one market per town", MarketRules.CanPlaceMarket(back));
            CollectionAssert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveRoundTrip_NoMarket_StaysEmpty()
        {
            MapData map = Map();
            SimWorld back = SimSave.Read(SimSave.Write(Flat(), "s", 1, map.MapId, map.ContentHash), map);
            Assert.AreEqual(0, back.Markets.Count);
        }

        [Test]
        public void MarketsSection_IsRegistered()
        {
            Assert.IsTrue(SaveSections.CreateDefault().Any(s => s.Id == LordsLegacy.Sim.Save.Sections.MarketsSection.SectionId));
        }
    }
}
