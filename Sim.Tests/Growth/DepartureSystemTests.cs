using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Growth;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Growth
{
    /// <summary>DepartureSystem: begin at 30 hungry days, walk to the nearest exit, vanish (T-502, GR-03, GR-11, GR-13).</summary>
    public class DepartureSystemTests
    {
        private const long Adult = -4_000_000;

        private static SimWorld World(out Family f)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            f = w.People.AddFamily(1, "Ash", 100, 100);
            w.People.AddCitizen(10, "A", Sex.Male, Adult, 1, 100, 100);
            w.People.AddCitizen(11, "B", Sex.Female, Adult, 1, 102, 100);
            return w;
        }

        private static void StepToMidnight(SimWorld w)
        {
            do w.Step(); while (!(w.Now.Hour == 0 && w.Now.Minute == 0));
        }

        [Test]
        public void EmptyLarder_LeavesOnDay30Not29_WalksToExitAndIsGone_IdsNotReused()
        {
            SimWorld w = World(out Family f);
            int camp = Larders.ForFamily(w, f).Id;
            for (int day = 1; day <= 29; day++) StepToMidnight(w);
            Assert.AreEqual(29, f.HungryDays);
            Assert.AreEqual(FamilyPassage.None, f.Passage, "29 days: stays");
            int version = w.People.Version;
            StepToMidnight(w);
            Assert.AreEqual(FamilyPassage.Leaving, f.Passage, "30th hungry midnight: leaves on the same tick");
            foreach (int id in f.MemberIds.ToList())
            {
                MoveOrder o = w.People.FindCitizen(id).Move;
                Assert.IsNotNull(o, "order at midnight, in the dark");
                Assert.IsTrue(MapExits.IsOnEdge(w, PointMm.FromMetres(o.TargetX, o.TargetZ)));
            }
            for (int i = 0; i < 3000 && w.People.FamilyCount > 0; i++) w.Step();
            Assert.AreEqual(0, w.People.CitizenCount);
            Assert.AreEqual(0, w.People.FamilyCount);
            Assert.IsNull(w.Inventory.Get(camp), "empty camp larder goes with the family");
            Assert.Greater(w.People.Version, version);
            Assert.AreEqual(2, w.People.NextFamilyId);
            Assert.AreEqual(12, w.People.NextCitizenId);
        }

        [Test]
        public void FedOnDay29_ResetsAndStays()
        {
            SimWorld w = World(out Family f);
            for (int day = 1; day <= 28; day++) StepToMidnight(w);
            Assert.AreEqual(28, f.HungryDays);
            w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 10);
            StepToMidnight(w);
            Assert.AreEqual(0, f.HungryDays);
            StepToMidnight(w);
            Assert.AreEqual(FamilyPassage.None, f.Passage);
            for (int i = 0; i < 200; i++) w.Step();
            Assert.AreEqual(1, w.People.FamilyCount);
        }

        [Test]
        public void SaveMidWalk_LoadKeepsTheSameExitTargets()
        {
            SimWorld w = World(out Family f);
            for (int day = 1; day <= 30; day++) StepToMidnight(w);
            for (int i = 0; i < 20; i++) w.Step();
            Assert.AreEqual(2, w.People.CitizenCount, "still walking");
            MapData map = SyntheticMap.CreateRamp();
            SimWorld back = SimSave.Read(SimSave.Write(w, "n", 0, map.MapId, map.ContentHash), map);
            foreach (Citizen c in w.People.Citizens)
            {
                MoveOrder a = c.Move, b = back.People.FindCitizen(c.Id).Move;
                Assert.IsNotNull(b);
                Assert.AreEqual(a.TargetX, b.TargetX);
                Assert.AreEqual(a.TargetZ, b.TargetZ);
            }
            Assert.AreEqual(FamilyPassage.Leaving, back.People.FindFamily(1).Passage);
            for (int i = 0; i < 3000 && back.People.FamilyCount > 0; i++) back.Step();
            Assert.AreEqual(0, back.People.FamilyCount);
        }

        [Test]
        public void Leaving_ByDay_AlsoGetsAnOrder_AndFailedPathRetriesOnTheHour()
        {
            SimWorld w = World(out Family f);
            FamilyDeparture.Begin(w, 1);
            w.Step();
            Citizen c = w.People.FindCitizen(10);
            Assert.IsNotNull(c.Move);
            var failed = new MoveOrder(c.Move.TargetX, c.Move.TargetZ) { Status = MoveStatus.Failed };
            c.Move = failed;
            while (w.Tick % 60 != 59) w.Step();
            Assert.AreSame(failed, c.Move, "not retried before the hour");
            w.Step();
            w.Step();
            Assert.AreNotSame(failed, c.Move, "retried on the hour");
        }
    }
}
