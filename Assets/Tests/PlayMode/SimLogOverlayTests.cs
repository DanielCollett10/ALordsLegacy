using LordsLegacy.Game;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>T-630: the F5 Sim log overlay lists entries newest first, with per-category toggles.</summary>
    public class SimLogOverlayTests
    {
        private GameObject _hostGo, _uiGo;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_uiGo);
            Object.DestroyImmediate(_hostGo);
        }

        private SimLogOverlayView Make(out SimHost host)
        {
            _hostGo = new GameObject("SimHost");
            host = _hostGo.AddComponent<SimHost>();
            host.AutoUpdate = false;
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _uiGo = new GameObject("SimLogOverlay");
            _uiGo.SetActive(false);
            _uiGo.AddComponent<UIDocument>().panelSettings = LoadPanel();
            var view = _uiGo.AddComponent<SimLogOverlayView>();
            view.Live = false;
            view.Host = host;
            _uiGo.SetActive(true);
            return view;
        }

        [Test]
        public void FormatsDayAndTime()
        {
            Assert.AreEqual("Day 2 01:05 [Food] eat", SimLogOverlayView.Format(new LogEntry(1440 + 65, LogCategory.Food, "eat")));
        }

        [Test]
        public void ToggleShowsNewestFirstAndRefusedCommandAppears()
        {
            var view = Make(out var host);
            view.SetVisible(true);
            Assert.AreEqual(DisplayStyle.Flex, view.Root.resolvedStyle.display);

            // A refused command: a one-point road is rejected. Its log line is written here so the test does not
            // depend on which Sim systems already log (T-629).
            var r = host.Execute(new DrawRoad(new[] { PointMm.FromMetres(60, 100) }, RoadTypes.DirtPathId));
            Assert.IsTrue(r.IsRejected);
            host.World.Log.Write(LogCategory.Commands, "DrawRoad refused: " + r.Text);
            host.World.Log.Write(LogCategory.Food, "later entry");
            view.Refresh();

            var texts = view.EntryTexts();
            Assert.AreEqual(2, texts.Count);
            StringAssert.IsMatch(@"^Day \d+ \d\d:\d\d \[Food\] later entry$", texts[0]);
            StringAssert.Contains("[Commands] DrawRoad refused", texts[1]);

            // Turning a category off hides its entries and stops writing.
            view.SetCategory(LogCategory.Commands, false);
            Assert.IsFalse(host.World.Log.IsEnabled(LogCategory.Commands));
            Assert.AreEqual(1, view.EntryTexts().Count);
            host.World.Log.Write(LogCategory.Commands, "ignored");
            view.SetCategory(LogCategory.Commands, true);
            Assert.AreEqual(2, view.EntryTexts().Count);

            // One toggle per category, and a click-path value change reaches the Sim log.
            foreach (LogCategory c in System.Enum.GetValues(typeof(LogCategory)))
                Assert.IsNotNull(view.Root.Q<Toggle>(SimLogOverlayView.TogglePrefix + c), c.ToString());
            view.Root.Q<Toggle>(SimLogOverlayView.TogglePrefix + LogCategory.Food).value = false;
            Assert.IsFalse(host.World.Log.IsEnabled(LogCategory.Food));

            view.SetVisible(false);
            Assert.AreEqual(DisplayStyle.None, view.Root.resolvedStyle.display);
        }

        [Test]
        public void OverlayDoesNotBlockWorldClicks()
        {
            var view = Make(out _);
            Assert.AreEqual(PickingMode.Ignore, view.Root.pickingMode);
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
