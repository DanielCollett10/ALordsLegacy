using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="RoadRenderer"/> with a synthetic SimHost.</summary>
    public class RoadRendererTests
    {
        private GameObject _hostGo;
        private GameObject _rendererGo;
        private SimHost _host;
        private RoadRenderer _renderer;

        [TearDown]
        public void TearDown()
        {
            if (_rendererGo != null) Object.DestroyImmediate(_rendererGo);
            if (_hostGo != null) Object.DestroyImmediate(_hostGo);
        }

        private void Setup(MapData map)
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), map);
            _host.AutoUpdate = false;
            _rendererGo = new GameObject("TestRoadRenderer");
            _renderer = _rendererGo.AddComponent<RoadRenderer>();
            _renderer.Configure(_host, null);
        }

        private static PointMm[] Line(double x0, double x1, double z, double step)
        {
            int n = (int)((x1 - x0) / step) + 1;
            var pts = new PointMm[n];
            for (int i = 0; i < n; i++) pts[i] = PointMm.FromMetres(x0 + i * step, z);
            return pts;
        }

        [UnityTest]
        public IEnumerator SegmentMeshAppearsAndGoesWithTheSegment()
        {
            Setup(SyntheticMap.CreateRamp());
            var samples = Line(20, 40, 50, 1);
            var seg = _host.World.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { samples[0], samples[samples.Length - 1] }, samples);
            yield return null;
            _renderer.Sync();

            var mesh = _renderer.GetSegmentMesh(seg.Id);
            Assert.IsNotNull(mesh, "No mesh for the new segment.");
            Assert.AreEqual(1, _renderer.SegmentMeshCount);
            Assert.AreEqual(samples.Length * 2, mesh.vertexCount);
            // Follows the ramp: height = 0.1 * x, plus the offset.
            var v = mesh.vertices;
            Assert.AreEqual(0.1f * v[0].x + RoadRenderer.HeightOffsetM, v[0].y, 0.01f);
            Assert.Greater(v[v.Length - 1].y, v[0].y + 1f);

            Assert.IsTrue(_host.World.Roads.RemoveSegment(seg.Id));
            _renderer.Sync();
            Assert.IsNull(_renderer.GetSegmentMesh(seg.Id));
            Assert.AreEqual(0, _renderer.SegmentMeshCount);
        }

        [UnityTest]
        public IEnumerator RoadPreviewAndHandleRenderersUseGroundOverlayShader()
        {
            Setup(SyntheticMap.CreateRamp());
            var samples = Line(20, 40, 50, 1);
            var seg = _host.World.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { samples[0], samples[samples.Length - 1] }, samples);
            yield return null;
            _renderer.Sync();
            _renderer.ShowNodeHandles(new[] { new Vector2(30, 50) });
            Assert.IsNotNull(_renderer.GetSegmentMesh(seg.Id));
            int checkedRenderers = 0;
            foreach (var mr in _rendererGo.GetComponentsInChildren<MeshRenderer>(true))
            {
                Assert.AreEqual(GroundOverlay.ShaderName, mr.sharedMaterial.shader.name, mr.name);
                checkedRenderers++;
            }
            Assert.GreaterOrEqual(checkedRenderers, 2, "Expected a road segment and a node handle.");
        }

        [UnityTest]
        public IEnumerator ExtendedDeadEndMeshesMeetFlush()
        {
            // RD-16: the new road bends away from the old one, so without the shared tangent the joint would kink.
            Setup(SyntheticMap.CreateRamp());
            var first = _host.Execute(new DrawRoad(new[] { PointMm.FromMetres(20, 50), PointMm.FromMetres(40, 50) }, RoadTypes.DirtPathId));
            Assert.IsTrue(first.IsAccepted, "Base road rejected.");
            var second = _host.Execute(new DrawRoad(new[] { PointMm.FromMetres(40, 50), PointMm.FromMetres(60, 58) }, RoadTypes.DirtPathId));
            Assert.IsTrue(second.IsAccepted, "Extension rejected.");
            yield return null;
            _renderer.Sync();

            int oldId = (int)first.Created[0];
            int newId = -1;
            foreach (long id in second.Created) if (id != oldId) newId = (int)id;
            Assert.AreNotEqual(-1, newId, "No new segment in the extension result.");
            var oldMesh = _renderer.GetSegmentMesh(oldId);
            var newMesh = _renderer.GetSegmentMesh(newId);
            Assert.IsNotNull(oldMesh);
            Assert.IsNotNull(newMesh);

            var ov = oldMesh.vertices;
            var nv = newMesh.vertices;
            int oEnd = ov.Length - 2, nStart = 0;
            for (int k = 0; k < 2; k++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < 2; j++)
                    best = Mathf.Min(best, Vector3.Distance(ov[oEnd + k], nv[nStart + j]));
                Assert.Less(best, 0.01f, "Old end vertex " + k + " has no new start vertex within 1 cm.");
            }
            for (int k = 0; k < 2; k++)
            {
                float best = float.MaxValue;
                for (int j = 0; j < 2; j++)
                    best = Mathf.Min(best, Vector3.Distance(nv[nStart + k], ov[oEnd + j]));
                Assert.Less(best, 0.01f, "New start vertex " + k + " has no old end vertex within 1 cm.");
            }
        }

        [UnityTest]
        public IEnumerator PreviewShowsRedVerticesForInvalidRange()
        {
            // Flat to x=100, then rising 0.4 m per metre: far over the 15% limit.
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                {
                    float x = ix * 2f;
                    h[iz * nodes + ix] = x <= 100f ? 0f : 0.4f * (x - 100f);
                }
            Setup(new MapData("steep", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]));

            var cps = new[] { PointMm.FromMetres(80, 50), PointMm.FromMetres(120, 50) };
            var preview = RoadValidator.Validate(_host.World, cps, RoadTypes.DirtPathId);
            Assert.AreEqual(RoadRefusal.TooSteep, preview.Reason);
            Assert.Greater(preview.InvalidRanges.Count, 0);
            yield return null;

            _renderer.ShowPreview(preview, 2.5f);
            var mesh = _renderer.PreviewMesh;
            Assert.IsNotNull(mesh);
            var colors = mesh.colors;
            Assert.AreEqual(preview.Samples.Count * 2, colors.Length);
            for (int i = 0; i < preview.Samples.Count; i++)
            {
                bool bad = false;
                foreach (var r in preview.InvalidRanges) if (i >= r.StartIndex && i <= r.EndIndex) bad = true;
                var expected = bad ? RoadRenderer.InvalidColor : RoadRenderer.ValidPreviewColor;
                Assert.AreEqual(expected, colors[i * 2], "Sample " + i);
                Assert.AreEqual(expected, colors[i * 2 + 1], "Sample " + i);
            }
            Assert.AreEqual(RoadRenderer.InvalidColor, colors[colors.Length - 1], "Last sample is on the steep part.");
            Assert.AreEqual(RoadRenderer.ValidPreviewColor, colors[0], "First sample is on flat ground.");

            _renderer.HidePreview();
            Assert.IsNull(_renderer.PreviewMesh);
        }
    }
}
