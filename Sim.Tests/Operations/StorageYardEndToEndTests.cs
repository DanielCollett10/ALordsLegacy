using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>
    /// The whole M18 loop from the start layout through real commands and ticks (T-548, ST-*, PL-30..34): a yard is placed
    /// and built by workers, a family staffs it, the camp's 10 planks and a sawpit's planks move into it, a stall is built
    /// from yard planks, the hewing reserve holds logs back, and the emptied camp goes. The one shortcut is housing
    /// (HomeHouseId and FoodHandedOver are set directly, as in CampYardRemovalTests); the workshop stores are filled by hand
    /// only where a test says so.
    /// </summary>
    public class StorageYardEndToEndTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static MapData _map;

        private sealed class Fx
        {
            public SimWorld W;
            public int Yard, YardOp, Sawpit, SawOp, Hewing, HewOp;
            public LedgerLocation YardStore => W.Inventory.FindGoodsStore(Yard);
            public LedgerLocation SawStore => W.Inventory.FindGoodsStore(Sawpit);
        }

        private static SimWorld NewWorld(int logs, int families)
        {
            _map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(_map, new SimTuning { StartHour = 8, BushCount = 0, StartingMeat = 400 });
            StartLayoutApplier.Apply(w, new StartLayout(M(150, 150), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2, stock: new[] { new StartStock(Items.Log, logs) }));
            Start.NoCampTests.UseHouseGoodsUp(w); // T-577: these tests count planks only
            for (int fam = 1; fam <= families; fam++)
            {
                Family f = w.People.AddFamily(fam, "F" + fam, 150, 150);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, Adult, fam, 150, 150);
                w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 60);
            }
            Assert.AreEqual(10, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id), "the 10 starting planks");
            return w;
        }

        private static int Place(SimWorld w, string type, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, at, 0));
            Assert.IsTrue(r.IsAccepted, type + ": " + r.Text);
            return (int)r.Created[0];
        }

        /// <summary>Steps minute by minute, auditing the ledger on every tick.</summary>
        private static void Run(SimWorld w, System.Func<bool> done, int maxMinutes, string what)
        {
            for (int i = 0; i < maxMinutes && !done(); i++)
            {
                w.Step(1);
                Assert.IsNull(w.Inventory.Audit(), what + " audit at tick " + w.Tick);
            }
            Assert.IsTrue(done(), what + " (day " + w.Tick / 1440 + ")");
        }

        private static void RunFor(SimWorld w, int minutes)
        {
            for (int i = 0; i < minutes; i++)
            {
                w.Step(1);
                Assert.IsNull(w.Inventory.Audit(), "audit at tick " + w.Tick);
            }
        }

        private static Fx PlaceAndBuild(SimWorld w, bool yard, bool sawpit, bool hewing)
        {
            var fx = new Fx { W = w };
            var ids = new System.Collections.Generic.List<int>();
            if (yard) ids.Add(fx.Yard = Place(w, ModuleTypes.StorageYardId, M(100, 60)));
            if (sawpit) ids.Add(fx.Sawpit = Place(w, ModuleTypes.SawpitId, M(50, 150)));
            if (hewing) ids.Add(fx.Hewing = Place(w, ModuleTypes.HewingStationId, M(50, 120)));
            Run(w, () => ids.All(w.IsBuilt), 10 * 1440, "everything built by workers");
            if (yard) fx.YardOp = w.Operations.FindByModule(fx.Yard).Id;
            if (sawpit) fx.SawOp = w.Operations.FindByModule(fx.Sawpit).Id;
            if (hewing) fx.HewOp = w.Operations.FindByModule(fx.Hewing).Id;
            return fx;
        }

        private static void Assign(SimWorld w, int op, int family) => Assert.IsTrue(w.Execute(new AssignFamily(op, family)).IsAccepted);

        private static void HouseEveryone(SimWorld w)
        {
            foreach (Family f in w.People.Families) f.HomeHouseId = 900 + f.Id;
            w.FoodHandedOver = true;
        }

        [Test]
        public void CampPlanksReachTheYard_SawpitLonePlankCollected_AStallTakesPlanksFromTheYard()
        {
            SimWorld w = NewWorld(100, 4);
            Fx fx = PlaceAndBuild(w, true, true, false);
            int granary = Place(w, ModuleTypes.GranaryId, M(60, 50));
            Run(w, () => w.IsBuilt(granary), 6 * 1440, "granary built");
            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            Assert.AreEqual(10, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id), "nothing built so far used a plank");

            Assign(w, fx.YardOp, 1);
            Run(w, () => fx.YardStore.CountOf(Items.Plank.Id) == 10, 3 * 1440, "the 10 camp planks reach the yard");
            Assert.AreEqual(0, w.Camp.GeneralStoreLocation?.CountOf(Items.Plank.Id) ?? 0);

            Assign(w, fx.SawOp, 2); // only a working building is collected from
            LedgerLocation saw = fx.SawStore;
            w.Inventory.Produce(saw, Items.Plank, 1);
            Run(w, () => fx.YardStore.CountOf(Items.Plank.Id) == 11, 2 * 1440, "a single plank in the sawpit is collected into the yard (ST-42: any free output)");
            Assert.AreEqual(0, saw.CountOf(Items.Plank.Id), "the sawpit is emptied");

            int yardBefore = fx.YardStore.CountOf(Items.Plank.Id);
            Assign(w, w.Operations.FindByModule(granary).Id, 3); // family 4 stays free to build the stall
            Assert.IsTrue(w.Execute(new SetGranarySupplies(w.Operations.FindByModule(granary).Id, true)).IsAccepted);
            Run(w, () => w.Markets.Current != null && w.Markets.Current.StallFootprintIds.Any(w.IsBuilt), 15 * 1440, "a stall is built");
            Assert.AreEqual(2, w.Inventory.ConsumedOf(Items.Plank.Id), "one stall = 2 planks");
            Assert.AreEqual(yardBefore - 2, fx.YardStore.CountOf(Items.Plank.Id), "the stall's planks came out of the yard");
        }

        [Test]
        public void DefaultReserve_ChainKeepsProducing_PlanksEndUpInTheYard()
        {
            SimWorld w = NewWorld(60, 5);
            Fx fx = PlaceAndBuild(w, true, true, true);
            Assert.AreEqual(w.Tuning.WorkshopDefaultReserve, InputReserve.ReserveOf(w, w.Operations.Get(fx.HewOp), Items.Log));
            Assign(w, fx.YardOp, 1);
            Assign(w, fx.HewOp, 2);
            Assign(w, fx.SawOp, 3);
            int logsBefore = w.Inventory.ConsumedOf(Items.Log.Id);
            Run(w, () => w.Inventory.ConsumedOf(Items.Beam.Id) > 0 && fx.YardStore.CountOf(Items.Plank.Id) > 10, 20 * 1440,
                "logs become beams become planks that reach the yard");
            Assert.Greater(w.Inventory.ConsumedOf(Items.Log.Id), logsBefore, "the hewers consumed logs above the reserve");
            Assert.GreaterOrEqual(GoodsStats.Logs(w), w.Tuning.WorkshopDefaultReserve, "the reserve of logs stays in storage");
        }

        [Test]
        public void HewingReserve60_With66Logs_FetchesExactlySix_ThenWaits()
        {
            SimWorld w = NewWorld(66 + 6, 2); // 6 logs build the hewing station
            Fx fx = PlaceAndBuild(w, false, false, true);
            LedgerLocation stockpile = w.Camp.StockpileLocation;
            Assert.AreEqual(66, stockpile.CountOf(Items.Log.Id), "66 logs in storage once the station is built");
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(fx.HewOp, Items.Log.Id, 60)).IsAccepted);
            Assign(w, fx.HewOp, 1);
            int before = w.Inventory.ConsumedOf(Items.Log.Id);
            RunFor(w, 6 * 1440);
            Assert.AreEqual(60, stockpile.CountOf(Items.Log.Id), "exactly 6 fetched");
            LedgerLocation stock = w.Inventory.FindWorkshopStock(fx.Hewing);
            Assert.Greater(w.Inventory.ConsumedOf(Items.Log.Id) - before, 0, "the fetched logs were hewn");
            Assert.AreEqual(0, stock.CountOf(Items.Log.Id), "fetched work is always processed");
            Assert.Greater(w.Inventory.FindGoodsStore(fx.Hewing).CountOf(Items.Beam.Id), 0);
            RunFor(w, 2 * 1440);
            Assert.AreEqual(60, stockpile.CountOf(Items.Log.Id), "and no more are fetched while storage sits at the reserve");
            Assert.AreEqual("Waiting: 60 / 60 logs in storage", WorkshopReport.Of(w, fx.Hewing).StatusText);
        }

        [Test]
        public void CampGoes_OnceHousedAndFoodHandedOver_AndLeftoverPlanksAreCollected()
        {
            SimWorld w = NewWorld(60, 2);
            Fx fx = PlaceAndBuild(w, true, false, false);
            HouseEveryone(w);
            Run(w, () => w.Camp == null, 2 * 1440, "camp removed at midnight");
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Plank.Id) > 0);
            Assert.IsNull(pile.OwnerOperationId);
            Assert.AreEqual(10, pile.CountOf(Items.Plank.Id), "the camp's planks dropped as one unowned pile");

            Assign(w, fx.YardOp, 1);
            Run(w, () => fx.YardStore.CountOf(Items.Plank.Id) == 10, 3 * 1440, "the leftover planks are collected into the yard");
            Assert.AreEqual(0, w.Inventory.Locations.Where(l => l.Kind == LocationKind.GroundPile).Sum(l => l.CountOf(Items.Plank.Id)));
        }

        [Test]
        public void SaveMidRun_ReloadedWorldIsByteIdenticalAfterTwoMoreDays()
        {
            SimWorld w = NewWorld(60, 5);
            Fx fx = PlaceAndBuild(w, true, true, true);
            Assign(w, fx.YardOp, 1);
            Assign(w, fx.HewOp, 2);
            Assign(w, fx.SawOp, 3);
            RunFor(w, 3 * 1440 + 600);
            byte[] saved = SimSave.Write(w, "s", 1, _map.MapId, _map.ContentHash);
            SimWorld back = SimSave.Read(saved, _map);
            Assert.IsNull(SaveDiff.FirstDifference(saved, SimSave.Write(back, "s", 1, _map.MapId, _map.ContentHash)));
            RunFor(w, 2 * 1440);
            RunFor(back, 2 * 1440);
            Assert.IsNull(SaveDiff.FirstDifference(SimSave.Write(w, "s", 1, _map.MapId, _map.ContentHash),
                SimSave.Write(back, "s", 1, _map.MapId, _map.ContentHash)));
        }
    }
}
