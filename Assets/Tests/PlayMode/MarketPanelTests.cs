using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the market panel and market picking (T-476, MK-23b, MK-40, MK-41).</summary>
    public class MarketPanelTests
    {
        private GameObject _hostGo, _camGo, _groundGo, _agentsGo, _ui;
        private UnityEngine.UIElements.PanelSettings _settings;
        private SimHost _host;
        private Camera _cam;
        private AgentPicker _agents;
        private MarketPanelView _panel;
        private int _marketId, _areaId, _stallId, _houseId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            _host.Initialize(new SimTuning { DeconstructTimeShare = 0, BushCount = 0, MarketRangeM = 80 }, map);
            _host.AutoUpdate = false;
            var w = _host.World;
            var r = _host.Execute(new DesignateArea(AreaShape.Straight(new[] { M(90, 90), M(130, 90), M(130, 130), M(90, 130) }), AreaTypes.MarketId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _marketId = w.Markets.Current.Id;
            _areaId = w.Markets.Current.AreaId;
            // A built, unstaffed stall holding food (no construction site, so it counts as built).
            var stall = w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.MarketStall, M(100, 100), 0);
            _stallId = stall.Id;
            w.Inventory.AddFoodStore(ModuleTypes.MarketStall.FoodCapacity, _stallId);
            w.Inventory.Produce(w.Inventory.FindFoodStore(_stallId), Items.Berries, 12);
            w.Markets.Current.StallFootprintIds.Add(_stallId);
            _houseId = w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.House, M(150, 110), 0).Id;
            w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.House, M(20, 20), 0);

            _camGo = new GameObject("Cam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 40f;
            _camGo.transform.position = new Vector3(110f, 50f, 110f);
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
            _ui = new GameObject("MarketPanel");
            _ui.SetActive(false);
            _ui.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _settings;
            _panel = _ui.AddComponent<MarketPanelView>();
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

        [Test]
        public void TryPickMarketHitsTheAreaAndTheStallButNotOutside()
        {
            var w = _host.World;
            Assert.IsTrue(AgentPicker.TryPickMarket(w, M(120, 120), out int byArea));
            Assert.AreEqual(_marketId, byArea);
            Assert.IsTrue(AgentPicker.TryPickMarket(w, M(100, 100), out int byStall));
            Assert.AreEqual(_marketId, byStall);
            Assert.IsFalse(AgentPicker.TryPickMarket(w, M(60, 60), out _));
        }

        [UnityTest]
        public IEnumerator ClickingTheMarketAreaOrAStallOpensThePanelWithItsContents()
        {
            Assert.IsTrue(_agents.ClickAt(ScreenOf(125, 125)));
            yield return null;
            Assert.IsTrue(_agents.HasMarket);
            Assert.IsTrue(_panel.IsShown);
            Assert.AreEqual("Market " + _marketId, _panel.TitleText);
            StringAssert.Contains("Berries: 12", _panel.StockText);
            StringAssert.Contains("Food on stalls: 12", _panel.StockText);
            StringAssert.StartsWith("Stalls: 1 built /", _panel.StallsText);
            StringAssert.Contains("Unstaffed, 12 items", _panel.StallRowText(_stallId));
            Assert.IsNotNull(_panel.DeconstructButton(_stallId));
            Assert.Greater(_panel.PanelElement.worldBound.width, 0f);
            Assert.AreEqual(1, _panel.InRangeCount);
            Assert.AreEqual(1, _panel.OutOfRangeCount);
            Assert.IsTrue(_panel.TintVisible);

            _agents.ClearMarket();
            yield return null;
            Assert.IsFalse(_panel.IsShown);
            Assert.IsFalse(_panel.TintVisible);

            Assert.IsTrue(_agents.ClickAt(ScreenOf(100, 100)));
            yield return null;
            Assert.IsTrue(_agents.HasMarket, "A click on the stall selects the market.");
        }

        [UnityTest]
        public IEnumerator PanelRefreshesWhenFoodChanges()
        {
            _agents.SelectMarket(_marketId);
            yield return null;
            var w = _host.World;
            w.Inventory.Produce(w.Inventory.FindFoodStore(_stallId), Items.Meat, 5);
            yield return null;
            StringAssert.Contains("Meat: 5", _panel.StockText);
            StringAssert.Contains("Unstaffed, 17 items", _panel.StallRowText(_stallId));
        }

        [UnityTest]
        public IEnumerator ConfirmedStallDeconstructSendsTheCommand()
        {
            var w = _host.World;
            _agents.SelectMarket(_marketId);
            yield return null;
            _panel.ClickDeconstructStall(_stallId);
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNull(w.Deconstruction.Get(_stallId), "Cancel sends nothing.");
            _panel.ClickDeconstructStall(_stallId);
            ConfirmDialog.Confirm();
            Assert.IsNotNull(w.Deconstruction.Get(_stallId));
            yield return null;
            Assert.IsNull(_panel.DeconstructButton(_stallId), "A stall being deconstructed offers no second Deconstruct.");
        }

        [UnityTest]
        public IEnumerator RemoveMarketUsesTheConfirmDialogAndDeletesTheArea()
        {
            var w = _host.World;
            _agents.SelectMarket(_marketId);
            yield return null;
            _panel.ClickRemoveMarket();
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.IsNotNull(w.Areas.Get(_areaId));
            _panel.ClickRemoveMarket();
            ConfirmDialog.Confirm();
            Assert.IsNull(w.Areas.Get(_areaId));
            Assert.IsNull(w.Markets.Get(_marketId));
            yield return null;
            Assert.IsFalse(_agents.HasMarket);
            Assert.IsFalse(_panel.IsShown);
        }
    }
}
