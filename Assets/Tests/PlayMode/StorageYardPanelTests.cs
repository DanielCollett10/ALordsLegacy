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
    /// <summary>PlayMode tests for the storage yard panel (T-549, storage.md ST-10, ST-45), on default SimTuning.</summary>
    public class StorageYardPanelTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _agentsGo, _ui;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private AgentPicker _agents;
        private StorageYardPanelView _panel;

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
            _ui = new GameObject("StorageYardPanel");
            _ui.SetActive(false);
            _ui.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _panel = _ui.AddComponent<StorageYardPanelView>();
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

        // A finished yard with its operation, built the way PlaceStructure does for a recipe-free type.
        private int BuildYard(int footprintId, double x, double z)
        {
            var w = _host.World;
            int id = w.Footprints.Add(footprintId, ModuleTypes.StorageYard, M(x, z), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.StorageYardOpId, 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            return id;
        }

        [UnityTest]
        public IEnumerator ClickingTheYardShowsStockStatusCapacityAndGreyedSupplies()
        {
            var w = _host.World;
            int yard = BuildYard(901, 100, 100);
            w.Inventory.Produce(w.Inventory.FindGoodsStore(yard), Items.Log, 4);
            Assert.IsTrue(_agents.ClickAt(ScreenOf(100f, 100f)));
            Assert.IsTrue(_agents.HasYard);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            Assert.AreEqual("Storage yard", _panel.TitleText);
            Assert.AreEqual("No workers", _panel.StatusText);
            StringAssert.Contains("Log: 4", _panel.StockText);
            Assert.AreEqual("Stored: 4 / " + w.Tuning.StorageYardCapacity, _panel.StoredText);
            Assert.IsFalse(_panel.SuppliesToggle.enabledSelf, "Supplies market is greyed.");
            Assert.AreEqual("No demand yet", _panel.SuppliesToggle.tooltip);
            Assert.AreEqual("Family 1: empty", _panel.SlotText(0));
            Assert.AreEqual("Deconstruct", _panel.DeconstructText);
            var b = _panel.PanelElement.worldBound;
            Assert.Greater(b.width, 0f);
            Assert.Greater(b.height, 0f);
            Assert.AreEqual(Visibility.Visible, _panel.PanelElement.resolvedStyle.visibility);
        }

        [UnityTest]
        public IEnumerator PanelAssignsAndUnassignsAFamily()
        {
            var w = _host.World;
            AddFamily(1, "Archer");
            int yard = BuildYard(901, 100, 100);
            _agents.SelectWorkshop(yard);
            yield return null;
            _panel.ClickAssignSlot();
            yield return null;
            Assert.IsTrue(_panel.PickerOpen);
            Assert.IsNotNull(_panel.PanelElement.Q<Button>("yard-assign-family-1"));
            _panel.ClickAssignFamily(1);
            yield return null;
            int opId = w.Operations.FindByModule(yard).Id;
            Assert.AreEqual(opId, w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family 1: Archer", _panel.SlotText(0));
            _panel.ClickRemoveFamily(1);
            yield return null;
            Assert.IsNull(w.People.FindFamily(1).WorkplaceId);
            Assert.AreEqual("Family 1: empty", _panel.SlotText(0));
        }

        [UnityTest]
        public IEnumerator DeconstructAsksToConfirmThenRemovesTheYard()
        {
            var w = _host.World;
            int yard = BuildYard(901, 100, 100);
            _agents.SelectWorkshop(yard);
            yield return null;
            _panel.ClickDeconstruct();
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNotNull(w.Footprints.Get(yard), "Cancel keeps the yard.");
            _panel.ClickDeconstruct();
            ConfirmDialog.Confirm();
            for (int i = 0; i < 120 && w.Footprints.Get(yard) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(yard));
            yield return null;
            Assert.IsFalse(_panel.IsShown);
        }

        [UnityTest]
        public IEnumerator CloseButtonClearsTheSelectionAndHidesThePanel()
        {
            int yard = BuildYard(901, 100, 100);
            _agents.SelectWorkshop(yard);
            yield return null;
            Assert.IsTrue(_panel.IsShown);
            _panel.ClickClose();
            yield return null;
            Assert.IsFalse(_agents.HasYard);
            Assert.IsFalse(_panel.IsShown);
        }

        [UnityTest]
        public IEnumerator WorkshopSelectionDoesNotShowTheYardPanel()
        {
            var w = _host.World;
            int id = w.Footprints.Add(902, ModuleTypes.Sawpit, M(100, 100), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.SawingId, 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            _agents.SelectWorkshop(id);
            yield return null;
            Assert.IsFalse(_agents.HasYard);
            Assert.IsFalse(_panel.IsShown);
        }
    }
}
