using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>WorkshopDef, its tuning and the WorkshopStock location (T-520, decision 0047).</summary>
    public class WorkshopDefTests
    {
        private static MapData Flat() =>
            new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);

        [Test]
        public void Defaults_MatchDecision0047()
        {
            var t = new SimTuning();
            WorkshopDef h = WorkshopDef.Hewing(t);
            Assert.AreSame(Items.Log, h.Input); Assert.AreSame(Items.Beam, h.Output);
            Assert.AreEqual(new[] { 1, 3, 6, 20, 1 },
                new[] { h.OutputsPerInput, h.InputsPerDayPerFamily, h.InputStockCapacity, h.OutputCapacity, h.OxLoadForInput });
            WorkshopDef s = WorkshopDef.Sawing(t);
            Assert.AreSame(Items.Beam, s.Input); Assert.AreSame(Items.Plank, s.Output);
            Assert.AreEqual(new[] { 3, 3, 6, 20, 1 },
                new[] { s.OutputsPerInput, s.InputsPerDayPerFamily, s.InputStockCapacity, s.OutputCapacity, s.OxLoadForInput });
            Assert.AreEqual(240, t.WorkshopShiftMinutes);
        }

        [Test]
        public void ChangingTuning_ChangesTheDefs()
        {
            var t = new SimTuning
            {
                HewingBeamsPerLog = 2, HewingLogsPerDay = 4, HewingStockCapacity = 7, HewingStoreCapacity = 21, OxLoadLogs = 2,
                SawingPlanksPerBeam = 4, SawingBeamsPerDay = 5, SawingStockCapacity = 8, SawingStoreCapacity = 22, OxLoadBeams = 3,
            };
            WorkshopDef h = WorkshopDef.Hewing(t);
            Assert.AreEqual(new[] { 2, 4, 7, 21, 2 },
                new[] { h.OutputsPerInput, h.InputsPerDayPerFamily, h.InputStockCapacity, h.OutputCapacity, h.OxLoadForInput });
            WorkshopDef s = WorkshopDef.Sawing(t);
            Assert.AreEqual(new[] { 4, 5, 8, 22, 3 },
                new[] { s.OutputsPerInput, s.InputsPerDayPerFamily, s.InputStockCapacity, s.OutputCapacity, s.OxLoadForInput });
        }

        [Test]
        public void Def_RejectsZeroCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WorkshopDef.Hewing(new SimTuning { HewingLogsPerDay = 0 }));
        }

        [Test]
        public void WorkshopStock_RefusesAnyItemButItsInput_AndCapacityHolds()
        {
            var led = new Ledger();
            LedgerLocation stock = led.AddWorkshopStock(7, Items.Log, 6);
            Assert.Throws<InvalidOperationException>(() => led.Produce(stock, Items.Beam, 1));
            Assert.Throws<InvalidOperationException>(() => led.Produce(stock, Items.Meat, 1));
            Assert.AreEqual(0, led.ReserveCapacity(stock, Items.Plank, 1, 1));
            led.Produce(stock, Items.Log, 6);
            Assert.Throws<InvalidOperationException>(() => led.Produce(stock, Items.Log, 1));
            Assert.AreSame(stock, led.FindWorkshopStock(7));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void WorkshopStock_RefusesTransferOfOtherItems()
        {
            var led = new Ledger();
            LedgerLocation stock = led.AddWorkshopStock(1, Items.Beam, 6);
            LedgerLocation goods = led.AddGoodsStore(10);
            led.Produce(goods, Items.Plank, 2);
            led.Produce(goods, Items.Beam, 2);
            Assert.IsFalse(led.Transfer(goods, stock, Items.Plank, 1));
            Assert.IsTrue(led.Transfer(goods, stock, Items.Beam, 2));
        }

        [Test]
        public void ConstructionSources_NeverReturnsWorkshopStock()
        {
            SimWorld w = SimWorld.FromMapData(Flat(), new SimTuning { StartHour = 8 });
            int logsBefore = ConstructionSources.Available(w, Items.Log);
            int beamsBefore = ConstructionSources.Available(w, Items.Beam);
            LedgerLocation logStock = w.Inventory.AddWorkshopStock(5, Items.Log, 6);
            LedgerLocation beamStock = w.Inventory.AddWorkshopStock(6, Items.Beam, 6);
            w.Inventory.Produce(logStock, Items.Log, 6);
            w.Inventory.Produce(beamStock, Items.Beam, 6);
            Assert.AreEqual(logsBefore, ConstructionSources.Available(w, Items.Log));
            Assert.AreEqual(beamsBefore, ConstructionSources.Available(w, Items.Beam));
            Assert.IsNull(ConstructionSources.TryReserve(w, Items.Beam, beamsBefore + 1, new PointMm(0, 0), 1));
            Assert.AreEqual(0, logStock.ReservedOf(Items.Log.Id));
        }

        [Test]
        public void VillageTotals_LeaveOutWorkshopStock()
        {
            var led = new Ledger();
            led.Produce(led.AddStockpile(), Items.Log, 3);
            led.Produce(led.AddWorkshopStock(2, Items.Log, 6), Items.Log, 4);
            Assert.AreEqual(3, led.TotalOf(Items.Log));
            Assert.IsNull(led.Audit());
        }

        [Test]
        public void SaveRoundTrip_KeepsWorkshopStock_AndNewTuning()
        {
            MapData map = Flat();
            var t = new SimTuning { StartHour = 8, HewingLogsPerDay = 5, SawingPlanksPerBeam = 4, OxLoadBeams = 2, WorkshopShiftMinutes = 300 };
            SimWorld w = SimWorld.FromMapData(map, t);
            LedgerLocation stock = w.Inventory.AddWorkshopStock(9, Items.Beam, 6);
            w.Inventory.Produce(stock, Items.Beam, 4);
            w.Inventory.ReserveCapacity(stock, Items.Beam, 2, 3);

            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            LedgerLocation l = back.Inventory.Get(stock.Id);
            Assert.AreEqual(LocationKind.WorkshopStock, l.Kind);
            Assert.AreEqual(Items.Beam.Id, l.InputItemId);
            Assert.AreEqual(6, l.Capacity);
            Assert.AreEqual(4, l.CountOf(Items.Beam.Id));
            Assert.AreEqual(2, l.IncomingOf(Items.Beam.Id));
            Assert.Throws<InvalidOperationException>(() => back.Inventory.Produce(l, Items.Plank, 1));
            Assert.AreEqual(5, back.Tuning.HewingLogsPerDay);
            Assert.AreEqual(4, back.Tuning.SawingPlanksPerBeam);
            Assert.AreEqual(2, back.Tuning.OxLoadBeams);
            Assert.AreEqual(300, back.Tuning.WorkshopShiftMinutes);
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void SaveVersion_IsBumpedTo28()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 28);
            byte[] bytes = { (byte)'L', (byte)'L', (byte)'S', (byte)'V', 27, 0, 0, 0, 0, 0, 0, 0 };
            var e = Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes));
            StringAssert.Contains("Unknown save format version 27", e.Message);
        }
    }
}
