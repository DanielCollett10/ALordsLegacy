using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="OperationDraftTool"/> (T-390, building-tool.md BT-10..18).</summary>
    public class OperationDraftToolTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private PanelSettings _panelSettings;
        private SimHost _host;
        private ToolPanelView _panel;
        private AreaTool _area;
        private OperationDraftTool _tool;
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
            _rendererGo = new GameObject("TestAreaRenderer");
            var renderer = _rendererGo.AddComponent<AreaRenderer>();
            renderer.Configure(_host);
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("DraftTools");
            _controller = _toolGo.AddComponent<ToolController>();
            _area = _toolGo.AddComponent<AreaTool>();
            _area.Configure("woodcutter-draw", AreaTypes.WoodcuttingId, _host, picker, renderer, _panel, _controller);
            _tool = _toolGo.AddComponent<OperationDraftTool>();
            _tool.Configure("woodcutter", OperationTypes.WoodcuttingId, _host, picker, _area, renderer, _panel, _controller);
            _controller.Register(_tool);
            yield return null;
            _controller.Toggle("woodcutter");
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z)
        {
            var p = Screen(x, z);
            _area.PointerDown(PressButton.Left, p);
            _area.PointerUp(PressButton.Left, p, _time += 2f);
        }

        private void LockSquare()
        {
            Click(40, 40); Click(80, 40); Click(80, 80); Click(40, 80);
            _panel.ClickConfirm();
        }

        [UnityTest]
        public IEnumerator WoodcutterEntryActivatesDraftToolAndOtherAreasKeepAreaTool()
        {
            yield return Setup();
            Assert.AreSame(_tool, _controller.ActiveTool);
            Assert.IsTrue(_area.IsActive, "Area stage is drawn by the composed AreaTool.");
            Assert.AreEqual(DraftStage.AreaStage, _tool.Draft.Stage);
            Assert.IsNotNull(_area.LockHandler, "The composed AreaTool hands its shape to the draft tool.");
            Assert.AreEqual("woodcutter", _tool.Id);
        }

        [UnityTest]
        public IEnumerator ConfirmingAreaLocksItAndShowsPaletteWithCosts()
        {
            yield return Setup();
            int logBefore = _host.World.CommandLog.Count;
            LockSquare();
            Assert.AreEqual(DraftStage.ModuleStage, _tool.Draft.Stage, _panel.Status);
            Assert.AreEqual(logBefore, _host.World.CommandLog.Count, "Locking the area sends nothing.");
            Assert.AreEqual(0, _host.World.Areas.Count);
            var names = _panel.ActionNames;
            CollectionAssert.AreEqual(new[]
            {
                OperationDraftTool.PickPrefix + ModuleTypes.WoodcuttingHutId,
                OperationDraftTool.PickPrefix + ModuleTypes.LogStorageId,
                OperationDraftTool.PickPrefix + ModuleTypes.OxenStandId
            }, names);
            StringAssert.Contains("Cost 0 logs", _panel.Status);
        }

        [UnityTest]
        public IEnumerator RightClickOnEmptyGroundDoesNothingAndGhostClickIsCheckedBySim()
        {
            yield return Setup();
            LockSquare();
            _panel.ClickAction(OperationDraftTool.PickPrefix + ModuleTypes.LogStorageId);
            Assert.AreEqual(ModuleTypes.LogStorageId, _tool.Draft.ModuleInHand);
            Assert.IsFalse(_tool.RemoveAt(Screen(150, 150)), "Right-click on empty ground does nothing.");
            _tool.ClickAt(Screen(60, 60));
            if (_tool.Draft.Ghosts.Count == 1)
            {
                Assert.AreEqual(1, _tool.PlacedGhostsDrawn);
                Assert.IsFalse(_tool.RemoveAt(Screen(150, 150)), "Empty ground still does nothing with a ghost placed.");
                Assert.IsTrue(_tool.RemoveAt(Screen(60, 60)), "Right-click on the ghost removes it.");
                Assert.AreEqual(0, _tool.Draft.Ghosts.Count);
            }
            else Assert.IsNotNull(_tool.Message, "A refused ghost shows the Sim's reason.");
        }

        [UnityTest]
        public IEnumerator EscapeDropsModuleThenCancelsAndLeavesCommandLogUnchanged()
        {
            yield return Setup();
            int logBefore = _host.World.CommandLog.Count;
            LockSquare();
            _tool.Pick(ModuleTypes.LogStorageId);
            Assert.IsTrue(_controller.HandleEscape(), "First Escape drops the module in hand.");
            Assert.IsNull(_tool.Draft.ModuleInHand);
            Assert.AreSame(_tool, _controller.ActiveTool, "Tool stays active after dropping the module.");
            Assert.IsTrue(_controller.HandleEscape(), "Second Escape cancels the draft.");
            Assert.IsNull(_controller.ActiveTool);
            Assert.AreEqual(logBefore, _host.World.CommandLog.Count, "Cancel leaves the Sim command log unchanged.");
            Assert.AreEqual(0, _host.World.Areas.Count);
            Assert.IsFalse(_panel.IsShown);
        }

        [UnityTest]
        public IEnumerator CancelButtonLeavesCommandLogUnchanged()
        {
            yield return Setup();
            int logBefore = _host.World.CommandLog.Count;
            LockSquare();
            _panel.ClickCancel();
            Assert.IsNull(_controller.ActiveTool);
            Assert.AreEqual(logBefore, _host.World.CommandLog.Count);
        }

        private void SeedLogs(int n)
        {
            var inv = _host.World.Inventory;
            inv.Produce(inv.AddStockpile(LordsLegacy.Sim.Geometry.PointMm.FromMetres(95, 95)), LordsLegacy.Sim.Inventory.Items.Log, n);
        }

        [UnityTest]
        public IEnumerator FullFlowCostGateConfirmSendsOneCommandAndSitesAppear()
        {
            yield return Setup();
            SeedLogs(40);
            int logBefore = _host.World.CommandLog.Count;
            LockSquare();
            StringAssert.Contains("Cost 0 logs, have 40", _panel.Status);
            StringAssert.Contains("Place a Hut", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled, "Confirm disabled while the hut is missing.");

            _tool.Pick(ModuleTypes.WoodcuttingHutId);
            _tool.ClickAt(Screen(60, 60));
            Assert.AreEqual(1, _tool.Draft.Ghosts.Count, _tool.Message);
            StringAssert.Contains("Cost 8 logs, have 40", _panel.Status);
            StringAssert.Contains("Place a Log storage", _panel.Status);
            Assert.IsFalse(_panel.IsConfirmEnabled);

            _tool.Pick(ModuleTypes.LogStorageId);
            _tool.ClickAt(Screen(50, 70));
            Assert.AreEqual(2, _tool.Draft.Ghosts.Count, _tool.Message);
            StringAssert.Contains("Cost " + _tool.Draft.RunningCost + " logs, have 40", _panel.Status);
            Assert.IsTrue(_panel.IsConfirmEnabled, _panel.Status);
            Assert.AreEqual(logBefore, _host.World.CommandLog.Count, "Nothing is sent before Confirm.");

            _panel.ClickConfirm();
            Assert.AreEqual(logBefore + 1, _host.World.CommandLog.Count, "Confirm sends exactly one command.");
            Assert.IsNull(_controller.ActiveTool, "The draft closes on success.");
            Assert.IsFalse(_panel.IsShown);
            Assert.AreEqual(1, _host.World.Operations.Count);
            int sites = 0;
            foreach (var s in _host.World.Construction.Sites) sites++;
            Assert.AreEqual(2, sites, "One site per module.");
        }

        [UnityTest]
        public IEnumerator CancelDiscardsAreaAndGhosts()
        {
            yield return Setup();
            SeedLogs(40);
            int logBefore = _host.World.CommandLog.Count;
            LockSquare();
            _tool.Pick(ModuleTypes.WoodcuttingHutId);
            _tool.ClickAt(Screen(60, 60));
            Assert.AreEqual(1, _tool.Draft.Ghosts.Count);
            _panel.ClickCancel();
            Assert.IsNull(_controller.ActiveTool);
            Assert.AreEqual(logBefore, _host.World.CommandLog.Count);
            Assert.AreEqual(0, _host.World.Operations.Count);
            Assert.AreEqual(0, _host.World.Areas.Count);
        }

        [UnityTest]
        public IEnumerator QAndERotateInFifteenDegreeSteps()
        {
            yield return Setup();
            LockSquare();
            _tool.Rotate(OperationDraftTool.RotateStepDeg);
            Assert.AreEqual(15.0, _tool.Rotation, 1e-6);
            _tool.Rotate(-OperationDraftTool.FineRotateStepDeg);
            Assert.AreEqual(14.0, _tool.Rotation, 1e-6);
        }
    }
}
