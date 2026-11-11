using System;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
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
    }
}
