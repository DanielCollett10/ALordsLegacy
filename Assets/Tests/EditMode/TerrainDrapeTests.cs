using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.EditMode
{
    public class TerrainDrapeTests
    {
        private sealed class Bumpy : IHeightField
        {
            public Bounds2 Bounds => Bounds2.Empty;
            public bool Contains(float x, float z) => true;
            public float HeightAt(float x, float z) => 2f * Mathf.Sin(x * 0.9f) + 1.5f * Mathf.Cos(z * 0.7f + x * 0.3f);
            public float SlopeDegAt(float x, float z) => 0f;
        }

        /// <summary>Ridge along z = 0: high in the middle, falling off both sides.</summary>
        private sealed class Ridge : IHeightField
        {
            public Bounds2 Bounds => Bounds2.Empty;
            public bool Contains(float x, float z) => true;
            public float HeightAt(float x, float z) => Mathf.Max(0f, 6f - Mathf.Abs(z) * 1.5f);
            public float SlopeDegAt(float x, float z) => 0f;
        }

        private static readonly Vector2[] Line100 = { new Vector2(0, 5), new Vector2(100, 5) };

        [Test]
        public void Densify_KeepsInputPointsAndLimitsSteps()
        {
            var input = new[] { new Vector2(0, 0), new Vector2(3.3f, 1f), new Vector2(10, 10) };
            var d = TerrainDrape.Densify(input, 1f);
            foreach (var p in input) CollectionAssert.Contains(d, p);
            for (int i = 1; i < d.Count; i++) Assert.LessOrEqual((d[i] - d[i - 1]).magnitude, 1.0001f);
        }

        [Test]
        public void Ribbon_StraightLine_FollowsGroundWithinOneCm()
        {
            var t = new Bumpy();
            var m = TerrainDrape.Ribbon(t, Line100, 0.4f, 0.1f, Color.white, false, false);
            Assert.Greater(m.Vertices.Count, 200);
            foreach (var v in m.Vertices)
                Assert.AreEqual(t.HeightAt(v.x, v.z) + 0.1f, v.y, 0.01f);
            // left-hand edge (even vertices) steps no more than 1 m
            for (int i = 2; i < m.Vertices.Count; i += 2)
                Assert.LessOrEqual(Plan(m.Vertices[i], m.Vertices[i - 2]), 1.0001f);
        }

        [Test]
        public void Densify_StraightLine_PlanPointsAreCollinear()
        {
            var d = TerrainDrape.Densify(Line100, 1f);
            Assert.GreaterOrEqual(d.Count, 101);
            foreach (var p in d) Assert.AreEqual(5f, p.y, 1e-4f);
            for (int i = 1; i < d.Count; i++) Assert.LessOrEqual((d[i] - d[i - 1]).magnitude, 1.0001f);
        }

        [Test]
        public void Ribbon_Dashed_DashesAreDensifiedAndDraped()
        {
            var t = new Bumpy();
            var m = TerrainDrape.Ribbon(t, Line100, 0.4f, 0.1f, Color.white, false, true);
            Assert.Greater(m.Vertices.Count, 0);
            foreach (var v in m.Vertices) Assert.AreEqual(t.HeightAt(v.x, v.z) + 0.1f, v.y, 0.01f);
        }

        [Test]
        public void Fill_AllTriangleEdgesAtMostTwoMetres()
        {
            var t = new Bumpy();
            var poly = new[] { PointMm.FromMetres(0, 0), PointMm.FromMetres(30, 0), PointMm.FromMetres(30, 20), PointMm.FromMetres(0, 20) };
            var m = TerrainDrape.Fill(t, poly, 0.1f, Color.white);
            Assert.Greater(m.Triangles.Count, 0);
            for (int i = 0; i < m.Triangles.Count; i += 3)
            {
                var a = m.Vertices[m.Triangles[i]]; var b = m.Vertices[m.Triangles[i + 1]]; var c = m.Vertices[m.Triangles[i + 2]];
                Assert.LessOrEqual(Plan(a, b), 2.0001f);
                Assert.LessOrEqual(Plan(b, c), 2.0001f);
                Assert.LessOrEqual(Plan(c, a), 2.0001f);
            }
        }

        [Test]
        public void Fill_FourPointAreaOverRidge_NoVertexBelowGround()
        {
            var t = new Ridge();
            var poly = new[] { PointMm.FromMetres(-10, -8), PointMm.FromMetres(10, -8), PointMm.FromMetres(10, 8), PointMm.FromMetres(-10, 8) };
            var m = TerrainDrape.Fill(t, poly, 0.05f, Color.white);
            foreach (var v in m.Vertices) Assert.GreaterOrEqual(v.y, t.HeightAt(v.x, v.z) - 1e-4f);
            Assert.IsTrue(m.Vertices.Exists(v => Mathf.Abs(v.z) < 0.01f && v.y > 5.9f), "a vertex sits on the ridge crest");
        }

        [Test]
        public void Point_AddsLiftToHeight()
        {
            var t = new Bumpy();
            var p = TerrainDrape.Point(t, 3f, 4f, 0.2f);
            Assert.AreEqual(t.HeightAt(3f, 4f) + 0.2f, p.y, 1e-5f);
        }

        private static float Plan(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}
