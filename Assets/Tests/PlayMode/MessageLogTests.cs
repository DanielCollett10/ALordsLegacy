using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>T-508 / GR-30: the message log shows SettlementLog entries, last 4, each gone after 30 s real time.</summary>
    public class MessageLogTests
    {
        private GameObject _hostGo, _uiGo;
        private float _clock;

        [SetUp]
        public void SetUp() => _clock = 100f;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_hostGo);
        }

        private MessageLogView BuildView(out SimHost host)
        {
            _hostGo = new GameObject("SimHost");
            host = _hostGo.AddComponent<SimHost>();
            host.AutoUpdate = false;
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());

            _uiGo = new GameObject("MessageLog");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UIDocument>().panelSettings = LoadPanel();
            var view = _uiGo.AddComponent<MessageLogView>();
            view.Host = host;
            view.Clock = () => _clock;
            _uiGo.SetActive(true);
            return view;
        }

        [Test]
        public void EntryAppearsAndIsGoneAfterThirtySeconds()
        {
            var view = BuildView(out var host);
            Assert.AreEqual(0, view.VisibleSlotCount, "An empty log shows nothing.");

            host.World.SettlementLog.Add(host.World.Tick, "The Test family has arrived");
            view.Refresh();

            Assert.IsTrue(view.IsSlotVisible(0));
            Assert.AreEqual("The Test family has arrived", view.TextOf(0));
            Assert.AreEqual(1f, view.OpacityOf(0), 0.001f);

            _clock += 29.9f;
            view.Refresh();
            Assert.IsTrue(view.IsSlotVisible(0), "Still shown just before 30 s.");
            Assert.Less(view.OpacityOf(0), 1f, "Fading after 25 s.");

            _clock += 0.1f;
            view.Refresh();
            Assert.IsFalse(view.IsSlotVisible(0), "Gone at 30 s.");
            Assert.AreEqual(0, view.VisibleSlotCount);
        }

        [Test]
        public void ShowsLastFourEntriesNewestLast()
        {
            var view = BuildView(out var host);
            for (int i = 1; i <= 5; i++) host.World.SettlementLog.Add(host.World.Tick, "Entry " + i);
            view.Refresh();

            Assert.AreEqual(4, view.VisibleSlotCount);
            Assert.AreEqual("Entry 2", view.TextOf(0));
            Assert.AreEqual("Entry 5", view.TextOf(3));
        }

        [Test]
        public void RebuildsOnlyWhenLogVersionMoves()
        {
            var view = BuildView(out var host);
            host.World.SettlementLog.Add(host.World.Tick, "First");
            view.Refresh();
            int rebuilds = view.RebuildCount;

            for (int i = 0; i < 10; i++) view.Refresh();
            Assert.AreEqual(rebuilds, view.RebuildCount, "No rebuild while the Version is unchanged.");

            _clock += 1f;
            view.Refresh();
            Assert.AreEqual(rebuilds, view.RebuildCount, "Fading alone does not rebuild.");

            host.World.SettlementLog.Add(host.World.Tick, "Second");
            view.Refresh();
            Assert.AreEqual(rebuilds + 1, view.RebuildCount, "One rebuild when the Version moves.");
        }

        [Test]
        public void FocusRulesAppliedToPanel()
        {
            BuildView(out _);
            var root = _uiGo.GetComponent<UIDocument>().rootVisualElement;
            Assert.IsFalse(root.Q<VisualElement>(MessageLogView.RootName).focusable);
            Assert.IsFalse(root.Q<Label>(MessageLogView.EntryName(0)).focusable);
        }

        private static PanelSettings LoadPanel()
        {
#if UNITY_EDITOR
            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/UI/PanelSettings.asset");
            Assert.IsNotNull(p, "Shared PanelSettings missing.");
            return p;
#else
            return TestPanelSettings.Create();
#endif
        }
    }
}
