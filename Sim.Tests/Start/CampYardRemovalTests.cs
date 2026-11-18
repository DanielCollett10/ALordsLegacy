using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Start
{
    /// <summary>Camp removal with and without a storage yard (ST-04, ST-30, ST-31, ST-32; T-546).</summary>
    public class CampYardRemovalTests
    {
        private const long Adult = -4_000_000;
        private const string Said = "The camp stays until a storage yard can take its goods";
        private static PointMm P(long x, long z) => new PointMm(x, z);
        private static readonly MapData Map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        private static SimWorld Make()
        {
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[]
                {
                    new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, Adult), new StartCitizen("B", Sex.Female, Adult) }),
                }, 2);
            var w = SimWorld.FromMapData(Map, new SimTuning { StartHour = 8 });
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static Operation BuildYardThroughTicks(SimWorld w)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.StorageYardId, PointMm.FromMetres(120, 130), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int id = (int)r.Created[0];
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "yard built through Sim ticks");
            return w.Operations.FindByModule(id);
        }

        private static void HouseEveryone(SimWorld w)
        {
            foreach (Family f in w.People.Families) f.HomeHouseId = 900 + f.Id;
            w.FoodHandedOver = true;
        }

        private static int LogCount(SimWorld w) => w.SettlementLog.Entries.Count(e => e.Text == Said);

        [Test]
        public void NoYard_GeneralGoodsHeld_CampStays_AndSaysSoOnce()
        {
            SimWorld w = Make();
            Assert.Greater(w.Camp.GeneralStoreLocation.Total, 0, "the camp starts with planks");
            HouseEveryone(w);

            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));

            Assert.IsNotNull(w.Camp);
            Assert.AreEqual(1, LogCount(w), "said once, not every day");
            Assert.IsTrue(w.CampStaysMessageSaid);
        }

        [Test]
        public void NoYard_NotDueYet_SaysNothing()
        {
            SimWorld w = Make(); // food not handed over, family homeless
            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.AreEqual(0, LogCount(w));
            Assert.IsFalse(w.CampStaysMessageSaid);
        }

        [Test]
        public void NoYard_EmptyGeneralStore_CampGoesWithoutTheMessage()
        {
            SimWorld w = Make();
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, w.Camp.GeneralStoreLocation.Total);
            HouseEveryone(w);
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.AreEqual(0, LogCount(w));
        }

        [Test]
        public void CampStaysMessageFlag_SurvivesSaveRoundTrip_SoItIsNotSaidAgain()
        {
            SimWorld w = Make();
            HouseEveryone(w);
            CampRemovalSystem.TryRemove(w);
            Assert.IsTrue(w.CampStaysMessageSaid);

            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, Map.MapId, Map.ContentHash), Map);
            Assert.IsTrue(back.CampStaysMessageSaid);
            CampRemovalSystem.TryRemove(back);
            Assert.AreEqual(1, LogCount(back));
        }

        [Test]
        public void CampStaysMessageFlag_FalseSurvivesSaveRoundTrip()
        {
            SimWorld w = Make();
            SimWorld back = SimSave.Read(SimSave.Write(w, "s", 1, Map.MapId, Map.ContentHash), Map);
            Assert.IsFalse(back.CampStaysMessageSaid);
        }

        [Test]
        public void WithYard_CampGoes_AndGeneralGoodsDropAsOneUnownedPile_ThenYardCollectsIt()
        {
            SimWorld w = Make();
            Operation yard = BuildYardThroughTicks(w);
            int planks = w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id);
            Assert.Greater(planks, 0);
            PointMm spot = w.Camp.Stockpile;
            int generalId = w.Camp.GeneralStoreLocation.Id;
            HouseEveryone(w);

            Assert.IsTrue(CampRemovalSystem.TryRemove(w));

            Assert.IsNull(w.Camp);
            Assert.IsNull(w.Inventory.Get(generalId));
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Plank.Id) > 0);
            Assert.IsNull(pile.OwnerOperationId);
            Assert.AreEqual(spot, pile.Position);
            Assert.AreEqual(planks, pile.CountOf(Items.Plank.Id));
            Assert.AreEqual(0, LogCount(w));
            Assert.IsNull(w.Inventory.Audit());
            Assert.IsNotNull(new YardPileSource().NextTrip(w, yard), "yard families collect the dropped pile (ST-41)");
        }

        [Test]
        public void WithYard_ReservationOnGeneralStore_BlocksRemovalUntilReleased()
        {
            SimWorld w = Make();
            BuildYardThroughTicks(w);
            HouseEveryone(w);
            LedgerLocation general = w.Camp.GeneralStoreLocation;
            int res = w.Inventory.ReserveStock(general, Items.Plank, 2, 555);
            Assert.Greater(res, 0);

            Assert.IsFalse(CampRemovalSystem.TryRemove(w));
            Assert.IsNotNull(w.Camp);

            Assert.IsTrue(w.Inventory.Release(res));
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
        }

        [Test]
        public void WithYard_EmptyGeneralStore_StoreClosesButCampStaysWhileOthersHomeless()
        {
            SimWorld w = Make();
            BuildYardThroughTicks(w);
            LedgerLocation general = w.Camp.GeneralStoreLocation;
            int id = general.Id;
            w.Inventory.Consume(general, Items.Plank, general.Total);

            Assert.IsFalse(CampRemovalSystem.TryRemove(w), "family still homeless");

            Assert.IsNotNull(w.Camp);
            Assert.IsNull(w.Camp.GeneralStoreLocation);
            Assert.IsNull(w.Inventory.Get(id));
            Assert.IsNull(w.Inventory.Audit());
        }

        [Test]
        public void NoYard_EmptyGeneralStore_StoreStaysOpen()
        {
            SimWorld w = Make();
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, w.Camp.GeneralStoreLocation.Total);
            CampRemovalSystem.TryRemove(w);
            Assert.IsNotNull(w.Camp.GeneralStoreLocation);
        }

        [Test]
        public void WithYard_ClosedStoreThenCampRemoval_Works()
        {
            SimWorld w = Make();
            BuildYardThroughTicks(w);
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, w.Camp.GeneralStoreLocation.Total);
            CampRemovalSystem.TryRemove(w);
            HouseEveryone(w);
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.IsNull(w.Camp);
        }

        private static SimWorld Reload(SimWorld w) => SimSave.Read(SimSave.Write(w, "s", 1, Map.MapId, Map.ContentHash), Map);

        private static void EmptyStoreAndRunToMidnight(SimWorld w)
        {
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, w.Camp.GeneralStoreLocation.Total);
            for (int i = 0; i < 2000 && w.Camp?.GeneralStoreLocation != null; i++) w.Step(1);
        }

        [Test]
        public void StoreClosedAfterLoad_SecondSaveLoads_WithNoGeneralStore()
        {
            SimWorld w = Make();
            BuildYardThroughTicks(w);
            SimWorld a = Reload(w);
            EmptyStoreAndRunToMidnight(a);
            Assert.IsNotNull(a.Camp);
            Assert.IsNull(a.Camp.GeneralStoreLocation, "closed at midnight");

            SimWorld b = null;
            Assert.DoesNotThrow(() => b = Reload(a));
            Assert.IsNull(b.Camp.GeneralStoreLocation);
        }

        [Test]
        public void CampRemovedRightAfterLoad_WithYard_SavesAndLoads()
        {
            SimWorld w = Make();
            BuildYardThroughTicks(w);
            SimWorld a = Reload(w);
            HouseEveryone(a);
            Assert.IsTrue(CampRemovalSystem.TryRemove(a));
            SimWorld b = null;
            Assert.DoesNotThrow(() => b = Reload(a));
            Assert.IsNull(b.Camp);
        }

        [Test]
        public void SaveVersion_IsAtLeast33()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 33);
        }
    }
}
