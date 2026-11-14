using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class LedgerSectionTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return (SimWorld.FromMapData(map, new SimTuning { StartHour = 8 }), map);
        }

        private static SimWorld RoundTrip(SimWorld w, MapData map)
        {
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            var back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash), "second write is identical");
            Assert.IsNull(back.Inventory.Audit(), "audit after load");
            return back;
        }

        private static void AssertSameLedger(Ledger a, Ledger b)
        {
            var la = a.Locations.ToList();
            var lb = b.Locations.ToList();
            Assert.AreEqual(la.Count, lb.Count);
            for (int i = 0; i < la.Count; i++)
            {
                Assert.AreEqual(la[i].Id, lb[i].Id);
                Assert.AreEqual(la[i].Kind, lb[i].Kind);
                Assert.AreEqual(la[i].Position, lb[i].Position);
                Assert.AreEqual(la[i].OwnerOperationId, lb[i].OwnerOperationId);
                Assert.AreEqual(la[i].ModuleId, lb[i].ModuleId);
                Assert.AreEqual(la[i].Capacity, lb[i].Capacity);
                Assert.AreEqual(la[i].CountOf(Items.Log.Id), lb[i].CountOf(Items.Log.Id));
                Assert.AreEqual(la[i].ReservedOf(Items.Log.Id), lb[i].ReservedOf(Items.Log.Id));
                Assert.AreEqual(la[i].IncomingOf(Items.Log.Id), lb[i].IncomingOf(Items.Log.Id));
            }
            var ra = a.Reservations.ToList();
            var rb = b.Reservations.ToList();
            Assert.AreEqual(ra.Count, rb.Count);
            for (int i = 0; i < ra.Count; i++)
            {
                Assert.AreEqual(ra[i].Id, rb[i].Id);
                Assert.AreEqual(ra[i].Kind, rb[i].Kind);
                Assert.AreEqual(ra[i].LocationId, rb[i].LocationId);
                Assert.AreEqual(ra[i].ItemId, rb[i].ItemId);
                Assert.AreEqual(ra[i].Count, rb[i].Count);
                Assert.AreEqual(ra[i].Owner, rb[i].Owner);
            }
            Assert.AreEqual(a.ConsumedOf(Items.Log.Id), b.ConsumedOf(Items.Log.Id));
            Assert.AreEqual(a.TotalOf(Items.Log), b.TotalOf(Items.Log));
        }

        [Test]
        public void EmptyLedger_RoundTrips()
        {
            var (w, map) = Make();
            SimWorld back = RoundTrip(w, map);
            AssertSameLedger(w.Inventory, back.Inventory);
        }

        [Test]
        public void LogsAtSite_OnOxMidDelivery_AndInSiteOwnedSunsetPile_RoundTripWithReservations()
        {
            var (w, map) = Make();
            Ledger led = w.Inventory;
            LedgerLocation stock = led.AddStockpile(M(10, 10));
            led.Produce(stock, Items.Log, 20);
            LedgerLocation site = led.GetOrAddSite(7);
            LedgerLocation ox = led.AddCarrier(4);
            LedgerLocation store = led.GetOrAddStorage(5, 30);
            // Logs already delivered at the site, part consumed.
            Assert.IsTrue(led.Transfer(stock, site, Items.Log, 4));
            Assert.IsTrue(led.Consume(site, Items.Log, 1));
            // Delivery mid-way: 3 logs on the ox, 2 more reserved at the stockpile, room reserved at the site.
            Assert.IsTrue(led.Transfer(stock, ox, Items.Log, 3));
            int stockRes = led.ReserveStock(stock, Items.Log, 2, 101);
            int capRes = led.ReserveCapacity(store, Items.Log, 5, 102);
            int oxRes = led.ReserveStock(ox, Items.Log, 3, 103);
            int siteRes = led.ReserveCapacity(site, Items.Log, 3, 103);
            // A sunset pile owned by the site (owner = footprint id 7) on the ground.
            LedgerLocation pile = led.ProduceOnGround(M(33, 44), 7, Items.Log, 2);
            Assert.IsNull(led.Audit());

            SimWorld back = RoundTrip(w, map);
            Ledger b = back.Inventory;
            AssertSameLedger(led, b);
            Assert.AreEqual(3, b.Get(ox.Id).CountOf(Items.Log.Id));
            Assert.AreEqual(LocationKind.Carrier, b.Get(ox.Id).Kind);
            Assert.AreEqual(4, b.Get(ox.Id).Capacity);
            Assert.AreEqual(3, b.Get(site.Id).CountOf(Items.Log.Id));
            Assert.AreEqual(7, b.FindSite(7).ModuleId);
            Assert.AreEqual(7, b.Get(pile.Id).OwnerOperationId);
            Assert.AreEqual(M(33, 44), b.Get(pile.Id).Position);
            Assert.AreEqual(2, b.GetReservation(stockRes).Count);
            Assert.AreEqual(ReservationKind.Capacity, b.GetReservation(capRes).Kind);
            Assert.AreEqual(102, b.GetReservation(capRes).Owner);
            Assert.AreEqual(oxRes, b.GetReservation(oxRes).Id);
            Assert.AreEqual(3, b.GetReservation(siteRes).Count);
            Assert.AreEqual(1, b.ConsumedOf(Items.Log.Id));
            Assert.AreEqual(30, b.FindStorage(5).Capacity);
            Assert.AreEqual(int.MaxValue, b.Get(stock.Id).Capacity);

            // Id counters carry on: new ids do not collide and are the same as in the original.
            int nextLoc = led.AddStockpile().Id;
            Assert.AreEqual(nextLoc, b.AddStockpile().Id);
            Assert.AreEqual(led.ReserveStock(stock, Items.Log, 1, 9), b.ReserveStock(b.Get(stock.Id), Items.Log, 1, 9));
        }

        [Test]
        public void LoadedLedger_BehavesLikeOriginal_ReleaseAndTransferWork()
        {
            var (w, map) = Make();
            Ledger led = w.Inventory;
            LedgerLocation a = led.AddStockpile();
            LedgerLocation c = led.AddCarrier(5);
            led.Produce(a, Items.Log, 5);
            int s = led.ReserveStock(a, Items.Log, 4, 1);
            int r = led.ReserveCapacity(c, Items.Log, 4, 1);
            SimWorld back = RoundTrip(w, map);
            Ledger b = back.Inventory;
            Assert.IsTrue(b.Transfer(b.Get(a.Id), b.Get(c.Id), Items.Log, 4, 1));
            Assert.IsNull(b.GetReservation(s));
            Assert.IsNull(b.GetReservation(r));
            Assert.IsNull(b.Audit());
        }

        [Test]
        public void CampStockpile_IsReattachedAfterLoad()
        {
            var (w, map) = Make();
            w.Footprints.Add(1, LordsLegacy.Sim.Footprints.FootprintTypeDef.Camp, M(50, 50), 0);
            LedgerLocation loc = w.Inventory.AddStockpile(M(52, 52));
            w.Inventory.Produce(loc, Items.Log, 6);
            w.Camp = new LordsLegacy.Sim.Start.CampInfo(1, M(51, 51), M(52, 52), M(53, 53), loc);
            SimWorld back = RoundTrip(w, map);
            Assert.IsNotNull(back.Camp.StockpileLocation);
            Assert.AreSame(back.Inventory.Get(loc.Id), back.Camp.StockpileLocation);
            Assert.AreEqual(6, back.Camp.StockpileLocation.CountOf(Items.Log.Id));
        }

        [Test]
        public void CampWithMissingStockpileLocation_IsRejected()
        {
            var (w, map) = Make();
            w.Footprints.Add(1, LordsLegacy.Sim.Footprints.FootprintTypeDef.Camp, M(50, 50), 0);
            w.Camp = new LordsLegacy.Sim.Start.CampInfo(1, M(51, 51), M(52, 52), M(53, 53));
            w.Camp.StockpileLocationId = 99;
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }

        [Test]
        public void JournalThatDisagreesWithCounts_IsRejectedByAudit()
        {
            var (w, map) = Make();
            Ledger led = w.Inventory;
            led.Produce(led.AddStockpile(), Items.Log, 3);
            led.RestoreJournalAndCounters(new[] { new System.Collections.Generic.KeyValuePair<string, int>(Items.Log.Id, 9) },
                new System.Collections.Generic.KeyValuePair<string, int>[0], led.NextLocationId, led.NextReservationIdValue);
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }
    }
}
