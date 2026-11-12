using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the selection order in <see cref="AgentPicker"/> and <see cref="OperationPanelView"/> (T-281).</summary>
    public class OperationPanelTests
    {
        private GameObject _hostGo, _camGo, _pickGo, _agentsGo, _uiGo, _controllerGo, _toolGo, _groundGo;
        private PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private GroundPicker _ground;
        private AgentPicker _agents;
        private OperationPanelView _view;
        private ToolController _controller;
        private Operation _op;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;

            var r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, "setup designate: " + r.Text + " " + r.Reason);
            _op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(_op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(112, 100), 0);
            w.Operations.AddModule(_op.Id, 2);

            var pop = w.People;
            for (int f = 1; f <= 3; f++)
            {
                pop.AddFamily(f, f == 1 ? "Archer" : f == 2 ? "Brook" : "Cole", 60, 60);
                pop.AddCitizen(f, "Person" + f, Sex.Male, -30L * Citizen.TicksPerYear / 2, f, 60, 60);
            }
            pop.FindCitizen(1).X = 100; pop.FindCitizen(1).Z = 100; // standing on the hut

            _camGo = new GameObject("Cam");
            _cam = _camGo.AddComponent<Camera>();
            float h = (float)w.Terrain.HeightAt(100, 100);
            _camGo.transform.position = new Vector3(100, h + 200, 100);
            _camGo.transform.rotation = Quaternion.Euler(90, 0, 0);
            _cam.farClipPlane = 2000;

            // Ground for the pick ray: a slab tilted to the ramp (height = 0.1 * x), top surface through (100, h, 100).
            _groundGo = new GameObject("GroundSlab");
            float tilt = Mathf.Atan(0.1f) * Mathf.Rad2Deg;
            _groundGo.transform.rotation = Quaternion.Euler(0, 0, tilt);
            var normal = _groundGo.transform.up;
            _groundGo.transform.position = new Vector3(100, h, 100) - normal * 0.5f;
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(600, 1, 600);

            _pickGo = new GameObject("GroundPicker");
            _ground = _pickGo.AddComponent<GroundPicker>();
            _ground.PickCamera = _cam;
            _ground.Host = _host;

            _controllerGo = new GameObject("ToolController");
            _controller = _controllerGo.AddComponent<ToolController>();

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, _ground, _controller);

            _uiGo = new GameObject("OperationPanel");
            _uiGo.SetActive(false);
            _settings = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _settings;
            _view = _uiGo.AddComponent<OperationPanelView>();
            _view.Configure(_host, _agents, _controller);
            _uiGo.SetActive(true);
            // Only the panel's own elements may take clicks; a theme-less test root must not count as "over UI".
            _uiGo.GetComponent<UIDocument>().rootVisualElement.pickingMode = PickingMode.Ignore;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _groundGo, _toolGo, _uiGo, _agentsGo, _controllerGo, _pickGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Vector2 ScreenOf(double x, double z)
        {
            Assert.IsTrue(_ground.TryWorldToScreen((float)x, (float)z, out var s), "ScreenOf " + x + "," + z);
            return s;
        }

        private void MoveCitizenAway() { var c = _host.World.People.FindCitizen(1); c.X = 60; c.Z = 60; }

        private IEnumerator OpenPanel()
        {
            MoveCitizenAway();
            yield return null;
            _agents.ClickAt(ScreenOf(100, 100));
            yield return null;
            Assert.IsTrue(_view.IsShown);
        }

        [UnityTest]
        public IEnumerator PickOrder_CitizenOnHutBeatsModule_ThenModule_ThenBuildingArea_ThenWorkArea()
        {
            yield return null;
            // A citizen stands on the hut: the citizen is picked, not the hut.
            _agents.ClickAt(ScreenOf(100, 100));
            Assert.IsTrue(_agents.HasSelection);
            Assert.IsFalse(_agents.HasOperation);

            // Citizen gone: the hut (a module) is picked and the agent selection is dropped.
            MoveCitizenAway();
            _agents.ClickAt(ScreenOf(100, 100));
            Assert.IsFalse(_agents.HasSelection);
            Assert.IsTrue(_agents.HasOperation);
            Assert.AreEqual(_op.Id, _agents.SelectedOperationId);

            // Storage module, then empty ground inside the building area.
            _agents.ClickAt(ScreenOf(60, 150));
            Assert.IsFalse(_agents.HasOperation);
            _agents.ClickAt(ScreenOf(112, 100));
            Assert.AreEqual(_op.Id, _agents.SelectedOperationId);
            _agents.ClearOperation();
            _agents.ClickAt(ScreenOf(117, 108));
            Assert.AreEqual(_op.Id, _agents.SelectedOperationId, "building area");

            // A work area counts only once the operation uses it.
            var r = _host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(140, 140), M(160, 140), M(160, 160), M(140, 160) }), AreaTypes.WoodcuttingWorkId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _agents.ClickAt(ScreenOf(150, 150));
            Assert.IsFalse(_agents.HasOperation, "unlinked work area");
            Assert.IsTrue(_host.Execute(new SetWorkArea(_op.Id, (int)r.Created[0])).IsAccepted);
            _agents.ClickAt(ScreenOf(150, 150));
            Assert.AreEqual(_op.Id, _agents.SelectedOperationId, "linked work area");
        }

        [UnityTest]
        public IEnumerator SelectingHutOpensPanelWithStatusModulesTreesAndLogs()
        {
            yield return OpenPanel();
            Assert.AreEqual(DisplayStyle.Flex, _view.PanelElement.style.display.value);
            Assert.AreEqual("Status: No workers", _view.StatusText);
            StringAssert.Contains("Hut", _view.ModulesText);
            StringAssert.Contains("Log storage", _view.ModulesText);
            Assert.AreEqual("Logs on the ground: 0", _view.LogsText);
            var region = OperationStatus.WorkRegion(_host.World, _op);
            Assert.AreEqual("Standing trees in work area: " + _host.World.Trees.CountStandingIn(region), _view.TreesText);
            Assert.AreEqual(2, OperationTypes.Find(_op.TypeId).FamilySlots);
            Assert.IsNotNull(_view.Find<Button>("assign-slot-0"));
            Assert.IsNotNull(_view.Find<Button>("assign-slot-1"));
            Assert.IsFalse(_view.Find<Button>("assign-slot-0").focusable);
            Assert.AreNotEqual(DisplayStyle.None, _view.PanelElement.resolvedStyle.display);
            Assert.AreEqual(Visibility.Visible, _view.PanelElement.resolvedStyle.visibility);
            Assert.Greater(_view.PanelElement.worldBound.width, 0f);
            Assert.Greater(_view.PanelElement.worldBound.height, 0f);
        }

        [UnityTest]
        public IEnumerator SelectingTheWorkAreaOpensPanel()
        {
            var r = _host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(140, 140), M(160, 140), M(160, 160), M(140, 160) }), AreaTypes.WoodcuttingWorkId));
            _host.Execute(new SetWorkArea(_op.Id, (int)r.Created[0]));
            MoveCitizenAway();
            yield return null;
            _agents.ClickAt(ScreenOf(150, 150));
            yield return null;
            Assert.IsTrue(_view.IsShown);
            StringAssert.Contains("drawn", _view.WorkAreaText);
        }

        [UnityTest]
        public IEnumerator AssignFamilyThroughThePanel_ShowsWorkerAndRemoveFrees()
        {
            yield return OpenPanel();
            _view.ClickAssignSlot();
            yield return null;
            Assert.IsNotNull(_view.Find<Button>("assign-family-1"), "list of unassigned families");
            Assert.IsNotNull(_view.Find<Button>("assign-family-3"));
            _view.ClickAssignFamily(1);
            yield return null;
            Assert.AreEqual(_op.Id, _host.World.People.FindFamily(1).WorkplaceId);
            CollectionAssert.AreEqual(new[] { 1 }, _op.FamilyIds);
            StringAssert.Contains("Archer", _view.Find<Label>("family-slot-0").text);
            Assert.IsNotNull(_view.Find<Button>("remove-family-1"));
            StringAssert.Contains("Person1 Archer", _view.WorkersText);

            _view.ClickRemoveFamily(1);
            yield return null;
            Assert.IsNull(_host.World.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual(0, _op.FamilyIds.Count);
            Assert.IsNull(_view.Find<Button>("remove-family-1"));
        }

        [UnityTest]
        public IEnumerator RefusalsShowTheirText()
        {
            yield return OpenPanel();
            _view.ClickAssignFamily(1);
            _view.ClickAssignFamily(2);
            Assert.AreEqual("", _view.MessageText);
            _view.ClickAssignFamily(3);
            Assert.AreEqual("no free family slot", _view.MessageText);
            _view.ClickRemoveFamily(3);
            Assert.AreEqual("family is not assigned to this operation", _view.MessageText);
        }

        [UnityTest]
        public IEnumerator PauseResumeAndUseDefaultSendCommands()
        {
            yield return OpenPanel();
            _view.ClickPauseResume();
            yield return null;
            Assert.IsTrue(_op.Paused);
            Assert.AreEqual("Status: Paused", _view.StatusText);
            Assert.AreEqual("Resume", _view.PauseButtonText);
            _view.ClickPauseResume();
            Assert.IsFalse(_op.Paused);
            Assert.AreEqual("Pause", _view.PauseButtonText);

            var r = _host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(140, 140), M(160, 140), M(160, 160), M(140, 160) }), AreaTypes.WoodcuttingWorkId));
            _host.Execute(new SetWorkArea(_op.Id, (int)r.Created[0]));
            Assert.IsTrue(_op.WorkAreaId.HasValue);
            _view.ClickUseDefault();
            Assert.IsFalse(_op.WorkAreaId.HasValue);
        }

        [UnityTest]
        public IEnumerator OxenTabAndStockRowsArePresentButDisabled()
        {
            yield return OpenPanel();
            var oxen = _view.Find<Button>("oxen-tab");
            Assert.IsNotNull(oxen);
            Assert.IsFalse(oxen.enabledSelf);
            StringAssert.Contains("Coming in M9", oxen.text);
            foreach (string name in new[] { "stock-row-pool", "stock-row-camp" })
            {
                var row = _view.Find<Label>(name);
                Assert.IsNotNull(row, name);
                Assert.IsFalse(row.enabledSelf, name);
                StringAssert.Contains("Coming in M9", row.text);
            }
        }

        [UnityTest]
        public IEnumerator DrawWorkAreaStartsToolAndConfirmSendsSetWorkArea()
        {
            _toolGo = new GameObject("WorkAreaTool");
            var tool = _toolGo.AddComponent<AreaTool>();
            tool.Configure("woodcutter-work", AreaTypes.WoodcuttingWorkId, _host, _ground, null, null, _controller);
            tool.SetViewSettings(new ViewSettings { Snapping = false });
            _controller.Register(tool);
            yield return OpenPanel();

            _view.ClickDrawWorkArea();
            Assert.AreSame(tool, _controller.ActiveTool);
            Assert.AreEqual(_op.Id, tool.WorkAreaFor);

            var pts = new[] { ScreenOf(60, 120), ScreenOf(100, 120), ScreenOf(100, 160), ScreenOf(60, 160) };
            float t = 10f;
            foreach (var p in pts)
            {
                tool.PointerDown(PressButton.Left, p);
                tool.PointerUp(PressButton.Left, p, t);
                t += 5f;
            }
            Assert.AreEqual(4, tool.PointCount);
            tool.Confirm();
            Assert.IsTrue(_op.WorkAreaId.HasValue, "SetWorkArea sent on confirm");
            Assert.AreEqual(AreaTypes.WoodcuttingWorkId, _host.World.Areas.Get(_op.WorkAreaId.Value).TypeId);
            Assert.IsNull(_controller.ActiveTool);
        }
    }
}
