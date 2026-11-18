using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the workshop panel and workshop picking (T-527).</summary>
    public class WorkshopPanelTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _agentsGo, _ui;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private AgentPicker _agents;
        private WorkshopPanelView _panel;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning { DeconstructTimeShare = 0 }, SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;

            _camGo = new GameObject("Cam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 30f;
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _groundGo = new GameObject("Ground");
            _groundGo.transform.position = new Vector3(100f, -0.5f, 100f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);
            var picker = new GameObject("Picker").AddComponent<GroundPicker>();
            picker.transform.SetParent(_groundGo.transform, false);
            picker.PickCamera = _cam; picker.Host = _host;

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, picker, null);

            _settings = TestPanelSettings.Create();
            _ui = new GameObject("WorkshopPanel");
            _ui.SetActive(false);
            _ui.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _panel = _ui.AddComponent<WorkshopPanelView>();
            _panel.Configure(_host, _agents, null);
            _ui.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            ConfirmDialog.Cancel();
            foreach (var go in new[] { _ui, _agentsGo, _groundGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Vector2 ScreenOf(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void AddFamily(int id, string surname)
        {
            var pop = _host.World.People;
            pop.AddFamily(id, surname, 100, 100);
            pop.AddCitizen(id, "Person" + id, Sex.Male, -30L * Citizen.TicksPerYear / 2, id, 100, 100);
        }

        // A finished workshop with its operation, the way the Sim tests build one.
        private int BuildWorkshop(string typeId, int footprintId, double x, double z)
        {
            var w = _host.World;
            int id = w.Footprints.Add(footprintId, ModuleTypes.Find(typeId), M(x, z), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(typeId), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            return id;
        }

        [Test]
        public void TryPickWorkshopHitsHewingStationAndSawpitButNotAGranary()
        {
            var w = _host.World;
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 60, 60);
            int saw = BuildWorkshop(ModuleTypes.SawpitId, 802, 100, 60);
            w.Footprints.Add(803, ModuleTypes.Granary, M(140, 60), 0);
            Assert.IsTrue(AgentPicker.TryPickWorkshop(w, M(60, 60), out int a));
            Assert.AreEqual(hew, a);
            Assert.IsTrue(AgentPicker.TryPickWorkshop(w, M(100, 60), out int b));
            Assert.AreEqual(saw, b);
            Assert.IsFalse(AgentPicker.TryPickWorkshop(w, M(140, 60), out _));
        }

        [UnityTest]
        public IEnumerator ClickingAHewingStationShowsItsPanelWithStockAndNoFamily()
        {
            var w = _host.World;
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            w.Inventory.Produce(w.Inventory.FindWorkshopStock(hew), Items.Log, 4);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(hew), Items.Beam, 7);
            Assert.IsTrue(_agents.ClickAt(ScreenOf(100f, 100f)));
            Assert.IsTrue(_agents.HasWorkshop);
            Assert.IsFalse(_agents.HasOperation, "the workshop panel, not the operation panel");
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            Assert.AreEqual("Hewing station", _panel.TitleText);
            Assert.AreEqual("No workers", _panel.StatusText);
            StringAssert.StartsWith("Input: 4 / ", _panel.InputText);
            StringAssert.EndsWith(" logs", _panel.InputText);
            StringAssert.StartsWith("Output: 7 / ", _panel.OutputText);
            StringAssert.EndsWith(" beams", _panel.OutputText);
            Assert.AreEqual("Family: none", _panel.FamilyText);
            Assert.AreEqual("Assign", _panel.AssignButton.text);
            Assert.AreEqual("Deconstruct", _panel.DeconstructButton.text);
            Assert.AreEqual(DisplayStyle.Flex, _panel.DeconstructButton.resolvedStyle.display);
            var b = _panel.PanelElement.worldBound;
            Assert.Greater(b.width, 0f);
            Assert.Greater(b.height, 0f);
            Assert.AreEqual(Visibility.Visible, _panel.PanelElement.resolvedStyle.visibility);
        }

        [UnityTest]
        public IEnumerator SawpitPanelAssignsAndUnassignsAFamily()
        {
            var w = _host.World;
            AddFamily(1, "Archer");
            int saw = BuildWorkshop(ModuleTypes.SawpitId, 802, 100, 100);
            _agents.SelectWorkshop(saw);
            yield return null;
            Assert.AreEqual("Sawpit", _panel.TitleText);
            StringAssert.EndsWith(" beams", _panel.InputText);
            StringAssert.EndsWith(" planks", _panel.OutputText);
            _panel.ClickAssign();
            yield return null;
            Assert.IsTrue(_panel.PickerOpen);
            Assert.IsNotNull(_panel.PanelElement.Q<Button>("workshop-assign-family-1"));
            _panel.ClickAssignFamily(1);
            yield return null;
            int opId = w.Operations.FindByModule(saw).Id;
            Assert.AreEqual(opId, w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family: Archer", _panel.FamilyText);
            Assert.AreEqual(DisplayStyle.None, _panel.AssignButton.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, _panel.UnassignButton.resolvedStyle.display);
            Assert.AreEqual("Waiting for beams", _panel.StatusText);
            _panel.ClickUnassign();
            yield return null;
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family: none", _panel.FamilyText);
            Assert.AreEqual(DisplayStyle.Flex, _panel.AssignButton.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator DeconstructAsksToConfirmThenRemovesTheWorkshop()
        {
            var w = _host.World;
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            _agents.SelectWorkshop(hew);
            yield return null;
            _panel.ClickDeconstruct();
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNotNull(w.Footprints.Get(hew), "Cancel keeps the workshop.");
            _panel.ClickDeconstruct();
            ConfirmDialog.Confirm();
            for (int i = 0; i < 60 && w.Footprints.Get(hew) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(hew));
            yield return null;
            Assert.IsFalse(_panel.IsShown);
        }

        [UnityTest]
        public IEnumerator ReserveFieldShowsCaptionAndItemsAndSetsTheReserve()
        {
            var w = _host.World;
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            int opId = w.Operations.FindByModule(hew).Id;
            _agents.SelectWorkshop(hew);
            yield return null;
            var field = _panel.ReserveField;
            Assert.AreEqual(DisplayStyle.Flex, _panel.ReserveRow.resolvedStyle.display);
            Assert.AreEqual("Keep in storage:", _panel.PanelElement.Q<Label>("workshop-reserve-caption").text);
            Assert.AreEqual("logs", _panel.ReserveItemsText);
            Assert.AreEqual(w.Tuning.WorkshopDefaultReserve, field.value);
            Assert.Greater(field.worldBound.width, 0f);
            field.value = 17;
            yield return null;
            Assert.AreEqual(17, InputReserve.ReserveOf(w, w.Operations.Get(opId), Items.Log));
            Assert.AreEqual("", _panel.MessageText);
            Assert.AreEqual(17, field.value);
        }

        [UnityTest]
        public IEnumerator RefusedReserveShowsTheReasonAndSnapsBack()
        {
            var w = _host.World;
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            int opId = w.Operations.FindByModule(hew).Id;
            _agents.SelectWorkshop(hew);
            yield return null;
            int before = _panel.ReserveField.value;
            _panel.ReserveField.value = -3;
            yield return null;
            Assert.AreEqual("Reserve must be 0 to " + SetWorkshopReserve.MaxReserve, _panel.MessageText);
            Assert.AreEqual(before, InputReserve.ReserveOf(w, w.Operations.Get(opId), Items.Log));
            Assert.AreEqual(before, _panel.ReserveField.value);
            _panel.ReserveField.value = SetWorkshopReserve.MaxReserve + 1;
            yield return null;
            Assert.AreEqual(before, _panel.ReserveField.value);
        }

        [UnityTest]
        public IEnumerator ReserveBoxIsNotFocusableUntilClickedAndTheRestOfThePanelIsNot()
        {
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            _agents.SelectWorkshop(hew);
            yield return null;
            var input = _panel.ReserveField.Q(className: TextInputBaseField<int>.inputUssClassName);
            Assert.IsFalse(_panel.AssignButton.focusable);
            Assert.IsFalse(UiFocusRules.TextInputActive);
            Assert.IsNotNull(input);
            // Stand-in for a click on the box (the pointer-down path calls BeginTextInput).
            UiFocusRules.BeginTextInput(_panel.ReserveField);
            yield return null;
            Assert.IsTrue(UiFocusRules.TextInputActive, "typing blocks game hotkeys");
            Assert.IsNull(UiFocusRules.GameKeyboard);
            UiFocusRules.ReleaseFocus(_panel.ReserveField);
            yield return null;
            yield return null;
            Assert.IsFalse(UiFocusRules.TextInputActive);
        }

        [UnityTest]
        public IEnumerator OpenPanelDoesNotTakeFocusOnNavigationKeys()
        {
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            _agents.SelectWorkshop(hew);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            var root = _ui.GetComponent<UIDocument>().rootVisualElement;
            var field = _panel.ReserveField;
            int before = field.value;

            // Stand-ins for the Input System Navigate action from A, D, W, S and the arrow keys, sent with the panel open.
            Vector2[] dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
            foreach (var dir in dirs)
            {
                var e = NavigationMoveEvent.GetPooled(dir);
                e.target = root;
                root.SendEvent(e);
                e.Dispose();
                yield return null;
                Assert.IsFalse(UiFocusRules.TextInputActive, "navigation must not open a text edit");
                Assert.IsNotNull(UiFocusRules.GameKeyboard);
                var focused = root.panel.focusController.focusedElement as VisualElement;
                Assert.IsFalse(focused != null && UiFocusRules.IsWithin(focused, field), "the reserve box must not get focus");
            }
            Assert.AreEqual(before, field.value, "the reserve value must not change");
        }

        [UnityTest]
        public IEnumerator ClosingThePanelWhileEditingGivesTheKeyboardBack()
        {
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            _agents.SelectWorkshop(hew);
            yield return null;
            UiFocusRules.BeginTextInput(_panel.ReserveField);
            yield return null;
            Assert.IsTrue(UiFocusRules.TextInputActive);
            _panel.ClickClose();
            yield return null;
            yield return null;
            Assert.IsFalse(UiFocusRules.TextInputActive, "closing must not leave the camera stuck");
            Assert.IsNotNull(UiFocusRules.GameKeyboard);
        }

        [UnityTest]
        public IEnumerator StatusShowsWaitingTotalOverReserveWhenOnlyTheReserveBlocks()
        {
            var w = _host.World;
            AddFamily(3, "Archer");
            for (int i = 0; i < 3; i++) w.People.AddOx(50 + i, "O" + i, 40, 44 + i).State = OxState.Resting(40000, 44000 + 1000 * i);
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 60, 60);
            int opId = w.Operations.FindByModule(hew).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 5);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(opId, Items.Log.Id, 5)).IsAccepted);
            w.Step(1);
            _agents.SelectWorkshop(hew);
            yield return null;
            Assert.AreEqual("Waiting: 5 / 5 logs in storage", _panel.StatusText);
        }

        [UnityTest]
        public IEnumerator CloseButtonClearsTheSelectionAndHidesThePanel()
        {
            int hew = BuildWorkshop(ModuleTypes.HewingStationId, 801, 100, 100);
            _agents.SelectWorkshop(hew);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            _panel.ClickClose();
            yield return null;
            Assert.IsFalse(_agents.HasWorkshop);
            Assert.IsFalse(_panel.IsShown);
        }
    }
}
