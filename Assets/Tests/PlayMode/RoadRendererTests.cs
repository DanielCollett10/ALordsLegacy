using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
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
            if (_rendererGo != null) Object.Destroy(_rendererGo);
            if (_hostGo != null) Object.Destroy(_hostGo);
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
