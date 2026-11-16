using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
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

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>The granary as an operation without an area (T-447, FD-32).</summary>
    public class GranaryOperationTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 8);
            // family 2 builds; families 3..5 are candidates for the granary
            for (int fam = 2; fam <= 5; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000); // the heavy build task needs a pool ox
            return (w, map);
        }

        private static int Place(SimWorld w)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static void BuildThroughTicks(SimWorld w, int id)
        {
            // families 3..5 have no workplace yet, so everyone may help; run until built
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "granary built by workers through Sim ticks only");
        }

        [Test]
        public void Place_CreatesExactlyOneGranaryOperation_WithModuleListAndAreaZero()
        {
            var (w, _) = Make();
            int id = Place(w);
            Operation op = w.Operations.All.Single();
            Assert.AreEqual(OperationTypes.GranaryId, op.TypeId);
            Assert.AreEqual(new[] { id }, op.ModuleIds.ToArray());
            Assert.AreEqual(0, op.AreaId);
            Assert.AreSame(op, w.Operations.FindByModule(id));
            Assert.IsNull(w.Operations.FindByModule(999));
        }

        [Test]
        public void Place_TwoGranaries_CreateTwoOperations()
        {
            var (w, _) = Make();
            w.Inventory.Produce(w.Inventory.AddStockpile(M(22, 22)), Items.Log, 8);
            Place(w);
            Assert.IsTrue(w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(80, 80), 0)).IsAccepted);
            Assert.AreEqual(2, w.Operations.Count);
        }

        [Test]
        public void FindByBuildingArea_NeverMatchesGranary()
        {
            Assert.IsNull(OperationTypes.FindByBuildingArea(null));
            Assert.IsNotNull(OperationTypes.Find(OperationTypes.GranaryId));
            Assert.AreEqual(2, OperationTypes.Granary.FamilySlots);
        }

        [Test]
        public void Assign_RefusedWhileUnbuilt_ThenTwoAcceptedAndThirdRefused_WorkplaceSet()
        {
            var (w, _) = Make();
            int id = Place(w);
            int opId = w.Operations.FindByModule(id).Id;
            CommandResult early = w.Execute(new AssignFamily(opId, 3));
            Assert.AreEqual(CommandRejectCode.NotAllowed, early.Reason);
            Assert.AreEqual("Hut not built yet", early.Text);

            BuildThroughTicks(w, id);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 4)).IsAccepted);
            CommandResult third = w.Execute(new AssignFamily(opId, 5));
            Assert.AreEqual(CommandRejectCode.NotAllowed, third.Reason);
            StringAssert.Contains("no free family slot", third.Text);
            Assert.AreEqual(opId, w.People.FindFamily(3).WorkplaceId);
            Assert.AreEqual(opId, w.People.FindFamily(4).WorkplaceId);
            Assert.IsNull(w.People.FindFamily(5).WorkplaceId);
        }

        [Test]
        public void Unassign_ClearsWorkplaceAndFamilyIds()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            Assert.IsTrue(w.Execute(new UnassignFamily(opId, 3)).IsAccepted);
            Assert.IsNull(w.People.FindFamily(3).WorkplaceId);
            CollectionAssert.DoesNotContain(w.Operations.Get(opId).FamilyIds, 3);
        }

        [Test]
        public void DeconstructBuiltGranary_RemovesOperationAndFreesFamilies()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            w.Execute(new AssignFamily(opId, 3));
            w.Execute(new AssignFamily(opId, 4));
            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsNull(w.Operations.Get(opId));
            Assert.AreEqual(0, w.Operations.Count);
            Assert.IsNull(w.People.FindFamily(3).WorkplaceId);
            Assert.IsNull(w.People.FindFamily(4).WorkplaceId);
        }

        [Test]
        public void DeconstructBuiltGranary_WithTimedWork_RemovesOperationWhenFinished()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            w.Execute(new AssignFamily(opId, 3));
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.IsNull(w.People.FindFamily(3).WorkplaceId, "families leave when marked");
            for (int i = 0; i < 6000 && w.Footprints.Get(id) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.IsNull(w.Operations.Get(opId));
        }

        [Test]
        public void CancelUnbuiltGranary_RemovesOperation_AndLeavesNoFamilyAssigned()
        {
            var (w, _) = Make();
            int id = Place(w);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(id));
            Assert.AreEqual(0, w.Operations.Count);
            foreach (Family f in w.People.Families) Assert.IsNull(f.WorkplaceId);
        }

        [Test]
        public void RemoveModuleOnUnbuiltGranary_RemovesOperation()
        {
            var (w, _) = Make();
            int id = Place(w);
            Assert.IsTrue(w.Execute(new RemoveModule(id)).IsAccepted);
            Assert.AreEqual(0, w.Operations.Count);
        }

        [Test]
        public void TwoGameDays_WithAssignedFamily_ThrowsNothing_AndAdultsGetNoWorkTasks()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            var banned = new HashSet<string>
            {
                WoodcuttingSystem.FellTaskId, ForagingSystem.ForageTaskId, HaulSystem.HaulTaskId,
                ConstructionTaskDefs.BuildId, ConstructionTaskDefs.DeconstructId,
            };
            int[] members = w.People.FindFamily(3).MemberIds.ToArray();
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 2 * 24 * 60; i++)
                {
                    w.Step(1);
                    foreach (int m in members)
                        foreach (TaskRecord t in w.Tasks.ByAssignee(m))
                            Assert.IsFalse(banned.Contains(t.Def.Id), "granary family got " + t.Def.Id);
                }
            });
        }

        [Test]
        public void MoveIn_HomelessFamilyAssignedToGranary_DoesNotThrow()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            Assert.IsTrue(w.People.FindFamily(3).IsHomeless);
            var house = new Footprint(900, ModuleTypes.Granary, M(60, 60), 0);
            Family chosen = null;
            Assert.DoesNotThrow(() => chosen = LordsLegacy.Sim.Homes.MoveInSystem.Choose(w, house));
            Assert.IsNotNull(chosen);
        }

        [Test]
        public void CitizenTaskText_WorkplaceOfAssignedCitizen_IsGranaryAndOperationId()
        {
            var (w, _) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            w.Execute(new AssignFamily(opId, 3));
            Citizen c = w.People.FindCitizen(30);
            Assert.AreEqual("Granary " + opId, CitizenTaskText.Workplace(w, c));
        }

        [Test]
        public void SaveRoundTrip_GranaryWithAssignedFamily_IsByteIdentical_AndKeepsModuleAndFamily()
        {
            var (w, map) = Make();
            int id = Place(w);
            BuildThroughTicks(w, id);
            int opId = w.Operations.FindByModule(id).Id;
            Assert.IsTrue(w.Execute(new AssignFamily(opId, 3)).IsAccepted);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Operation op = back.Operations.Get(opId);
            Assert.IsNotNull(op);
            Assert.AreEqual(new[] { id }, op.ModuleIds.ToArray());
            Assert.AreEqual(new[] { 3 }, op.FamilyIds.ToArray());
            Assert.AreEqual(0, op.AreaId);
        }
    }
}
