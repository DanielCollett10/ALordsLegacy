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

        private static void WriteOlderVersionFile(string folder, string stem)
        {
            Directory.CreateDirectory(folder);
            var bytes = new System.Collections.Generic.List<byte> { (byte)'L', (byte)'L', (byte)'S', (byte)'V' };
            bytes.AddRange(System.BitConverter.GetBytes(SaveWriter.CurrentVersion - 1));
            bytes.AddRange(new byte[16]);
            File.WriteAllBytes(Path.Combine(folder, stem + SaveStore.Extension), bytes.ToArray());
        }

        [UnityTest]
        public IEnumerator OlderVersionSaveIsGreyedWithReasonLoadDisabledAndBinDeletes()
        {
            yield return LoadGame();
            var host = Object.FindFirstObjectByType<SimHost>();
            var panel = Object.FindFirstObjectByType<LoadPanelController>();
            var store = new SaveStore(_folder);
            panel.Store = store;
            string reloaded = null;
            panel.SceneLoader = n => reloaded = n;
            host.World.Pause();
            WriteOlderVersionFile(_folder, "Old one");

            var menu = panel.GetComponent<GameMenuView>();
            menu.Open();
            menu.ClickLoad();
            Assert.AreEqual(1, panel.Rows.Count, "An unreadable save is still listed.");
            Assert.IsFalse(panel.Rows[0].enabledSelf, "Load is disabled on it.");
            StringAssert.Contains(SaveStore.OlderVersionReason, panel.Rows[0].text);
            Assert.IsFalse(panel.TryLoad(store.ListAll()[0]));
            Assert.IsNull(reloaded);
            Assert.IsFalse(PendingLoad.HasPending);

            Assert.IsTrue(panel.DeleteButtons[0].enabledSelf, "The bin works.");
            panel.AskDelete(store.ListAll()[0]);
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Confirm();
            Assert.AreEqual(0, panel.Rows.Count);
            Assert.AreEqual(0, Directory.GetFiles(_folder).Length);
            menu.Close();
        }

        [UnityTest]
        public IEnumerator BinConfirmDeletesAndCancelKeepsIncludingAutosave()
        {
            yield return LoadGame();
            var host = Object.FindFirstObjectByType<SimHost>();
            var panel = Object.FindFirstObjectByType<LoadPanelController>();
            var store = new SaveStore(_folder);
            panel.Store = store;
            host.World.Pause();
            byte[] bytes = SimSave.Write(host.World, "Mine", 100, host.Map.MapId, host.Map.ContentHash);
            byte[] auto = SimSave.Write(host.World, "Autosave", 200, host.Map.MapId, host.Map.ContentHash);
            store.WriteNew("Mine", bytes);
            store.WriteAutosave(auto);

            var menu = panel.GetComponent<GameMenuView>();
            menu.Open();
            menu.ClickLoad();
            Assert.AreEqual(2, panel.DeleteButtons.Count, "A bin on every row, Autosave included.");

            panel.AskDelete(store.ListAll()[0]);
            Assert.IsTrue(ConfirmDialog.IsOpen);
            ConfirmDialog.Cancel();
            Assert.AreEqual(2, store.ListAll().Count, "Cancel changes nothing.");

            var autoEntry = store.ListAll()[0];
            Assert.AreEqual("Autosave", autoEntry.Name);
            panel.AskDelete(autoEntry);
            ConfirmDialog.Confirm();
            Assert.AreEqual(1, panel.Rows.Count);
            Assert.IsFalse(File.Exists(autoEntry.Path));
            store.WriteAutosave(auto);
            Assert.AreEqual(2, store.ListAll().Count, "The next autosave recreates the slot.");
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
