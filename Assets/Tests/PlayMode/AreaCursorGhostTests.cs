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
    /// <summary>PlayMode tests for the area cursor sphere and ghost edges (AR-20, AR-21, T-164).</summary>
    public class AreaCursorGhostTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private AreaRenderer _renderer;
        private AreaTool _tool;
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
            _rendererGo = new GameObject("TestAreaRenderer");
            _renderer = _rendererGo.AddComponent<AreaRenderer>();
            _renderer.Configure(_host);
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            var panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);
            _toolGo = new GameObject("AreaTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<AreaTool>();
            _tool.SetViewSettings(new ViewSettings { Snapping = false });
            _tool.GhostEnabled = true; // the ghost is off by default (T-164 playtest)
            _tool.Configure(AreaTool.TestToolId, AreaTypes.TestId, _host, picker, _renderer, panel, _controller);
            _controller.Register(_tool);
            yield return null;
            _controller.Toggle(AreaTool.TestToolId);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z)
        {
            var p = Screen(x, z);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
        }

        [UnityTest]
        public IEnumerator SphereFollowsCursorFree()
        {
            yield return Setup();
            _tool.PointerMove(Screen(60, 70));
            var c = _tool.Cursor;
            Assert.IsNotNull(c);
            Assert.IsTrue(c.IsShown);
            Assert.AreEqual(CursorMarkerShape.Sphere, c.Shape);
            Assert.AreEqual(0.6f, c.DiameterM, 1e-4f);
            Assert.IsFalse(c.IsSnapped);
            Assert.AreEqual(60f, c.transform.position.x, 0.2f);
            Assert.AreEqual(70f, c.transform.position.z, 0.2f);
            Assert.AreEqual(0, _renderer.GhostEdgeCount, "No ghost without points.");
        }

        [UnityTest]
        public IEnumerator SphereSnapsToExistingPointVertexColour()
        {
            yield return Setup();
            _tool.SetViewSettings(new ViewSettings { Snapping = true });
            // Confirm a square so its corners are snap targets.
            Click(40, 40); Click(80, 40); Click(80, 80); Click(40, 80);
            _tool.Confirm();
            Assert.AreEqual(1, _host.World.Areas.Count);
            _tool.PointerMove(Screen(40.1f, 40.1f));
            var c = _tool.Cursor;
            Assert.IsTrue(c.IsSnapped);
            Assert.AreEqual(40f, c.transform.position.x, 0.05f);
            Assert.AreEqual(40f, c.transform.position.z, 0.05f);
        }

        [UnityTest]
        public IEnumerator GhostEdgesPresentWithTwoPointsAbsentWhileDraggingAndAfterClose()
        {
            yield return Setup();
            _tool.PointerMove(Screen(50, 50));
            Click(40, 40);
            _tool.PointerMove(Screen(50, 50));
            Assert.AreEqual(1, _renderer.GhostEdgeCount, "One point: one ghost edge.");
            Click(80, 40);
            _tool.PointerMove(Screen(70, 70));
            Assert.AreEqual(2, _renderer.GhostEdgeCount, "Last -> sphere -> point 1.");
            var status = _tool.CurrentPreview.ReasonText;

            // Drag point 2: ghost hidden during the drag.
            var down = Screen(80, 40);
            _tool.PointerDown(PressButton.Left, down);
            _tool.PointerMove(Screen(85, 45));
            _tool.PointerMove(Screen(90, 50));
            Assert.AreEqual(0, _renderer.GhostEdgeCount, "Ghost hidden while dragging a point.");
            _tool.PointerUp(PressButton.Left, Screen(90, 50), _time += 2f);
            _tool.PointerMove(Screen(70, 70));
            Assert.AreEqual(2, _renderer.GhostEdgeCount, "Ghost returns after the drag.");
            Assert.AreEqual(2, _tool.PointCount);
            Assert.AreEqual(status, _tool.CurrentPreview.ReasonText, "Ghost never changes the validated preview.");

            Click(90, 90); Click(40, 90);
            _tool.Confirm();
            Assert.IsFalse(_tool.HasDraft);
            Assert.AreEqual(0, _renderer.GhostEdgeCount, "Ghost hidden after close.");
        }

        [UnityTest]
        public IEnumerator DeactivateHidesSphereAndGhost()
        {
            yield return Setup();
            Click(40, 40);
            _tool.PointerMove(Screen(60, 60));
            Assert.IsTrue(_tool.Cursor.IsShown);
            _controller.Toggle(AreaTool.TestToolId);
            Assert.IsFalse(_tool.IsActive);
            Assert.IsFalse(_tool.Cursor.IsShown);
            Assert.AreEqual(0, _renderer.GhostEdgeCount);
        }
    }
}
