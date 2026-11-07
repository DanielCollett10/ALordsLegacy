using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>MVP-0-04 / PRF-03: the debug overlay shows stats and the hovered tree's data from the Sim.</summary>
    public class DebugOverlayTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _pickGo, _uiGo;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_pickGo);
            Object.DestroyImmediate(_groundGo);
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_hostGo);
        }

        [Test]
        public void OverlayShowsStatsAndHoveredTree()
        {
            _hostGo = new GameObject("SimHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.AutoUpdate = false;
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            // Synthetic ramp map already has tree 3 at (100,100), trunk 11 m: the camera looks straight at it.
            Assert.IsTrue(host.World.Trees.TryGet(new TreeId(3), out var tree));
            host.Tick(1f);

            _camGo = new GameObject("Cam");
            _camGo.transform.SetPositionAndRotation(new Vector3(100f, 300f, 100f), Quaternion.Euler(90f, 0f, 0f));
            var cam = _camGo.AddComponent<Camera>();
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(500f, -0.5f, 500f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(2000f, 1f, 2000f);
            Physics.SyncTransforms();
            _pickGo = new GameObject("Picker");
            var picker = _pickGo.AddComponent<GroundPicker>();
            picker.PickCamera = cam;
            picker.Host = host;

            _uiGo = new GameObject("DebugOverlay");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UIDocument>().panelSettings = LoadPanel();
            var view = _uiGo.AddComponent<DebugOverlayView>();
            view.Live = false;
            view.Picker = picker;
            view.Host = host;
            _uiGo.SetActive(true);

            view.SetVisible(true);
            view.RefreshStats(60f);
            view.InspectScreen(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f));

            Assert.IsTrue(view.Visible);
            Assert.AreEqual("Tree id 3", view.TextOf(DebugOverlayView.TreeIdName));
            Assert.AreEqual("Trunk 11.0 m", view.TextOf(DebugOverlayView.TreeTrunkName));
            Assert.AreEqual("Logs " + tree.LogYield(host.World.Tuning), view.TextOf(DebugOverlayView.TreeLogsName));
            Assert.AreEqual("Trees " + host.World.Trees.Count, view.TextOf(DebugOverlayView.TreeCountName));
            StringAssert.IsMatch(@"^Tick \d+\.\d+ ms$", view.TextOf(DebugOverlayView.TickMsName));
            StringAssert.IsMatch(@"^Ticks/s \d+\.\d+$", view.TextOf(DebugOverlayView.TpsName));

            SaveScreenshot();
        }

        [Test]
        public void OverlayShowsRoadCountsAndSegmentUnderCursor()
        {
            _hostGo = new GameObject("SimHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.AutoUpdate = false;
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            // A straight road through the ground point the camera looks at, (100,100).
            var road = host.Execute(new DrawRoad(new[] { PointMm.FromMetres(60, 100), PointMm.FromMetres(140, 100) },
                RoadTypes.DirtPathId));
            Assert.IsTrue(road.IsAccepted, "Test road was rejected.");
            int segId = (int)road.Created[0];
            var seg = host.World.Roads.GetSegment(segId);
            Assert.IsNotNull(seg);

            _camGo = new GameObject("Cam");
            _camGo.transform.SetPositionAndRotation(new Vector3(100f, 300f, 100f), Quaternion.Euler(90f, 0f, 0f));
            var cam = _camGo.AddComponent<Camera>();
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(500f, -0.5f, 500f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(2000f, 1f, 2000f);
            Physics.SyncTransforms();
            _pickGo = new GameObject("Picker");
            var picker = _pickGo.AddComponent<GroundPicker>();
            picker.PickCamera = cam;
            picker.Host = host;

            _uiGo = new GameObject("DebugOverlay");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UIDocument>().panelSettings = LoadPanel();
            var view = _uiGo.AddComponent<DebugOverlayView>();
            view.Live = false;
            view.Picker = picker;
            view.Host = host;
            _uiGo.SetActive(true);

            view.SetVisible(true);
            view.RefreshStats(60f);
            view.InspectScreen(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f));

            Assert.AreEqual("Road nodes 2", view.TextOf(DebugOverlayView.RoadNodesName));
            Assert.AreEqual("Road segments 1", view.TextOf(DebugOverlayView.RoadSegmentsName));
            Assert.AreEqual("Road networks 1", view.TextOf(DebugOverlayView.RoadNetworksName));
            Assert.AreEqual("Segment id " + segId, view.TextOf(DebugOverlayView.SegIdName));
            Assert.AreEqual("Ends " + seg.NodeA + " " + seg.NodeB, view.TextOf(DebugOverlayView.SegEndsName));
            Assert.AreEqual("Reachable segments 1", view.TextOf(DebugOverlayView.SegReachName));
        }

        [Test]
        public void OverlayShowsAreaCountAndAreaUnderCursor()
        {
            _hostGo = new GameObject("SimHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.AutoUpdate = false;
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            // A 40 m x 40 m test area centred on (100,100), the ground point the camera looks at: 1600 m².
            var shape = AreaShape.Straight(new[]
            {
                PointMm.FromMetres(80, 80), PointMm.FromMetres(120, 80),
                PointMm.FromMetres(120, 120), PointMm.FromMetres(80, 120),
            });
            var designate = host.Execute(new DesignateArea(shape, AreaTypes.TestId));
            Assert.IsTrue(designate.IsAccepted, "Test area was rejected.");
            var area = host.World.Areas.Get((int)designate.Created[0]);
            Assert.IsNotNull(area);

            _camGo = new GameObject("Cam");
            _camGo.transform.SetPositionAndRotation(new Vector3(100f, 300f, 100f), Quaternion.Euler(90f, 0f, 0f));
            var cam = _camGo.AddComponent<Camera>();
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(500f, -0.5f, 500f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(2000f, 1f, 2000f);
            Physics.SyncTransforms();
            _pickGo = new GameObject("Picker");
            var picker = _pickGo.AddComponent<GroundPicker>();
            picker.PickCamera = cam;
            picker.Host = host;

            _uiGo = new GameObject("DebugOverlay");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UIDocument>().panelSettings = LoadPanel();
            var view = _uiGo.AddComponent<DebugOverlayView>();
            view.Live = false;
            view.Picker = picker;
            view.Host = host;
            _uiGo.SetActive(true);

            view.SetVisible(true);
            view.RefreshStats(60f);
            view.InspectScreen(new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f));

            Assert.AreEqual("Areas 1", view.TextOf(DebugOverlayView.AreaCountName));
            Assert.AreEqual("Area " + area.Name, view.TextOf(DebugOverlayView.AreaNameName));
            Assert.AreEqual("Type Test area", view.TextOf(DebugOverlayView.AreaTypeName));
            Assert.AreEqual("Area 1600 m²", view.TextOf(DebugOverlayView.AreaM2Name));

            // Moving the cursor off the area clears the area under the cursor but keeps the count.
            view.InspectScreen(new Vector2(0f, 0f));
            Assert.AreEqual("Area --", view.TextOf(DebugOverlayView.AreaNameName));
            Assert.AreEqual("Areas 1", view.TextOf(DebugOverlayView.AreaCountName));
        }

        private static void SaveScreenshot()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("No GPU: skipping M1_debug_overlay.png");
                return;
            }
            try
            {
                var dir = Path.Combine("TestResults", "Screenshots");
                Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "M1_debug_overlay.png"));
            }
            catch (System.Exception e) { Debug.LogWarning("Screenshot failed: " + e.Message); }
        }

        private static PanelSettings LoadPanel()
        {
#if UNITY_EDITOR
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/UI/PanelSettings.asset");
            Assert.IsNotNull(p, "Shared PanelSettings missing.");
            return p;
#else
            return TestPanelSettings.Create();
#endif
        }
    }
}
