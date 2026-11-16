using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="StructureTool"/> (T-424, food.md FD-30, FD-30b).</summary>
    public class StructureToolTests
    {
        private GameObject _hostGo, _panelGo, _toolGo, _camGo, _groundGo;
        private UnityEngine.UIElements.PanelSettings _panelSettings;
        private SimHost _host;
        private ToolPanelView _panel;
        private StructureTool _tool;
        private ToolController _controller;
        private Camera _cam;
        private float _time;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _hostGo, _panelGo, _toolGo, _camGo, _groundGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
        }

        private IEnumerator Setup(int logs)
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(PointMm.FromMetres(60, 40)), Items.Log, logs);
            _camGo = new GameObject("TestCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 60f;
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _groundGo = new GameObject("TestGround");
            _groundGo.transform.position = new Vector3(100f, -0.5f, 100f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);
            var pickerGo = new GameObject("Picker");
            pickerGo.transform.SetParent(_groundGo.transform, false);
            var picker = pickerGo.AddComponent<GroundPicker>();
            picker.PickCamera = _cam; picker.Host = _host;
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("GranaryTool");
            _controller = _toolGo.AddComponent<ToolController>();
            _tool = _toolGo.AddComponent<StructureTool>();
            _tool.Configure(StructureTool.GranaryToolId, ModuleTypes.GranaryId, _host, picker, _panel, _controller);
            _controller.Register(_tool);
            yield return null;
            _controller.Toggle(StructureTool.GranaryToolId);
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z)
        {
            var p = Screen(x, z);
            _tool.PointerMove(p);
            _tool.PointerDown(PressButton.Left, p);
            _tool.PointerUp(PressButton.Left, p, _time += 2f);
        }

        [UnityTest]
        public IEnumerator GhostShowsAndClickPlacesScaffold()
        {
            yield return Setup(40);
            Assert.AreSame(_tool, _controller.ActiveTool);
            _tool.PointerMove(Screen(100, 100));
            Assert.IsTrue(_tool.GhostVisible, "Building and yard outline drawn.");
            Assert.IsFalse(_tool.GhostIsRed, _tool.Preview?.Text);
            Assert.AreEqual(0, _host.World.Footprints.All.Count(f => f.Type.Id == ModuleTypes.GranaryId));
            Click(100, 100);
            Assert.AreEqual(1, _host.World.Footprints.All.Count(f => f.Type.Id == ModuleTypes.GranaryId), "Click sends PlaceStructure.");
        }

        [UnityTest]
        public IEnumerator RotateKeysTurnGhostAndWrap()
        {
            yield return Setup(40);
            _tool.Rotate(StructureTool.RotateStepDeg);
            Assert.AreEqual(15.0, _tool.Rotation, 1e-9);
            _tool.Rotate(-StructureTool.FineRotateStepDeg);
            Assert.AreEqual(14.0, _tool.Rotation, 1e-9);
            _tool.Rotate(-30);
            Assert.AreEqual(344.0, _tool.Rotation, 1e-9);
        }

        [UnityTest]
        public IEnumerator UnaffordableGhostIsRedWithReasonAndClickDoesNothing()
        {
            yield return Setup(0);
            _tool.PointerMove(Screen(100, 100));
            Assert.IsTrue(_tool.GhostIsRed);
            StringAssert.Contains("Not enough logs", _tool.Preview.Text);
            StringAssert.Contains("Not enough logs", _panel.Status);
            Click(100, 100);
            Assert.AreEqual(0, _host.World.Footprints.All.Count(f => f.Type.Id == ModuleTypes.GranaryId));
        }

        [UnityTest]
        public IEnumerator RightClickLeavesTheTool()
        {
            yield return Setup(40);
            var p = Screen(100, 100);
            _tool.PointerMove(p);
            _tool.PointerDown(PressButton.Right, p);
            _tool.PointerUp(PressButton.Right, p, _time += 2f);
            Assert.IsNull(_controller.ActiveTool);
            Assert.IsFalse(_tool.GhostVisible);
        }
    }
}
