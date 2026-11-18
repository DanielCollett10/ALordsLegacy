using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    /// <summary>Whole-world save round trips at four scenario points, and the determinism check (T-368, SAV-06).</summary>
    public class SimSaveRoundTripTests
    {
        private const long AdultBirth = -4_000_000;
        private const int Day = 24 * 60;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map()
        {
            var trees = new[]
            {
                new MapTree(new TreeId(1), 100f, 85f, 16), new MapTree(new TreeId(2), 105f, 86f, 16),
                new MapTree(new TreeId(3), 95f, 86f, 16), new MapTree(new TreeId(4), 110f, 84f, 16),
            };
            return new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees);
        }

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private static SimWorld Woodcutting(MapData map, int month, int day, int hour)
        {
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = month, StartDay = day, StartHour = hour, StartMinute = 0, StartWeekday = Weekday.Monday,
                OxLoadMinutes = 2, OxUnloadMinutes = 3,
            });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            w.Footprints.Add(1, ModuleTypes.WoodcuttingHut, M(100, 100), 0);
            w.Operations.AddModule(op.Id, 1);
            w.Footprints.Add(2, ModuleTypes.LogStorage, M(110, 100), 0);
            w.Operations.AddModule(op.Id, 2);
            w.People.AddFamily(1, "F", 100, 100);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(10 + i, "C" + i, Sex.Male, AdultBirth, 1, 100, 100);
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsAccepted);
            Ox ox = w.People.AddOx(50, "O", 100, 104);
            ox.State = OxState.Resting(100000, 104000);
            return w;
        }

        // Dec 21 (sunrise 08:00, sunset 16:00): 8 logs at a far stockpile, hut site being supplied.
        private static SimWorld Building(MapData map, int startMinuteOfDay)
        {
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                StartMonth = 12, StartDay = 21, StartHour = startMinuteOfDay / 60, StartMinute = startMinuteOfDay % 60,
                StartWeekday = Weekday.Monday,
                OxLoadMinutes = 2, OxUnloadMinutes = 3,
            });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Operation op = w.Operations.FindByArea((int)r.Created[0]);
            LedgerLocation stock = w.Inventory.AddStockpile(M(100, 190));
            w.Inventory.Produce(stock, Items.Log, 8);
            w.People.AddFamily(2, "Helpers", 100, 100);
            w.People.AddCitizen(20, "H", Sex.Male, AdultBirth, 2, 100, 100);
            Ox ox = w.People.AddOx(50, "O", 100, 104);
            ox.State = OxState.Resting(100000, 104000);
            CommandResult p = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(110, 100), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            return w;
        }

        private static bool Carrying(SimWorld w)
        {
            Ox ox = w.People.FindOx(50);
            return ox != null && w.OxCarrier(ox).CountOf(Items.Log.Id) > 0;
        }

        private static bool HasSitePile(SimWorld w) => w.Inventory.Locations.Any(l =>
            l.Kind == LocationKind.GroundPile && l.OwnerOperationId.HasValue && l.CountOf(Items.Log.Id) > 0);

        private static void StepUntil(SimWorld w, System.Func<SimWorld, bool> done, int max)
        {
            for (int i = 0; i < max && !done(w); i++) w.Step(1);
            Assert.IsTrue(done(w), "scenario point reached");
        }

        private static IEnumerable<TestCaseData> Scenarios()
        {
            yield return new TestCaseData("start").SetName("Scenario_Start");
            yield return new TestCaseData("midhaul").SetName("Scenario_MidHaulAtNoon");
            yield return new TestCaseData("sunsetpile").SetName("Scenario_MidBuildWithSunsetPile");
            yield return new TestCaseData("night").SetName("Scenario_NightWorkersAsleep");
        }

        private static SimWorld Make(string scenario, MapData map)
        {
            switch (scenario)
            {
                case "start":
                    return Woodcutting(map, 6, 21, 8);
                case "midhaul":
                {
                    SimWorld w = Woodcutting(map, 6, 21, 8);
                    StepUntil(w, x => x.Tick >= 4 * 60, 4 * 60);
                    Assert.IsTrue(w.Tasks.All.Any(t => t.Def.Id == HaulSystem.HaulTaskId && t.State == TaskState.Active),
                        "a haul is under way at noon");
                    return w;
                }
                case "sunsetpile":
                {
                    // Probe: minutes from noon until an ox first carries logs; then start so it carries just before sunset.
                    SimWorld probe = Building(map, 12 * 60);
                    StepUntil(probe, Carrying, 2000);
                    int d = (int)probe.Tick;
                    SimWorld w = Building(map, 16 * 60 - d - 1);
                    StepUntil(w, HasSitePile, 6 * 60);
                    return w;
                }
                default:
                {
                    SimWorld w = Woodcutting(map, 12, 21, 12);
                    StepUntil(w, x => x.People.Citizens.All(c => c.Activity == Activity.Sleeping), 14 * 60);
                    return w;
                }
            }
        }

        private static string Mismatch(byte[] a, byte[] b) => SaveDiff.FirstDifference(a, b);

        [TestCaseSource(nameof(Scenarios))]
        public void RoundTrip_WriteReadWrite_IsIdentical(string scenario)
        {
            MapData map = Map();
            SimWorld w = Make(scenario, map);
            byte[] first = Bytes(w, map);
            byte[] second = Bytes(SimSave.Read(first, map), map);
            Assert.IsNull(Mismatch(first, second), "Write(Read(Write(w))) vs Write(w)");
            Assert.AreEqual(first, second);
        }

        [TestCaseSource(nameof(Scenarios))]
        public void Saving_DoesNotChangeTheWorld(string scenario)
        {
            MapData map = Map();
            SimWorld w = Make(scenario, map);
            byte[] before = Bytes(w, map);
            Bytes(w, map);
            byte[] after = Bytes(w, map);
            Assert.IsNull(Mismatch(before, after));
            Assert.AreEqual(before, after);
        }

        [TestCaseSource(nameof(Scenarios))]
        public void LoadedWorld_SteppedOneGameDay_WritesSameBytesAsOriginal(string scenario)
        {
            MapData map = Map();
            SimWorld original = Make(scenario, map);
            SimWorld loaded = SimSave.Read(Bytes(original, map), map);
            original.Step(Day);
            loaded.Step(Day);
            byte[] a = Bytes(original, map);
            byte[] b = Bytes(loaded, map);
            Assert.IsNull(Mismatch(a, b), "after one game day");
            Assert.AreEqual(a, b);
        }

        [Test]
        public void RoundTrip_HouseholdWithHungryDays_AndLastHungryTick_AndTuning_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            var layout = new LordsLegacy.Sim.Start.StartLayout(
                M(100, 100), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0],
                new[] { new LordsLegacy.Sim.Start.StartFamily("Ash", M(-8, -4),
                    new[] { new LordsLegacy.Sim.Start.StartCitizen("A", Sex.Male, -4_000_000) }) }, 2);
            var tuning = new SimTuning { HungryDaysToLeave = 11, ArrivalFoodDays = 12, ArrivalNoHungerDays = 13, ArrivalGapDays = 14, MapEdgeToleranceM = 3.5 };
            SimWorld w = SimWorld.FromMapData(map, tuning);
            LordsLegacy.Sim.Start.StartLayoutApplier.Apply(w, layout);
            Assert.IsNull(w.LastHungryTick);
            w.Step(Day * 3 + 5);
            Family f = w.People.Families.First();
            Assert.Greater(f.HungryDays, 0);
            Assert.IsNotNull(w.LastHungryTick);
            byte[] first = Bytes(w, map);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(f.HungryDays, back.People.FindFamily(f.Id).HungryDays);
            Assert.AreEqual(w.LastHungryTick, back.LastHungryTick);
            Assert.AreEqual(11, back.Tuning.HungryDaysToLeave);
            Assert.AreEqual(12, back.Tuning.ArrivalFoodDays);
            Assert.AreEqual(13, back.Tuning.ArrivalNoHungerDays);
            Assert.AreEqual(14, back.Tuning.ArrivalGapDays);
            Assert.AreEqual(3.5, back.Tuning.MapEdgeToleranceM);
            Assert.IsNull(Mismatch(first, Bytes(back, map)));
        }

        [Test]
        public void RoundTrip_LeavingFamily_AndThreeEntryLog_AreIdentical()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            w.People.AddFamily(7, "Gone", 100, 100).Passage = FamilyPassage.Leaving;
            w.SettlementLog.Add(10, "The Gone family left");
            w.SettlementLog.Add(20, "Two");
            w.SettlementLog.Add(30, "Three");
            byte[] first = Bytes(w, map);
            SimWorld back = SimSave.Read(first, map);
            Family f = back.People.FindFamily(7);
            Assert.AreEqual(FamilyPassage.Leaving, f.Passage);
            Assert.IsTrue(f.IsInPassage);
            Assert.AreEqual(new[] { "The Gone family left", "Two", "Three" },
                back.SettlementLog.Entries.Select(e => e.Text).ToArray());
            Assert.AreEqual(30, back.SettlementLog.Entries[2].Tick);
            Assert.IsNull(Mismatch(first, Bytes(back, map)));
        }

        [Test]
        public void RoundTrip_ArrivingFamily_IsKept()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = SimWorld.FromMapData(map, new SimTuning());
            w.People.AddFamily(8, "New", 100, 100).Passage = FamilyPassage.Arriving;
            SimWorld back = SimSave.Read(Bytes(w, map), map);
            Assert.AreEqual(FamilyPassage.Arriving, back.People.FindFamily(8).Passage);
            Assert.AreEqual(0, back.SettlementLog.Entries.Count);
        }

        [Test]
        public void SaveVersion_IsBumpedTo22_ForPassageAndLog()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 22);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 21, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 21 (this build reads version " + SaveWriter.CurrentVersion + ").", e.Message);
        }

        [Test]
        public void Scenarios_AreNotTrivial_MidHaulHasLogsOrTasks()
        {
            MapData map = Map();
            SimWorld w = Make("midhaul", map);
            Assert.Greater(w.Tasks.Count, 0);
        }

        [Test]
        public void MapIdMismatch_ThrowsSaveMapMismatch_WithSv30Text()
        {
            MapData map = Map();
            byte[] bytes = Bytes(SimWorld.FromMapData(map, new SimTuning()), map);
            MapData other = new MapData("other", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var ex = Assert.Throws<SaveMapMismatchException>(() => SimSave.Read(bytes, other));
            StringAssert.StartsWith("This save was made on a different version of the test map", ex.Message);
        }

        [Test]
        public void MapHashMismatch_ThrowsSaveMapMismatch()
        {
            MapData map = Map();
            byte[] bytes = Bytes(SimWorld.FromMapData(map, new SimTuning()), map);
            MapData changed = Map();
            changed.ContentHash = map.ContentHash + 1;
            Assert.Throws<SaveMapMismatchException>(() => SimSave.Read(bytes, changed));
        }

        [Test]
        public void SaveDiff_NamesFirstDifferingSection()
        {
            MapData map = Map();
            SimWorld w = Woodcutting(map, 6, 21, 8);
            byte[] a = Bytes(w, map);
            Assert.IsNull(SaveDiff.FirstDifference(a, (byte[])a.Clone()));
            w.Random.NextUInt();
            string d = SaveDiff.FirstDifference(a, Bytes(w, map));
            StringAssert.StartsWith("first difference in section", d);
            // Flip the last byte: it lies in the last section (Tasks, id 7 per decision 0034).
            byte[] c = (byte[])a.Clone();
            c[c.Length - 1] ^= 0xFF;
            StringAssert.Contains("section 12", SaveDiff.FirstDifference(a, c));
        }
    }
}
