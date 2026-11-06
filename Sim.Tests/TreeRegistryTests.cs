using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class TreeRegistryTests
    {
        [Test]
        public void AddAndGet()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(7), 1.5f, -2f, 12);
            var t = r.Get(new TreeId(7));
            Assert.AreEqual(1.5f, t.X);
            Assert.AreEqual(-2f, t.Z);
            Assert.AreEqual(12, t.TrunkLengthM);
            Assert.AreEqual(TreeState.Standing, t.State);
            Assert.AreEqual(1, r.Count);
            Assert.IsTrue(r.TryGet(new TreeId(7), out _));
            Assert.IsFalse(r.TryGet(new TreeId(8), out _));
            Assert.Throws<KeyNotFoundException>(() => r.Get(new TreeId(8)));
        }

        [Test]
        public void DuplicateIdThrows()
        {
            var r = new TreeRegistry();
            r.Add(new TreeId(1), 0, 0, 5);
            Assert.Throws<ArgumentException>(() => r.Add(new TreeId(1), 9, 9, 5));
            Assert.AreEqual(1, r.Count);
        }

        [Test]
        public void EnumeratesInIdOrder()
        {
            var r = new TreeRegistry();
            foreach (var i in new[] { 5, 1, 9, 3 }) r.Add(new TreeId(i), i, i, 4);
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 9 }, r.All.Select(t => t.Id.Value).ToArray());
        }

        [Test]
        public void LogYieldFloors()
        {
            var tuning = new SimTuning();
            var r = new TreeRegistry();
            Assert.AreEqual(3, r.Add(new TreeId(1), 0, 0, 12).LogYield(tuning));
            Assert.AreEqual(4, r.Add(new TreeId(2), 0, 0, 19.9).LogYield(tuning));
            Assert.AreEqual(0, r.Add(new TreeId(3), 0, 0, 3.9).LogYield(tuning));
        }

        [Test]
        public void RectQueryInclusiveAndSorted()
        {
            var r = new TreeRegistry(10f);
            r.Add(new TreeId(3), 5, 5, 4);
            r.Add(new TreeId(1), 25, 5, 4);
            r.Add(new TreeId(2), 10, 10, 4);
            r.Add(new TreeId(4), -5, -5, 4);
            r.Add(new TreeId(5), 50, 50, 4);
            var ids = r.QueryRect(new Bounds2(-5, -5, 25, 10)).Select(t => t.Id.Value).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, ids);
            Assert.AreEqual(0, r.QueryRect(Bounds2.Empty).Count);
        }

        [Test]
        public void RadiusQuery()
        {
            var r = new TreeRegistry(10f);
            r.Add(new TreeId(1), 0, 0, 4);
            r.Add(new TreeId(2), 5, 0, 4);
            r.Add(new TreeId(3), 4, 4, 4);
            r.Add(new TreeId(4), -30, 0, 4);
            var ids = r.QueryRadius(0, 0, 5f).Select(t => t.Id.Value).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2 }, ids);
        }
    }
}
