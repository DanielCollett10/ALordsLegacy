using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for placement.md PL-05/PL-06 in the area tools (T-209).</summary>
    public class AreaPlacementTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private AreaTool _tool;
        private AreaTool _workTool;
        private ToolController _controller;
        private Camera _cam;
        private float _time;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
        }

        private IEnumerator Setup(bool snapping)
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
            _rendererGo = new GameObject("TestAreaRenderer");
            var renderer = _rendererGo.AddComponent<AreaRenderer>();
            renderer.Configure(_host);
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            var panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);
            _toolGo = new GameObject("AreaTool");
            _controller = _toolGo.AddComponent<ToolController>();
            var settings = new ViewSettings { Snapping = snapping };
            _tool = _toolGo.AddComponent<AreaTool>();
            _tool.SetViewSettings(settings);
            _tool.Configure(AreaTool.TestToolId, AreaTypes.TestId, _host, picker, renderer, panel, _controller);
            _controller.Register(_tool);
            _workTool = _toolGo.AddComponent<AreaTool>();
            _workTool.SetViewSettings(settings);
            _workTool.Configure(AreaTool.TestWorkToolId, AreaTypes.TestWorkId, _host, picker, renderer, panel, _controller);
            _controller.Register(_workTool);
            yield return null;
            _controller.Toggle(AreaTool.TestToolId);
            // An existing test area, 40..80 square.
            Click(_tool, 40, 40); Click(_tool, 80, 40); Click(_tool, 80, 80); Click(_tool, 40, 80);
            _tool.Confirm();
            Assert.AreEqual(1, _host.World.Areas.Count);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(AreaTool tool, float x, float z)
        {
            var p = Screen(x, z);
            tool.PointerDown(PressButton.Left, p);
            tool.PointerUp(PressButton.Left, p, _time += 2f);
        }

        [UnityTest]
        public IEnumerator ClickInsideExistingAreaAddsNothingAndHidesSphere()
        {
            yield return Setup(false);
            _tool.PointerMove(Screen(120, 120));
            Assert.IsTrue(_tool.Cursor.IsShown, "Placeable ground shows the sphere.");
            _tool.PointerMove(Screen(60, 60));
            Assert.IsFalse(_tool.Cursor.IsShown, "Sphere hidden inside an area.");
            Click(_tool, 60, 60);
            Assert.AreEqual(0, _tool.PointCount);
        }

        [UnityTest]
        public IEnumerator SnappedToAreaEdgeIsPlaced()
        {
            yield return Setup(true);
            _tool.PointerMove(Screen(40.1f, 40.1f));
            Assert.IsTrue(_tool.Cursor.IsShown);
            Click(_tool, 40.1f, 40.1f);
            Assert.AreEqual(1, _tool.PointCount);
            Assert.AreEqual(40.0, _tool.Points[0].XMetres, 0.06);
        }

        [UnityTest]
        public IEnumerator WorkAreaPointInsideTestAreaIsPlaced()
        {
            yield return Setup(false);
            _controller.Toggle(AreaTool.TestWorkToolId);
            _workTool.PointerMove(Screen(60, 60));
            Assert.IsTrue(_workTool.Cursor.IsShown);
            Click(_workTool, 60, 60);
            Assert.AreEqual(1, _workTool.PointCount);
        }

        [UnityTest]
        public IEnumerator DragIntoAreaStopsAtLastValidSpot()
        {
            yield return Setup(false);
            Click(_tool, 100, 100);
            var down = Screen(100, 100);
            _tool.PointerDown(PressButton.Left, down);
            _tool.PointerMove(Screen(95, 95));
            _tool.PointerMove(Screen(90, 90));
            Assert.AreEqual(90.0, _tool.Points[0].XMetres, 0.1);
            _tool.PointerMove(Screen(60, 60));
            Assert.AreEqual(90.0, _tool.Points[0].XMetres, 0.1, "Stays at the last placeable position.");
            Assert.AreEqual(90.0, _tool.Points[0].ZMetres, 0.1);
            Assert.IsFalse(_tool.Cursor.IsShown);
            _tool.PointerMove(Screen(110, 110));
            Assert.AreEqual(110.0, _tool.Points[0].XMetres, 0.1, "Follows again on placeable ground.");
            _tool.PointerUp(PressButton.Left, Screen(110, 110), _time += 2f);
        }
    }
}
