using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Areas
{
    /// <summary>T-585: the fence footprint type and the derived fence line / panel count (decision 0053, HL-10..12c).</summary>
    public class FenceLineTests
    {
        private const AreaEdgeKind F = AreaEdgeKind.FollowsRoad, S = AreaEdgeKind.Straight;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static AreaRecord Plot(PointMm[] v, IReadOnlyList<PointMm> polygon, params AreaEdgeKind[] kinds)
        {
            var e = new AreaEdge[v.Length];
            for (int i = 0; i < e.Length; i++)
                e[i] = kinds[i] == F ? AreaEdge.FollowsRoad(1, RoadSide.Left) : AreaEdge.Straight();
            return new AreaRecord(1, AreaTypes.PlotId, "plot", new AreaShape(v, e), polygon);
        }

        private static AreaRecord Plot(PointMm[] v, params AreaEdgeKind[] kinds) => Plot(v, v, kinds);

        private static PointMm[] Rect(double w, double d) => new[] { M(0, 0), M(w, 0), M(w, d), M(0, d) };

        [Test]
        public void FenceType_ClearsNoTreesAndDoesNotBlock_SmallMarker()
        {
            FootprintTypeDef t = ModuleTypes.Fence;
            Assert.AreEqual("footprint.fence", t.Id);
            Assert.IsFalse(t.ClearsTrees);
            Assert.IsFalse(t.BlocksMovement);
            Assert.AreEqual(1000, t.WidthMm);
            Assert.AreEqual(1000, t.DepthMm);
            Assert.AreSame(t, ModuleTypes.Find(ModuleTypes.FenceId));
        }

        [Test]
        public void OneFrontageEdge_LineIsThreeOtherEdges_FrontageLeftOpen()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, S, S, S));
            Assert.AreEqual(34000, line.LengthMm, 1e-6); // 12 + 10 + 12
            Assert.AreEqual(1, line.Runs.Count);
            Assert.AreEqual(M(10, 0), line.Runs[0][0]);
            Assert.AreEqual(M(0, 0), line.Runs[0][line.Runs[0].Count - 1]);
            Assert.IsTrue(line.HasFrontage);
            Assert.AreEqual(M(5, 0), line.AccessPoint);
        }

        [Test]
        public void OneFrontageEdge_NotFirstEdge_RunsStillMergeAcrossWrap()
        {
            // Frontage is edge 2; edges 3, 0 and 1 are fenced and join into one run through the wrap.
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), S, S, F, S));
            Assert.AreEqual(1, line.Runs.Count);
            Assert.AreEqual(10000 + 12000 + 12000, line.LengthMm, 1e-6);
            Assert.AreEqual(M(5, 12), line.AccessPoint);
        }

        [Test]
        public void TwoFrontageEdges_Adjacent_LineIsTheTwoOthers()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, F, S, S));
            Assert.AreEqual(10000 + 12000, line.LengthMm, 1e-6);
            Assert.AreEqual(1, line.Runs.Count);
        }

        [Test]
        public void TwoFrontageEdges_Opposite_LineIsTwoSeparateRuns()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, S, F, S));
            Assert.AreEqual(2, line.Runs.Count);
            Assert.AreEqual(24000, line.LengthMm, 1e-6);
            Assert.AreEqual(M(5, 0), line.AccessPoint); // equal lengths: lowest edge index
        }

        [Test]
        public void LongestFrontageEdge_ChoosesTheAccessPoint()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, F, S, S));
            Assert.AreEqual(M(10, 6), line.AccessPoint); // edge 1 is 12 m, edge 0 is 10 m
        }

        [Test]
        public void CurvedFencedEdge_LengthFollowsTheFlattenedCurve_LongerThanChord()
        {
            var v = Rect(10, 10);
            var edges = new[] { AreaEdge.FollowsRoad(1, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Bent(M(5, 13)), AreaEdge.Straight() };
            var shape = new AreaShape(v, edges);
            var rec = new AreaRecord(1, AreaTypes.PlotId, "plot", shape, AreaFlattener.Flatten(shape, null));
            FenceLine line = FenceLine.Of(rec);
            Assert.Greater(line.LengthMm, 30000 + 1000, "curved top edge is longer than its 10 m chord");
            Assert.AreEqual(1, line.Runs.Count);
        }

        [Test]
        public void CurvedFrontageEdge_IsExcludedWholeAlongItsFlattenedPoints()
        {
            // Frontage edge 0 curves through (5,-2); the flattened polygon carries the extra points.
            var v = Rect(10, 10);
            var poly = new[] { M(0, 0), M(5, -2), M(10, 0), M(10, 10), M(0, 10) };
            AreaRecord rec = Plot(v, poly, F, S, S, S);
            FenceLine line = FenceLine.Of(rec);
            Assert.AreEqual(30000, line.LengthMm, 1e-6); // only the three straight edges
            Assert.AreEqual(M(5, -2), line.AccessPoint); // middle of the curved frontage by length
        }

        [Test]
        public void NoFrontage_WholeRingIsFenced_NoAccessFlag()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), S, S, S, S));
            Assert.IsFalse(line.HasFrontage);
            Assert.AreEqual(44000, line.LengthMm, 1e-6);
        }

        [Test]
        public void AllFrontage_LineIsEmpty()
        {
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, F, F, F));
            Assert.AreEqual(0, line.Runs.Count);
            Assert.AreEqual(0, line.LengthMm);
            Assert.AreEqual(0, line.PanelCount(new SimTuning()));
        }

        [Test]
        public void Degenerate_TwoPointPlot_IsEmptyNotACrash()
        {
            var v = new[] { M(0, 0), M(5, 0) };
            FenceLine line = FenceLine.Of(Plot(v, S, S));
            Assert.AreEqual(0, line.LengthMm);
            Assert.AreEqual(0, line.Runs.Count);
        }

        [Test]
        public void NullPlot_Throws() => Assert.Throws<ArgumentNullException>(() => FenceLine.Of(null));

        [TestCase(30000.0, 15)]   // 30 m / 2 m exactly
        [TestCase(30001.0, 16)]   // a millimetre over rounds up
        [TestCase(1.0, 1)]
        [TestCase(1999.0, 1)]
        [TestCase(2000.0, 1)]
        [TestCase(2001.0, 2)]
        public void PanelCount_RoundsUp_PerTwoMetres(double lengthMm, int expected)
        {
            Assert.AreEqual(expected, PanelCountOfLength(lengthMm, new SimTuning()));
        }

        // A line of exactly the given length: a plot whose single fenced run is one straight edge, the other edges frontage.
        private static int PanelCountOfLength(double lengthMm, SimTuning t)
        {
            var v = new[] { new PointMm(0, 0), new PointMm((long)lengthMm, 0), new PointMm((long)lengthMm, 5000) };
            AreaRecord rec = Plot(v, S, F, F);
            FenceLine line = FenceLine.Of(rec);
            Assert.AreEqual(lengthMm, line.LengthMm, 1e-6);
            return line.PanelCount(t);
        }

        [Test]
        public void Recipe_PanelsAreWattle_MinutesTwentyEach()
        {
            var t = new SimTuning();
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, S, S, S)); // 34 m -> 17 panels
            Assert.AreEqual(17, line.PanelCount(t));
            ConstructionRecipe r = line.Recipe(t);
            Assert.AreEqual(17, r.Items[Items.WattlePanel.Id]);
            Assert.AreEqual(17 * 20, r.BuildWorkerMinutes);
        }

        [Test]
        public void Recipe_FollowsTuning_PanelLengthAndMinutes()
        {
            var t = new SimTuning { FencePanelLengthM = 3.0, FenceMinutesPerPanel = 7 };
            FenceLine line = FenceLine.Of(Plot(Rect(10, 12), F, S, S, S)); // 34 m / 3 m -> 12 panels
            Assert.AreEqual(12, line.PanelCount(t));
            Assert.AreEqual(12 * 7, line.Recipe(t).BuildWorkerMinutes);
        }

        [Test]
        public void Defaults_AreTwoMetresAndTwentyMinutes()
        {
            var t = new SimTuning();
            Assert.AreEqual(2.0, t.FencePanelLengthM, 1e-9);
            Assert.AreEqual(20, t.FenceMinutesPerPanel);
        }
    }
}
