using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
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
    /// <summary>task.deconstruct and the 50% refund (T-394, CN-53..55).</summary>
    public class DeconstructTaskTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        // Built module of the given type in a woodcutting operation, with helpers ready.
        private static SimWorld Make(string type, int helpers, out int id, out Operation op)
        {
            var w = SimWorld.FromMapData(Map(), new SimTuning { StartHour = 8 });
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            op = w.Operations.FindByArea((int)r.Created[0]);
            w.Inventory.Produce(w.Inventory.AddStockpile(M(95, 95)), Items.Log, 20);
            w.People.AddFamily(2, "Helpers", 100, 100);
            for (int i = 0; i < helpers; i++) w.People.AddCitizen(20 + i, "H" + i, Sex.Male, AdultBirth, 2, 100, 100);
            CommandResult p = w.Execute(new PlaceModule(op.Id, type, M(110, 100), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            id = (int)p.Created[0];
            w.Construction.Remove(id);
            return w;
        }

        private static int Unowned(SimWorld w) => w.Inventory.Locations
            .Where(l => l.Kind == LocationKind.GroundPile && l.OwnerOperationId == null).Sum(l => l.CountOf(Items.Log.Id));

        [Test]
        public void TaskDeconstruct_IsInCatalog_HelpersOnly_AndHasVerb()
        {
            Assert.IsTrue(TaskDefCatalog.TryGet(ConstructionTaskDefs.DeconstructId, out var e, out var req));
            Assert.AreEqual(EligibilityPolicy.HelpersOnly, e);
            Assert.AreEqual(RequiredResource.None, req);
            Assert.AreEqual("Deconstructing Hut", ConstructionTaskDefs.VerbFor(ConstructionTaskDefs.DeconstructId, "Hut"));
        }

        [Test]
        public void Tuning_DefaultsAreHalf()
        {
            var t = new SimTuning();
            Assert.AreEqual(0.5, t.DeconstructTimeShare);
            Assert.AreEqual(0.5, t.DeconstructRefundShare);
        }

        [Test]
        public void Hut_Marked_OffersTasksOfRecipeMinutesTimesHalf()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 0, out int hut, out _);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(hut)).IsAccepted);
            w.Step(1);
            var tasks = w.Tasks.All.Where(t => t.Def.Id == ConstructionTaskDefs.DeconstructId && !t.IsFinished).ToList();
            Assert.AreEqual(4, tasks.Count);
            Assert.IsTrue(tasks.All(t => t.Def.Steps[0].DurationTicks == 480 && t.SiteFootprintId == hut));
        }

        [Test]
        public void Hut_FourHelpers_TakeItDown_FourLogsAppearInOneUnownedPile_AuditPasses()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 4, out int hut, out Operation op);
            w.Execute(new MarkForDeconstruction(hut));
            for (int i = 0; i < 400 && w.Footprints.Get(hut) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(hut));
            Assert.AreEqual(0, w.Deconstruction.Count);
            Assert.IsFalse(op.ModuleIds.Contains(hut));
            Assert.AreEqual(4, Unowned(w));
            Assert.AreEqual(1, w.Inventory.Locations.Count(l => l.Kind == LocationKind.GroundPile));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void Hut_ZeroHelpers_NothingHappens_ProgressStaysZero()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 0, out int hut, out _);
            w.Execute(new MarkForDeconstruction(hut));
            w.Step(100);
            Assert.IsNotNull(w.Footprints.Get(hut));
            Assert.AreEqual(0, w.Deconstruction.Get(hut).ProgressMinutes);
        }

        [Test]
        public void OxenStand_RefundsZeroLogs()
        {
            SimWorld w = Make(ModuleTypes.OxenStandId, 4, out int stand, out _);
            w.Execute(new MarkForDeconstruction(stand));
            for (int i = 0; i < 100 && w.Footprints.Get(stand) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(stand));
            Assert.AreEqual(0, Unowned(w));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void ZeroMinuteRecipe_IsRemovedAtOnce()
        {
            SimWorld w = Make(ModuleTypes.OxenStandId, 0, out int stand, out _);
            w.Tuning.DeconstructTimeShare = 0;
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(stand)).IsAccepted);
            Assert.IsNull(w.Footprints.Get(stand));
            Assert.AreEqual(0, w.Deconstruction.Count);
        }

        [Test]
        public void Storage_ContentsJoinTheRefundPile()
        {
            SimWorld w = Make(ModuleTypes.LogStorageId, 4, out int store, out _);
            w.Inventory.Produce(w.Inventory.GetOrAddStorage(store, 100), Items.Log, 7);
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(store)).IsAccepted);
            int recipe = ModuleTypes.LogStorage.ConstructionRecipe.Logs;
            for (int i = 0; i < 1000 && w.Footprints.Get(store) != null; i++) w.Step(1);
            Assert.IsNull(w.Footprints.Get(store));
            Assert.AreEqual(7 + recipe / 2, Unowned(w));
            Assert.AreEqual(1, w.Inventory.Locations.Count(l => l.Kind == LocationKind.GroundPile));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void CancelMidWork_CancelsTasks_AndModuleStays()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 2, out int hut, out _);
            w.Execute(new MarkForDeconstruction(hut));
            w.Step(20);
            Assert.IsTrue(w.Execute(new CancelDeconstruction(hut)).IsAccepted);
            w.Step(2);
            Assert.IsNotNull(w.Footprints.Get(hut));
            Assert.IsFalse(w.Tasks.All.Any(t => t.Def.Id == ConstructionTaskDefs.DeconstructId && !t.IsFinished));
        }

        [Test]
        public void RoundTrip_MidDeconstruction_ResumesAndFinishes()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 4, out int hut, out _);
            w.Execute(new MarkForDeconstruction(hut));
            w.Step(60);
            int progress = w.Deconstruction.Get(hut).ProgressMinutes;
            Assert.Greater(progress, 0);
            MapData map = Map();
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.AreEqual(progress, back.Deconstruction.Get(hut).ProgressMinutes);
            Assert.AreEqual(0.5, back.Tuning.DeconstructTimeShare);
            Assert.AreEqual(0.5, back.Tuning.DeconstructRefundShare);
            Assert.IsTrue(back.Tasks.All.Any(t => t.Def.Id == ConstructionTaskDefs.DeconstructId && !t.IsFinished));
            for (int i = 0; i < 400 && back.Footprints.Get(hut) != null; i++) back.Step(1);
            Assert.IsNull(back.Footprints.Get(hut));
            Assert.AreEqual(4, Unowned(back));
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void RoundTrip_CustomShares_AreSavedInCore()
        {
            SimWorld w = Make(ModuleTypes.WoodcuttingHutId, 0, out _, out _);
            w.Tuning.DeconstructTimeShare = 0.25;
            w.Tuning.DeconstructRefundShare = 0.75;
            MapData map = Map();
            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, map.MapId, map.ContentHash), map);
            Assert.AreEqual(0.25, back.Tuning.DeconstructTimeShare);
            Assert.AreEqual(0.75, back.Tuning.DeconstructRefundShare);
        }
    }
}
