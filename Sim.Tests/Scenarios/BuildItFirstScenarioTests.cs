using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Scenarios
{
    /// <summary>
    /// End-to-end MVP-1 scenario: start camp to a working hut (GDD MVP-1-04/05/06, construction.md, tuning.md).
    /// Everything runs through the public Sim API: <see cref="StartLayoutApplier"/>, <see cref="SimWorld.Execute"/>
    /// and <see cref="SimWorld.Step"/>. No Unity types.
    /// </summary>
    [TestFixture]
    public class BuildItFirstScenarioTests
    {
        // ---- Setup, stated once so the scenario reads top to bottom -------------------------------------------

        /// <summary>World seed. Same seed must give identical state (see RunTwiceWithSameSeed test).</summary>
        private const int Seed = 12345;

        /// <summary>Born 4,000,000 ticks before the start: aged 14+ under POP-27 (odd-jobbers must be working age).</summary>
        private const long AdultBirth = -4_000_000;

        /// <summary>Logs in the default start stockpile (CMP-06).</summary>
        private const int StartingLogs = 40;

        /// <summary>Logs a woodcutting hut takes from the stockpile (ModuleTypes recipe).</summary>
        private const int HutLogs = 8;

        /// <summary>Tick of 2 March 00:00 when the start is 1 March 06:00 (18 game hours later).</summary>
        private const int Midnight2March = 18 * 60;

        /// <summary>Day of year of 2 March in a non-leap year (1 Jan = 1).</summary>
        private const int DayOfYear2March = 61;

        /// <summary>Hard stop for the run: four game days (felling must start well before this).</summary>
        private const int MaxTicks = 4 * 1440;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        /// <summary>
        /// Current pace tuning, stated explicitly: ox 1.04 m/min, walk 1.74 m/min, 4 builders per site, 30-minute
        /// day (tuning.md). The fell pace is <c>ProcessDefs.Timber</c>, not a tuning knob.
        /// Start 1 March 06:00 (tick 0 of the run).
        /// </summary>
        private static SimTuning PaceTuning() => new SimTuning
        {
            WorldSeed = Seed,
            StartMonth = 3,
            StartDay = 1,
            StartHour = 6,
            StartMinute = 0,
            WalkSpeedMPerGameMinute = 1.74,
            OxSpeedMPerGameMinute = 1.04,
            MaxBuildersPerSite = 4,
        };

        private static StartCitizen Adult(string name, Sex sex) => new StartCitizen(name, sex, AdultBirth);

        /// <summary>
        /// Default start layout: camp centre (0,0) unrotated, fire (0,0), stockpile (6,0), ox stand (-6,0), no trunk road,
        /// three unassigned families of two adults (six odd-jobbers), two oxen and 40 logs in the stockpile.
        /// Family ids are 1..3 (Apply numbers them in order), so family 1 is "Ashford".
        /// </summary>
        private static StartLayout DefaultLayout(int logs = StartingLogs)
        {
            var families = new List<StartFamily>
            {
                new StartFamily("Ashford", M(-9, -5), new[] { Adult("Edmund", Sex.Male), Adult("Mary", Sex.Female) }),
                new StartFamily("Marsh", M(-9, 5), new[] { Adult("John", Sex.Male), Adult("Alice", Sex.Female) }),
                new StartFamily("Thorne", M(9, 5), new[] { Adult("Hugh", Sex.Male), Adult("Joan", Sex.Female) }),
            };
            return new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0], families, 2,
                stock: new[] { new StartStock(Items.Log, logs) });
        }

        /// <summary>
        /// Builds the world on flat ground from -50 m to 150 m (a map, so the nav grid has cells and agents can path),
        /// applies the default start layout and adds trees near the hut site.
        /// </summary>
        private static SimWorld StartWorld(int logs = StartingLogs)
        {
            var map = new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, PaceTuning());
            StartLayoutApplier.Apply(w, DefaultLayout(logs));
            // Four 16 m trunks (4 logs each) within 20 m of the hut access point (45,-3).
            w.Trees.Add(new TreeId(1), 30f, -8f, 16);
            w.Trees.Add(new TreeId(2), 34f, 6f, 16);
            w.Trees.Add(new TreeId(3), 58f, -10f, 16);
            w.Trees.Add(new TreeId(4), 50f, 11f, 16);
            return w;
        }

        /// <summary>Woodcutting area x 38..66, z -12..12. Hut centre (45,0) with access point (45,-3); storage (55,0).</summary>
        private static Operation DesignateWoodcutting(SimWorld w)
        {
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(38, -12), M(66, -12), M(66, 12), M(38, 12) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static List<TaskRecord> FellTasks(SimWorld w, Operation op) =>
            w.Tasks.ByOwner(op.Id).Where(t => t.Def.Id == WoodcuttingSystem.FellTaskId).ToList();

        /// <summary>Canonical text of the world at this tick, used to compare two runs with the same seed.</summary>
        private static string Snapshot(SimWorld w, LedgerLocation stockpile)
        {
            var sb = new StringBuilder();
            sb.Append(w.Tick).Append('|').Append(stockpile.Total).Append('|')
              .Append(w.Inventory.ConsumedOf(Items.Log.Id)).Append('|').Append(w.Trees.Count);
            foreach (Citizen c in w.People.Citizens.OrderBy(c => c.Id))
                sb.Append('|').Append(c.Id).Append(':').Append(Q(c.X)).Append(',').Append(Q(c.Z)).Append(',').Append(c.Activity);
            foreach (Ox o in w.People.Oxen.OrderBy(o => o.Id))
                sb.Append('|').Append("ox").Append(o.Id).Append(':').Append(Q(o.X)).Append(',').Append(Q(o.Z));
            foreach (TaskRecord t in w.Tasks.All.OrderBy(t => t.Id))
                sb.Append('|').Append(t.Id).Append(':').Append(t.Def.Id).Append(':').Append(t.State);
            return sb.ToString();
        }

        private static string Q(double v) => Math.Round(v, 4).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Outcome of one full run of the scenario.</summary>
        private sealed class ScenarioRun
        {
            public long? BuiltTick;
            public long? FirstFellTick;
            public string BuiltAt;
            public long SunsetBoundTick;
            public string Trace;
        }

        /// <summary>
        /// Runs the whole scenario and asserts every criterion that happens along the way. Returns what the
        /// caller needs to check the end state and compare two runs.
        /// </summary>
        private static ScenarioRun RunScenario()
        {
            SimWorld w = StartWorld();
            LedgerLocation stockpile = w.Camp.StockpileLocation;
            Assert.AreEqual(StartingLogs, stockpile.Total, "start stockpile");
            Assert.AreEqual(6, w.People.Citizens.Count(), "six odd-jobbers (3 families of 2 adults)");
            Assert.AreEqual(2, w.People.OxCount, "two oxen");

            Operation op = DesignateWoodcutting(w);

            // 06:00 on 1 March (tick 0): hut and storage placed. Hut access point is 39 m from the stockpile.
            Assert.AreEqual(3, w.Now.Month, "March");
            Assert.AreEqual(1, w.Now.DayOfMonth, "1st");
            Assert.AreEqual(6, w.Now.Hour, "06:00");
            CommandResult hutResult = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(45, 0), 0));
            Assert.IsTrue(hutResult.IsAccepted, hutResult.Text);
            int hutId = (int)hutResult.Created[0];
            CommandResult storageResult = w.Execute(new PlaceModule(op.Id, ModuleTypes.LogStorageId, M(55, 0), 0));
            Assert.IsTrue(storageResult.IsAccepted, storageResult.Text);

            Footprint hut = w.Footprints.Get(hutId);
            double dx = hut.AccessPoint.XMetres - stockpile.Position.XMetres;
            double dz = hut.AccessPoint.ZMetres - stockpile.Position.ZMetres;
            Assert.LessOrEqual(Math.Sqrt(dx * dx + dz * dz), 60.0, "hut access point within 60 m of the camp stockpile");

            // Before Built (MVP-1-06): status MissingHut, no fell tasks, AssignFamily refused.
            Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op));
            Assert.AreEqual(0, FellTasks(w, op).Count);
            CommandResult early = w.Execute(new AssignFamily(op.Id, 1));
            Assert.IsTrue(early.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotAllowed, early.Reason);

            var run = new ScenarioRun { SunsetBoundTick = SunsetTick2March() };
            var trace = new StringBuilder();
            trace.AppendLine(Snapshot(w, stockpile));
            bool familyAssigned = false;

            for (int i = 0; i < MaxTicks && !(run.BuiltTick != null && run.FirstFellTick != null && FellDone(w, op)); i++)
            {
                w.Step(1);

                if (w.Tick == Midnight2March)
                    Assert.AreEqual(DayOfYear2March, w.Now.DayOfYear, "tick 1080 is 2 March 00:00");

                if (w.Tick % 60 == 0)
                {
                    Assert.IsNull(w.Inventory.Audit(), "ledger audit at tick " + w.Tick);
                    trace.AppendLine(Snapshot(w, stockpile));
                }

                if (run.BuiltTick == null)
                {
                    if (!w.IsBuilt(hutId))
                    {
                        Assert.AreEqual(OperationState.MissingHut, OperationStatus.Of(w, op), "tick " + w.Tick);
                        Assert.AreEqual(0, FellTasks(w, op).Count, "no fell tasks before Built, tick " + w.Tick);
                        Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 1)).IsRejected, "AssignFamily refused, tick " + w.Tick);
                        continue;
                    }

                    run.BuiltTick = w.Tick;
                    run.BuiltAt = w.Now.Format();
                    Assert.AreEqual(StartingLogs - HutLogs, stockpile.Total, "stockpile exactly 8 lower at Built");
                    Assert.AreEqual(HutLogs, w.Inventory.ConsumedOf(Items.Log.Id), "the hut consumed exactly 8 logs");
                    CommandResult assign = w.Execute(new AssignFamily(op.Id, 1));
                    Assert.IsTrue(assign.IsAccepted, "AssignFamily after Built: " + assign.Text);
                    familyAssigned = true;
                }

                if (run.FirstFellTick == null && FellTasks(w, op).Count > 0)
                {
                    run.FirstFellTick = w.Tick;
                    Assert.IsTrue(familyAssigned, "felling only after AssignFamily");
                }
            }

            Assert.IsNotNull(run.BuiltTick, "hut built within the run");
            Assert.LessOrEqual(run.BuiltTick.Value, run.SunsetBoundTick, "hut built by sunset on 2 March");
            Assert.IsNotNull(run.FirstFellTick, "the first logs were felled");
            Assert.GreaterOrEqual(run.FirstFellTick.Value, run.BuiltTick.Value, "felling starts after Built");
            Assert.IsTrue(FellDone(w, op), "at least one fell task finished");
            Assert.Greater(OperationStatus.LogLoad(w, op), 0, "felled logs are held by the operation");
            Assert.IsNull(w.Inventory.Audit(), "final ledger audit");

            // Stockpile exactly 8 lower (40 - 8), and the hut's logs were consumed, not lost.
            Assert.AreEqual(StartingLogs - HutLogs, stockpile.Total, "stockpile exactly 8 lower");
            Assert.AreEqual(HutLogs, w.Inventory.ConsumedOf(Items.Log.Id), "the hut consumed 8 logs");
            Assert.IsNull(ConstructionProgress.Of(w, hutId), "no construction site once built");

            run.Trace = trace.ToString();
            // Information only: the measured completion tick is not a pass/fail bound (the bound is sunset, 2 March).
            TestContext.Out.WriteLine("Hut built at tick " + run.BuiltTick.Value + " (" + run.BuiltAt + "); sunset bound tick " +
                run.SunsetBoundTick + "; first fell at tick " + run.FirstFellTick.Value + ".");
            return run;
        }

        private static bool FellDone(SimWorld w, Operation op) =>
            FellTasks(w, op).Any(t => t.State == TaskState.Done);

        /// <summary>Tick of sunset on 2 March, from the Daylight table (tuning min/max daylight hours).</summary>
        private static long SunsetTick2March()
        {
            SimTuning t = PaceTuning();
            double sunsetHours = new Daylight(t.MinDaylightHours, t.MaxDaylightHours).For(DayOfYear2March).Sunset;
            return Midnight2March + (long)Math.Ceiling(sunsetHours * 60.0);
        }

        // ---- Tests -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Criteria, in order: placement at 06:00 1 March; MissingHut, no fell tasks and refused AssignFamily before
        /// Built; hut Built by sunset on 2 March; audit clean every game hour; stockpile 8 lower; AssignFamily accepted
        /// after Built and felling follows.
        /// </summary>
        [Test]
        public void StartCamp_ToWorkingHut_BuiltBySunsetDay2_StockpileEightLower_FellingAfterAssign()
        {
            ScenarioRun run = RunScenario();
            Assert.IsNotNull(run.BuiltTick);
        }

        /// <summary>Same seed, same state: two complete runs give the same hourly snapshots and the same built tick.</summary>
        [Test]
        public void RunTwiceWithSameSeed_GivesIdenticalState()
        {
            ScenarioRun first = RunScenario();
            ScenarioRun second = RunScenario();
            Assert.AreEqual(first.BuiltTick, second.BuiltTick);
            Assert.AreEqual(first.FirstFellTick, second.FirstFellTick);
            Assert.AreEqual(first.Trace, second.Trace);
        }

        /// <summary>A second hut with fewer than 8 logs available is refused and changes nothing.</summary>
        /// <remarks>
        /// One hut is allowed per operation, so the second hut goes in a second woodcutting area (x 70..96) with its
        /// own operation. Done in its own world: the default 40 logs leave 32 after the first hut, which is enough for
        /// a second one. Here 15 logs are in stock, so 7 are available after the first hut reserves 8.
        /// </remarks>
        [Test]
        public void SecondHut_WithSevenAvailable_IsRefused_AndChangesNothing()
        {
            SimWorld w = StartWorld(logs: 15);
            LedgerLocation stockpile = w.Camp.StockpileLocation;
            Operation op = DesignateWoodcutting(w);

            CommandResult first = w.Execute(new PlaceModule(op.Id, ModuleTypes.WoodcuttingHutId, M(45, 0), 0));
            Assert.IsTrue(first.IsAccepted, first.Text);
            Assert.AreEqual(7, stockpile.AvailableOf(Items.Log.Id), "7 logs available after the first hut");

            CommandResult area2 = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(70, -12), M(96, -12), M(96, 12), M(70, 12) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(area2.IsAccepted, area2.Text);
            Operation op2 = w.Operations.FindByArea((int)area2.Created[0]);

            int sitesBefore = w.Construction.Count;
            int modulesBefore = op2.ModuleIds.Count;
            CommandResult second = w.Execute(new PlaceModule(op2.Id, ModuleTypes.WoodcuttingHutId, M(80, 0), 0));
            Assert.IsTrue(second.IsRejected);
            StringAssert.Contains("have 7, need 8", second.Text);
            Assert.AreEqual(sitesBefore, w.Construction.Count);
            Assert.AreEqual(modulesBefore, op2.ModuleIds.Count);
            Assert.AreEqual(15, stockpile.Total, "nothing consumed or reserved by the refused hut");
            Assert.AreEqual(7, stockpile.AvailableOf(Items.Log.Id));
            Assert.IsNull(w.Inventory.Audit());
        }
    }
}
