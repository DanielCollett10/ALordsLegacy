using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="AgentPicker"/>, <see cref="CitizenPanelView"/> and <see cref="SelectionRing"/> (T-263).</summary>
    public class CitizenPanelTests
    {
        private GameObject _hostGo, _camGo, _pickGo, _agentsGo, _uiGo, _ringGo;
        private PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private GroundPicker _ground;
        private AgentPicker _agents;
        private CitizenPanelView _view;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var pop = _host.World.People;
            pop.AddFamily(1, "Archer", 100, 100);
            pop.AddCitizen(1, "Alda", Sex.Female, -30L * Citizen.TicksPerYear / 2, 1, 100, 100);
            pop.AddCitizen(2, "Bran", Sex.Male, -20L * Citizen.TicksPerYear, 1, 100.6, 100);
            pop.AddOx(1, "Bess", 100.2, 100);
            pop.AddOx(2, "Far", 130, 130);

            _camGo = new GameObject("Cam");
            _cam = _camGo.AddComponent<Camera>();
            float h = (float)_host.World.Terrain.HeightAt(100, 100);
            _camGo.transform.position = new Vector3(100, h + 30, 100);
            _camGo.transform.rotation = Quaternion.Euler(90, 0, 0);

            _pickGo = new GameObject("GroundPicker");
            _ground = _pickGo.AddComponent<GroundPicker>();
            _ground.PickCamera = _cam;
            _ground.Host = _host;

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, _ground, null);

            _uiGo = new GameObject("CitizenPanel");
            _uiGo.SetActive(false);
            _settings = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _settings;
            _view = _uiGo.AddComponent<CitizenPanelView>();
            _view.Configure(_host, _agents, null);
            _uiGo.SetActive(true);

            _ringGo = new GameObject("Ring");
            _ringGo.AddComponent<SelectionRing>().Configure(_host, _agents);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _ringGo, _uiGo, _agentsGo, _pickGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        private Vector2 ScreenOf(double x, double z)
        {
            Assert.IsTrue(_ground.TryWorldToScreen((float)x, (float)z, out var s));
            return s;
        }

        private Label L(string name) => _uiGo.GetComponent<UIDocument>().rootVisualElement.Q<Label>(name);

        [UnityTest]
        public IEnumerator SelectingKnownCitizenShowsItsName()
        {
            yield return null;
            Assert.IsTrue(_agents.ClickAt(ScreenOf(100, 100)));
            Assert.IsTrue(_agents.HasSelection);
            Assert.IsFalse(_agents.Selected.IsOx);
            Assert.AreEqual(1, _agents.Selected.Id);
            yield return null;
            Assert.IsTrue(_view.IsShown);
            StringAssert.Contains("Alda", L("citizen-name").text);
            StringAssert.Contains("Archer", L("citizen-family").text);
            Assert.AreEqual("Age: 30", L("citizen-age").text, "15 years of calendar time at the default 2x ageing.");
            StringAssert.Contains("Milling about", L("citizen-activity").text);
            Assert.AreEqual(DisplayStyle.Flex, _view.PanelElement.style.display.value);
        }

        [UnityTest]
        public IEnumerator CitizenBeatsOxAndNearestCitizenWins()
        {
            yield return null;
            // The ox stands 0.2 m from the click, Bran 0.6 m, Alda 0.4 m on the other side: citizens first, nearest of them.
            _agents.ClickAt(ScreenOf(100.2, 100));
            Assert.IsFalse(_agents.Selected.IsOx);
            Assert.AreEqual(1, _agents.Selected.Id);
        }

        [UnityTest]
        public IEnumerator OxShowsNameAndActivityOnly()
        {
            yield return null;
            _agents.ClickAt(ScreenOf(130, 130));
            Assert.IsTrue(_agents.Selected.IsOx);
            yield return null;
            StringAssert.Contains("Far", L("citizen-name").text);
            StringAssert.Contains("Waiting", L("citizen-activity").text);
            Assert.AreEqual(DisplayStyle.None, L("citizen-age").style.display.value);
            Assert.AreEqual(DisplayStyle.None, L("citizen-family").style.display.value);
        }

        [UnityTest]
        public IEnumerator ClickingEmptyGroundClearsAndCloseButtonWorks()
        {
            yield return null;
            _agents.ClickAt(ScreenOf(100, 100));
            Assert.IsTrue(_agents.HasSelection);
            _agents.ClickAt(ScreenOf(60, 60)); // nothing within 1.5 m / 12 px
            Assert.IsFalse(_agents.HasSelection);
            yield return null;
            Assert.IsFalse(_view.IsShown);

            _agents.ClickAt(ScreenOf(100, 100));
            _view.ClickClose();
            Assert.IsFalse(_agents.HasSelection);
            Assert.IsFalse(_view.IsShown);
        }

        [UnityTest]
        public IEnumerator PanelUpdatesEachFrameFromTheSim()
        {
            yield return null;
            _agents.ClickAt(ScreenOf(100, 100));
            yield return null;
            _host.World.People.FindCitizen(1).Activity = Activity.Working;
            yield return null;
            StringAssert.Contains("Working", L("citizen-activity").text);
        }

        [UnityTest]
        public IEnumerator PanelIsOnScreenAndFollowsFocusRules()
        {
            yield return null;
            _agents.ClickAt(ScreenOf(100, 100));
            yield return null;
            Assert.IsFalse(L("citizen-name").focusable);
            Assert.IsFalse(_uiGo.GetComponent<UIDocument>().rootVisualElement.Q<Button>("citizen-close").focusable);
            Assert.AreNotEqual(DisplayStyle.None, _view.PanelElement.style.display.value);
        }

        [UnityTest]
        public IEnumerator SelectedAgentGetsADrapedRing()
        {
            var ring = _ringGo.GetComponent<SelectionRing>();
            yield return null;
            Assert.IsFalse(ring.IsShown);
            _agents.ClickAt(ScreenOf(100, 100));
            yield return null;
            Assert.IsTrue(ring.IsShown);
            Assert.Greater(ring.VertexCount, 20);
            Assert.AreEqual(100f, ring.Centre.x, 0.01f);
            _agents.Clear();
            yield return null;
            Assert.IsFalse(ring.IsShown);
        }

        [UnityTest]
        public IEnumerator FellingWorkerShowsWorkplaceAndTask()
        {
            var w = _host.World;
            var op = w.Operations.Create(OperationTypes.WoodcuttingId, 7);
            w.People.FindFamily(1).WorkplaceId = op.Id;
            var def = new TaskDef(WoodcuttingSystem.FellTaskId, new[]
            {
                TaskStep.MoveTo(),
                TaskStep.Work(10, true, "stage.fell"),
                TaskStep.Work(10, true, "stage.buck"),
                TaskStep.Work(10, true, "stage.buck"),
                TaskStep.Work(10, true, "stage.buck"),
                TaskStep.Work(10, true, "stage.buck"),
            }, EligibilityPolicy.AssignedFamilyOnly);
            var t = w.Tasks.Create(def, op.Id, new TreeId(1));
            w.Tasks.Assign(t.Id, 1);
            w.Tasks.Start(t.Id);
            // Past MoveTo and the fell step: the second buck step (2 of 4) is current.
            w.Tasks.CompleteStep(t.Id);
            w.Tasks.CompleteStep(t.Id);
            w.Tasks.CompleteStep(t.Id);
            w.People.FindCitizen(1).Activity = Activity.Working;
            yield return null;
            _agents.ClickAt(ScreenOf(100, 100));
            yield return null;
            Assert.AreEqual("Work: Woodcutter " + op.Id, _view.WorkplaceText);
            Assert.AreEqual("Task: Felling (Buck 2/4)", _view.TaskText);
            Assert.AreEqual(DisplayStyle.Flex, _uiGo.GetComponent<UIDocument>().rootVisualElement.Q<Label>("citizen-task").style.display.value);
        }
    }
}
