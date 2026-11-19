using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Game.Tools;
using LordsLegacy.Game.Tools.Shared;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="PlotTool"/> (T-408, homes.md HM-01..05).</summary>
    public class PlotToolTests
    {
        private GameObject _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo;
        private UnityEngine.UIElements.PanelSettings _panelSettings;
        private SimHost _host;
        private ToolPanelView _panel;
        private AreaTool _area;
        private PlotTool _tool;
        private ToolController _controller;
        private Camera _cam;
        private float _time;

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _hostGo, _rendererGo, _panelGo, _toolGo, _camGo, _groundGo })
                if (go != null) Object.DestroyImmediate(go);
            if (_panelSettings != null) Object.DestroyImmediate(_panelSettings);
        }

        private IEnumerator Setup(int logs = 20)
        {
            _hostGo = new GameObject("SyntheticSimHost");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;
            var w = _host.World;
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { PointMm.FromMetres(20, 50), PointMm.FromMetres(80, 50) },
                new[] { PointMm.FromMetres(20, 50), PointMm.FromMetres(50, 50), PointMm.FromMetres(80, 50) });
            if (logs > 0)
            {
                // The house recipe needs logs, thatch and wattle panels (MAT-40), so stock all three.
                // Thatch and panels are General items: they come only from goods stores (decision 0046).
                var pile = w.Inventory.AddStockpile(PointMm.FromMetres(60, 40));
                w.Inventory.Produce(pile, Items.Log, logs);
                var goods = w.Inventory.AddGoodsStore(PointMm.FromMetres(60, 40));
                w.Inventory.Produce(goods, Items.Thatch, logs);
                w.Inventory.Produce(goods, Items.WattlePanel, logs);
            }
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
            var renderer = _rendererGo.AddComponent<AreaRenderer>();
            renderer.Configure(_host);
            _panelGo = new GameObject("ToolPanel");
            _panelGo.SetActive(false);
            _panelSettings = TestPanelSettings.Create();
            _panelGo.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = _panelSettings;
            _panel = _panelGo.AddComponent<ToolPanelView>();
            _panelGo.SetActive(true);

            _toolGo = new GameObject("PlotTools");
            _controller = _toolGo.AddComponent<ToolController>();
            _area = _toolGo.AddComponent<AreaTool>();
            _area.Configure("plot-draw", AreaTypes.PlotId, _host, picker, renderer, _panel, _controller);
            _tool = _toolGo.AddComponent<PlotTool>();
            _tool.Configure("house", _host, _area, _panel, _controller);
            _controller.Register(_tool);
            yield return null;
            _controller.Toggle("house");
        }

        private Vector2 Screen(float x, float z) => _cam.WorldToScreenPoint(new Vector3(x, 0f, z));

        private void Click(float x, float z)
        {
            var p = Screen(x, z);
            _area.PointerDown(PressButton.Left, p);
            _area.PointerUp(PressButton.Left, p, _time += 2f);
        }

        // Two points on the road's left side line (z 51.25), two behind it.
        private void Draw(float x0, float x1, float depth)
        {
            Click(x0, 51.25f); Click(x1, 51.25f); Click(x1, 51.25f + depth); Click(x0, 51.25f + depth);
        }

        [UnityTest]
        public IEnumerator ValidPlotShowsHouseGhostAndConfirmSendsDesignatePlot()
        {
            yield return Setup();
            Assert.AreSame(_tool, _controller.ActiveTool);
            Draw(30, 50, 10);
            yield return null;
            Assert.IsTrue(_area.CurrentPreview.IsValid, _area.CurrentPreview.ReasonText);
            Assert.IsTrue(_tool.GhostVisible, "House ghost on the frontage. Panel: " + _panel.Status);
            Assert.IsNull(_tool.Refusal);
            Assert.AreEqual(0, _host.World.Homes.Count);
            _panel.ClickConfirm();
            Assert.AreEqual(1, _host.World.Homes.Count, "Confirm sends DesignatePlot.");
            Assert.AreEqual(1, _host.World.Areas.All.Count(a => a.TypeId == AreaTypes.PlotId));
            Assert.IsNull(_controller.ActiveTool, "The tool ends after a confirmed plot.");
            Assert.IsFalse(_tool.GhostVisible);
        }

        [UnityTest]
        public IEnumerator PlotWithNoFrontageIsRefusedAndCannotBeConfirmed()
        {
            yield return Setup();
            Click(30, 70); Click(50, 70); Click(50, 80); Click(30, 80);
            yield return null;
            Assert.IsFalse(_area.CurrentPreview.IsValid);
            Assert.AreEqual("A plot must face a road", _tool.Refusal);
            StringAssert.Contains("A plot must face a road", _panel.Status);
            Assert.IsFalse(_tool.GhostVisible);
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Homes.Count);
        }

        [UnityTest]
        public IEnumerator TooSmallPlotShowsTheSimsReason()
        {
            yield return Setup();
            Draw(30, 38, 5);
            yield return null;
            Assert.AreEqual("Too small for a house", _tool.Refusal);
            StringAssert.Contains("Too small for a house", _panel.Status);
            Assert.IsFalse(_tool.GhostVisible);
        }

        [UnityTest]
        public IEnumerator PlotThatCannotHoldTheHouseSaysNoRoom()
        {
            yield return Setup();
            Draw(30, 40, 6.25f);
            yield return null;
            Assert.IsTrue(_area.CurrentPreview.IsValid, _area.CurrentPreview.ReasonText);
            Assert.AreEqual("No room for the house", _tool.Refusal);
            StringAssert.Contains("No room for the house", _panel.Status);
            Assert.IsFalse(_tool.GhostVisible);
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Homes.Count);
        }

        [UnityTest]
        public IEnumerator CancelDiscardsTheDraftAndSendsNothing()
        {
            yield return Setup();
            Draw(30, 50, 10);
            yield return null;
            Assert.IsTrue(_tool.GhostVisible);
            int log = _host.World.CommandLog.Count;
            _panel.ClickCancel();
            Assert.IsNull(_controller.ActiveTool);
            Assert.IsFalse(_tool.GhostVisible);
            Assert.AreEqual(0, _area.PointCount);
            Assert.AreEqual(log, _host.World.CommandLog.Count);
            Assert.AreEqual(0, _host.World.Homes.Count);
        }

        // Granary whose yard's west edge is x = minX and whose yard spans z 56.25 .. 72.25 (T-480, PL-13).
        private double AddGranaryBesideFrontage()
        {
            var w = _host.World;
            var f = w.Footprints.Add(w.Footprints.NextId(), LordsLegacy.Sim.Footprints.ModuleTypes.Granary, PointMm.FromMetres(70, 64.25), 0);
            double minX = double.MaxValue;
            foreach (var p in f.PlotPolygon) minX = System.Math.Min(minX, p.XMetres);
            return minX;
        }

        [UnityTest]
        public IEnumerator PlotFlushAgainstAGranaryYardIsAcceptedAndOneOverItIsRefused()
        {
            yield return Setup();
            float edge = (float)AddGranaryBesideFrontage();
            Click(edge - 15f, 51.25f); Click(edge, 51.25f); Click(edge, 61.25f); Click(edge - 15f, 61.25f);
            yield return null;
            Assert.IsTrue(_area.CurrentPreview.IsValid, "Flush against the yard: " + _area.CurrentPreview.ReasonText);
            _area.Cancel();
            yield return null;
            Click(edge - 15f, 51.25f); Click(edge + 3f, 51.25f); Click(edge + 3f, 61.25f); Click(edge - 15f, 61.25f);
            yield return null;
            Assert.IsFalse(_area.CurrentPreview.IsValid, "Three metres into the yard must be refused.");
        }

        [UnityTest]
        public IEnumerator OverlayShowsOutlinesCornersAndPileCirclesWhileToolIsActive()
        {
            yield return Setup();
            var w = _host.World;
            AddGranaryBesideFrontage();
            w.Inventory.Produce(w.Inventory.GetOrAddGroundPile(PointMm.FromMetres(30, 80), null), Items.Log, 2);
            var overlay = _toolGo.GetComponent<PlacementOverlay>();
            Assert.IsNotNull(overlay);
            yield return null; yield return null;
            Assert.IsTrue(overlay.IsShown, "Plot tool is a placing tool (PL-12).");
            Assert.AreEqual(1, overlay.OutlineCount, "The granary yard outline.");
            Assert.AreEqual(4, overlay.CornerCount);
            Assert.AreEqual(1, overlay.PileCircleCount, "PL-15 circle for the pile with items.");
            _controller.Deselect();
            yield return null;
            Assert.IsFalse(overlay.IsShown, "The overlay goes when the tool closes.");
        }

        [UnityTest]
        public IEnumerator NotEnoughLogsRefusesOnConfirm()
        {
            yield return Setup(logs: 5);
            Draw(30, 50, 10);
            yield return null;
            _panel.ClickConfirm();
            Assert.AreEqual(0, _host.World.Homes.Count);
            Assert.AreSame(_tool, _controller.ActiveTool, "A refused confirm keeps the draft.");
            StringAssert.Contains("Not enough logs", _panel.Status);
        }
    }
}
