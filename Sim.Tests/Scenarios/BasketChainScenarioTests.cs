using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Scenarios
{
    /// <summary>
    /// T-567, M19 end to end: from the start layout, a coppice hut, weaver and wattle yard are built and staffed by real
    /// commands; rods become baskets and panels, the yard collects both, a forager hut's kit gets a basket, and a forager
    /// carries 20 instead of 10. No shortcuts: every state is reached through commands and Sim ticks.
    /// </summary>
    [TestFixture]
    public class BasketChainScenarioTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData Map() =>
            new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static StartCitizen Adult(string n, Sex s) => new StartCitizen(n, s, AdultBirth);

        private static SimWorld Start(MapData map)
        {
            var w = SimWorld.FromMapData(map, new SimTuning
            {
                WorldSeed = 7, StartMonth = 6, StartDay = 1, StartHour = 6, StartMinute = 0,
                BushCount = 0, HazelStoolCount = 0, WillowStoolCount = 0, WorkshopDefaultReserve = 0,
                ForagerHutCapacity = 200, // 10-loads fill a 30 store before the basket arrives, which would cap a trip below 20
                BushBaskets = 25, // a trip plans from one bush, so a full bush must hold the 20 a basket allows
            });
            var fams = new List<StartFamily>();
            for (int i = 0; i < 6; i++)
                fams.Add(new StartFamily("F" + i, M(-9 + 3 * i, 8), new[] { Adult("A" + i, Sex.Male), Adult("B" + i, Sex.Female) }));
            StartLayoutApplier.Apply(w, new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0], fams, 2,
                stock: new[] { new StartStock(Items.Log, 40) }));
            return w;
        }

        private static int Place(SimWorld w, string type, double x, double z)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, M(x, z), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static void Check(SimWorld w) => Assert.IsNull(w.Inventory.Audit(), "audit at tick " + w.Tick);

        private static void StepAudited(SimWorld w, int ticks, System.Func<bool> stop = null)
        {
            for (int i = 0; i < ticks && (stop == null || !stop()); i++) { w.Step(1); Check(w); }
        }

        private static byte[] Bytes(SimWorld w, MapData map) => SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);

        private sealed class Run { public SimWorld W; public LedgerLocation Kit, ForagerStore; public Operation Forage; }

        private static Run Build(MapData map)
        {
            SimWorld w = Start(map);
            AreaShape coppiceArea = AreaShape.Straight(new[] { M(30, 20), M(60, 20), M(60, 40), M(30, 40) });
            CommandResult c = w.Execute(new DesignateOperation(coppiceArea, OperationTypes.CoppiceId,
                new List<ModuleSpec> { new ModuleSpec(ModuleTypes.CoppiceHutId, M(45, 30), 0) }));
            Assert.IsTrue(c.IsAccepted, c.Text);
            int coppice = (int)c.Created[1];
            int weaver = Place(w, ModuleTypes.WeaverId, 30, -10);
            int wattle = Place(w, ModuleTypes.WattleYardId, 45, -10);
            int yard = Place(w, ModuleTypes.StorageYardId, 15, -30);
            CommandResult f = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(-40, 30), M(-10, 30), M(-10, 50), M(-40, 50) }), AreaTypes.ForagingId));
            Assert.IsTrue(f.IsAccepted, f.Text);
            Operation forage = w.Operations.FindByArea((int)f.Created[0]);
            CommandResult fh = w.Execute(new PlaceModule(forage.Id, ModuleTypes.ForagerHutId, M(-25, 40), 0));
            Assert.IsTrue(fh.IsAccepted, fh.Text);
            int hut = (int)fh.Created[0];
            int[] all = { coppice, weaver, wattle, yard, hut };
            StepAudited(w, 6 * 1440, () => all.All(id => w.IsBuilt(id)));
            foreach (int id in all) Assert.IsTrue(w.IsBuilt(id), "built: " + id);

            // Stools and bushes, then staff each building with its own family (families 1..5).
            for (int i = 0; i < 6; i++)
            {
                w.Bushes.Add(35 + 4 * i, 25, BushKind.HazelStool, 10);
                w.Bushes.Add(35 + 4 * i, 35, BushKind.WillowStool, 10);
            }
            for (int i = 0; i < 4; i++) w.Bushes.Add(-35 + 4 * i, 35, BushKind.Berries, w.Tuning.BushBaskets);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(coppice).Id, 1)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(weaver).Id, 2)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(wattle).Id, 3)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(yard).Id, 4)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(forage.Id, 5)).IsAccepted); // kit fetch is made by the hut's own family
            return new Run
            {
                W = w, Forage = forage, Kit = KitStores.Find(w, hut), ForagerStore = w.Inventory.FindFoodStore(hut),
            };
        }

        [Test]
        public void StartLayout_RodsToBasketsToDoubledForage_AndRodsToPanelsToYard_AuditedEveryTick_SaveMidRunByteIdentical()
        {
            MapData map = Map();
            Run r = Build(map);
            SimWorld w = r.W;
            LedgerLocation yardStore = null;
            foreach (Footprint fp in w.Footprints.All)
                if (fp.Type.Id == ModuleTypes.StorageYardId) yardStore = w.Inventory.FindGoodsStore(fp.Id);
            Assert.NotNull(yardStore);

            // Run until panels sit in the yard and the forager hut kit holds a basket.
            StepAudited(w, 12 * 1440, () => yardStore.CountOf(Items.WattlePanel.Id) > 0 && r.Kit.CountOf(Items.Basket.Id) > 0);
            Assert.Greater(yardStore.CountOf(Items.WattlePanel.Id), 0, "panels reach the yard");
            Assert.Greater(r.Kit.CountOf(Items.Basket.Id), 0, "a basket reaches the forager hut kit");

            StepAudited(w, 300);

            // Save mid-run: byte-identical round trip, and the loaded world keeps running identically.
            byte[] first = Bytes(w, map);
            SimWorld back = SimSave.Read(first, map);
            CollectionAssert.AreEqual(first, Bytes(back, map), "save mid-run is byte-identical after a round trip");
            for (int i = 0; i < 400; i++) { w.Step(1); back.Step(1); Check(w); Check(back); }
            CollectionAssert.AreEqual(Bytes(w, map), Bytes(back, map), "original and loaded worlds stay identical");

            // A forager carried 20 with a basket.
            int max = 0;
            for (int i = 0; i < 6 * 1440 && max < 20; i++)
            {
                w.Step(1); Check(w);
                foreach (TaskRecord t in w.Tasks.All)
                    if (t.Def.Id == ForagingSystem.ForageTaskId)
                        foreach (TaskStep s in t.Def.Steps)
                            if (s.Kind == StepKind.DepositForage && s.Count > max) max = s.Count;
            }
            Assert.AreEqual(20, max, "a forager with a kit basket plans and carries 20 (normal carry is 10)");
        }
    }
}
