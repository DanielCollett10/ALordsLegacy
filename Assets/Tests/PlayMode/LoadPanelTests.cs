using System.Collections;
using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the Load panel and load by scene reload (T-372, saving.md SV-20..22, SV-30).</summary>
    public class LoadPanelTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            PendingLoad.Clear();
            _folder = Path.Combine(Path.GetTempPath(), "ll-load-tests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PendingLoad.Clear();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static IEnumerator LoadGame()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null; // SimHost.Start
        }

        [UnityTest]
        public IEnumerator MismatchLeavesPendingLoadEmptyAndDoesNotReload()
        {
            yield return LoadGame();
            var host = Object.FindFirstObjectByType<SimHost>();
            var panel = Object.FindFirstObjectByType<LoadPanelController>();
            Assert.IsNotNull(panel, "No LoadPanelController in the Game scene.");
            var store = new SaveStore(_folder);
            panel.Store = store;
            string reloaded = null;
            panel.SceneLoader = n => reloaded = n;

            host.World.Pause();
            byte[] other = SimSave.Write(host.World, "Other map", 100, "some-other-map", host.Map.ContentHash);
            byte[] wrongHash = SimSave.Write(host.World, "Wrong hash", 200, host.Map.MapId, host.Map.ContentHash + 1);
            store.WriteNew("Other map", other);
            store.WriteNew("Wrong hash", wrongHash);

            var menu = panel.GetComponent<GameMenuView>();
            menu.Open();
            menu.ClickLoad();
            Assert.AreEqual(2, panel.Rows.Count);
            Assert.That(panel.Rows[0].text, Does.StartWith("Wrong hash"), "Newest first.");

            foreach (var entry in store.List())
            {
                Assert.IsFalse(panel.TryLoad(entry));
                Assert.AreEqual(LoadPanelController.MismatchMessage, panel.NoteLabel.text);
                Assert.IsFalse(PendingLoad.HasPending, "A mismatch must set nothing.");
                Assert.IsNull(reloaded, "A mismatch must not reload the scene.");
            }
            menu.Close();
        }

        [UnityTest]
        public IEnumerator LoadRestoresTickThenNextReloadStartsNewGame()
        {
            yield return LoadGame();
            var host = Object.FindFirstObjectByType<SimHost>();
            host.World.Step(50);
            host.World.Pause();
            long tick = host.World.Tick;
            Assert.GreaterOrEqual(tick, 50);
            byte[] bytes = SimSave.Write(host.World, "Mine", 1000, host.Map.MapId, host.Map.ContentHash);
            Assert.IsNotNull(host.World.Camp, "The start layout gives a new game a camp.");

            var panel = Object.FindFirstObjectByType<LoadPanelController>();
            var store = new SaveStore(_folder);
            store.WriteNew("Mine", bytes);
            panel.Store = store;
            Assert.IsTrue(panel.TryLoad(store.List()[0]), "Matching map: the scene reload starts.");
            yield return null;
            yield return null;

            var host2 = Object.FindFirstObjectByType<SimHost>();
            Assert.IsTrue(host2.IsInitialized);
            Assert.IsFalse(PendingLoad.HasPending, "SimHost clears PendingLoad at once.");
            Assert.AreEqual(tick, host2.World.Tick, "The loaded game continues at the saved tick.");
            Assert.AreEqual(new TickPacer(host2.World.Tuning).CurrentSpeedIndex, host2.Pacer.CurrentSpeedIndex,
                "Speed starts at its default after a load.");

            yield return LoadGame();
            var host3 = Object.FindFirstObjectByType<SimHost>();
            Assert.AreEqual(0, host3.World.Tick, "With nothing pending a reload starts a new game.");
            Assert.IsNotNull(host3.World.Camp, "A new game uses the start layout.");
        }
    }
}
