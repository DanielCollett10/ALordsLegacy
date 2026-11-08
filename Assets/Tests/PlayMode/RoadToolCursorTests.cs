using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests that the road tool shows the cursor marker at the pointer and hides it on deactivate.</summary>
    public class RoadToolCursorTests
    {
        private GameObject _hostGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private RoadTool _tool;
        private ToolController _controller;
        private Camera _cam;

        [TearDown]
        public void TearDown()
        {
            if (_toolGo != null) Object.Destroy(_toolGo);
            if (_panelGo != null) Object.Destroy(_panelGo);
            if (_camGo != null) Object.Destroy(_camGo);
            if (_groundGo != null) Object.Destroy(_groundGo);
            if (_hostGo != null) Object.Destroy(_hostGo);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
        }

        private IEnumerator Setup()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;

            _camGo = new GameObject("TestCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 60f;
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _groundGo = new GameObject("TestGround");
            _groundGo.transform.position = new Vector3(100f, -0.5f, 100f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);

            var pickerGo = new GameObject("Picker");
            pickerGo.transform.SetParent(_groundGo.transform, false);
            var picker = pickerGo.AddComponent<GroundPicker>();
            picker.PickCamera = _cam;
            picker.Host = _host;

            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            var panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("RoadTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<RoadTool>();
            _tool.Configure(_host, picker, null, panel, _controller);
            _tool.SetViewSettings(new ViewSettings());
            _controller.Register(_tool);
            yield return null; // let physics see the ground collider
            _controller.Toggle(RoadTool.ToolId);
        }

        // Note: named Screen here shadows UnityEngine.Screen, so the size helpers below use the full name.
        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        [UnityTest]
        public IEnumerator PointerOverGroundShowsRingAtSimHeightAndDeactivateHidesIt()
        {
            yield return Setup();
            Assert.IsNull(_toolGo.GetComponentInChildren<CursorMarker>(), "no marker before the first pointer move");

            _tool.PointerMove(Screen(100f, 100f));

            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsNotNull(marker, "the road tool must create the cursor marker as a child");
            Assert.IsTrue(marker.IsShown);
            Assert.AreEqual(CursorMarkerShape.Ring, marker.Shape);
            Assert.AreEqual(2.5f, marker.DiameterM, 0.01f, "ring diameter is the dirt path width");
            Assert.AreEqual(100f, marker.transform.position.x, 0.2f);
            Assert.AreEqual(100f, marker.transform.position.z, 0.2f);
            float groundY = _host.World.Terrain.HeightAt(marker.transform.position.x, marker.transform.position.z);
            Assert.AreEqual(groundY + CursorMarker.HeightOffsetM, marker.transform.position.y, 0.001f);

            _tool.Deactivate();
            Assert.IsFalse(marker.IsShown, "deactivating the road tool hides the marker");
        }

        [UnityTest]
        public IEnumerator RingFollowsCursorWithAndWithoutDraft()
        {
            yield return Setup();
            _tool.PointerMove(Screen(90f, 90f));
            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.AreEqual(90f, marker.transform.position.x, 0.2f);
            _tool.PointerDown(PressButton.Left, Screen(90f, 90f));
            _tool.PointerUp(PressButton.Left, Screen(90f, 90f), 0f);
            Assert.IsTrue(_tool.HasDraft);
            _tool.PointerMove(Screen(110f, 95f));
            Assert.IsTrue(marker.IsShown);
            Assert.AreEqual(110f, marker.transform.position.x, 0.2f);
            Assert.AreEqual(95f, marker.transform.position.z, 0.2f);
        }

        private float PixelsPerMetre() => Screen(101f, 100f).x - Screen(100f, 100f).x;

        private void BuildRoad()
        {
            var r = _host.Execute(new LordsLegacy.Sim.Commands.DrawRoad(
                new[] { LordsLegacy.Sim.Geometry.PointMm.FromMetres(60, 100), LordsLegacy.Sim.Geometry.PointMm.FromMetres(100, 100) },
                LordsLegacy.Sim.Roads.RoadTypes.DirtPathId));
            Assert.IsTrue(r.IsAccepted, r.Text);
        }

        [UnityTest]
        public IEnumerator RingSnapsToRoadEndWithin16PxButNotAt20Px()
        {
            yield return Setup();
            BuildRoad();
            float ppm = PixelsPerMetre();
            Vector2 end = Screen(100f, 100f);
            _tool.PointerMove(end + new Vector2(10f, 0f));
            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsTrue(marker.IsSnapped, "10 px from the road end snaps");
            Assert.AreEqual(100f, marker.transform.position.x, 0.2f);
            _tool.PointerMove(end + new Vector2(0f, 14f));
            Assert.IsTrue(marker.IsSnapped, "14 px (beyond the old 12 px) snaps");
            _tool.PointerMove(end + new Vector2(20f, 0f));
            Assert.IsFalse(marker.IsSnapped, "20 px does not snap");
            Assert.AreEqual(100f + 20f / ppm, marker.transform.position.x, 0.3f);
        }

        [UnityTest]
        public IEnumerator RingStaysDuringPointDragAndHidesOverUi()
        {
            yield return Setup();
            var marker = default(CursorMarker);
            _tool.PointerDown(PressButton.Left, Screen(90f, 90f));
            _tool.PointerUp(PressButton.Left, Screen(90f, 90f), 0f);
            _tool.PointerDown(PressButton.Left, Screen(90f, 90f));
            _tool.PointerMove(Screen(90f, 90f) + new Vector2(40f, 0f));
            marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsTrue(marker.IsShown, "ring shows during a point drag");

            // Find a screen position over the tool panel.
            yield return null;
            Vector2 over = Vector2.zero;
            bool found = false;
            for (int y = 0; y < Screen_Height() && !found; y += 8)
                for (int x = 0; x < Screen_Width() && !found; x += 8)
                    if (_controller.IsPointerOverUiAt(new Vector2(x, y))) { over = new Vector2(x, y); found = true; }
            Assert.IsTrue(found, "the tool panel covers part of the screen");
            _tool.PointerMove(over);
            Assert.IsFalse(marker.IsShown, "ring is hidden over UI");
        }

        private static int Screen_Height() => UnityEngine.Screen.height;
        private static int Screen_Width() => UnityEngine.Screen.width;
    }
}
