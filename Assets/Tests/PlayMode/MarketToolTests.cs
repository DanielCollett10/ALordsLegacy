using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="MarketTool"/> (T-474, markets.md MK-01, MK-04, MK-05, MK-41).</summary>
    public class MarketToolTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private UnityEngine.UIElements.PanelSettings _panelSettings;
        private SimHost _host;
        private ToolPanelView _panel;
        private AreaTool _area;
        private MarketTool _tool;
        private ToolController _controller;
        private AreaRenderer _renderer;
        private Camera _cam;
        private float _time;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
        }

        private IEnumerator Setup()
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            _camGo = new GameObject("TestCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 60f;
            _camGo.transform.position = new Vector3(100f, 50f, 100f);
            _camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _groundGo = new GameObject("TestGround");
            _groundGo.transform.position = new Vector3(100f, -0.5f, 100f);
            _groundGo.AddComponent<BoxCollider>().size = new Vector3(1000f, 1f, 1000f);
            var pickerGo = new GameObject("Picker");
            pickerGo.transform.SetParent(_groundGo.transform, false);
            var picker = pickerGo.AddComponent<GroundPicker>();
            picker.PickCamera = _cam; picker.Host = _host;
            _rendererGo = new GameObject("TestAreaRenderer");
            _renderer = _rendererGo.AddComponent<AreaRenderer>();
            _renderer.Configure(_host);
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("MarketTools");
            _controller = _toolGo.AddComponent<ToolController>();
            _area = _toolGo.AddComponent<AreaTool>();
            _area.Configure("market-draw", AreaTypes.MarketId, _host, picker, _renderer, _panel, _controller);
            _tool = _toolGo.AddComponent<MarketTool>();
            _tool.Configure(ToolEntry.MarketToolId, _host, _area, _panel, _controller);
            _controller.Register(_tool);
            yield return null;
            _controller.Toggle(ToolEntry.MarketToolId);
        }

        private void Click(float x, float z)
        {
            var p = _cam.WorldToScreenPoint(new Vector3(x, 0f, z));
            _area.PointerDown(PressButton.Left, p);
            _area.PointerUp(PressButton.Left, p, _time += 2f);
        }

        private void DrawRect(float x0, float z0, float x1, float z1)
        {
            Click(x0, z0); Click(x1, z0); Click(x1, z1); Click(x0, z1);
        }

        [Test]
        public void BuildBarEntryIsFreeThenDisabledWithReasonOnceAMarketExists()
        {
            ToolEntry entry = ToolEntry.Defaults().First(e => e.Id == ToolEntry.MarketToolId);
            Assert.IsTrue(entry.Enabled);
            Assert.IsNull(entry.CostLogs, "A market is free (MK-01).");
            var world = new LordsLegacy.Sim.Core.SimWorld(new SimTuning());
            Assert.AreEqual(string.Empty, entry.DisabledReason(world));
            world.Markets.Add(1);
            Assert.AreEqual("Only one market per town", entry.DisabledReason(world));
        }

        [UnityTest]
        public IEnumerator DrawingShowsSlotOutlinesAndConfirmDesignatesTheMarket()
        {
            yield return Setup();
            Assert.AreSame(_tool, _controller.ActiveTool);
            DrawRect(30f, 70f, 60f, 95f);
            yield return null;
            Assert.IsTrue(_area.CurrentPreview.IsValid, _area.CurrentPreview.ReasonText);
            Assert.IsTrue(_tool.OverlayVisible, "Slot outlines draw while the draft is valid.");
            Assert.Greater(_tool.SlotCount, 0);
            Assert.IsNull(_tool.Refusal);
            Assert.AreEqual(0, _host.World.Markets.Count);
            _panel.ClickConfirm();
            Assert.AreEqual(1, _host.World.Markets.Count, "Confirm registers the market.");
            Assert.AreEqual(1, _host.World.Areas.All.Count(a => a.TypeId == AreaTypes.MarketId));
            Assert.IsNull(_controller.ActiveTool, "The tool ends after a confirmed market.");
            Assert.IsFalse(_tool.OverlayVisible);
            _renderer.Sync();
            Assert.AreEqual(1, _renderer.AreaCount, "The confirmed market area is drawn.");
        }

        [UnityTest]
        public IEnumerator TooSmallMarketShowsTheSimsReasonAndCannotBeConfirmed()
        {
            yield return Setup();
            DrawRect(30f, 70f, 36f, 76f); // 36 m²
            yield return null;
            Assert.IsFalse(_area.CurrentPreview.IsValid);
            Assert.AreEqual("Too small for a market", _tool.Refusal);
            StringAssert.Contains("Too small for a market", _panel.Status);
            Assert.IsFalse(_tool.OverlayVisible);
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Markets.Count);
        }
    }
}
