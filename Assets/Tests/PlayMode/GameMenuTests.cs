using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the Esc game menu (T-370, saving.md SV-01..04).</summary>
    public class GameMenuTests
    {
        private sealed class DraftTool : ITool
        {
            public bool Draft = true;
            public string Id => "road";
            public bool HasDraft => Draft;
            public void Activate() { }
            public void Deactivate() { }
            public void Cancel() { Draft = false; }
            public bool HandleEscape() { if (!Draft) return false; Draft = false; return true; }
        }

        private GameObject _hostGo, _camGo, _pickGo, _agentsGo, _uiGo, _ctlGo;
        private PanelSettings _settings;
        private SimHost _host;
        private GroundPicker _ground;
        private AgentPicker _agents;
        private GameMenuView _menu;
        private ToolController _ctl;
        private DraftTool _tool;

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

            _camGo = new GameObject("Cam");
            var cam = _camGo.AddComponent<Camera>();
            float h = (float)_host.World.Terrain.HeightAt(100, 100);
            _camGo.transform.position = new Vector3(100, h + 30, 100);
            _camGo.transform.rotation = Quaternion.Euler(90, 0, 0);

            _pickGo = new GameObject("GroundPicker");
            _ground = _pickGo.AddComponent<GroundPicker>();
            _ground.PickCamera = cam;
            _ground.Host = _host;

            _ctlGo = new GameObject("Tools");
            _ctl = _ctlGo.AddComponent<ToolController>();
            _tool = new DraftTool();
            _ctl.Register(_tool);

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, _ground, _ctl);

            _uiGo = new GameObject("GameMenu");
            _uiGo.SetActive(false);
            _settings = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _settings;
            _menu = _uiGo.AddComponent<GameMenuView>();
            _menu.SetHost(_host);
            _uiGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _uiGo, _ctlGo, _agentsGo, _pickGo, _camGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
            Assert.IsFalse(GameMenuState.IsOpen, "The menu must not leave the input block on.");
        }

        private Vector2 ScreenOfCitizen()
        {
            Assert.IsTrue(_ground.TryWorldToScreen(100, 100, out var s));
            return s;
        }

        [UnityTest]
        public IEnumerator OpenPausesAndResumeRestoresRunning()
        {
            yield return null;
            Assert.IsFalse(_host.World.Paused);
            _menu.Open();
            Assert.IsTrue(_menu.IsOpen);
            Assert.IsTrue(GameMenuState.IsOpen);
            Assert.IsTrue(_host.World.Paused, "Opening pauses the Sim.");
            _menu.ClickResume();
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsFalse(GameMenuState.IsOpen);
            Assert.IsFalse(_host.World.Paused, "A game that was running runs again after Resume.");
        }

        [UnityTest]
        public IEnumerator OpenKeepsAlreadyPausedGamePausedOnClose()
        {
            yield return null;
            _host.World.Pause();
            _menu.Open();
            _menu.HandleEscape();
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsTrue(_host.World.Paused, "A game paused before the menu stays paused.");
        }

        [UnityTest]
        public IEnumerator SimDoesNotAdvanceWhileOpen()
        {
            yield return null;
            _menu.Open();
            long tick = _host.World.Tick;
            _host.Tick(5f);
            Assert.AreEqual(tick, _host.World.Tick);
        }

        [UnityTest]
        public IEnumerator PickingAndToolSelectionAreBlockedWhileOpen()
        {
            yield return null;
            _menu.Open();
            Assert.IsFalse(_agents.ClickAt(ScreenOfCitizen()), "Picking ignores clicks while the menu is open.");
            Assert.IsFalse(_agents.HasSelection);
            _ctl.Toggle("road");
            Assert.IsNull(_ctl.ActiveTool, "Tools cannot be selected while the menu is open.");
            _menu.Close();
            _ctl.Toggle("road");
            Assert.AreSame(_tool, _ctl.ActiveTool, "Tools work again after closing.");
            _ctl.Deselect();
            Assert.IsTrue(_agents.ClickAt(ScreenOfCitizen()), "Picking works again after closing.");
        }

        [UnityTest]
        public IEnumerator EscapeOpensMenuOnlyWithNoToolAndNoDraft()
        {
            yield return null;
            _ctl.Toggle("road");
            Assert.IsTrue(_ctl.HandleEscapeKey());
            Assert.IsFalse(_menu.IsOpen, "Esc with a draft cancels the draft.");
            Assert.IsFalse(_tool.Draft);
            Assert.IsTrue(_ctl.HandleEscapeKey());
            Assert.IsNull(_ctl.ActiveTool, "Esc without a draft leaves the tool as before.");
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsTrue(_ctl.HandleEscapeKey());
            Assert.IsTrue(_menu.IsOpen, "Esc with no tool opens the menu.");
            Assert.IsTrue(_host.World.Paused);
            Assert.IsTrue(_ctl.HandleEscapeKey());
            Assert.IsFalse(_menu.IsOpen, "Esc again closes it.");
            Assert.IsFalse(_host.World.Paused);
        }

        [UnityTest]
        public IEnumerator EscapeDoesNotOpenMenuWhileAnAgentIsSelected()
        {
            yield return null;
            Assert.IsTrue(_agents.ClickAt(ScreenOfCitizen()));
            Assert.IsFalse(_ctl.HandleEscapeKey(), "The citizen panel owns this Esc.");
            Assert.IsFalse(_menu.IsOpen);
        }

        [UnityTest]
        public IEnumerator ButtonsAndPanelsWork()
        {
            yield return null;
            _menu.Open();
            var root = _uiGo.GetComponent<UIDocument>().rootVisualElement;
            var rootEl = _menu.RootElement;
            Assert.AreEqual(DisplayStyle.Flex, rootEl.style.display.value);
            Assert.IsFalse(_menu.OptionsButton.enabledSelf, "Options is disabled (SV-03).");
            Assert.IsFalse(_menu.QuitButton.enabledSelf, "Quit is disabled (SV-03).");
            Assert.IsNotNull(root.Q<Button>("game-menu-resume"));
            Assert.IsTrue(root.Q<Button>("game-menu-save").enabledSelf);
            Assert.IsTrue(root.Q<Button>("game-menu-load").enabledSelf);

            _menu.ClickSave();
            Assert.IsTrue(_menu.ShowingSave);
            Assert.IsFalse(_menu.ShowingMain);
            _menu.HandleEscape();
            Assert.IsTrue(_menu.ShowingMain, "Esc inside Save goes back to the list.");
            Assert.IsTrue(_menu.IsOpen);
            _menu.ClickLoad();
            Assert.IsTrue(_menu.ShowingLoad);
            _menu.ClickBack();
            Assert.IsTrue(_menu.ShowingMain);
            _menu.Close();
            Assert.AreEqual(DisplayStyle.None, rootEl.style.display.value);
        }
    }
}
