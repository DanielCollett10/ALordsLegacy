using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Save.Sections;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class BushesSectionTests
    {
        private const int Day = 1440;

        private static (SimWorld world, MapData map) Make(SimTuning t = null)
        {
            var map = SyntheticMap.CreateRamp();
            return (SimWorld.FromMapData(map, t ?? new SimTuning()), map);
        }

        private static byte[] Save(SimWorld w, MapData m) => SimSave.Write(w, "s", 1, m.MapId, m.ContentHash);

        [Test]
        public void Scatter_SameSeed_SamePositions_DifferentSeed_Differs()
        {
            var (a, _) = Make();
            var (b, _) = Make();
            var (c, _) = Make(new SimTuning { WorldSeed = new SimTuning().WorldSeed + 1 });
            Assert.Greater(a.Bushes.Count, 0);
            Assert.AreEqual(a.Bushes.All.Select(x => (x.X, x.Z, x.Kind)), b.Bushes.All.Select(x => (x.X, x.Z, x.Kind)));
            Assert.AreNotEqual(a.Bushes.All.Select(x => x.X), c.Bushes.All.Select(x => x.X));
        }

        [Test]
        public void Scatter_DoesNotTouchWorldRandom_AndNeverPlacesNearTrunk()
        {
            var (w, map) = Make();
            Assert.AreEqual(new SimWorld(new SimTuning()).Random.State, w.Random.State);
            foreach (var b in w.Bushes.All)
                Assert.AreEqual(0, w.Trees.QueryRadius(b.X, b.Z, (float)w.Tuning.BushTreeClearM).Count, "bush " + b.Id);
        }

        [Test]
        public void Scatter_StartsFullWithTunedBaskets()
        {
            var (w, _) = Make(new SimTuning { BushBaskets = 5 });
            Assert.IsTrue(w.Bushes.All.All(b => b.Baskets == 5));
        }

        [Test]
        public void Scatter_BushCountZero_NoBushes()
        {
            var (w, _) = Make(new SimTuning { BushCount = 0 });
            Assert.AreEqual(0, w.Bushes.Count);
        }

        [Test]
        public void World_WithoutMap_HasNoBushes()
        {
            Assert.AreEqual(0, new SimWorld(new SimTuning()).Bushes.Count);
        }

        [Test]
        public void Defaults_ThreeBaskets_FourteenDays()
        {
            var t = new SimTuning();
            Assert.AreEqual(3, t.BushBaskets);
            Assert.AreEqual(14, t.BushRegrowDays);
        }

        [Test]
        public void Reserve_OneOwnerAtATime_ReleaseFrees_EmptyCannotBeReserved()
        {
            var r = new BushRegistry();
            var b = r.Add(1, 1, BushKind.Berries, 3);
            Assert.IsTrue(r.Reserve(b.Id, 7));
            Assert.IsTrue(r.Reserve(b.Id, 7), "same owner again");
            Assert.IsFalse(r.Reserve(b.Id, 8));
            Assert.IsNull(r.NearestAvailable(1, 1));
            Assert.IsTrue(r.Release(b.Id));
            Assert.IsTrue(r.Reserve(b.Id, 8));
            Assert.IsFalse(r.Reserve(999, 1), "unknown id");
            var e = r.Add(2, 2, BushKind.Mushrooms, 0);
            Assert.IsFalse(r.Reserve(e.Id, 1));
        }

        [Test]
        public void TakeBasket_RequiresReservation_LastBasketClearsIt()
        {
            var r = new BushRegistry();
            var b = r.Add(0, 0, BushKind.Berries, 3);
            Assert.IsFalse(r.TakeBasket(b.Id, 1, 10), "not reserved");
            r.Reserve(b.Id, 1);
            Assert.IsFalse(r.TakeBasket(b.Id, 2, 10), "wrong owner");
            Assert.IsTrue(r.TakeBasket(b.Id, 1, 10));
            Assert.IsTrue(r.TakeBasket(b.Id, 1, 11));
            Assert.IsTrue(r.TakeBasket(b.Id, 1, 12));
            Assert.AreEqual(0, b.Baskets);
            Assert.AreEqual(12, b.EmptiedAtTick);
            Assert.IsNull(b.ReservedBy);
            Assert.IsFalse(r.TakeBasket(b.Id, 1, 13));
        }

        [Test]
        public void Regrow_FullAfterFourteenDaysNotBefore_ThroughSimTicks()
        {
            var (w, _) = Make(new SimTuning { BushCount = 1 });
            BushRecord b = w.Bushes.All.First();
            w.Bushes.Reserve(b.Id, 1);
            for (int i = 0; i < 3; i++) Assert.IsTrue(w.Bushes.TakeBasket(b.Id, 1, w.Tick));
            long emptied = w.Tick;
            w.Step(14 * Day - 1);
            Assert.AreEqual(0, b.Baskets, "13 days 23:59 later still empty");
            w.Step(1);
            Assert.AreEqual(3, b.Baskets);
            Assert.AreEqual(emptied + 14 * Day, w.Tick);
        }

        [Test]
        public void Regrow_PartlyPickedBushDoesNotRefill()
        {
            var (w, _) = Make(new SimTuning { BushCount = 1 });
            BushRecord b = w.Bushes.All.First();
            w.Bushes.Reserve(b.Id, 1);
            w.Bushes.TakeBasket(b.Id, 1, w.Tick);
            w.Step(20 * Day);
            Assert.AreEqual(2, b.Baskets);
        }

        [Test]
        public void RoundTrip_BushesStateReservationAndCountersSurvive()
        {
            var (w, map) = Make();
            var list = w.Bushes.All.ToList();
            w.Bushes.Reserve(list[0].Id, 5);
            w.Bushes.Reserve(list[1].Id, 6);
            w.Bushes.TakeBasket(list[1].Id, 6, 0);
            w.Bushes.Reserve(list[2].Id, 6);
            for (int i = 0; i < 3; i++) w.Bushes.TakeBasket(list[2].Id, 6, 100);
            w.Step(30);
            byte[] bytes = Save(w, map);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, Save(back, map), "byte for byte");
            Assert.AreEqual(w.Bushes.NextIdValue, back.Bushes.NextIdValue);
            Assert.AreEqual(
                w.Bushes.All.Select(b => (b.Id, b.X, b.Z, b.Kind, b.Baskets, b.EmptiedAtTick, b.ReservedBy)).ToList(),
                back.Bushes.All.Select(b => (b.Id, b.X, b.Z, b.Kind, b.Baskets, b.EmptiedAtTick, b.ReservedBy)).ToList());
            Assert.AreEqual(5, back.Bushes.All.First().ReservedBy);
            w.Step(Day); back.Step(Day);
            Assert.AreEqual(Save(w, map), Save(back, map), "stepped worlds stay identical");
        }

        [Test]
        public void Load_RestoresSavedBushesNotANewScatter()
        {
            var (w, map) = Make();
            var first = w.Bushes.All.First();
            w.Bushes.Reserve(first.Id, 1);
            for (int i = 0; i < 3; i++) w.Bushes.TakeBasket(first.Id, 1, 0);
            SimWorld back = SimSave.Read(Save(w, map), map);
            Assert.AreEqual(w.Bushes.Count, back.Bushes.Count);
            Assert.AreEqual(0, back.Bushes.All.First().Baskets);
        }

        [Test]
        public void Section_RegisteredRightAfterLand()
        {
            var ids = SaveSections.CreateDefault().Select(s => s.Id).ToList();
            Assert.AreEqual(ids.IndexOf(LandSection.SectionId) + 1, ids.IndexOf(BushesSection.SectionId));
            Assert.AreEqual(10, BushesSection.SectionId);
        }

        [Test]
        public void SaveVersion_IsTwelve_VersionTenBytesAreRefused()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 17);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 10, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 10 (this build reads version " + SaveWriter.CurrentVersion + ").", e.Message);
        }

        [Test]
        public void TuningRoundTrip_BushValuesSurvive()
        {
            var (w, map) = Make(new SimTuning { BushBaskets = 4, BushRegrowDays = 9, BushCount = 7, BushEdgeM = 11, BushTreeClearM = 3, BushClearingChance = 0.5 });
            SimWorld back = SimSave.Read(Save(w, map), map);
            Assert.AreEqual(4, back.Tuning.BushBaskets);
            Assert.AreEqual(9, back.Tuning.BushRegrowDays);
            Assert.AreEqual(7, back.Tuning.BushCount);
            Assert.AreEqual(11, back.Tuning.BushEdgeM);
            Assert.AreEqual(3, back.Tuning.BushTreeClearM);
            Assert.AreEqual(0.5, back.Tuning.BushClearingChance);
        }
    }
}
