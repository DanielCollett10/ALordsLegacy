using System;
using System.Linq;
using System.Reflection;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Save.Sections;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class CoreSectionTests
    {
        private static byte[] WriteBytes(CoreState s)
        {
            var w = new SaveWriter();
            CoreSection.Write(w, s);
            return w.ToArray();
        }

        private static CoreState ReadBack(byte[] bytes)
        {
            var r = new SaveReader(bytes);
            Assert.IsTrue(r.NextSection(out int id, out var body));
            Assert.AreEqual(CoreSection.Id, id);
            var s = CoreSection.Read(body);
            Assert.AreEqual(0, body.Remaining, "section body fully consumed");
            return s;
        }

        private static CoreState Sample() => new CoreState
        {
            Tick = 123456789012L,
            Paused = true,
            RandomState = 0xFEDCBA9876543210UL,
            Tuning = new SimTuning { WorldSeed = 77, StartMonth = 9, RestDayEnabled = true, Speeds = new[] { 1, 3 } },
            RoadTypes = RoadTypes.DefaultCatalog,
            AreaTypes = LordsLegacy.Sim.Areas.AreaTypes.DefaultCatalog
        };

        [Test]
        public void RoundTrip_TickPausedRandom_AreEqual()
        {
            var back = ReadBack(WriteBytes(Sample()));
            Assert.AreEqual(123456789012L, back.Tick);
            Assert.IsTrue(back.Paused);
            Assert.AreEqual(0xFEDCBA9876543210UL, back.RandomState);
        }

        [Test]
        public void RoundTrip_NotPaused_StaysNotPaused()
        {
            var s = Sample(); s.Paused = false;
            Assert.IsFalse(ReadBack(WriteBytes(s)).Paused);
        }

        [Test]
        public void RoundTrip_Catalogs_AreEqual()
        {
            var back = ReadBack(WriteBytes(Sample()));
            Assert.AreEqual(RoadTypes.DefaultCatalog.All.Count, back.RoadTypes.All.Count);
            for (int i = 0; i < back.RoadTypes.All.Count; i++)
            {
                var a = RoadTypes.DefaultCatalog.All[i]; var b = back.RoadTypes.All[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.WidthM, b.WidthM);
                Assert.AreEqual(a.MaxGrade, b.MaxGrade);
                Assert.AreEqual(a.SpeedMultiplier, b.SpeedMultiplier);
                Assert.AreEqual(a.VisualId, b.VisualId);
            }
            var areas = LordsLegacy.Sim.Areas.AreaTypes.DefaultCatalog;
            Assert.AreEqual(areas.All.Count, back.AreaTypes.All.Count);
            for (int i = 0; i < areas.All.Count; i++)
            {
                var a = areas.All[i]; var b = back.AreaTypes.All[i];
                foreach (var p in typeof(AreaTypeDef).GetProperties())
                    Assert.AreEqual(p.GetValue(a), p.GetValue(b), p.Name + " of " + a.Id);
            }
        }

        [Test]
        public void RoundTrip_AreaTypeWithoutSlopeLimit_StaysNull()
        {
            var s = Sample();
            s.AreaTypes = new AreaTypeCatalog(new[]
            {
                new AreaTypeDef("a.x", "X", AreaKind.Work, 3, 8, 1, 2, null, true, false, 4.5),
                new AreaTypeDef("a.y", "Y", AreaKind.Building, 4, 6, 1, 2, 12.5, false, true)
            });
            var back = ReadBack(WriteBytes(s));
            Assert.IsNull(back.AreaTypes.Find("a.x").MaxSlopeDegrees);
            Assert.AreEqual(12.5, back.AreaTypes.Find("a.y").MaxSlopeDegrees);
            Assert.AreEqual(4.5, back.AreaTypes.Find("a.x").NearRoadReachM);
        }

        [Test]
        public void RoundTrip_EmptyCatalogs_AreEmpty()
        {
            var s = Sample();
            s.RoadTypes = new RoadTypeCatalog(null);
            s.AreaTypes = new AreaTypeCatalog(null);
            var back = ReadBack(WriteBytes(s));
            Assert.AreEqual(0, back.RoadTypes.All.Count);
            Assert.AreEqual(0, back.AreaTypes.All.Count);
        }

        [Test]
        public void WriteReadWrite_IsByteIdentical()
        {
            var bytes = WriteBytes(Sample());
            Assert.AreEqual(bytes, WriteBytes(ReadBack(bytes)));
        }

        [Test]
        public void Tuning_FromSaveWinsOverTuningPassedIn()
        {
            var back = ReadBack(WriteBytes(Sample()));
            var passedIn = new SimTuning { WorldSeed = 1, StartMonth = 1 };
            var chosen = CoreSection.ChooseTuning(back, passedIn);
            Assert.AreEqual(77, chosen.WorldSeed);
            Assert.AreEqual(9, chosen.StartMonth);
            Assert.AreEqual(77, CoreSection.ChooseTuning(back, null).WorldSeed);
        }

        [Test]
        public void Truncated_ThrowsSaveFormatException()
        {
            var bytes = WriteBytes(Sample());
            var cut = bytes.Skip(8).Take(10).ToArray(); // skip section id + length, keep part of the body
            Assert.Throws<SaveFormatException>(() => CoreSection.Read(new SaveReader(cut)));
        }

        /// <summary>Gives every settable SimTuning property a value different from its default, so a field the
        /// section forgets comes back as the default and fails the comparison.</summary>
        private static SimTuning AllNonDefault()
        {
            var t = new SimTuning();
            var def = new SimTuning();
            foreach (var p in typeof(SimTuning).GetProperties().Where(p => p.CanWrite))
            {
                object v = p.GetValue(def);
                if (v is double d) v = d + 1.25;
                else if (v is int i) v = i + 3;
                else if (v is bool b) v = !b;
                else if (v is int[] arr) v = arr.Select(x => x + 1).Concat(new[] { 99 }).ToArray();
                else if (v is Weekday) v = Weekday.Friday;
                else Assert.Fail("Test cannot vary SimTuning." + p.Name + " of type " + p.PropertyType.Name
                    + ": teach AllNonDefault() and update CoreSection.");
                p.SetValue(t, v);
            }
            return t;
        }

        [Test]
        public void EverySimTuningProperty_RoundTrips()
        {
            var s = Sample();
            s.Tuning = AllNonDefault();
            var back = ReadBack(WriteBytes(s)).Tuning;
            foreach (var p in typeof(SimTuning).GetProperties().Where(p => p.CanWrite))
            {
                object a = p.GetValue(s.Tuning), b = p.GetValue(back);
                if (a is int[] ia) CollectionAssert.AreEqual(ia, (int[])b, p.Name);
                else Assert.AreEqual(a, b, "SimTuning." + p.Name + " is not saved by CoreSection");
            }
        }

        [Test]
        public void SimTuning_WritablePropertyCount_MatchesCoreSection()
        {
            // CoreSection writes one entry per writable SimTuning property. Bump this number only together with
            // CoreSection's tuning writer/reader; EverySimTuningProperty_RoundTrips shows which one is missing.
            int writable = typeof(SimTuning).GetProperties().Count(p => p.CanWrite);
            Assert.AreEqual(46, writable, "SimTuning changed: update CoreSection and this count");
        }
    }
}
