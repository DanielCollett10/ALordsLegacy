using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="RoadTool"/> on a synthetic SimHost (T-100).</summary>
    public class RoadToolTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private RoadRenderer _renderer;
        private ToolPanelView _panel;
        private RoadTool _tool;
        private ToolController _controller;
        private Camera _cam;
        private float _time;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo })
                if (go != null) Object.Destroy(go);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
        }

        private IEnumerator Setup(MapData map)
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), map);
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

            _rendererGo = new GameObject("TestRoadRenderer");
            _renderer = _rendererGo.AddComponent<RoadRenderer>();
            _renderer.Configure(_host, null);

            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("RoadTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<RoadTool>();
            _tool.Configure(_host, picker, _renderer, _panel, _controller);
            _controller.Register(_tool);
            yield return null; // let physics see the ground collider
            _controller.Toggle(RoadTool.ToolId);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z, PressButton b = PressButton.Left)
        {
            var p = Screen(x, z);
            _tool.PointerDown(b, p);
            _time += 2f; // never a double click
            _tool.PointerUp(b, p, _time);
        }

        private static MapData SteepMap()
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                {
                    float x = ix * 2f;
                    h[iz * nodes + ix] = x <= 100f ? 0f : 0.4f * (x - 100f);
                }
            return new MapData("steep", new HeightGrid(0f, 0f, 2f, nodes, nodes, h), new MapTree[0]);
        }

        [UnityTest]
        public IEnumerator ClickClickEnterCreatesRoadAndClearsDraft()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Assert.AreEqual(1, _tool.PointCount);
            Assert.IsTrue(_panel.IsShown);
            Click(80, 100);
            Assert.AreEqual(2, _tool.PointCount);
            Assert.IsNotNull(_renderer.PreviewMesh, "Preview should show with two points.");
            StringAssert.Contains("2 points", _panel.Status);
            StringAssert.Contains("Valid", _panel.Status);
            Assert.IsTrue(_panel.IsConfirmEnabled);

            _host.World.Pause(); // works while paused
            _tool.Confirm();
            Assert.GreaterOrEqual(_host.World.Roads.SegmentCount, 1);
            Assert.IsFalse(_tool.HasDraft);
            StringAssert.Contains("no points", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
            Assert.IsNull(_renderer.PreviewMesh);
        }

        [UnityTest]
        public IEnumerator PanelShowsAsSoonAsToolIsActive()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Assert.IsTrue(_panel.IsShown, "Panel should be visible right after clicking Road.");
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _controller.Deselect();
            Assert.IsFalse(_panel.IsShown, "Panel hides when the tool is left.");
        }

        [UnityTest]
        public IEnumerator PanelConfirmButtonSendsTheRoad()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Click(80, 100);
            _panel.ClickConfirm();
            Assert.GreaterOrEqual(_host.World.Roads.SegmentCount, 1);
            Assert.IsFalse(_tool.HasDraft);
        }

        [UnityTest]
        public IEnumerator TooSteepIsBlocked()
        {
            yield return Setup(SteepMap());
            Click(80, 50);
            Click(120, 50);
            Assert.IsFalse(_tool.CurrentPreview.IsValid);
            StringAssert.Contains("too steep", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _tool.Confirm();
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Roads.SegmentCount);
            Assert.AreEqual(2, _tool.PointCount, "Draft stays.");
        }

        [UnityTest]
        public IEnumerator RightClickCancelsButRightDragDoesNot()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Click(80, 100);

            var a = Screen(60, 120);
            _tool.PointerDown(PressButton.Right, a);
            _tool.PointerMove(a + new Vector2(30f, 0f));
            _tool.PointerUp(PressButton.Right, a + new Vector2(30f, 0f), _time += 2f);
            Assert.AreEqual(2, _tool.PointCount, "Right drag must not cancel.");

            Click(60, 120, PressButton.Right);
            Assert.IsFalse(_tool.HasDraft);
            StringAssert.Contains("no points", _panel.Status);
            Assert.IsNull(_renderer.PreviewMesh);
        }

        [UnityTest]
        public IEnumerator EscapeCancelsThenSecondEscapeLeavesTool()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Assert.IsTrue(_tool.HandleEscape());
            Assert.IsFalse(_tool.HasDraft);
            Assert.IsFalse(_tool.HandleEscape());

            Click(40, 100);
            Assert.IsTrue(_controller.HandleEscape(), "First Escape cancels the draft.");
            Assert.AreSame(_tool, _controller.ActiveTool);
            Assert.IsTrue(_controller.HandleEscape(), "Second Escape leaves the tool.");
            Assert.IsNull(_controller.ActiveTool);
        }

        [UnityTest]
        public IEnumerator DragPlacedPointMovesItAndPreviewFollowsWithoutMovingCamera()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Click(80, 100);
            Vector3 camPos = _camGo.transform.position;
            Quaternion camRot = _camGo.transform.rotation;

            _tool.PointerDown(PressButton.Left, Screen(40, 100)); // on the first placed point
            _tool.PointerMove(Screen(45, 105));
            Assert.IsTrue(_tool.IsActive);
            _tool.PointerMove(Screen(50, 110));
            Assert.AreEqual(50.0, _tool.Points[0].XMetres, 0.5, "Point should follow the cursor during the drag.");
            Assert.AreEqual(110.0, _tool.Points[0].ZMetres, 0.5);
            Assert.AreEqual(2, _tool.PointCount, "A drag moves a point, it does not add one.");
            Assert.AreEqual(50.0, _tool.CurrentPreview.Samples[0].XMetres, 0.5, "Preview starts at the moved point.");
            Assert.AreEqual(110.0, _tool.CurrentPreview.Samples[0].ZMetres, 0.5);

            _tool.PointerUp(PressButton.Left, Screen(50, 110), _time += 2f);
            Assert.AreEqual(2, _tool.PointCount, "Release after a drag adds nothing.");
            Assert.AreEqual(50.0, _tool.Points[0].XMetres, 0.5);
            Assert.AreEqual(camPos, _camGo.transform.position, "Camera must not move while dragging a point.");
            Assert.AreEqual(camRot, _camGo.transform.rotation);
        }

        [UnityTest]
        public IEnumerator BackspaceRemovesLastPoint()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Click(80, 100);
            Click(60, 120);
            Assert.AreEqual(3, _tool.PointCount);

            // Real key path: a Backspace press queued on the Keyboard device, read by RoadTool.Update.
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backspace));
            yield return null;
            Assert.AreEqual(2, _tool.PointCount, "Backspace removes the last point.");
            Assert.AreEqual(80.0, _tool.Points[1].XMetres, 0.5, "The earlier points stay.");

            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backspace));
            yield return null;
            Assert.AreEqual(1, _tool.PointCount);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Backspace));
            yield return null;
            Assert.AreEqual(0, _tool.PointCount);
            Assert.IsFalse(_tool.HasDraft);
            StringAssert.Contains("no points", _panel.Status);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoubleClickAddsExactlyOnePointAndConfirms()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Click(80, 100);
            Assert.AreEqual(0, _host.World.Roads.SegmentCount);

            // Double click at (120, 100): the first click adds its point, the second replaces it with one final point.
            var p = Screen(120, 100);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(3, _tool.PointCount, "The first click of the pair adds one point.");
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 0.1f);

            Assert.IsFalse(_tool.HasDraft, "Double click confirms the road.");
            Assert.AreEqual(1, _host.World.Roads.SegmentCount);
            RoadSegment seg = null;
            foreach (var s in _host.World.Roads.Segments) seg = s;
            Assert.AreEqual(3, seg.ControlPoints.Count, "The double click added exactly one point.");
            Assert.AreEqual(120.0, seg.ControlPoints[2].XMetres, 0.5, "The final point is at the cursor.");
        }
    }
}
