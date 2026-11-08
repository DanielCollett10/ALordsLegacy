using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="AreaTool"/> on a synthetic SimHost (T-114).</summary>
    public class AreaToolTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private AreaRenderer _renderer;
        private ToolPanelView _panel;
        private AreaTool _tool;
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

        private IEnumerator Setup(string toolId = AreaTool.TestToolId, string typeId = AreaTypes.TestId)
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
            _renderer = _rendererGo.AddComponent<AreaRenderer>();
            _renderer.Configure(_host);

            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("AreaTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<AreaTool>();
            _tool.Configure(toolId, typeId, _host, picker, _renderer, _panel, _controller);
            _controller.Register(_tool);
            yield return null; // let physics see the ground collider
            _controller.Toggle(toolId);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z, PressButton b = PressButton.Left)
        {
            var p = Screen(x, z);
            _tool.PointerDown(b, p);
            _time += 2f; // never a double click
            _tool.PointerUp(b, p, _time);
        }

        /// <summary>Four corners of a 40 m square starting at (40, 40): enough for a valid test area.</summary>
        private void ClickSquareMinusLast()
        {
            Click(40, 40); Click(80, 40); Click(80, 80);
        }

        [UnityTest]
        public IEnumerator FourPointsConfirmCreatesAreaAndClearsDraft()
        {
            yield return Setup();
            ClickSquareMinusLast();
            Assert.AreEqual(3, _tool.PointCount);
            Click(40, 80);
            Assert.AreEqual(4, _tool.PointCount);
            StringAssert.Contains("Test area", _panel.Status);
            StringAssert.Contains("4 points", _panel.Status);
            StringAssert.Contains("1,600 m²", _panel.Status);
            StringAssert.Contains("Valid", _panel.Status);
            Assert.IsTrue(_panel.IsConfirmEnabled);
            Assert.IsNotNull(_renderer.PreviewMesh);

            _host.World.Pause(); // works while paused
            _panel.ClickConfirm();
            Assert.AreEqual(1, _host.World.Areas.Count);
            Assert.IsFalse(_tool.HasDraft);
            StringAssert.Contains("no points", _panel.Status);
            Assert.IsNull(_renderer.PreviewMesh);
        }

        [UnityTest]
        public IEnumerator ThreePointsCannotBeConfirmedOrClosed()
        {
            yield return Setup();
            ClickSquareMinusLast();
            Assert.IsFalse(_tool.CurrentPreview.IsValid);
            StringAssert.Contains("too few points", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _tool.Confirm();
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Areas.Count, "Confirm does nothing with 3 points.");

            Click(40, 40); // click on point 1: must not close
            Assert.AreEqual(0, _host.World.Areas.Count, "Click on point 1 cannot close from 3 points.");
            Assert.AreEqual(3, _tool.PointCount, "A click on a placed point adds nothing.");

            // Enter key path.
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Enter));
            yield return null;
            Assert.AreEqual(0, _host.World.Areas.Count, "Enter cannot close from 3 points.");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;

            // Double-click on new ground: one point is added, no close.
            var p = Screen(40, 80);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(4, _tool.PointCount);
            Assert.AreEqual(0, _host.World.Areas.Count);
        }

        [UnityTest]
        public IEnumerator DoubleClickClosesFromFourPointsAddingOnePoint()
        {
            yield return Setup();
            ClickSquareMinusLast();
            var p = Screen(40, 80);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(4, _tool.PointCount);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 0.1f);
            Assert.AreEqual(1, _host.World.Areas.Count);
            Assert.AreEqual(4, new System.Collections.Generic.List<AreaRecord>(_host.World.Areas.All)[0].Shape.Vertices.Count);
            Assert.IsFalse(_tool.HasDraft);
        }

        [UnityTest]
        public IEnumerator DoubleClickWithThreePointsDoesNotClose()
        {
            yield return Setup();
            Click(40, 40); Click(80, 40);
            var p = Screen(80, 80);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 2f);
            _tool.PointerDown(PressButton.Left, p); _tool.PointerUp(PressButton.Left, p, _time += 0.1f);
            Assert.AreEqual(3, _tool.PointCount, "The pair adds exactly one point.");
            Assert.AreEqual(0, _host.World.Areas.Count);
        }

        [UnityTest]
        public IEnumerator ClickOnPointOneClosesRatherThanDrags()
        {
            yield return Setup();
            ClickSquareMinusLast();
            Click(40, 80);
            Assert.AreEqual(4, _tool.PointCount);
            var before = _tool.Points[0];
            Click(40, 40); // press and release on point 1, no movement
            Assert.AreEqual(1, _host.World.Areas.Count, "A click on point 1 closes the shape.");
            Assert.IsFalse(_tool.HasDraft);
            foreach (var a in _host.World.Areas.All) Assert.AreEqual(before, a.Shape.Vertices[0], "Point 1 did not move.");
        }

        [UnityTest]
        public IEnumerator DragMovesVertexWithoutClosingOrAdding()
        {
            yield return Setup();
            ClickSquareMinusLast();
            Click(40, 80);
            Vector3 camPos = _camGo.transform.position;

            _tool.PointerDown(PressButton.Left, Screen(40, 40)); // on point 1
            _tool.PointerMove(Screen(35, 35));
            _tool.PointerMove(Screen(30, 30));
            Assert.AreEqual(30.0, _tool.Points[0].XMetres, 0.5);
            Assert.AreEqual(30.0, _tool.Points[0].ZMetres, 0.5);
            _tool.PointerUp(PressButton.Left, Screen(30, 30), _time += 2f);
            Assert.AreEqual(4, _tool.PointCount, "A drag moves a point, it does not add one.");
            Assert.AreEqual(0, _host.World.Areas.Count, "Dragging point 1 must not close the shape.");
            Assert.AreEqual(30.0, _tool.Points[0].XMetres, 0.5);
            Assert.AreEqual(camPos, _camGo.transform.position);
        }

        [UnityTest]
        public IEnumerator MaxPointsStopsAtEight()
        {
            yield return Setup();
            float[,] pts = { { 40, 20 }, { 60, 20 }, { 80, 40 }, { 80, 60 }, { 60, 80 }, { 40, 80 }, { 20, 60 }, { 20, 40 } };
            for (int i = 0; i < 8; i++) Click(pts[i, 0], pts[i, 1]);
            Assert.AreEqual(8, _tool.PointCount);
            Click(100, 100);
            Assert.AreEqual(8, _tool.PointCount, "A ninth point is not added.");
        }

        [UnityTest]
        public IEnumerator BackspaceRemovesLastPoint()
        {
            yield return Setup();
            Click(40, 40); Click(80, 40); Click(80, 80);
            // Keyboard events are not delivered while the Game view is unfocused, so call what the key calls.
            _tool.RemoveLastPoint();
            Assert.AreEqual(2, _tool.PointCount);
            Assert.AreEqual(80.0, _tool.Points[1].XMetres, 0.5);
        }

        [UnityTest]
        public IEnumerator RightClickOnVertexRemovesItButEmptyGroundCancels()
        {
            yield return Setup();
            ClickSquareMinusLast();
            Click(80, 40, PressButton.Right);
            Assert.AreEqual(2, _tool.PointCount);
            Assert.AreEqual(80.0, _tool.Points[1].XMetres, 0.5, "Point (80,40) removed, (80,80) remains.");
            Assert.AreEqual(80.0, _tool.Points[1].ZMetres, 0.5);

            Click(150, 150, PressButton.Right);
            Assert.IsFalse(_tool.HasDraft, "Right click on empty ground cancels the draft.");
            StringAssert.Contains("no points", _panel.Status);
        }

        [UnityTest]
        public IEnumerator RightDragDoesNothing()
        {
            yield return Setup();
            ClickSquareMinusLast();
            foreach (var start in new[] { Screen(80, 40), Screen(150, 150) }) // on a vertex, and on empty ground
            {
                _tool.PointerDown(PressButton.Right, start);
                _tool.PointerMove(start + new Vector2(30f, 0f));
                _tool.PointerUp(PressButton.Right, start + new Vector2(30f, 0f), _time += 2f);
                Assert.AreEqual(3, _tool.PointCount, "Right drag must neither remove a point nor cancel.");
            }
        }

        [UnityTest]
        public IEnumerator RightDragDoesNotDeleteAnArea()
        {
            yield return Setup();
            Assert.IsTrue(_host.Execute(new DesignateArea(Square(), AreaTypes.TestId)).IsAccepted);
            var a = Screen(60, 60);
            _tool.PointerDown(PressButton.Right, a);
            _tool.PointerMove(a + new Vector2(30f, 0f));
            _tool.PointerUp(PressButton.Right, a + new Vector2(30f, 0f), _time += 2f);
            Assert.AreEqual(1, _host.World.Areas.Count);
        }

        [UnityTest]
        public IEnumerator EscapeCancelsThenSecondEscapeLeavesTool()
        {
            yield return Setup();
            Click(40, 40);
            Assert.IsTrue(_controller.HandleEscape(), "First Escape cancels the draft.");
            Assert.IsFalse(_tool.HasDraft);
            Assert.AreSame(_tool, _controller.ActiveTool);
            Assert.IsTrue(_controller.HandleEscape(), "Second Escape leaves the tool.");
            Assert.IsNull(_controller.ActiveTool);
        }

        [UnityTest]
        public IEnumerator CancelButtonCancelsDraft()
        {
            yield return Setup();
            Click(40, 40);
            _panel.ClickCancel();
            Assert.IsFalse(_tool.HasDraft);
        }

        [UnityTest]
        public IEnumerator InvalidReasonShownAndConfirmBlocked()
        {
            yield return Setup();
            // Zoom in so points 2 m apart are further apart than the 12 px hit radius at any Game view size.
            _camGo.transform.position = new Vector3(41f, 50f, 41f);
            _cam.orthographicSize = 1.5f;
            Click(40, 40); Click(42, 40); Click(42, 42); Click(40, 42); // 4 m², too small
            Assert.IsFalse(_tool.CurrentPreview.IsValid);
            StringAssert.Contains("too small", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _tool.Confirm();
            Assert.AreEqual(0, _host.World.Areas.Count);
            Assert.AreEqual(4, _tool.PointCount);
        }

        private static AreaShape Square()
        {
            return AreaShape.Straight(new[]
            {
                PointMm.FromMetres(40, 40), PointMm.FromMetres(80, 40), PointMm.FromMetres(80, 80), PointMm.FromMetres(40, 80),
            });
        }

        [UnityTest]
        public IEnumerator RightClickDeletesAreaOfThisKindOnlyWhenNoDraft()
        {
            yield return Setup();
            Assert.IsTrue(_host.Execute(new DesignateArea(Square(), AreaTypes.TestId)).IsAccepted);
            // A work area elsewhere is not of this tool's kind.
            var work = AreaShape.Straight(new[]
            {
                PointMm.FromMetres(120, 40), PointMm.FromMetres(160, 40), PointMm.FromMetres(160, 80), PointMm.FromMetres(120, 80),
            });
            Assert.IsTrue(_host.Execute(new DesignateArea(work, AreaTypes.TestWorkId)).IsAccepted);

            Click(140, 60, PressButton.Right);
            Assert.AreEqual(2, _host.World.Areas.Count, "Test area tool does not delete a work area.");
            Click(150, 150, PressButton.Right);
            Assert.AreEqual(2, _host.World.Areas.Count, "Empty ground deletes nothing.");

            Click(150, 150); // a draft exists: right click on the area cancels, deletes nothing
            Click(60, 60, PressButton.Right);
            Assert.AreEqual(2, _host.World.Areas.Count);
            Assert.IsFalse(_tool.HasDraft);

            Click(60, 60, PressButton.Right);
            Assert.AreEqual(1, _host.World.Areas.Count, "With no draft a right click deletes the area.");
        }

        [UnityTest]
        public IEnumerator WorkAreaToolDrawsAndDeletesWorkAreas()
        {
            yield return Setup(AreaTool.TestWorkToolId, AreaTypes.TestWorkId);
            Click(40, 40); Click(80, 40); Click(80, 80); Click(40, 80);
            StringAssert.Contains("Test work area", _panel.Status);
            _tool.Confirm();
            Assert.AreEqual(1, _host.World.Areas.Count);
            foreach (var a in _host.World.Areas.All) Assert.AreEqual(AreaTypes.TestWorkId, a.TypeId);
            Click(60, 60, PressButton.Right);
            Assert.AreEqual(0, _host.World.Areas.Count);
        }

        private void Drag(Vector2 from, Vector2 to)
        {
            _tool.PointerDown(PressButton.Left, from);
            _tool.PointerMove(Vector2.Lerp(from, to, 0.5f));
            _tool.PointerMove(to);
            _tool.PointerUp(PressButton.Left, to, _time += 2f);
        }

        private void FourSquare() { Click(40, 40); Click(80, 40); Click(80, 80); Click(40, 80); }

        private void DrawEastWestRoad()
        {
            Assert.IsTrue(_host.Execute(new DrawRoad(
                new[] { PointMm.FromMetres(20, 100), PointMm.FromMetres(180, 100) }, "road.dirt_path")).IsAccepted);
        }

        [UnityTest]
        public IEnumerator ClickOnEdgeInsertsPointThereUpToMax()
        {
            yield return Setup();
            FourSquare();
            Click(50, 40); // on edge 0, away from its handle at (60, 40)
            Assert.AreEqual(5, _tool.PointCount);
            Assert.AreEqual(50.0, _tool.Points[1].XMetres, 0.5);
            Assert.AreEqual(40.0, _tool.Points[1].ZMetres, 0.5);
            Assert.AreEqual(80.0, _tool.Points[2].XMetres, 0.5, "Later points shift up by one.");
            Click(72, 40); Click(80, 50); Click(80, 74);
            Assert.AreEqual(8, _tool.PointCount);
            int before = _tool.PointCount;
            Click(40, 60);
            Assert.AreEqual(before, _tool.PointCount, "No insert beyond the maximum.");
        }

        [UnityTest]
        public IEnumerator DraggingHandleBendsEdgeThroughCursorAndDoubleClickStraightens()
        {
            yield return Setup();
            FourSquare();
            Assert.AreEqual(AreaEdgeKind.Straight, _tool.Edges[0].Kind);
            Drag(Screen(60, 40), Screen(60, 28));
            Assert.AreEqual(4, _tool.PointCount, "Dragging a handle adds no point.");
            Assert.AreEqual(AreaEdgeKind.Bent, _tool.Edges[0].Kind);
            Assert.AreEqual(28.0, _tool.Edges[0].Handle.ZMetres, 0.5);
            double min = double.MaxValue;
            foreach (var p in _tool.CurrentPreview.Polygon)
                min = System.Math.Min(min, p.DistanceTo(PointMm.FromMetres(60, 28)));
            Assert.Less(min, 1500.0, "The flattened outline passes through the cursor.");

            var h = Screen(60, 28);
            _tool.PointerDown(PressButton.Left, h); _tool.PointerUp(PressButton.Left, h, _time += 2f);
            Assert.AreEqual(AreaEdgeKind.Bent, _tool.Edges[0].Kind, "A single click on a handle does nothing.");
            _tool.PointerDown(PressButton.Left, h); _tool.PointerUp(PressButton.Left, h, _time += 0.1f);
            Assert.AreEqual(AreaEdgeKind.Straight, _tool.Edges[0].Kind);
            Assert.AreEqual(4, _tool.PointCount);
            Assert.AreEqual(0, _host.World.Areas.Count, "Straightening does not close the shape.");
        }

        [UnityTest]
        public IEnumerator SnappedVerticesOnSameRoadSideMakeEdgeFollowRoad()
        {
            yield return Setup();
            DrawEastWestRoad();
            _tool.SetViewSettings(new ViewSettings());
            Click(60, 100.9f); Click(140, 100.9f); Click(140, 150); Click(60, 150);
            var seg = new System.Collections.Generic.List<LordsLegacy.Sim.Roads.RoadSegment>(_host.World.Roads.Segments)[0];
            double sideZ = seg.SideLine(_tool.Edges[0].Side)[0].ZMetres;
            Assert.AreEqual(100.0 + (sideZ > 100 ? 1.25 : -1.25), _tool.Points[0].ZMetres, 0.01, "Point snapped to the side line.");
            Assert.AreEqual(AreaEdgeKind.FollowsRoad, _tool.Edges[0].Kind);
            Assert.AreEqual(seg.Id, _tool.Edges[0].SegmentId);
            for (int i = 1; i < 4; i++) Assert.AreNotEqual(AreaEdgeKind.FollowsRoad, _tool.Edges[i].Kind);
        }

        [UnityTest]
        public IEnumerator SnappingOffNothingSnapsAndNoEdgeFollowsRoad()
        {
            yield return Setup();
            DrawEastWestRoad();
            _tool.SetViewSettings(new ViewSettings { Snapping = false });
            Click(60, 100.9f); Click(140, 100.9f); Click(140, 150); Click(60, 150);
            Assert.AreEqual(100.9, _tool.Points[0].ZMetres, 0.05, "No snapping to the road side.");
            for (int i = 0; i < 4; i++) Assert.AreNotEqual(AreaEdgeKind.FollowsRoad, _tool.Edges[i].Kind);
        }

        [UnityTest]
        public IEnumerator SnapsToAreaVertexWithinSnapDistance()
        {
            yield return Setup();
            _tool.SetViewSettings(new ViewSettings());
            Assert.IsTrue(_host.Execute(new DesignateArea(Square(), AreaTypes.TestId)).IsAccepted);
            Click(40.3f, 40.3f);
            Assert.AreEqual(PointMm.FromMetres(40, 40), _tool.Points[0]);
            _panel.ClickCancel();
            _tool.SetViewSettings(new ViewSettings { Snapping = false });
            Click(40.3f, 40.3f);
            Assert.AreNotEqual(PointMm.FromMetres(40, 40), _tool.Points[0]);
        }

        private int CountMarkers(SnapMarkerKind kind)
        {
            int n = 0;
            foreach (var m in _tool.ShownMarkers) if (m.Kind == kind) n++;
            return n;
        }

        private bool HasMarker(SnapMarkerKind kind, PointMm p)
        {
            foreach (var m in _tool.ShownMarkers) if (m.Kind == kind && m.Point == p) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator RoadDotsShowNearTheCursorAndNotFarAndDoNotMove()
        {
            yield return Setup();
            DrawEastWestRoad();
            _tool.SetViewSettings(new ViewSettings());
            _tool.PointerMove(Screen(100, 105));
            Assert.Greater(CountMarkers(SnapMarkerKind.RoadDot), 10);
            var seg = new System.Collections.Generic.List<LordsLegacy.Sim.Roads.RoadSegment>(_host.World.Roads.Segments)[0];
            double sideZ = seg.SideLine(LordsLegacy.Sim.Roads.RoadSide.Left)[0].ZMetres;
            var dot = PointMm.FromMetres(60, sideZ);
            Assert.IsTrue(HasMarker(SnapMarkerKind.RoadDot, dot) || HasMarker(SnapMarkerKind.RoadDot, PointMm.FromMetres(60, 200 - sideZ)));
            Assert.AreEqual(_tool.ShownMarkers.Count, _tool.VisibleMarkerCount);
            foreach (var m in _tool.ShownMarkers) Assert.LessOrEqual(m.Point.DistanceTo(PointMm.FromMetres(100, 105)), 25001.0);

            _tool.PointerMove(Screen(104, 105)); // markers depend on the road, not the cursor
            Assert.IsTrue(HasMarker(SnapMarkerKind.RoadDot, PointMm.FromMetres(100, sideZ)) || HasMarker(SnapMarkerKind.RoadDot, PointMm.FromMetres(100, 200 - sideZ)));
            Assert.IsTrue(HasMarker(SnapMarkerKind.RoadDot, PointMm.FromMetres(105, sideZ)) || HasMarker(SnapMarkerKind.RoadDot, PointMm.FromMetres(105, 200 - sideZ)));

            _tool.PointerMove(Screen(100, 155)); // more than 25 m from both sides
            Assert.AreEqual(0, _tool.ShownMarkers.Count);
            Assert.AreEqual(0, _tool.VisibleMarkerCount);

            _tool.SetViewSettings(new ViewSettings { Snapping = false });
            _tool.PointerMove(Screen(100, 105));
            Assert.AreEqual(0, _tool.ShownMarkers.Count);
            _tool.SetViewSettings(new ViewSettings());
            _tool.PointerMove(Screen(100, 105));
            Assert.Greater(_tool.ShownMarkers.Count, 0);
            _controller.Toggle(AreaTool.TestToolId); // tool off
            Assert.AreEqual(0, _tool.ShownMarkers.Count);
            Assert.AreEqual(0, _tool.VisibleMarkerCount);
        }

        [UnityTest]
        public IEnumerator AreaCornersAndEdgeDotsShow()
        {
            yield return Setup();
            _tool.SetViewSettings(new ViewSettings());
            Assert.IsTrue(_host.Execute(new DesignateArea(Square(), AreaTypes.TestId)).IsAccepted);
            _tool.PointerMove(Screen(50, 50));
            Assert.IsTrue(HasMarker(SnapMarkerKind.Corner, PointMm.FromMetres(40, 40)));
            Assert.IsTrue(HasMarker(SnapMarkerKind.AreaDot, PointMm.FromMetres(50, 40)));
            Assert.IsTrue(HasMarker(SnapMarkerKind.AreaDot, PointMm.FromMetres(40, 52.5)));
            Assert.IsFalse(HasMarker(SnapMarkerKind.AreaDot, PointMm.FromMetres(40, 40)), "A corner is not also a dot.");
            Assert.IsFalse(HasMarker(SnapMarkerKind.Corner, PointMm.FromMetres(80, 80)), "Out of range.");
        }

        [UnityTest]
        public IEnumerator SnapOrderIsCornerThenRoadDotThenAreaDot()
        {
            yield return Setup();
            DrawEastWestRoad();
            _tool.SetViewSettings(new ViewSettings());
            var seg = new System.Collections.Generic.List<LordsLegacy.Sim.Roads.RoadSegment>(_host.World.Roads.Segments)[0];
            double sideZ = seg.SideLine(LordsLegacy.Sim.Roads.RoadSide.Left)[0].ZMetres;
            double sign = sideZ > 100 ? 1 : -1;
            double zv = 100 + sign * 1.75, zClick = 100 + sign * 1.4;
            var shape = AreaShape.Straight(new[]
            {
                PointMm.FromMetres(40, zv), PointMm.FromMetres(80, zv),
                PointMm.FromMetres(80, zv + sign * 40), PointMm.FromMetres(40, zv + sign * 40),
            });
            Assert.IsTrue(_host.Execute(new DesignateArea(shape, AreaTypes.TestId)).IsAccepted);
            Click(40, (float)zClick); // corner and road dot both in range: the corner wins
            Assert.AreEqual(PointMm.FromMetres(40, zv), _tool.Points[0]);
            Click(60, (float)zClick); // road dot and area-edge dot in range: the road dot wins
            Assert.AreEqual(PointMm.FromMetres(60, 100 + sign * 1.25), _tool.Points[1]);
        }

        [UnityTest]
        public IEnumerator TerracedPlotSnappedToNeighbourConfirms()
        {
            yield return Setup();
            _tool.SetViewSettings(new ViewSettings());
            Assert.IsTrue(_host.Execute(new DesignateArea(Square(), AreaTypes.TestId)).IsAccepted);
            Click(80.2f, 45.2f); Click(120, 45); Click(120, 80); Click(80.2f, 80.2f);
            Assert.AreEqual(PointMm.FromMetres(80, 45), _tool.Points[0], "Snapped to an edge dot.");
            Assert.AreEqual(PointMm.FromMetres(80, 80), _tool.Points[3], "Snapped to a corner.");
            Assert.IsTrue(_tool.CurrentPreview.IsValid, _tool.CurrentPreview.ReasonText);
            _tool.Confirm();
            Assert.AreEqual(2, _host.World.Areas.Count);
        }

        private void AssertHourglassRefused(int expectedPoints)
        {
            Assert.AreEqual(expectedPoints, _tool.PointCount);
            Assert.IsFalse(_tool.CurrentPreview.IsValid);
            StringAssert.Contains("edges cross", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
            _tool.Confirm();
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Areas.Count, "An hourglass is never designated.");
            Assert.AreEqual(expectedPoints, _tool.PointCount, "The draft is kept.");
            StringAssert.Contains("edges cross", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);
        }

        /// <summary>Whatever the draft is, a refused shape can't be confirmed and a valid one can (AR-11).</summary>
        private void AssertPanelMatchesValidity()
        {
            Assert.AreEqual(_tool.CurrentPreview.IsValid, _panel.IsConfirmEnabled);
            if (_tool.CurrentPreview.IsValid) return;
            StringAssert.Contains("edges cross", _panel.Status);
            AssertHourglassRefused(_tool.PointCount);
        }

        [UnityTest]
        public IEnumerator ClickingFourPointsInCrossingOrderIsReorderedIntoAValidShape()
        {
            yield return Setup();
            Click(40, 40); Click(80, 80); Click(80, 40); Click(40, 80);
            Assert.AreEqual(4, _tool.PointCount);
            Assert.IsTrue(LordsLegacy.Sim.Geometry.Polygons.IsSimple(_tool.Points), "Points were auto-reordered.");
            Assert.AreEqual(PointMm.FromMetres(40, 40), _tool.Points[0], "Point 1 stays first.");
            Assert.IsTrue(_tool.CurrentPreview.IsValid);
            Assert.IsTrue(_panel.IsConfirmEnabled);
            StringAssert.Contains("1,600 m²", _panel.Status);
            Click(40, 40); // a click on point 1 closes the now valid shape
            Assert.AreEqual(1, _host.World.Areas.Count);
        }

        [UnityTest]
        public IEnumerator DraggingAPointIntoAnHourglassIsReordered()
        {
            yield return Setup();
            FourSquare();
            Assert.IsTrue(_tool.CurrentPreview.IsValid);
            Drag(Screen(80, 40), Screen(80, 100));
            Assert.AreEqual(4, _tool.PointCount);
            Assert.IsTrue(LordsLegacy.Sim.Geometry.Polygons.IsSimple(_tool.Points));
            AssertPanelMatchesValidity();
            Assert.IsTrue(_panel.IsConfirmEnabled);
        }

        [UnityTest]
        public IEnumerator BendingAnEdgeIntoAnHourglassIsRefused()
        {
            yield return Setup();
            FourSquare();
            Drag(Screen(60, 40), Screen(60, 100));
            Assert.AreEqual(AreaEdgeKind.Bent, _tool.Edges[0].Kind);
            AssertHourglassRefused(4);
        }

        [UnityTest]
        public IEnumerator InsertingAPointThatWouldCrossIsFixedOrRefused()
        {
            yield return Setup();
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _cam.orthographicSize = 20f;
            float u = 1f / (Screen(0, 101).y - Screen(0, 100).y); // metres per pixel (screen y grows with z)
            // A body with a thin arm whose tip sits just below the bottom edge (the last edge, Br -> Bl).
            Click(88, 100); Click(98, 100 - 2 * u); Click(98, 100 - 20 * u); Click(86, 100 - 20 * u);
            Click(88, 112); Click(112, 112); Click(112, 100);
            Assert.AreEqual(7, _tool.PointCount);
            Assert.IsTrue(_tool.CurrentPreview.IsValid, _panel.Status);
            // Insert on the bottom edge, 7 px below it: the new edge cuts across the arm's tip.
            var p = Screen(106, 100) + new Vector2(0f, -7f);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
            Assert.AreEqual(8, _tool.PointCount);
            AssertPanelMatchesValidity(); // reordered into a valid shape, or refused: never confirmable while crossing
        }

        [Test]
        public void BuildBarEnablesBothAreaEntries()
        {
            foreach (var e in ToolEntry.Defaults())
                if (e.Id == AreaTool.TestToolId || e.Id == AreaTool.TestWorkToolId) Assert.IsTrue(e.Enabled, e.Id);
        }
    }
}
