using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the coppice build entry, stool drawing and operation panel (T-568, MAT-10..16).</summary>
    public class CoppiceUiTests
    {
        private GameObject _hostGo, _agentsGo, _opUi, _bushGo;
        private PanelSettings _settings;
        private SimHost _host;
        private AgentPicker _agents;
        private OperationPanelView _op;
        private Operation _operation;
        private const int HutId = 902;

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
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.CoppiceId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _operation = w.Operations.FindByArea((int)r.Created[0]);
            var hut = w.Footprints.Add(HutId, ModuleTypes.CoppiceHut, M(100, 100), 0);
            w.Operations.AddModule(_operation.Id, HutId);
            LordsLegacy.Sim.Operations.Workshops.WorkshopBuildings.OnBuilt(w, hut); // creates the rod store, as a finished build does

            _agentsGo = new GameObject("AgentPicker");
            _agents = _agentsGo.AddComponent<AgentPicker>();
            _agents.ReadMouse = false;
            _agents.Configure(_host, null, null);
            _settings = TestPanelSettings.Create();
            _opUi = new GameObject("OperationPanel");
            _opUi.SetActive(false);
            _opUi.AddComponent<UIDocument>().panelSettings = _settings;
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
        public void CoppiceEntryIsEnabledAndGatedByTheHutCost()
        {
            ToolEntry e = null;
            foreach (var t in ToolEntry.Defaults()) if (t.Id == "coppice") e = t;
            Assert.IsNotNull(e);
            Assert.IsTrue(e.Enabled);
            Assert.AreEqual(BuildCost.MinimumFor(OperationTypes.Coppice), e.CostLogs());
            Assert.AreEqual(ModuleTypes.CoppiceHut.ConstructionRecipe.Logs, e.CostLogs());
            Assert.AreEqual(LordsLegacy.Game.Tools.OperationDraftTool.CoppiceToolId, e.Id);
        }

        [UnityTest]
        public IEnumerator StoolsAreDrawnByKindAndLookCutWhileRegrowing()
        {
            var w = _host.World;
            int rods = w.Tuning.CoppiceRodsPerCut;
            w.Bushes.Add(50f, 50f, BushKind.HazelStool, rods);
            var willow = w.Bushes.Add(52f, 50f, BushKind.WillowStool, rods);
            w.Bushes.Add(54f, 50f, BushKind.Berries, w.Tuning.BushBaskets);
            var renderer = _bushGo.GetComponent<BushRenderer>();
            yield return null;
            Assert.AreEqual(1, renderer.StoolFullCount(BushKind.HazelStool));
            Assert.AreEqual(1, renderer.StoolFullCount(BushKind.WillowStool));
            Assert.AreEqual(0, renderer.StoolCutCount(BushKind.WillowStool));
            Assert.AreEqual(1, renderer.FullCount, "forage bushes are counted apart from stools");
            for (int i = 0; i < rods; i++)
            {
                Assert.IsTrue(w.Bushes.Reserve(willow.Id, 1));
                Assert.IsTrue(w.Bushes.TakeBasket(willow.Id, 1, w.Tick));
            }
            yield return null;
            Assert.AreEqual(0, renderer.StoolFullCount(BushKind.WillowStool));
            Assert.AreEqual(1, renderer.StoolCutCount(BushKind.WillowStool));
            Assert.AreEqual(1, renderer.StoolFullCount(BushKind.HazelStool));
        }

        [UnityTest]
        public IEnumerator OperationPanelShowsStatusRodsStoolsWorkAreaAndDeconstruct()
        {
            var w = _host.World;
            w.Bushes.Add(100f, 95f, BushKind.HazelStool, w.Tuning.CoppiceRodsPerCut);
            var cut = w.Bushes.Add(102f, 95f, BushKind.WillowStool, 0);
            Assert.IsNotNull(cut);
            _agents.SelectOperation(_operation.Id);
            yield return null;
            Assert.IsTrue(_op.IsShown);
            Assert.AreEqual("Coppice hut " + _operation.Id, _op.Find<Label>("operation-title").text);
            StringAssert.StartsWith("Status:", _op.StatusText);
            StringAssert.Contains("hazel: 0 / 40", _op.StockLineText);
            StringAssert.Contains("willow: 0 / 40", _op.StockLineText);
            StringAssert.Contains("hazel: 1 available, 0 regrowing", _op.TreesText);
            StringAssert.Contains("willow: 0 available, 1 regrowing", _op.TreesText);
            StringAssert.StartsWith("Work area:", _op.WorkAreaText);
            Assert.AreEqual(DisplayStyle.None, _op.Find<Label>("operation-logs").resolvedStyle.display);
            Assert.IsNotNull(_op.Find<Button>("draw-work-area"));
            Assert.IsNotNull(_op.Find<Button>("deconstruct-operation"));
            Assert.IsNotNull(_op.Find<Button>("deconstruct-" + HutId));
            Assert.IsNotNull(_op.Find<Button>("assign-slot-0"));
            Assert.IsTrue(_op.PanelElement.worldBound.width > 0 && _op.PanelElement.worldBound.height > 0);
        }
    }
}
