using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the thatcher's hut build entry, operation panel, reed beds and stooks (T-580, MAT-31, MAT-33, MAT-39e).</summary>
    public class ThatcherUiTests
    {
        private GameObject _hostGo, _agentsGo, _opUi, _bushGo;
        private PanelSettings _settings;
        private SimHost _host;
        private AgentPicker _agents;
        private OperationPanelView _op;
        private Operation _operation;
        private const int HutId = 903;

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
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.ThatchingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _operation = w.Operations.FindByArea((int)r.Created[0]);
            var hut = w.Footprints.Add(HutId, ModuleTypes.ThatcherHut, M(100, 100), 0);
            w.Operations.AddModule(_operation.Id, HutId);
            LordsLegacy.Sim.Operations.Workshops.WorkshopBuildings.OnBuilt(w, hut);

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
        public void ThatcherEntryIsEnabledAndGatedByTheHutCost()
        {
            ToolEntry e = null;
            foreach (var t in ToolEntry.Defaults()) if (t.Id == "thatcher") e = t;
            Assert.IsNotNull(e);
            Assert.IsTrue(e.Enabled);
            Assert.AreEqual(BuildCost.MinimumFor(OperationTypes.Thatching), e.CostLogs());
            Assert.AreEqual(ModuleTypes.ThatcherHut.ConstructionRecipe.Logs, e.CostLogs());
            Assert.AreEqual(LordsLegacy.Game.Tools.OperationDraftTool.ThatcherToolId, e.Id);
            Assert.IsNotNull(OperationTypes.Find(OperationTypes.ThatchingId));
        }

        [UnityTest]
        public IEnumerator ReedBedsAreDrawnGreenAndLookCutWhileRegrowing()
        {
            var w = _host.World;
            int sheaves = w.Tuning.ReedSheavesPerCut;
            w.Bushes.Add(50f, 50f, BushKind.ReedBed, sheaves);
            var bed = w.Bushes.Add(53f, 50f, BushKind.ReedBed, sheaves);
            var renderer = _bushGo.GetComponent<BushRenderer>();
            yield return null;
            Assert.AreEqual(2, renderer.ReedFullCount);
            Assert.AreEqual(0, renderer.ReedCutCount);
            Assert.AreEqual(0, renderer.FullCount, "reed beds are counted apart from forage bushes");
            for (int i = 0; i < sheaves; i++)
            {
                Assert.IsTrue(w.Bushes.Reserve(bed.Id, 1));
                Assert.IsTrue(w.Bushes.TakeBasket(bed.Id, 1, w.Tick));
            }
            yield return null;
            Assert.AreEqual(1, renderer.ReedFullCount);
            Assert.AreEqual(1, renderer.ReedCutCount);
        }

        [UnityTest]
        public IEnumerator StooksAreDrawnGreenWhileCuringAndDryOncePiled()
        {
            var w = _host.World;
            w.Curing.Add(M(60, 60), _operation.Id, 5, w.Tick + 100000);
            yield return null;
            var stooks = _bushGo.GetComponent<StookRenderer>();
            Assert.IsNotNull(stooks, "the bush renderer adds the stook renderer");
            yield return null;
            Assert.AreEqual(1, stooks.GreenCount);
            Assert.AreEqual(0, stooks.DryCount);
            w.Inventory.ProduceOnGround(M(62, 60), _operation.Id, Items.Thatch, 4);
            yield return null;
            Assert.AreEqual(1, stooks.GreenCount);
            Assert.AreEqual(1, stooks.DryCount);
        }

        [UnityTest]
        public IEnumerator OperationPanelShowsStatusStoreStooksFamiliesAndGreyedGrainToggle()
        {
            var w = _host.World;
            w.Bushes.Add(100f, 95f, BushKind.ReedBed, w.Tuning.ReedSheavesPerCut);
            w.Curing.Add(M(95, 95), _operation.Id, 6, w.Tick + 100000);
            _agents.SelectOperation(_operation.Id);
            yield return null;
            Assert.IsTrue(_op.IsShown);
            Assert.AreEqual("Thatcher's hut " + _operation.Id, _op.Find<Label>("operation-title").text);
            StringAssert.StartsWith("Status:", _op.StatusText);
            Assert.AreEqual("Status: " + LordsLegacy.Sim.Thatch.ThatchingStatus.TextOf(w, OperationState.NoWorkers), _op.StatusText);
            StringAssert.StartsWith("Store: 0 /", _op.StockLineText);
            StringAssert.Contains("Reed beds in work area: 1 ready", _op.TreesText);
            Assert.AreEqual("Stooks curing: 1 (6 bundles)\nNext stook ready: " + w.TimeAt(w.Tick + 100000).DayOfMonth
                + " " + new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }[w.TimeAt(w.Tick + 100000).Month - 1],
                _op.Find<Label>("thatch-stooks").text);
            Assert.AreEqual("Dry sheaves: 0", _op.Find<Label>("thatch-dry").text);
            var grain = _op.Find<Toggle>("thatch-grain");
            Assert.IsNotNull(grain);
            Assert.AreEqual("Grown grain: Needs farming", grain.label);
            Assert.IsFalse(grain.enabledSelf, "the grain toggle is greyed");
            Assert.AreEqual(DisplayStyle.Flex, _op.Find<VisualElement>("thatch-box").resolvedStyle.display);
            Assert.IsNotNull(_op.Find<Button>("assign-slot-0"));
            Assert.IsNotNull(_op.Find<Button>("deconstruct-" + HutId));
            Assert.AreEqual(DisplayStyle.None, _op.Find<Label>("operation-logs").resolvedStyle.display);
            Assert.IsTrue(_op.PanelElement.worldBound.width > 0 && _op.PanelElement.worldBound.height > 0);
        }

        [UnityTest]
        public IEnumerator PanelStatusSaysNoReedBedsWhenNoneAreInTheWorkArea()
        {
            var w = _host.World;
            _agents.SelectOperation(_operation.Id);
            yield return null;
            StringAssert.Contains("Reed beds in work area: 0 ready, 0 regrowing", _op.TreesText);
            Assert.AreEqual("No reed beds", LordsLegacy.Sim.Thatch.ThatchingStatus.TextOf(w, OperationState.NoBushes));
            StringAssert.StartsWith("Cutting", LordsLegacy.Sim.Thatch.ThatchingStatus.TextOf(w, OperationState.Working));
        }
    }
}
