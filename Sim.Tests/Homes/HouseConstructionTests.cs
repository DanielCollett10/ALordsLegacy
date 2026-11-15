using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>
    /// A house site with no operation gets deliver and build tasks (T-443, CN-20..23, HM-06). Unlike the move-in tests,
    /// nothing here removes the site: the house is built by the sim itself, by an odd-jobber family and a pool ox.
    /// </summary>
    public class HouseConstructionTests
    {
        private const long Adult = -4_000_000;
        private const int MaxTicks = 40000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        // Road along z = 50 from x 20 to 80; 20 logs in a stockpile at (60, 40); one ox at (40, 45) unless told otherwise.
        private static SimWorld Make(int logs = 20, int oxen = 1)
        {
            var w = SimWorld.FromMapData(SyntheticMap.CreateRamp(), new SimTuning());
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal,
                new[] { M(20, 50), M(80, 50) }, new[] { M(20, 50), M(50, 50), M(80, 50) });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(60, 40)), Items.Log, logs);
            for (int i = 0; i < oxen; i++) w.People.AddOx(50 + i, "Ox" + i, 40, 45);
            return w;
        }

        private static AreaShape Plot(SimWorld w, double x)
        {
            int road = w.Roads.Segments.First().Id;
            return new AreaShape(
                new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
        }

        // An odd-jobber family: no workplace, one adult member.
        private static Family AddOddJobbers(SimWorld w, int id)
        {
            Family f = w.People.AddFamily(id, "F" + id, 10, 10);
            w.People.AddCitizen(id * 10, "C" + id, Sex.Male, Adult, id, 10, 10);
            return f;
        }

        private static int Place(SimWorld w, double x)
        {
            CommandResult r = w.Execute(new DesignatePlot(Plot(w, x)));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[1];
        }

        private static List<TaskRecord> TasksOf(SimWorld w, string defId) =>
            w.Tasks.All.Where(t => t.Def.Id == defId).ToList();

        [Test]
        public void HouseSite_WithoutOperation_OpensDeliverTasksOwnedByHouseOwnerId_AndTakesPoolOx()
        {
            SimWorld w = Make();
            AddOddJobbers(w, 1);
            int house = Place(w, 30);
            Assert.IsNull(w.Operations.All.FirstOrDefault(op => op.ModuleIds.Contains(house)), "a house belongs to no operation");
            w.Step(1);
            List<TaskRecord> delivers = TasksOf(w, ConstructionTaskDefs.DeliverId);
            Assert.AreEqual(3, delivers.Count, "10 logs in loads of 4 (4 + 4 + 2) open three tasks in one step");
            foreach (TaskRecord t in delivers)
            {
                Assert.AreEqual(DeconstructionWork.HouseOwnerId, t.OwnerOperationId);
                Assert.IsFalse(t.OxWasAssigned, "a pool ox, not an operation's");
            }
            w.Step(1);
            Assert.IsTrue(TasksOf(w, ConstructionTaskDefs.DeliverId).Any(t => t.OxId.HasValue), "the pool ox was attached");
        }

        [Test]
        public void HouseSite_NoLogsOnSite_IsBuiltByOddJobbersAndOxen_AndFamilyMovesIn()
        {
            SimWorld w = Make();
            Family f = AddOddJobbers(w, 1);
            int house = Place(w, 30);
            var seenDeliver = new HashSet<int>();
            var seenBuild = new HashSet<int>();
            bool allHouseOwned = true;
            for (int i = 0; i < MaxTicks && w.Construction.Get(house) != null; i++)
            {
                w.Step(1);
                foreach (TaskRecord t in w.Tasks.All)
                {
                    if (t.Def.Id != ConstructionTaskDefs.DeliverId && t.Def.Id != ConstructionTaskDefs.BuildId) continue;
                    if (t.OwnerOperationId != DeconstructionWork.HouseOwnerId) allHouseOwned = false;
                    if (t.Def.Id == ConstructionTaskDefs.DeliverId) seenDeliver.Add(t.Id);
                    else seenBuild.Add(t.Id);
                }
            }
            Assert.IsNull(w.Construction.Get(house), "the house reached Built");
            Assert.IsTrue(w.IsBuilt(house));
            Assert.IsTrue(seenDeliver.Count >= 3, "logs were delivered by ox");
            Assert.IsTrue(seenBuild.Count >= 1, "builders were offered");
            Assert.IsTrue(allHouseOwned, "every construction task of the house is owned by HouseOwnerId");
            w.Step(2);
            Assert.IsFalse(f.IsHomeless, "a homeless family moved in");
            Assert.AreEqual(house, f.HomeHouseId);
            Assert.AreEqual(1, w.Homes.Get(house).FamilyId);
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void HouseSite_DeliverAndBuildTaskText_ReadsDeliveringLogsToHouseAndBuildingHouse()
        {
            SimWorld w = Make();
            AddOddJobbers(w, 1);
            Place(w, 30);
            var texts = new HashSet<string>();
            for (int i = 0; i < MaxTicks && !(texts.Contains("Delivering logs to House") && texts.Contains("Building House")); i++)
            {
                w.Step(1);
                foreach (Citizen c in w.People.Citizens) texts.Add(CitizenTaskText.Task(w, c));
            }
            Assert.Contains("Delivering logs to House", texts.ToList());
            Assert.Contains("Building House", texts.ToList());
        }

        [Test]
        public void HouseSite_SetPriorityHigh_RaisesItsOpenTasks_WithNoOperation()
        {
            SimWorld w = Make();
            AddOddJobbers(w, 1);
            int house = Place(w, 30);
            w.Step(1);
            CommandResult r = w.Execute(new SetSitePriority(house, ConstructionPriority.High));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.IsTrue(TasksOf(w, ConstructionTaskDefs.DeliverId).Where(t => !t.IsFinished).All(t => t.Priority == TaskPriority.High));
        }

        [Test]
        public void HouseSite_MidConstruction_SaveRoundTrips_WithOwnerMinusOneTasks()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make();
            AddOddJobbers(w, 1);
            int house = Place(w, 30);
            for (int i = 0; i < MaxTicks; i++)
            {
                w.Step(1);
                if (TasksOf(w, ConstructionTaskDefs.DeliverId).Any(t => !t.IsFinished)) break;
            }
            Assert.IsTrue(TasksOf(w, ConstructionTaskDefs.DeliverId).Any(t => !t.IsFinished), "a delivery is live");
            Assert.IsNotNull(w.Construction.Get(house));

            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            List<TaskRecord> loaded = TasksOf(back, ConstructionTaskDefs.DeliverId);
            Assert.IsTrue(loaded.Count > 0);
            Assert.IsTrue(loaded.All(t => t.OwnerOperationId == DeconstructionWork.HouseOwnerId));
            byte[] second = SimSave.Write(back, "s", 1, map.MapId, map.ContentHash);
            Assert.IsNull(SaveDiff.FirstDifference(first, second));

            w.Step(300);
            back.Step(300);
            Assert.AreEqual(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash),
                SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
        }
    }
}
