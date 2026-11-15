using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>Woodcutter build bar entry greys out until its minimum cost is affordable (T-388, BT-02).</summary>
    public class BuildBarAffordabilityTests
    {
        private const string DisabledClass = "build-bar__button--disabled";
        private const string DrawTooltip = "Draw the woodcutter's area, then place its modules";

        private GameObject _uiGo;
        private PanelSettings _panel;
        private BuildBarView _view;
        private int _clicks;
        private int _available;

        [SetUp]
        public void SetUp()
        {
            _uiGo = new GameObject("BuildBar");
            _uiGo.SetActive(false);
            _panel = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _view = _uiGo.AddComponent<BuildBarView>();
            _available = 5;
            _view.AvailableLogs = () => _available;
            _uiGo.SetActive(true);
            _clicks = 0;
            _view.ToolClicked += id => { if (id == "woodcutter") _clicks++; };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_panel);
        }

        [UnityTest]
        public IEnumerator BelowCost_DisabledWithTooltip_ThenReenablesWithoutClick()
        {
            yield return null;
            var button = _view.GetButton("woodcutter");
            Assert.IsNotNull(button);
            Assert.IsTrue(button.ClassListContains(DisabledClass), "Woodcutter should grey out at 5 logs.");
            Assert.AreEqual("Needs 8 logs (have 5)", button.tooltip);
            _view.Click("woodcutter");
            Assert.AreEqual(0, _clicks, "A disabled entry must ignore clicks.");

            _available = 8;
            yield return null;
            Assert.IsFalse(button.ClassListContains(DisabledClass), "Woodcutter should re-enable at 8 logs without a click.");
            Assert.AreEqual(DrawTooltip, button.tooltip);
            Assert.IsTrue(_view.IsEntryEnabled("woodcutter"));
            _view.Click("woodcutter");
            Assert.AreEqual(1, _clicks);
        }

        [UnityTest]
        public IEnumerator WiredGameScene_WoodcutterMatchesLiveConstructionLogs()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            var bar = Object.FindFirstObjectByType<BuildBarView>();
            var host = Object.FindFirstObjectByType<SimHost>();
            Assert.IsNotNull(bar, "BuildBarView missing.");
            Assert.IsNotNull(host, "SimHost missing.");
            Assert.IsTrue(host.IsInitialized, "SimHost should be initialised.");

            int logs = ConstructionSources.Available(host.World, Items.Log);
            var button = bar.GetButton("woodcutter");
            Assert.IsNotNull(button, "Woodcutter button missing.");
            bool expected = logs >= 8;
            Assert.AreEqual(expected, bar.IsEntryEnabled("woodcutter"), "Logs available: " + logs);
            Assert.AreEqual(!expected, button.ClassListContains(DisabledClass));
            Assert.AreEqual(expected ? DrawTooltip : "Needs 8 logs (have " + logs + ")", button.tooltip);
        }
    }
}
