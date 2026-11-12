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
        private RenderTexture _viewport;
        private float _time;

        /// <summary>Fixed test viewport (pixels). The Game view size varies (Test Runner, player window), so the test
        /// camera renders into this texture; pixel tolerances (RoadSnapDistancePx etc.) then mean the same everywhere.</summary>
        private const int ViewportWidth = 748;
        private const int ViewportHeight = 421;

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_viewport != null) { _viewport.Release(); Object.Destroy(_viewport); _viewport = null; }
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
            _cam.orthographicSize = 60f; // 120 m tall view: 60 m from the road is ~210 px at ViewportHeight
            _viewport = new RenderTexture(ViewportWidth, ViewportHeight, 16);
            _cam.targetTexture = _viewport; // fixed pixel size, independent of the Game view
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
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("RoadTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<RoadTool>();
            _tool.Configure(_host, picker, _renderer, _panel, _controller);
            // Pin the view settings so no test depends on a TopBarController left alive in the scene (snapping on, 12 px).
            _tool.SetViewSettings(new ViewSettings());
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
            Assert.IsFalse(_panel.IsShown, "UIP-03: a successful confirm hides the panel.");
            Assert.IsFalse(_panel.IsConfirmEnabled);
            Assert.IsNull(_renderer.PreviewMesh);
        }

        [UnityTest]
        public IEnumerator PanelHiddenWhileIdleAndShownOnFirstPoint()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Assert.IsFalse(_panel.IsShown, "UIP-01: activating Road with no draft shows no panel.");
            Click(40, 100);
            Assert.IsTrue(_panel.IsShown, "UIP-01: the first placed point shows the panel.");
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _controller.Deselect();
            Assert.IsFalse(_panel.IsShown, "Panel hides when the tool is left.");
        }

        [UnityTest]
        public IEnumerator RingFollowsCursorWhilePanelHidden()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Assert.IsFalse(_panel.IsShown);
            _tool.PointerMove(Screen(60, 100));
            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsNotNull(marker, "UIP-02: the ring is created on the first pointer move.");
            Assert.IsTrue(marker.IsShown, "UIP-02: the ring shows with the panel hidden.");
            Assert.AreEqual(60f, marker.transform.position.x, 0.5f, "UIP-02: the ring sits at the cursor.");
            Assert.IsFalse(_panel.IsShown);
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
            Assert.IsFalse(_panel.IsShown, "UIP-03: cancelling the draft hides the panel.");
            Assert.IsNull(_renderer.PreviewMesh);
        }

        [UnityTest]
        public IEnumerator EscapeCancelsThenSecondEscapeLeavesTool()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            Click(40, 100);
            Assert.IsTrue(_tool.HandleEscape());
            Assert.IsFalse(_tool.HasDraft);
            Assert.IsFalse(_panel.IsShown, "UIP-03: Escape hides the panel.");
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
            Assert.IsFalse(_panel.IsShown, "UIP-03: Backspace of the last point hides the panel.");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
        }

        private void DrawExisting(float x1, float z1, float x2, float z2)
        {
            var r = _host.Execute(new LordsLegacy.Sim.Commands.DrawRoad(
                new[] { LordsLegacy.Sim.Geometry.PointMm.FromMetres(x1, z1), LordsLegacy.Sim.Geometry.PointMm.FromMetres(x2, z2) },
                RoadTypes.DirtPathId));
            Assert.IsTrue(r.IsAccepted, r.Text);
        }

        [UnityTest]
        public IEnumerator SnapOnSnapsToRoadEndAndLineSnapOffDoesNot()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 120, 100);
            // 60 px = 12 px snap distance at ~1.0 px/m? use a few pixels off the end (and the line).
            var end = Screen(40, 100) + new Vector2(0f, 5f);
            _tool.SetViewSettings(new ViewSettings { Snapping = true });
            _tool.PointerDown(PressButton.Left, end); _tool.PointerUp(PressButton.Left, end, _time += 2f);
            Assert.AreEqual(40.0, _tool.Points[0].XMetres, 0.01, "Snaps to the road end.");
            Assert.AreEqual(100.0, _tool.Points[0].ZMetres, 0.01);
            _tool.Cancel();

            var line = Screen(80, 100) + new Vector2(0f, 6f);
            _tool.PointerDown(PressButton.Left, line); _tool.PointerUp(PressButton.Left, line, _time += 2f);
            Assert.AreEqual(80.0, _tool.Points[0].XMetres, 1.0);
            Assert.AreEqual(100.0, _tool.Points[0].ZMetres, 0.01, "Snaps onto the centre line.");
            _tool.Cancel();

            // Snapping off: a first click a few pixels off the road is placed where it was clicked.
            _tool.SetViewSettings(new ViewSettings { Snapping = false });
            // 4 m off the road is outside its 2.5 m drawn width at any screen size, but would be inside a 12 px snap
            // radius on a small view; with snapping off it must be placed where clicked.
            Click(40, 104);
            Assert.AreEqual(104.0, _tool.Points[0].ZMetres, 0.5, "No snapping when off.");
            Click(80, 104);
            Assert.AreEqual(104.0, _tool.Points[1].ZMetres, 0.5, "Placed points do not snap when off.");
        }

        private LordsLegacy.Sim.Roads.RoadNode NodeNear(float x, float z)
        {
            LordsLegacy.Sim.Roads.RoadNode best = null;
            double bd = double.MaxValue;
            foreach (var n in _host.World.Roads.Nodes)
            {
                double d = System.Math.Abs(n.Position.XMetres - x) + System.Math.Abs(n.Position.ZMetres - z);
                if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        [UnityTest]
        public IEnumerator HoverRoadShowsNodeHandlesAtItsEnds()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 120, 100);
            _tool.PointerMove(Screen(80, 100));
            Assert.AreEqual(2, _renderer.NodeHandleCount);
            // The far cursor point must sit well outside the road snap tolerance at this fixed viewport.
            float px = (Screen(80, 160) - Screen(80, 100)).magnitude;
            Assert.Greater(px, 3f * new ViewSettings().RoadSnapDistancePx, "Far point must be clearly outside the pixel tolerance.");
            _tool.PointerMove(Screen(80, 160));
            Assert.AreEqual(0, _renderer.NodeHandleCount, "No handles away from a road.");
        }

        [UnityTest]
        public IEnumerator DragRoadEndMovesNodeWhenValid()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 100, 100);
            int node = NodeNear(100, 100).Id;
            _tool.PointerDown(PressButton.Left, Screen(100, 100));
            _tool.PointerMove(Screen(110, 110));
            _tool.PointerMove(Screen(120, 120));
            Assert.IsTrue(_tool.IsDraggingNode);
            Assert.AreEqual(1, _renderer.NodeMovePreviewCount);
            StringAssert.Contains("Move node · 1 road · Valid", _panel.Status);
            Assert.IsTrue(_panel.IsShown, "The panel shows while a road node is dragged (UIP-01).");
            _tool.PointerUp(PressButton.Left, Screen(120, 120), _time += 2f);
            Assert.IsFalse(_tool.IsDraggingNode);
            Assert.AreEqual(0, _renderer.NodeMovePreviewCount);
            Assert.AreEqual(1, _host.World.Roads.SegmentCount);
            var n = _host.World.Roads.GetNode(node);
            Assert.AreEqual(120.0, n.Position.XMetres, 0.5);
            Assert.AreEqual(120.0, n.Position.ZMetres, 0.5);
            Assert.IsFalse(_tool.HasDraft, "A node drag leaves no draft.");
        }

        [UnityTest]
        public IEnumerator DragJunctionReshapesAllAttachedRoads()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 120, 100);
            DrawExisting(80, 100, 80, 150);
            var j = NodeNear(80, 100);
            Assert.AreEqual(3, j.SegmentIds.Count);
            _tool.PointerDown(PressButton.Left, Screen(80, 100));
            _tool.PointerMove(Screen(82, 94));
            _tool.PointerMove(Screen(80, 90));
            Assert.AreEqual(3, _renderer.NodeMovePreviewCount, "All attached segments are previewed.");
            StringAssert.Contains("3 roads", _panel.Status);
            _tool.PointerUp(PressButton.Left, Screen(80, 90), _time += 2f);
            Assert.AreEqual(90.0, _host.World.Roads.GetNode(j.Id).Position.ZMetres, 0.5);
            Assert.AreEqual(3, _host.World.Roads.GetNode(j.Id).SegmentIds.Count);
        }

        [UnityTest]
        public IEnumerator InvalidNodeDragRevertsAndKeepsReason()
        {
            yield return Setup(SteepMap());
            DrawExisting(50, 100, 90, 100);
            int node = NodeNear(90, 100).Id;
            _tool.PointerDown(PressButton.Left, Screen(90, 100));
            _tool.PointerMove(Screen(110, 100));
            _tool.PointerMove(Screen(130, 100));
            StringAssert.Contains("Move node · 1 road ·", _panel.Status);
            StringAssert.DoesNotContain("Valid", _panel.Status.Replace("Invalid", ""));
            _tool.PointerUp(PressButton.Left, Screen(130, 100), _time += 2f);
            Assert.AreEqual(90.0, _host.World.Roads.GetNode(node).Position.XMetres, 0.5, "Node stays put.");
            StringAssert.Contains("Rejected", _panel.Status);
            Assert.AreEqual(0, _renderer.NodeMovePreviewCount);
            Assert.IsTrue(_panel.IsShown, "UIP-01: a refused node move keeps the panel up with its reason.");
            // The reason stays until the next input.
            _tool.PointerMove(Screen(60, 150));
            StringAssert.Contains("Rejected", _panel.Status);
            Assert.IsTrue(_panel.IsShown);
            _tool.PointerDown(PressButton.Left, Screen(60, 150));
            Assert.IsFalse(_panel.IsShown, "The refused-move reason clears on the next input.");
        }

        [UnityTest]
        public IEnumerator EscapeAndRightClickCancelNodeDrag()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 100, 100);
            int node = NodeNear(100, 100).Id;
            _tool.PointerDown(PressButton.Left, Screen(100, 100));
            _tool.PointerMove(Screen(110, 110));
            _tool.PointerMove(Screen(120, 120));
            Assert.IsTrue(_tool.IsDraggingNode);
            Assert.IsTrue(_controller.HandleEscape());
            Assert.IsFalse(_tool.IsDraggingNode);
            Assert.AreEqual(0, _renderer.NodeMovePreviewCount);
            _tool.PointerMove(Screen(130, 130));
            _tool.PointerUp(PressButton.Left, Screen(130, 130), _time += 2f);
            Assert.AreEqual(100.0, _host.World.Roads.GetNode(node).Position.XMetres, 0.5, "Escape changes nothing.");
            Assert.AreSame(_tool, _controller.ActiveTool, "Escape only cancelled the drag.");

            _tool.PointerDown(PressButton.Left, Screen(100, 100));
            _tool.PointerMove(Screen(110, 110));
            _tool.PointerMove(Screen(120, 120));
            Assert.IsTrue(_tool.IsDraggingNode);
            _tool.PointerDown(PressButton.Right, Screen(120, 120));
            _tool.PointerUp(PressButton.Right, Screen(120, 120), _time += 2f);
            Assert.IsFalse(_tool.IsDraggingNode);
            _tool.PointerUp(PressButton.Left, Screen(120, 120), _time += 2f);
            Assert.AreEqual(100.0, _host.World.Roads.GetNode(node).Position.XMetres, 0.5, "Right-click changes nothing.");
            Assert.AreEqual(1, _host.World.Roads.SegmentCount, "Right-click during the drag deletes nothing.");
        }

        [UnityTest]
        public IEnumerator ClickOnRoadEndStillStartsBranch()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 100, 100);
            var p = Screen(100, 100);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.IsFalse(_tool.IsDraggingNode);
            Assert.AreEqual(1, _tool.PointCount);
            Assert.AreEqual(100.0, _tool.Points[0].XMetres, 0.01);
        }

        [UnityTest]
        public IEnumerator BranchFromRoadCreatesJunction()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 120, 100);
            Assert.AreEqual(1, _host.World.Roads.SegmentCount);
            var p = Screen(80, 100) + new Vector2(0f, 4f);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(1, _tool.PointCount);
            Assert.AreEqual(100.0, _tool.Points[0].ZMetres, 0.01, "First point is on the road centre line.");
            Click(80, 150);
            Assert.IsTrue(_tool.CurrentPreview.IsValid, _tool.CurrentPreview.ReasonText);
            _tool.Confirm();
            Assert.IsFalse(_tool.HasDraft);
            Assert.AreEqual(3, _host.World.Roads.SegmentCount, "Original split in two plus the branch.");
            int any = -1, other = -1;
            foreach (var s in _host.World.Roads.Segments)
            {
                if (any < 0) any = s.Id; else other = s.Id;
            }
            Assert.IsTrue(_host.World.Roads.AreConnected(any, other));
        }

        [UnityTest]
        public IEnumerator RightClickDeletesOneSegmentRightDragAndEmptyGroundDoNot()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            DrawExisting(40, 100, 120, 100);
            DrawExisting(40, 160, 120, 160);
            Assert.AreEqual(2, _host.World.Roads.SegmentCount);

            var on = Screen(80, 100);
            _tool.PointerDown(PressButton.Right, on);
            _tool.PointerMove(on + new Vector2(30f, 0f));
            _tool.PointerUp(PressButton.Right, on + new Vector2(30f, 0f), _time += 2f);
            Assert.AreEqual(2, _host.World.Roads.SegmentCount, "Right drag deletes nothing.");

            Click(80, 130, PressButton.Right);
            Assert.AreEqual(2, _host.World.Roads.SegmentCount, "Empty ground does nothing.");

            Click(80, 100, PressButton.Right);
            Assert.AreEqual(1, _host.World.Roads.SegmentCount, "Exactly one segment deleted.");
        }

        // Test building area covering x 60..80, z 60..80 (PL-03/PL-06).
        private void AddTestArea()
        {
            var P = new System.Func<double, double, LordsLegacy.Sim.Geometry.PointMm>(LordsLegacy.Sim.Geometry.PointMm.FromMetres);
            var shape = LordsLegacy.Sim.Areas.AreaShape.Straight(new[] { P(60, 60), P(80, 60), P(80, 80), P(60, 80) });
            var r = _host.Execute(new LordsLegacy.Sim.Commands.DesignateArea(shape, LordsLegacy.Sim.Areas.AreaTypes.TestId));
            Assert.IsTrue(r.IsAccepted, r.Text);
        }

        [UnityTest]
        public IEnumerator ClickInsideBuildingAreaAddsNothingAndHidesRing()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            AddTestArea();
            _tool.PointerMove(Screen(40, 100));
            var marker = _toolGo.GetComponentInChildren<CursorMarker>();
            Assert.IsTrue(marker != null && marker.IsShown, "Ring shows outside the area.");
            _tool.PointerMove(Screen(70, 70));
            Assert.IsFalse(marker.IsShown, "PL-03: ring hidden inside a building area.");
            Click(70, 70);
            Assert.AreEqual(0, _tool.PointCount, "PL-03: the click adds nothing.");
            Assert.IsFalse(marker.IsShown);
            Click(40, 100);
            Assert.AreEqual(1, _tool.PointCount, "Outside the area still works.");
        }

        [UnityTest]
        public IEnumerator DraftPointDragStopsAtAreaEdge()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            AddTestArea();
            Click(40, 70);
            Click(40, 120);
            _tool.PointerDown(PressButton.Left, Screen(40, 70));
            for (float x = 42; x <= 70; x += 2) _tool.PointerMove(Screen(x, 70));
            Assert.Less(_tool.Points[0].XMetres, 60.5, "Point stays outside the area.");
            Assert.Greater(_tool.Points[0].XMetres, 55.0, "Point reached the area edge.");
            _tool.PointerUp(PressButton.Left, Screen(70, 70), _time += 2f);
            Assert.AreEqual(2, _tool.PointCount);
        }

        [UnityTest]
        public IEnumerator ClickOnRoadInsideNothingStillBranchesWithAreaPresent()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            AddTestArea();
            DrawExisting(40, 100, 120, 100);
            var p = Screen(100, 100) + new Vector2(0f, 4f);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(1, _tool.PointCount, "A click on a road still starts a branch.");
            Assert.AreEqual(100.0, _tool.Points[0].ZMetres, 0.01);
        }

        [UnityTest]
        public IEnumerator NodeDragIntoAreaStopsAtItsEdge()
        {
            yield return Setup(SyntheticMap.CreateRamp());
            AddTestArea();
            DrawExisting(20, 70, 40, 70);
            int node = NodeNear(40, 70).Id;
            _tool.PointerDown(PressButton.Left, Screen(40, 70));
            for (float x = 42; x <= 72; x += 2) _tool.PointerMove(Screen(x, 70));
            Assert.IsTrue(_tool.IsDraggingNode);
            _tool.PointerUp(PressButton.Left, Screen(72, 70), _time += 2f);
            double nx = _host.World.Roads.GetNode(node).Position.XMetres;
            Assert.Less(nx, 60.5, "PL-06: node stops at the area edge.");
            Assert.Greater(nx, 55.0, "Node followed the cursor up to the edge.");
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
