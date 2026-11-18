using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the forager build entry, bush drawing and operation panel (T-425, FD-20..24).</summary>
    public class ForagerUiTests
    {
        private GameObject _hostGo, _agentsGo, _opUi, _bushGo;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private AgentPicker _agents;
        private OperationPanelView _op;
        private Operation _operation;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning { BushCount = 0 }, SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            var r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ForagingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _operation = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(901, ModuleTypes.ForagerHut, M(100, 100), 0);
            w.Operations.AddModule(_operation.Id, 901);
            w.Inventory.AddFoodStore(w.Tuning.ForagerHutCapacity, 901);

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, null, null);
            _settings = TestPanelSettings.Create();
            _opUi = new GameObject("OperationPanel");
            _opUi.SetActive(false);
            _opUi.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _op = _opUi.AddComponent<OperationPanelView>();
            _op.Configure(_host, _agents, null);
            _opUi.SetActive(true);
            _bushGo = new GameObject("BushRenderer");
            _bushGo.AddComponent<BushRenderer>().Configure(_host);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _bushGo, _opUi, _agentsGo, _hostGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_settings != null) Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ForagerEntryIsEnabledAndGatedByTheHutCost()
        {
            ToolEntry e = null;
            foreach (var t in ToolEntry.Defaults()) if (t.Id == "forager") e = t;
            Assert.IsNotNull(e);
            Assert.IsTrue(e.Enabled);
            Assert.AreEqual(BuildCost.MinimumFor(OperationTypes.Foraging), e.CostLogs());
            Assert.AreEqual(ModuleTypes.ForagerHut.ConstructionRecipe.Logs, e.CostLogs());
        }

        [UnityTest]
        public IEnumerator BushesAreDrawnFullOrEmptyAndFollowTheirBaskets()
        {
            var w = _host.World;
            var a = w.Bushes.Add(50f, 50f, LordsLegacy.Sim.Foraging.BushKind.Berries, w.Tuning.BushBaskets);
            w.Bushes.Add(52f, 50f, LordsLegacy.Sim.Foraging.BushKind.Mushrooms, w.Tuning.BushBaskets);
            var renderer = _bushGo.GetComponent<BushRenderer>();
            yield return null;
            Assert.AreEqual(2, renderer.FullCount);
            Assert.AreEqual(0, renderer.EmptyCount);
            for (int i = 0; i < w.Tuning.BushBaskets; i++)
            {
                Assert.IsTrue(w.Bushes.Reserve(a.Id, 1));
                Assert.IsTrue(w.Bushes.TakeBasket(a.Id, 1, w.Tick));
            }
            yield return null;
            Assert.AreEqual(1, renderer.FullCount);
            Assert.AreEqual(1, renderer.EmptyCount);
        }

        [UnityTest]
        public IEnumerator OperationPanelShowsHutStoreBushesAndStorageFull()
        {
            var w = _host.World;
            w.Bushes.Add(100f, 95f, LordsLegacy.Sim.Foraging.BushKind.Berries, w.Tuning.BushBaskets);
            _agents.SelectOperation(_operation.Id);
            yield return null;
            Assert.IsTrue(_op.IsShown);
            StringAssert.StartsWith("Forager camp", _op.Find<UnityEngine.UIElements.Label>("operation-title")?.text ?? "Forager camp");
            Assert.AreEqual("Hut store: 0 / 30", _op.StockLineText);
            StringAssert.Contains("Baskets: 0 / " + w.Tuning.ForagerHutBasketSlots, _op.ModulesText);
            StringAssert.Contains("1 full, 0 empty", _op.TreesText);
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, _op.Find<UnityEngine.UIElements.Label>("operation-logs").resolvedStyle.display);
            var store = w.Inventory.FindFoodStore(901);
            w.Inventory.Produce(store, Items.Berries, 30);
            _op.Refresh();
            Assert.AreEqual("Hut store: 30 / 30 - Storage full", _op.StockLineText);
        }
    }
}
