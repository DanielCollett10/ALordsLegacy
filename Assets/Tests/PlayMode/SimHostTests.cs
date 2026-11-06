using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    public class SimHostTests
    {
        private GameObject _go;
        private SimHost _host;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SimHost");
            _host = _go.AddComponent<SimHost>();
            _host.AutoUpdate = false;
            _host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void TickAdvancesWhileRunningAndNotWhilePaused()
        {
            _host.Tick(1f);
            long t = _host.World.Tick;
            Assert.Greater(t, 0);
            _host.TogglePause();
            _host.Tick(1f);
            Assert.AreEqual(t, _host.World.Tick);
            _host.TogglePause();
            _host.Tick(1f);
            Assert.Greater(_host.World.Tick, t);
        }

        [Test]
        public void HugeFrameAt32xRunsExactlyMaxTicksPerFrame()
        {
            _host.Pacer.SetSpeed(_host.Pacer.Speeds.Count - 1);
            _host.Tick(10f);
            Assert.AreEqual(_host.World.Tuning.MaxTicksPerFrame, _host.World.Tick);
            Assert.IsTrue(_host.Pacer.SlowedDown);
        }

        [Test]
        public void TreesMatchSyntheticMap()
        {
            Assert.AreEqual(SyntheticMap.CreateRamp().Trees.Count, _host.World.Trees.Count);
        }

        [Test]
        public void MissingMapFileLogsError()
        {
            string path = Path.Combine(Application.temporaryCachePath, "nope.lsmap");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("nope\\.lsmap.*map-2"));
            Assert.IsNull(SimHost.TryLoadMap(path));
        }
    }
}
