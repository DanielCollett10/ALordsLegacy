using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Core
{
    /// <summary>T-629: the Sim writes SimLog entries from commands, dispatch and construction. Logging must not change state.</summary>
    public class SimLogWriterTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Flat() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static List<string> Texts(SimWorld w, LogCategory c) => w.Log.Snapshot(c).Select(e => e.Text).ToList();

        [Test]
        public void RefusedCommand_LogsItsResultTextUnderCommands()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning());
            CommandResult r = w.Execute(new RemoveModule(9999));
            Assert.IsTrue(r.IsRejected);
            Assert.IsNotEmpty(r.Text);
            List<string> texts = Texts(w, LogCategory.Commands);
            Assert.AreEqual(1, texts.Count);
            StringAssert.Contains(r.Text, texts[0]);
            StringAssert.Contains("RemoveModule", texts[0]);
        }

        [Test]
        public void AcceptedCommand_WritesNothingUnderCommands()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning());
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0)).IsAccepted);
            Assert.AreEqual(0, Texts(w, LogCategory.Commands).Count);
        }

        [Test]
        public void Dispatch_LogsAssignmentWithTaskAndCitizen_AndNoWorkerSkip()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8 });
            w.Tasks.Create(new TaskDef("task.nobody", new[] { TaskStep.MoveTo(), TaskStep.Work(5) }, EligibilityPolicy.AssignedFamilyOnly), 7, null);
            w.Step(10); // first dispatch tick, nobody to take it
            List<string> skip = Texts(w, LogCategory.Tasks);
            Assert.AreEqual(1, skip.Count);
            StringAssert.Contains("no worker", skip[0]);
            StringAssert.Contains("task.nobody", skip[0]);

            w.People.AddFamily(1, "F1", 100, 100).WorkplaceId = 7;
            Citizen c = w.People.AddCitizen(5, "Ann", Sex.Female, Adult, 1, 100, 100);
            w.Step(10);
            string assigned = Texts(w, LogCategory.Tasks).Last(t => t.Contains("assigned"));
            StringAssert.Contains("task.nobody", assigned);
            StringAssert.Contains("citizen " + c.Id, assigned);
        }

        [Test]
        public void Construction_PlaceBuildThroughTicks_LogsReservedBuildingDone()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            w.People.AddFamily(2, "Helpers", 40, 40);
            for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, Adult, 2, 40, 40);
            w.People.AddOx(50, "O", 40, 44).State = OxState.Resting(40000, 44000);
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int fp = (int)r.Created[0];
            for (int i = 0; i < 4000 && !w.IsBuilt(fp); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(fp));
            List<string> texts = Texts(w, LogCategory.Construction);
            int reserved = texts.IndexOf("site " + fp + " reserved");
            int building = texts.IndexOf("site " + fp + " building");
            int done = texts.IndexOf("site " + fp + " done");
            Assert.That(reserved, Is.GreaterThanOrEqualTo(0));
            Assert.That(building, Is.GreaterThan(reserved));
            Assert.That(done, Is.GreaterThan(building));
        }

        [Test]
        public void Construction_RemovedBeforeDone_LogsCancelled()
        {
            var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            int fp = (int)w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0)).Created[0];
            CommandResult r = w.Execute(new RemoveModule(fp));
            Assert.IsTrue(r.IsAccepted, r.Text);
            CollectionAssert.Contains(Texts(w, LogCategory.Construction), "site " + fp + " cancelled");
        }

        [Test]
        public void Logging_DisabledOrEnabled_DoesNotChangeTheWorld()
        {
            string Run(bool log)
            {
                var w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8 });
                w.Log.SetAllEnabled(log);
                w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
                w.People.AddFamily(2, "Helpers", 40, 40);
                for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, Adult, 2, 40, 40);
                w.People.AddOx(50, "O", 40, 44).State = OxState.Resting(40000, 44000);
                w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0));
                w.Step(1500);
                return w.Tick + ":" + w.Random.State + ":" + string.Join(",", w.People.Citizens.Select(c => c.X + "/" + c.Z));
            }
            Assert.AreEqual(Run(false), Run(true));
        }
    }
}
