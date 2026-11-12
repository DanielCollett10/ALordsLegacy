using System;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class StartLayoutTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static SimWorld Ramp() => SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());

        // Camp at (100,100) m contains tree 3 at (100,100); tree 2 (50,80) is outside.
        private static StartLayout Layout(double rot = 0, PointMm[] road = null) => new StartLayout(
            P(100000, 100000), rot, P(0, 0), P(6000, 0), P(-6000, 0),
            road ?? new[] { P(60000, 130000), P(100000, 130000), P(140000, 130000) },
            new[]
            {
                new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, -100), new StartCitizen("B", Sex.Female, -90) }),
                new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, -80) }),
            }, 2);

        [Test]
        public void Apply_AddsFamiliesCitizensOxen()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            Assert.AreEqual(2, w.People.FamilyCount);
            Assert.AreEqual(3, w.People.CitizenCount);
            Assert.AreEqual(2, w.People.OxCount);
        }

        [Test]
        public void Apply_HomeIsTentSpotAndCitizensStartThere()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            Family f = w.People.FindFamily(1);
            Assert.AreEqual(92.0, f.HomeX, 1e-9);
            Assert.AreEqual(96.0, f.HomeZ, 1e-9);
            Assert.AreEqual(2, f.MemberIds.Count);
            foreach (int id in f.MemberIds)
            {
                Citizen c = w.People.FindCitizen(id);
                Assert.AreEqual(92.0, c.X, 1e-9);
                Assert.AreEqual(96.0, c.Z, 1e-9);
            }
        }

        [Test]
        public void Apply_OxenStandAtStand()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            foreach (Ox o in w.People.Oxen)
            {
                Assert.AreEqual(94.0, o.X, 1e-9);
                Assert.AreEqual(100.0, o.Z, 1e-9);
            }
        }

        [Test]
        public void Apply_ExposesCampPoints()
        {
            var w = Ramp();
            Assert.IsNull(w.Camp);
            StartLayoutApplier.Apply(w, Layout());
            Assert.IsNotNull(w.Footprints.Get(w.Camp.FootprintId));
            Assert.AreEqual(P(100000, 100000), w.Camp.Fire);
            Assert.AreEqual(P(106000, 100000), w.Camp.Stockpile);
            Assert.AreEqual(P(94000, 100000), w.Camp.OxStand);
        }

        [Test]
        public void Apply_RotatedCamp_RotatesRelativePoints()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout(90, new PointMm[0]));
            Assert.AreEqual(P(100000, 106000), w.Camp.Stockpile);
            Assert.AreEqual(P(100000, 94000), w.Camp.OxStand);
        }

        [Test]
        public void Apply_RemovesTreesUnderCampOnly()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout(0, new PointMm[0]));
            Assert.AreEqual(4, w.Trees.Count);
            Assert.AreEqual(0, w.Trees.QueryRadius(100f, 100f, 1f).Count());
            Assert.AreEqual(1, w.Trees.QueryRadius(50f, 80f, 1f).Count());
        }

        [Test]
        public void Apply_DrawsTrunkRoad()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            Assert.Greater(w.Roads.Segments.Count(), 0);
        }

        [Test]
        public void Apply_RefusedRoad_ThrowsWithRefusalText()
        {
            var w = Ramp();
            var ex = Assert.Throws<InvalidOperationException>(() =>
                StartLayoutApplier.Apply(w, Layout(0, new[] { P(60000, 130000) })));
            StringAssert.Contains("Trunk road refused", ex.Message);
            Assert.Greater(ex.Message.Length, "Trunk road refused: ".Length);
        }

        [Test]
        public void Apply_Twice_Throws()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            Assert.Throws<InvalidOperationException>(() => StartLayoutApplier.Apply(w, Layout()));
        }

        [Test]
        public void Start_Has20LogsInCampStockpile()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            LedgerLocation sp = w.Camp.StockpileLocation;
            Assert.IsNotNull(sp);
            Assert.AreEqual(LocationKind.Stockpile, sp.Kind);
            Assert.AreEqual(20, sp.CountOf(Items.Log.Id));
            Assert.AreEqual(20, w.Inventory.TotalOf(Items.Log));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Start_StockpileLocationSitsAtStockpilePoint()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout(90));
            Assert.AreEqual(w.Camp.Stockpile, w.Camp.StockpileLocation.Position);
            Assert.AreEqual(P(100000, 106000), w.Camp.StockpileLocation.Position);
        }

        [Test]
        public void Start_LogsNotCountedInAnyOperationPool()
        {
            var w = Ramp();
            StartLayoutApplier.Apply(w, Layout());
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { P(20000, 20000), P(40000, 20000), P(40000, 40000), P(20000, 40000) }),
                AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            Assert.AreEqual(20, w.Inventory.TotalOf(Items.Log));
            OperationPool pool = w.OperationPool(op.Id, Items.Log);
            Assert.AreEqual(0, pool.Total);
            Assert.AreEqual(0, pool.Reserved);
            Assert.AreEqual(0, pool.Available);
            Assert.AreEqual(0, pool.Incoming);
        }

        [Test]
        public void Start_CustomStockReplacesDefault()
        {
            var w = Ramp();
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new StartFamily[0], 0, LordsLegacy.Sim.Roads.RoadTypes.DirtPathId, new[] { new StartStock(Items.Log, 7) });
            StartLayoutApplier.Apply(w, layout);
            Assert.AreEqual(7, w.Camp.StockpileLocation.CountOf(Items.Log.Id));
        }

        [Test]
        public void Start_EmptyStockStillHasStockpileWithNoLogs()
        {
            var w = Ramp();
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new StartFamily[0], 0, LordsLegacy.Sim.Roads.RoadTypes.DirtPathId, new StartStock[0]);
            StartLayoutApplier.Apply(w, layout);
            Assert.IsNotNull(w.Camp.StockpileLocation);
            Assert.AreEqual(0, w.Inventory.TotalOf(Items.Log));
        }

        [Test]
        public void Stock_NegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StartStock(Items.Log, -1));
        }

        [Test]
        public void DefaultLayout_Stock_IsTwentyLogs()
        {
            Assert.AreEqual(1, StartLayout.DefaultStock.Count);
            Assert.AreEqual("item.log", StartLayout.DefaultStock[0].Item.Id);
            Assert.AreEqual(20, StartLayout.DefaultStock[0].Count);
        }
    }
}
