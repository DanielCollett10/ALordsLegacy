using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="AreaRenderer"/> with a synthetic SimHost.</summary>
    public class AreaRendererTests
    {
        private GameObject _hostGo;
        private GameObject _rendererGo;
        private SimHost _host;
        private AreaRenderer _renderer;

        [TearDown]
        public void TearDown()
        {
            if (_rendererGo != null) Object.DestroyImmediate(_rendererGo);
            if (_hostGo != null) Object.DestroyImmediate(_hostGo);
        }

        private void Setup()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            _rendererGo = new GameObject("TestAreaRenderer");
            _renderer = _rendererGo.AddComponent<AreaRenderer>();
            _renderer.Configure(_host);
        }

        private static AreaShape Square(double x, double z, double size) =>
            AreaShape.Straight(new[]
            {
                PointMm.FromMetres(x, z), PointMm.FromMetres(x + size, z),
                PointMm.FromMetres(x + size, z + size), PointMm.FromMetres(x, z + size)
            });

        [UnityTest]
        public IEnumerator DesignatedAreasAreDrawnAndRebuiltOnVersionChange()
        {
            Setup();
            var r1 = _host.Execute(new DesignateArea(Square(30, 30, 12), AreaTypes.TestId));
            Assert.IsTrue(r1.IsAccepted, r1.ToString());
            var r2 = _host.Execute(new DesignateArea(Square(60, 30, 20), AreaTypes.TestWorkId));
            Assert.IsTrue(r2.IsAccepted, r2.ToString());
            yield return null;
            _renderer.Sync();

            Assert.AreEqual(2, _renderer.AreaCount);
            int id1 = 1, id2 = 2;
            foreach (var a in _host.World.Areas.All) { if (a.TypeId == AreaTypes.TestId) id1 = a.Id; else id2 = a.Id; }
            var m1 = _renderer.GetAreaMesh(id1);
            var m2 = _renderer.GetAreaMesh(id2);
            Assert.IsNotNull(m1);
            Assert.IsNotNull(m2);
            Assert.Greater(m1.vertexCount, 8);

            // Draped on the ramp (height = 0.1 * x): the mesh spans the slope.
            var b = m1.bounds;
            Assert.AreEqual(0.1f * 12f, b.size.y, 0.3f, "Area mesh should follow the ramp over its 12 m width.");
            Assert.AreEqual(0.1f * 30f + AreaRenderer.HeightOffsetM, b.min.y, 0.05f);

            // Building and work areas differ: work is dashed blue, building solid warm.
            Assert.IsTrue(HasColor(m1, AreaRenderer.BuildingOutline));
            Assert.IsFalse(HasColor(m1, AreaRenderer.WorkOutline));
            Assert.IsTrue(HasColor(m2, AreaRenderer.WorkOutline));
            Assert.IsFalse(HasColor(m2, AreaRenderer.BuildingOutline));

            // Version change: delete one area, the mesh goes.
            Assert.IsTrue(_host.Execute(new DeleteArea(id1)).IsAccepted);
            _renderer.Sync();
            Assert.AreEqual(1, _renderer.AreaCount);
            Assert.IsNull(_renderer.GetAreaMesh(id1));
            Assert.IsNotNull(_renderer.GetAreaMesh(id2));
        }

        [UnityTest]
        public IEnumerator AreaOverARidgeIsDenseAndNeverBelowGround()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRidge());
            _host.AutoUpdate = false;
            _rendererGo = new GameObject("TestAreaRenderer");
            _renderer = _rendererGo.AddComponent<AreaRenderer>();
            _renderer.Configure(_host);

            Assert.IsTrue(_host.Execute(new DesignateArea(Square(50, 30, 20), AreaTypes.TestWorkId)).IsAccepted); // work type: no slope limit (the ridge is steep)
            yield return null;
            _renderer.Sync();
            int id = 1;
            foreach (var a in _host.World.Areas.All) id = a.Id;
            var mesh = _renderer.GetAreaMesh(id);
            Assert.IsNotNull(mesh);
            var verts = mesh.vertices;
            var cols = mesh.colors;
            var terrain = _host.World.Terrain;
            bool sawRidge = false;
            for (int i = 0; i < verts.Length; i++)
            {
                float ground = terrain.HeightAt(verts[i].x, verts[i].z);
                Assert.GreaterOrEqual(verts[i].y, ground, "vertex below ground at " + verts[i]);
                if (ground > 4f) sawRidge = true;
                bool outline = cols[i].a > 0.5f;
                float limit = outline ? 1.1f : 2.1f;
                float best = float.MaxValue;
                for (int j = 0; j < verts.Length; j++)
                {
                    if (j == i || (cols[j].a > 0.5f) != outline) continue;
                    var d = new Vector2(verts[i].x - verts[j].x, verts[i].z - verts[j].z).magnitude;
                    if (d > 1e-4f && d < best) best = d;
                }
                Assert.LessOrEqual(best, limit, "vertex " + verts[i] + " has no neighbour within " + limit + " m");
            }
            Assert.IsTrue(sawRidge, "test area should cross the ridge crest");
        }

        [UnityTest]
        public IEnumerator PreviewIsRedWhenInvalidAndHidesAgain()
        {
            Setup();
            yield return null;
            var good = Square(30, 30, 12);
            var okPreview = AreaValidator.Validate(_host.World, good, AreaTypes.TestId);
            Assert.IsTrue(okPreview.IsValid, okPreview.ReasonText);
            _renderer.ShowPreview(okPreview, good);
            var mesh = _renderer.PreviewMesh;
            Assert.IsNotNull(mesh);
            Assert.IsTrue(HasColor(mesh, AreaRenderer.DotColor), "Vertex dots");
            Assert.IsTrue(HasColor(mesh, AreaRenderer.HandleColor), "Edge handles");
            Assert.IsFalse(HasColor(mesh, AreaRenderer.InvalidColor));

            var tiny = Square(30, 30, 3); // far under the 100 m² minimum
            var badPreview = AreaValidator.Validate(_host.World, tiny, AreaTypes.TestId);
            Assert.IsFalse(badPreview.IsValid);
            _renderer.ShowPreview(badPreview, tiny);
            mesh = _renderer.PreviewMesh;
            Assert.IsNotNull(mesh);
            Assert.IsTrue(HasColor(mesh, AreaRenderer.InvalidColor));
            Assert.IsFalse(HasColor(mesh, AreaRenderer.DotColor));
            Assert.IsFalse(HasColor(mesh, AreaRenderer.HandleColor));

            _renderer.HidePreview();
            Assert.IsNull(_renderer.PreviewMesh);
        }

        private static bool HasColor(Mesh mesh, Color c)
        {
            // Mesh vertex colours are stored as 8-bit, so compare with a tolerance, not Color ==.
            foreach (var col in mesh.colors)
                if (Mathf.Abs(col.r - c.r) < 0.01f && Mathf.Abs(col.g - c.g) < 0.01f &&
                    Mathf.Abs(col.b - c.b) < 0.01f && Mathf.Abs(col.a - c.a) < 0.01f) return true;
            return false;
        }
    }
}
