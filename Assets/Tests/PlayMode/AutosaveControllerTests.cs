using System.IO;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the autosave slot and timer (T-373, saving.md SV-11/12). Time is fed by hand, not by frames.</summary>
    public class AutosaveControllerTests
    {
        private GameObject _hostGo, _autoGo;
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
            _auto.SetIntervalMinutes(0.0005f); // 0.03 real seconds
        }

        [TearDown]
        public void TearDown()
        {
            GameMenuState.IsOpen = false;
            Object.DestroyImmediate(_autoGo);
            Object.DestroyImmediate(_hostGo);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void TwoAutosavesLeaveExactlyOneAutosaveFile()
        {
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
            GameMenuState.IsOpen = true;
            _auto.Advance(5f);
            Assert.AreEqual(0, _clock);
            Assert.AreEqual(0, _auto.Timer.ElapsedSeconds, 1e-9);
            GameMenuState.IsOpen = false;
        }

        [Test]
        public void SimPausedStillCounts()
        {
            _host.World.Pause();
            _auto.Advance(0.05f);
            Assert.AreEqual(1, _clock, "Paused game time counts (SV-11).");
        }

        [Test]
        public void ManualSaveRestartsTheCount()
        {
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
