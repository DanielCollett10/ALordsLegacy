using System.Collections;
using System.Linq;
using System.Reflection;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>The House plot build bar entry is gated by the full recipe and lists have / need per item (T-581, MAT-40).</summary>
    public class HousePlotGateTests
    {
        private const string DisabledClass = "build-bar__button--disabled";

        private GameObject _hostGo, _uiGo;
        private PanelSettings _panel;
        private SimHost _host;
        private BuildBarView _view;
        private int _clicks;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, 20);

            _uiGo = new GameObject("BuildBar");
            _uiGo.SetActive(false);
            _panel = TestPanelSettings.Create();
            _uiGo.AddComponent<UIDocument>().panelSettings = _panel;
            _view = _uiGo.AddComponent<BuildBarView>();
            typeof(BuildBarView).GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_view, _host);
            _uiGo.SetActive(true);
            _clicks = 0;
            _view.ToolClicked += id => { if (id == "house") _clicks++; };
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_uiGo != null) Object.DestroyImmediate(_uiGo);
            if (_hostGo != null) Object.DestroyImmediate(_hostGo);
            if (_panel != null) Object.DestroyImmediate(_panel);
        }

        [UnityTest]
        public IEnumerator ShortOnThatchAndPanels_DisablesTheEntryAndListsEveryItem()
        {
            var button = _view.GetButton("house");
            Assert.IsNotNull(button);
            Assert.IsFalse(_view.IsEntryEnabled("house"));
            Assert.IsTrue(button.ClassListContains(DisabledClass));
            Assert.AreEqual("Not enough thatch (have 0, need 10)\nLogs: 20 / 10\nThatch: 0 / 10\nWattle panels: 0 / 10", button.tooltip);
            _view.Click("house");
            Assert.AreEqual(0, _clicks, "A disabled entry must ignore clicks.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StockingTheRecipe_ReenablesTheEntryWithoutAClick()
        {
            var goods = _host.World.Inventory.AddGoodsStore(M(60, 40));
            _host.World.Inventory.Produce(goods, Items.Thatch, 10);
            yield return null;
            var button = _view.GetButton("house");
            Assert.IsFalse(_view.IsEntryEnabled("house"), "Still short of wattle panels.");
            StringAssert.StartsWith("Not enough wattle panels (have 0, need 10)", button.tooltip);
            StringAssert.Contains("Thatch: 10 / 10", button.tooltip);

            _host.World.Inventory.Produce(goods, Items.WattlePanel, 10);
            yield return null;
            Assert.IsTrue(_view.IsEntryEnabled("house"));
            Assert.IsFalse(button.ClassListContains(DisabledClass));
            StringAssert.Contains("Wattle panels: 10 / 10", button.tooltip);
            _view.Click("house");
            Assert.AreEqual(1, _clicks);
        }

        [Test]
        public void RefusedPlot_ShowsTheSimRefusalText()
        {
            var w = _host.World;
            int road = w.Roads.Segments.First().Id;
            var shape = new AreaShape(new[] { M(40, 51.25), M(60, 51.25), M(60, 61.25), M(40, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
            var r = _host.Execute(new DesignatePlot(shape));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual("Not enough thatch (have 0, need 10)", r.Text);
        }
    }
}
