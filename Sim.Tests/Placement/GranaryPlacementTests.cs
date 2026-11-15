using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>Granary as a standalone structure with a 5 m yard (T-422, FD-30, FD-30b).</summary>
    public class GranaryPlacementTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make(int logs = 8, MapTree[] trees = null)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees ?? new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, logs);
            return (w, map);
        }

        private static CommandResult Place(SimWorld w, double x, double z, double rot = 0) =>
            w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(x, z), rot));

        [Test]
        public void GranaryType_Is8x6Building_5mYard_Plot18x16_Capacity100_Recipe8Logs600()
        {
            FootprintTypeDef t = ModuleTypes.Granary;
            Assert.AreEqual(8000, t.WidthMm);
            Assert.AreEqual(6000, t.DepthMm);
            Assert.AreEqual(5000, t.YardMarginMm);
            Assert.AreEqual(100, t.FoodCapacity);
            Assert.AreEqual(8, t.ConstructionRecipe.Logs);
            Assert.AreEqual(600, t.ConstructionRecipe.BuildWorkerMinutes);
            Assert.IsTrue(t.BlocksMovement);
            var fp = new Footprint(1, t, M(50, 50), 0);
            Assert.AreEqual(18.0 * 16.0, Polygons.SignedAreaM2(fp.PlotPolygon), 0.001);
            Assert.AreSame(t, ModuleTypes.Find(ModuleTypes.GranaryId));
        }

        [Test]
        public void Place_CreatesSiteReservesLogsAndFoodStore_InOneApply()
        {
            var (w, _) = Make();
            CommandResult r = Place(w, 50, 50);
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            Assert.IsNotNull(w.Footprints.Get(id));
            Assert.IsNotNull(w.Construction.Get(id));
            Assert.IsFalse(w.IsBuilt(id));
            Assert.AreEqual(8, w.Inventory.Reservations.Sum(x => x.Count));
            LedgerLocation store = w.Inventory.FindFoodStore(id);
            Assert.IsNotNull(store);
            Assert.AreEqual(LocationKind.FoodStore, store.Kind);
            Assert.AreEqual(100, store.Capacity);
            Assert.IsFalse(store.Accepts(ItemCategory.Log));
        }

        [Test]
        public void Place_NotEnoughLogs_RejectedAndChangesNothing()
        {
            var (w, _) = Make(logs: 7);
            int fps = w.Footprints.Count;
            CommandResult r = Place(w, 50, 50);
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(fps, w.Footprints.Count);
            Assert.AreEqual(0, w.Construction.Count);
            Assert.AreEqual(0, w.Inventory.Reservations.Count());
        }

        [Test]
        public void Place_UnknownOrNonStructureType_AndBadRotation_AreInvalidArgument()
        {
            var (w, _) = Make();
            Assert.AreEqual(CommandRejectCode.InvalidArgument, w.Execute(new PlaceStructure("module.nope", M(50, 50), 0)).Reason);
            Assert.AreEqual(CommandRejectCode.InvalidArgument, w.Execute(new PlaceStructure(ModuleTypes.HouseId, M(50, 50), 0)).Reason);
            Assert.AreEqual(CommandRejectCode.InvalidArgument, w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(50, 50), double.NaN)).Reason);
        }

        [Test]
        public void YardOverlapsExistingBuildingFootprint_Refused_ButBuildingOnlyClearIsNotEnough()
        {
            var (w, _) = Make(logs: 16);
            Assert.IsTrue(Place(w, 50, 50).IsAccepted);
            // 10 m east: the two buildings are 2 m apart (clear), but the new yard (5 m) reaches the old building.
            CommandResult r = Place(w, 60, 50);
            Assert.IsTrue(r.IsRejected, "yard touches the neighbour's building");
            Assert.AreEqual(StructureRefusal.OverlapsFootprint, StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(60, 50), 0).Refusal);
            // Buildings 8 + 5 + 5 = 18 m apart centre to centre: plots touch only at an edge, which is allowed.
            Assert.IsTrue(Place(w, 68.001, 50).IsAccepted);
        }

        [Test]
        public void YardOverlapsOnlyAYard_IsAllowed_NoMatterWhichSideOfTheNeighbour()
        {
            var (w, _) = Make(logs: 24);
            Assert.IsTrue(Place(w, 50, 50).IsAccepted);
            // Rotated 90 degrees: footprint is 6 wide (x) and 8 deep (z) -> centre distance in x must exceed 4+... still blocked near.
            Assert.IsTrue(Place(w, 50, 70, 90).IsAccepted, "20 m away in z, plots clear");
            Assert.IsTrue(Place(w, 30, 50, 0).IsAccepted, "20 m away in x");
        }

        [Test]
        public void OverlapsArea_Refused_EvenWhenOnlyTheYardReachesIt()
        {
            var (w, _) = Make();
            // Building at x 46..54; yard to x 59. Area starts at x 57.
            var shape = AreaShape.Straight(new[] { M(57, 40), M(80, 40), M(80, 60), M(57, 60) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId)).IsAccepted);
            StructurePlacementResult p = StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(50, 50), 0);
            Assert.AreEqual(StructureRefusal.OverlapsArea, p.Refusal);
            Assert.IsTrue(Place(w, 50, 50).IsRejected);
            Assert.IsTrue(Place(w, 40, 50).IsAccepted, "yard ends at x 49 <= 57 -> plot at 31..49 is clear");
        }

        [Test]
        public void OverlapsRoad_Refused_EvenWhenOnlyTheYardReachesIt()
        {
            var (w, _) = Make();
            Assert.IsTrue(w.Execute(new DrawRoad(new[] { M(58, 20), M(58, 80) }, RoadTypes.DirtPathId)).IsAccepted);
            StructurePlacementResult p = StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(50, 50), 0);
            Assert.AreEqual(StructureRefusal.OverlapsRoad, p.Refusal);
            Assert.IsTrue(StructurePlacement.Preview(w, ModuleTypes.GranaryId, M(30, 50), 0).Ok);
        }

        [Test]
        public void PlotUnderRotation_ClearsTreesAcrossBuildingAndYard_NotOutside()
        {
            var trees = new[]
            {
                new MapTree(new TreeId(1), 50f, 50f, 16), // under the building
                new MapTree(new TreeId(2), 58f, 56f, 16), // in the yard
                new MapTree(new TreeId(3), 70f, 70f, 16), // far away
            };
            var (w, _) = Make(trees: trees);
            CommandResult r = Place(w, 50, 50);
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(2, r.RemovedTreeIds.Length);
            Assert.AreEqual(1, w.Trees.Count);
        }

        [Test]
        public void OnlyBuildingBlocksNavGrid_YardIsWalkable()
        {
            var (w, _) = Make();
            Assert.IsTrue(Place(w, 50, 50).IsAccepted);
            w.Step(1);
            // Building 46..54 x 47..53; yard to 41..59 x 42..58.
            Assert.IsTrue(w.Nav.IsBlocked(w.Nav.CellX(50), w.Nav.CellZ(50)), "building centre blocks");
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(44), w.Nav.CellZ(50)), "yard west is walkable");
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(56.5), w.Nav.CellZ(50)), "yard east is walkable");
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(50), w.Nav.CellZ(45)), "yard north is walkable");
            Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(50), w.Nav.CellZ(56)), "yard south is walkable");
            int blocked = 0;
            for (int ix = 0; ix < w.Nav.Width; ix++)
                for (int iz = 0; iz < w.Nav.Depth; iz++)
                    if (w.Nav.IsBlocked(ix, iz)) blocked++;
            double cell = w.Nav.CellSizeM;
            Assert.LessOrEqual(blocked * cell * cell, 8 * 6 + 4 * (cell * 8) + cell * cell * 2, "only about the 48 m2 building is blocked");
        }

        [Test]
        public void Pipeline_PlaceBuildThroughSimTicks_ThenFoodOnlyStore_ThenDeconstructDropsFoodPile()
        {
            var (w, _) = Make();
            w.People.AddFamily(2, "Helpers", 40, 40);
            for (int i = 0; i < 4; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 40, 40);
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            CommandResult r = Place(w, 50, 50);
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 4000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "built by workers through Sim ticks only");

            LedgerLocation store = w.Inventory.FindFoodStore(id);
            Assert.IsNotNull(store);
            w.Inventory.Produce(store, Items.Find(Items.Berries.Id), 40);
            Assert.Throws<System.InvalidOperationException>(() => w.Inventory.Produce(store, Items.Log, 1), "food only");
            Assert.Throws<System.InvalidOperationException>(() => w.Inventory.Produce(store, Items.Find(Items.Berries.Id), 61), "capacity 100");

            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(id)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(id));
            int pile = w.Inventory.Locations
                .Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.CountOf(Items.Berries.Id));
            Assert.AreEqual(40, pile, "contents dropped as a ground pile (CN-55)");
            Assert.AreEqual(0, store.Total);
        }

        [Test]
        public void Granary_RoundTripsThroughSave_AndSaveVersionIsUnchangedLayout()
        {
            var (w, map) = Make();
            CommandResult r = Place(w, 50, 50, 30);
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Footprint f = back.Footprints.Get(id);
            Assert.AreEqual(ModuleTypes.GranaryId, f.Type.Id);
            Assert.AreEqual(30, f.RotationDegrees);
            Assert.IsNotNull(back.Construction.Get(id));
            LedgerLocation store = back.Inventory.FindFoodStore(id);
            Assert.IsNotNull(store);
            Assert.AreEqual(100, store.Capacity);
            Assert.AreEqual(f.PlotPolygon, new Footprint(id, ModuleTypes.Granary, M(50, 50), 30).PlotPolygon);
        }
    }
}
