using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Core
{
    /// <summary>The settlement message log: last 20 entries, in order, Version bumps on Add (T-498, decision 0045).</summary>
    public class SettlementLogTests
    {
        [Test]
        public void NewLog_IsEmpty_AndVersionIsZero()
        {
            var log = new SettlementLog();
            Assert.AreEqual(0, log.Entries.Count);
            Assert.AreEqual(0, log.Version);
        }

        [Test]
        public void SimWorld_StartsWithAnEmptySettlementLog()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsNotNull(w.SettlementLog);
            Assert.AreEqual(0, w.SettlementLog.Entries.Count);
        }

        [Test]
        public void Add_ThreeEntries_KeepsThemInOrderWithTheirTicks()
        {
            var log = new SettlementLog();
            log.Add(100, "first");
            log.Add(200, "second");
            log.Add(300, "third");
            Assert.AreEqual(new[] { "first", "second", "third" }, log.Entries.Select(e => e.Text).ToArray());
            Assert.AreEqual(new long[] { 100, 200, 300 }, log.Entries.Select(e => e.Tick).ToArray());
        }

        [Test]
        public void Add_TwentyOne_KeepsTheLastTwentyInOrder_AndDropsTheOldest()
        {
            var log = new SettlementLog();
            for (int i = 1; i <= 21; i++) log.Add(i, "e" + i);
            Assert.AreEqual(SettlementLog.Capacity, log.Entries.Count);
            Assert.AreEqual(20, SettlementLog.Capacity);
            Assert.AreEqual("e2", log.Entries.First().Text, "the first entry was dropped");
            Assert.AreEqual("e21", log.Entries.Last().Text);
            for (int i = 0; i < 20; i++) Assert.AreEqual(i + 2, log.Entries[i].Tick);
        }

        [Test]
        public void Add_BumpsVersionOnEveryAdd_IncludingWhenFull()
        {
            var log = new SettlementLog();
            for (int i = 1; i <= 25; i++)
            {
                int before = log.Version;
                log.Add(i, "x");
                Assert.AreEqual(before + 1, log.Version, "after add " + i);
            }
        }

        [Test]
        public void Add_NullText_IsStoredAsEmptyText()
        {
            var log = new SettlementLog();
            log.Add(5, null);
            Assert.AreEqual(string.Empty, log.Entries.Single().Text);
        }

        [Test]
        public void Restore_ReplacesEntries_AndKeepsOnlyTheLastTwenty()
        {
            var log = new SettlementLog();
            log.Add(1, "old");
            var saved = Enumerable.Range(1, 25).Select(i => new SettlementEntry(i, "n" + i)).ToArray();
            log.Restore(saved);
            Assert.AreEqual(20, log.Entries.Count);
            Assert.AreEqual("n6", log.Entries.First().Text);
            Assert.AreEqual("n25", log.Entries.Last().Text);
        }
    }
}
