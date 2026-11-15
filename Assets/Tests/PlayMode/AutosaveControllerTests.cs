using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the autosave slot and timer (T-373, saving.md SV-11/12). Time is fed by hand through
    /// <c>Advance</c>, not by frames. SetUp keeps the default 30-minute interval so that real frames run by the test
    /// runner between SetUp and the test body cannot fire an autosave. Each test that counts saves calls
    /// <see cref="UseShortInterval"/> first, and test bodies are synchronous, so no frame runs while they count.
    /// </summary>
    public class AutosaveControllerTests
    {
        private GameObject _hostGo, _autoGo, _menuGo;
        private GameMenuView _menu;
        private SimHost _host;
        private AutosaveController _auto;
        private string _dir;
        private long _clock;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ll-autosave-" + System.Guid.NewGuid().ToString("N"));
            _hostGo = new GameObject("Host");
            _host = _hostGo.AddComponent<SimHost>();
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            _host.AutoUpdate = false;

            _autoGo = new GameObject("Autosave");
            _auto = _autoGo.AddComponent<AutosaveController>();
            _auto.SetHost(_host);
            _auto.Store = new SaveStore(_dir);
            _auto.Clock = () => ++_clock;

            _menuGo = new GameObject("Menu");
            _menu = _menuGo.AddComponent<GameMenuView>();
            _menu.SetHost(_host);
        }

        [TearDown]
        public void TearDown()
        {
            if (_menu != null) _menu.Close();
            Object.DestroyImmediate(_menuGo);
            Object.DestroyImmediate(_autoGo);
            Object.DestroyImmediate(_hostGo);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>Switches to a 0.03 s interval, restarts the count and zeroes the save counter. Call first in a test body.</summary>
        private void UseShortInterval()
        {
            _clock = 0;
            _auto.SetIntervalMinutes(0.0005f); // 0.03 real seconds
        }

        [Test]
        public void TwoAutosavesLeaveExactlyOneAutosaveFile()
        {
            UseShortInterval();
            _auto.Advance(0.02f);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, SaveStore.AutosaveStem + SaveStore.Extension)), "No save before the interval.");
            _auto.Advance(0.02f);
            _auto.Advance(0.05f);
            Assert.AreEqual(2, _clock, "Two autosaves were written.");

            var autosaves = Directory.GetFiles(_dir, SaveStore.AutosaveStem + "*" + SaveStore.Extension);
            Assert.AreEqual(1, autosaves.Length);
            Assert.AreEqual(2, SaveReader.ReadHeaderOnly(File.ReadAllBytes(autosaves[0])).CreatedRealTime, "Its date is the last write.");
        }

        [Test]
        public void ManualSavesAreNeverOverwrittenByAutosave()
        {
            UseShortInterval();
            string manual = _auto.Store.WriteNew("Camp", SimSave.Write(_host.World, "Camp", 1, _host.Map.MapId, _host.Map.ContentHash));
            _auto.Advance(0.05f);
            _auto.Advance(0.05f);
            Assert.IsTrue(File.Exists(manual));
            Assert.AreEqual(1, SaveReader.ReadHeaderOnly(File.ReadAllBytes(manual)).CreatedRealTime, "The manual save keeps its bytes.");
            Assert.AreEqual(2, _auto.Store.List().Count, "One manual save and one autosave.");
        }

        [Test]
        public void MenuOpenStopsTheCount()
        {
            UseShortInterval();
            _menu.Open();
            Assert.IsTrue(GameMenuState.IsOpen);
            _auto.Advance(5f);
            Assert.AreEqual(0, _clock);
            Assert.AreEqual(0, _auto.Timer.ElapsedSeconds, 1e-9);
            _menu.Close();
            _auto.Advance(0.05f);
            Assert.AreEqual(1, _clock, "The count runs again once the menu is closed.");
        }

        [Test]
        public void SimPausedStillCounts()
        {
            UseShortInterval();
            _host.World.Pause();
            _auto.Advance(0.05f);
            Assert.AreEqual(1, _clock, "Paused game time counts (SV-11).");
        }

        [Test]
        public void ManualSaveRestartsTheCount()
        {
            UseShortInterval();
            _auto.Advance(0.02f);
            _auto.OnManualSaved();
            _auto.Advance(0.02f);
            Assert.AreEqual(0, _clock, "The count restarted, so no autosave yet.");
        }

        [Test]
        public void DefaultIntervalIsThirtyMinutes()
        {
            var go = new GameObject("Default");
            var c = go.AddComponent<AutosaveController>();
            Assert.AreEqual(30f, c.IntervalMinutes);
            Assert.AreEqual(30 * 60.0, c.Timer.IntervalSeconds, 1e-9);
            Object.DestroyImmediate(go);
        }
    }
}
