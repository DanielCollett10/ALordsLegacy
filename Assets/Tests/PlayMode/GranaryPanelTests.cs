using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Sim.Commands;
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
    /// <summary>PlayMode tests for the granary panel and granary picking (T-449).</summary>
    public class GranaryPanelTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _agentsGo, _granaryUi, _opUi;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private AgentPicker _agents;
        private GranaryPanelView _panel;
        private OperationPanelView _op;
        private int _granaryId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning { DeconstructTimeShare = 0 }, SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 40);
            var r = _host.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(100, 100), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _granaryId = (int)r.Created[0];

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
            _granaryUi = new GameObject("GranaryPanel");
            _granaryUi.SetActive(false);
            _granaryUi.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _panel = _granaryUi.AddComponent<GranaryPanelView>();
            _panel.Configure(_host, _agents, null);
            _granaryUi.SetActive(true);
            _opUi = new GameObject("OperationPanel");
            _opUi.SetActive(false);
            _opUi.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _op = _opUi.AddComponent<OperationPanelView>();
            _op.Configure(_host, _agents, null);
            _opUi.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            ConfirmDialog.Cancel();
            foreach (var go in new[] { _opUi, _granaryUi, _agentsGo, _groundGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Vector2 ScreenOf(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        [Test]
        public void TryPickGranaryHitsTheBuildingAndMissesHousesAndHuts()
        {
            var w = _host.World;
            Assert.IsTrue(AgentPicker.TryPickGranary(w, M(100, 100), out int id));
            Assert.AreEqual(_granaryId, id);
            Assert.IsTrue(AgentPicker.TryPickGranary(w, M(103.5, 102.5), out _), "Inside the 8 x 6 m building.");
            Assert.IsFalse(AgentPicker.TryPickGranary(w, M(106, 100), out _), "Outside the building.");
            w.Footprints.Add(901, ModuleTypes.House, M(140, 100), 0);
            w.Footprints.Add(902, ModuleTypes.WoodcuttingHut, M(60, 100), 0);
            Assert.IsFalse(AgentPicker.TryPickGranary(w, M(140, 100), out _), "A house is not a granary.");
            Assert.IsFalse(AgentPicker.TryPickGranary(w, M(60, 100), out _), "A woodcutter hut is not a granary.");
        }

        [UnityTest]
        public IEnumerator SelectingAnUnbuiltGranaryShowsDeliveringAndLeavesTheOperationPanelHidden()
        {
            _agents.SelectGranary(_granaryId);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            Assert.AreEqual("Granary", _panel.TitleText);
            StringAssert.StartsWith("Delivering", _panel.StateText);
            Assert.AreEqual("Cancel granary", _panel.DeconstructText);
            Assert.IsFalse(_op.IsShown, "The operation panel stays hidden.");
            var b = _panel.PanelElement.worldBound;
            Assert.Greater(b.width, 0f);
            Assert.Greater(b.height, 0f);
        }

        [UnityTest]
        public IEnumerator ClickingTheGranaryWithNoToolSelectsItNotTheOperation()
        {
            Assert.IsTrue(_agents.ClickAt(ScreenOf(100f, 100f)));
            Assert.IsTrue(_agents.HasGranary);
            Assert.IsFalse(_agents.HasOperation);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            Assert.IsFalse(_op.IsShown);
        }

        [UnityTest]
        public IEnumerator BuiltGranaryShowsStoredFoodByTypeAndTotal()
        {
            var w = _host.World;
            w.Construction.Remove(_granaryId);
            w.Inventory.Produce(w.Inventory.FindFoodStore(_granaryId), Items.Berries, 40);
            _agents.SelectGranary(_granaryId);
            yield return null;
            Assert.AreEqual("Built", _panel.StateText);
            StringAssert.Contains("Berries: 40", _panel.StockText);
            Assert.AreEqual("Stored: 40 / 100", _panel.StoredText);
            Assert.AreEqual("Deconstruct", _panel.DeconstructText);
        }

        [UnityTest]
        public IEnumerator ConfirmedDeconstructRemovesTheGranaryAndLeavesAFoodPile()
        {
            var w = _host.World;
            w.Construction.Remove(_granaryId);
            w.Inventory.Produce(w.Inventory.FindFoodStore(_granaryId), Items.Berries, 40);
            _agents.SelectGranary(_granaryId);
            yield return null;
            _panel.ClickDeconstruct();
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNotNull(w.Footprints.Get(_granaryId), "Cancel keeps the granary.");
            _panel.ClickDeconstruct();
            ConfirmDialog.Confirm();
            for (int i = 0; i < 50 && w.Footprints.Get(_granaryId) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(_granaryId));
            yield return null;
            Assert.IsFalse(_panel.IsShown);
            Assert.AreEqual(40, w.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.CountOf(Items.Berries.Id)));
        }

        [UnityTest]
        public IEnumerator UnbuiltCancelRemovesTheSite()
        {
            var w = _host.World;
            _agents.SelectGranary(_granaryId);
            yield return null;
            Assert.AreEqual("Cancel granary", _panel.DeconstructText);
            _panel.ClickDeconstruct();
            ConfirmDialog.Confirm();
            for (int i = 0; i < 5 && w.Footprints.Get(_granaryId) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(_granaryId));
            yield return null;
            Assert.IsFalse(_panel.IsShown);
        }

        private void AddFamily(int id, string surname)
        {
            var pop = _host.World.People;
            pop.AddFamily(id, surname, 100, 100);
            pop.AddCitizen(id, "Person" + id, LordsLegacy.Sim.People.Sex.Male, -30L * LordsLegacy.Sim.People.Citizen.TicksPerYear / 2, id, 100, 100);
        }

        private IEnumerator SelectBuilt()
        {
            _host.World.Construction.Remove(_granaryId);
            _agents.SelectGranary(_granaryId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuiltGranaryShowsTwoEmptySlotsWithAssignButtons()
        {
            yield return SelectBuilt();
            Assert.AreEqual("Family 1: empty", _panel.SlotText(0));
            Assert.AreEqual("Family 2: empty", _panel.SlotText(1));
            Assert.IsNotNull(_panel.AssignButton(0));
            Assert.IsNotNull(_panel.AssignButton(1));
            Assert.AreEqual("Assign", _panel.AssignButton(0).text);
            Assert.IsTrue(_panel.AssignButton(0).enabledSelf);
        }

        [UnityTest]
        public IEnumerator AssignThenRemoveFamilyRoundTrips()
        {
            var w = _host.World;
            AddFamily(1, "Archer");
            yield return SelectBuilt();
            int opId = w.Operations.FindByModule(_granaryId).Id;
            _panel.ClickAssignSlot();
            yield return null;
            Assert.IsTrue(_panel.PickerOpen);
            Assert.IsNotNull(_panel.PanelElement.Q<UnityEngine.UIElements.Button>("assign-family-1"));
            _panel.ClickAssignFamily(1);
            yield return null;
            Assert.AreEqual(opId, w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family 1: Archer", _panel.SlotText(0));
            StringAssert.StartsWith("Person1: ", _panel.MembersText(0));
            Assert.IsNotNull(_panel.PanelElement.Q<UnityEngine.UIElements.Button>("remove-family-1"));
            _panel.ClickRemoveFamily(1);
            yield return null;
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family 1: empty", _panel.SlotText(0));
        }

        [UnityTest]
        public IEnumerator UnbuiltGranaryDisablesAssignAndSendsNothing()
        {
            var w = _host.World;
            AddFamily(1, "Archer");
            _agents.SelectGranary(_granaryId);
            yield return null;
            var b = _panel.AssignButton(0);
            Assert.IsNotNull(b);
            Assert.IsFalse(b.enabledSelf);
            Assert.AreEqual(GranaryPanelView.NotBuiltText, b.text);
            _panel.ClickAssignSlot();
            Assert.IsFalse(_panel.PickerOpen);
            Assert.AreEqual(GranaryPanelView.NotBuiltText, _panel.MessageText);
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
        }

        [UnityTest]
        public IEnumerator FamilyAtAWoodcutterIsNotInTheAssignList()
        {
            var w = _host.World;
            AddFamily(1, "Archer");
            AddFamily(2, "Brook");
            var r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(40, 40), M(70, 40), M(70, 60), M(40, 60) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            var wood = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(902, ModuleTypes.WoodcuttingHut, M(60, 100), 0);
            w.Operations.AddModule(wood.Id, 902);
            Assert.IsTrue(w.Execute(new AssignFamily(wood.Id, 1)).IsAccepted);
            yield return SelectBuilt();
            _panel.ClickAssignSlot();
            yield return null;
            Assert.IsNull(_panel.PanelElement.Q<UnityEngine.UIElements.Button>("assign-family-1"));
            Assert.IsNotNull(_panel.PanelElement.Q<UnityEngine.UIElements.Button>("assign-family-2"));
        }

        [UnityTest]
        public IEnumerator CloseHidesAndSelectingAHouseOrCitizenHidesThePanel()
        {
            var w = _host.World;
            _agents.SelectGranary(_granaryId);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            _panel.ClickClose();
            Assert.IsFalse(_panel.IsShown);

            _agents.SelectGranary(_granaryId);
            Assert.IsTrue(_panel.IsShown);
            w.People.AddFamily(1, "Archer", 10, 10);
            w.People.AddCitizen(1, "Alda", LordsLegacy.Sim.People.Sex.Female, -4_000_000, 1, 10, 10);
            _agents.Select(AgentRef.Citizen(1));
            yield return null;
            Assert.IsFalse(_agents.HasGranary);
            Assert.IsFalse(_panel.IsShown);
        }
    }
}
