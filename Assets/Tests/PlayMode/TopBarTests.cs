using LordsLegacy.Game;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    public class TopBarTests
    {
        private GameObject _hostGo, _uiGo;
        private SimHost _host;
        private TopBarController _bar;
        private PanelSettings _panel;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("SimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.AutoUpdate = false;
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());

            _uiGo = new GameObject("TopBar");
            _uiGo.SetActive(false);
            _panel = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _bar = _uiGo.AddComponent<TopBarController>();
            _bar.SetHost(_host);
            _uiGo.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_hostGo);
            Object.DestroyImmediate(_panel);
        }

        [Test]
        public void FoodReadoutShowsWholeDaysAndRawCountsInTooltip()
        {
            // No people yet: nobody to feed, so no days to count (FD-04).
            _bar.Refresh();
            Assert.AreEqual("Food: –", _bar.FoodReadoutText);

            var w = _host.World;
            w.People.AddFamily(1, "Archer", 100, 100);
            w.People.AddCitizen(1, "Alda", Sex.Female, -20L * Citizen.TicksPerYear, 1, 100, 100); // one adult: 1 ration a day
            Family family = w.People.FindFamily(1);
            w.Inventory.Produce(Larders.ForFamily(w, family), Items.Meat, 4);     // 12 rations
            w.Inventory.Produce(Larders.ForFamily(w, family), Items.Berries, 2);  // 2 rations

            _bar.Refresh();
            Assert.AreEqual("Food: 14 days", _bar.FoodReadoutText);
            Assert.AreEqual("Meat: 4 (12 rations)\nBerries: 2 (2 rations)\nMushrooms: 0 (0 rations)", _bar.FoodReadoutTooltip);
        }

        [Test]
        public void PopulationReadoutShowsFamiliesCitizensAndTooltipCounts()
        {
            var w = _host.World;
            w.People.AddFamily(1, "Archer", 100, 100);
            w.People.AddCitizen(1, "Alda", Sex.Female, -20L * Citizen.TicksPerYear, 1, 100, 100);
            w.People.AddCitizen(2, "Bran", Sex.Male, -20L * Citizen.TicksPerYear, 1, 100, 100);
            w.People.AddFamily(2, "Baker", 90, 90);
            w.People.AddCitizen(3, "Cy", Sex.Male, -20L * Citizen.TicksPerYear, 2, 90, 90);
            LordsLegacy.Sim.Growth.FamilyDeparture.Begin(w, 2);
            _bar.Refresh();
            Assert.AreEqual("Families: 2  Citizens: 3", _bar.PopulationText);
            StringAssert.Contains("Arriving: 0", _bar.PopulationTooltip);
            StringAssert.Contains("Leaving: 1", _bar.PopulationTooltip);
            StringAssert.Contains("Next family:", _bar.PopulationTooltip);
            Assert.IsFalse(_bar.PopulationTipShown);
            _bar.ShowPopulationTip();
            Assert.IsTrue(_bar.PopulationTipShown);
            StringAssert.Contains("Leaving: 1", _bar.PopulationTipText);
            _bar.HidePopulationTip();
            Assert.IsFalse(_bar.PopulationTipShown);
        }

        private long Run100()
        {
            long start = _host.World.Tick;
            for (int i = 0; i < 100; i++) _host.Tick(0.1f);
            return _host.World.Tick - start;
        }

        [Test]
        public void SpeedPauseAndSteadyLight()
        {
            long n = Run100();
            Assert.Greater(n, 0);
            _bar.Refresh();
            Assert.AreEqual(_host.World.Now.Format(), _bar.DateText);

            _bar.SetSpeed(2); // 4x
            Assert.AreEqual(4, _host.Pacer.CurrentSpeed);
            Assert.AreEqual(4 * n, Run100(), 1);

            long tick = _host.World.Tick;
            _bar.ToggleSteadyLight();
            Assert.IsTrue(_bar.ViewSettings.SteadyLight);
            Assert.AreEqual(tick, _host.World.Tick);

            _bar.TogglePause();
            Assert.AreEqual(0, Run100());
            Assert.AreEqual("Paused", _bar.DateText);
        }

        [Test]
        public void SpeedButtonsNeverTakeFocusAndNavigationKeysKeepSpeed()
        {
            var root = _uiGo.GetComponent<UIDocument>().rootVisualElement;
            var speed = root.Q<Button>("speed-2");
            Assert.IsNotNull(speed);
            Assert.IsFalse(speed.focusable);
            Assert.IsFalse(root.Q<Button>("pause-button").focusable);

            _bar.SetSpeed(2); // 4x, as if clicked
            Assert.IsNull(root.panel.focusController.focusedElement);

            // Stand-ins for the Input System Navigate action from A, D, W, S and the arrow keys.
            Vector2[] dirs = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
            foreach (var dir in dirs)
            {
                var e = NavigationMoveEvent.GetPooled(dir);
                e.target = root;
                root.SendEvent(e);
                e.Dispose();
            }
            Assert.AreEqual(2, _host.Pacer.CurrentSpeedIndex);
            Assert.AreEqual(4, _host.Pacer.CurrentSpeed);
            Assert.IsNull(root.panel.focusController.focusedElement);
        }
    }
}
