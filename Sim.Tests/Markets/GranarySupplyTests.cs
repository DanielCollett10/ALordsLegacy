using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>The granary "Supplies market" switch (T-458, MK-10..12, MKT-02, decision 0038).</summary>
    public class GranarySupplyTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        /// <summary>Flat map, a log stockpile, families 2..5 with adults and one pool ox (as in GranaryOperationTests).</summary>
        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0 });
            w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, 40);
            for (int fam = 2; fam <= 5; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            Ox ox = w.People.AddOx(50, "O", 40, 44);
            ox.State = OxState.Resting(40000, 44000);
            return (w, map);
        }

        /// <summary>A market at 10..30 m, clear of the granary at 50 m. Designated through the real command.</summary>
        private static void DesignateMarket(SimWorld w)
        {
            var shape = AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted, "market designated");
        }

        private static int PlaceGranary(SimWorld w, double x)
        {
            CommandResult r = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(x, 50), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
        }

        private static int OpOf(SimWorld w, int footprintId) => w.Operations.FindByModule(footprintId).Id;

        /// <summary>Reaches the built state only through PlaceStructure and Sim ticks (no SetState or Build shortcut).</summary>
        private static void BuildThroughTicks(SimWorld w, int id)
        {
            for (int i = 0; i < 6000 && !w.IsBuilt(id); i++) w.Step(1);
            Assert.IsTrue(w.IsBuilt(id), "granary built by workers through Sim ticks only");
        }

        /// <summary>Places and builds a granary through the real pipeline; returns its operation id.</summary>
        private static int BuiltGranary(SimWorld w, double x)
        {
            int id = PlaceGranary(w, x);
            BuildThroughTicks(w, id);
            return OpOf(w, id);
        }

        [Test]
        public void Refused_UnbuiltGranary_WithMarket_SaysHutNotBuiltYet()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = OpOf(w, PlaceGranary(w, 50));
            CommandResult r = w.Execute(new SetGranarySupplies(op, true));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual("Hut not built yet", r.Text);
            Assert.IsFalse(w.Operations.Get(op).SuppliesMarket);
        }

        [Test]
        public void Refused_BuiltGranary_WithNoMarket_SaysNoMarketYet()
        {
            var (w, _) = Make();
            int op = BuiltGranary(w, 50); // built through ticks, no market designated
            CommandResult r = w.Execute(new SetGranarySupplies(op, true));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual("No market yet", r.Text);
            Assert.IsFalse(w.Operations.Get(op).SuppliesMarket);
        }

        [Test]
        public void Refused_UnknownOperation_IsNotFound()
        {
            var (w, _) = Make();
            CommandResult r = w.Execute(new SetGranarySupplies(999, true));
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void BuiltGranaryAndMarket_ThroughPipeline_SwitchOnAndOff()
        {
            // Full pipeline: DesignateArea(market), PlaceStructure(granary), Step until built, SetGranarySupplies.
            var (w, _) = Make();
            DesignateMarket(w);
            int op = BuiltGranary(w, 50);
            Assert.IsFalse(w.Operations.Get(op).SuppliesMarket, "a new granary supplies nothing until switched on (MK-11)");
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, true)).IsAccepted);
            Assert.IsTrue(w.Operations.Get(op).SuppliesMarket);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, false)).IsAccepted);
            Assert.IsFalse(w.Operations.Get(op).SuppliesMarket);
        }

        [Test]
        public void SettingSameValue_IsAccepted_AndChangesNothing()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int op = BuiltGranary(w, 50);
            long before = w.Operations.Version;
            Assert.IsTrue(w.Execute(new SetGranarySupplies(op, false)).IsAccepted);
            Assert.AreEqual(before, w.Operations.Version);
        }

        [Test]
        public void ManyGranaries_MaySupplyTheSameMarket()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int a = BuiltGranary(w, 50);
            int b = BuiltGranary(w, 80);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(a, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(b, true)).IsAccepted);
            Assert.IsTrue(w.Operations.Get(a).SuppliesMarket);
            Assert.IsTrue(w.Operations.Get(b).SuppliesMarket);
        }

        [Test]
        public void ForagerHut_CannotSupplyTheMarket_IsRefused()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            var hut = new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ForagerHutId, M(100, 100), 0) };
            var square = AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) });
            CommandResult d = w.Execute(new DesignateOperation(square, OperationTypes.ForagingId, hut));
            Assert.IsTrue(d.IsAccepted, d.Text);
            int forager = OpOf(w, (int)d.Created[1]); // created ids: [0] area, [1] forager hut footprint (as ForagingTests)
            CommandResult r = w.Execute(new SetGranarySupplies(forager, true));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.AreEqual("Only a granary can supply the market", r.Text);
            Assert.IsFalse(w.Operations.Get(forager).SuppliesMarket);
        }

        [Test]
        public void WoodcuttingOperation_CannotSupplyTheMarket_IsRefused()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            var op = w.Operations.Create(OperationTypes.WoodcuttingId, 77);
            CommandResult r = w.Execute(new SetGranarySupplies(op.Id, true));
            Assert.AreEqual(CommandRejectCode.NotAllowed, r.Reason);
            Assert.IsFalse(op.SuppliesMarket);
        }

        [Test]
        public void RemovingTheMarket_ClearsEveryGranarysFlag()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int a = BuiltGranary(w, 50);
            int b = BuiltGranary(w, 80);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(a, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(b, true)).IsAccepted);
            int marketArea = w.Markets.Current.AreaId;
            Assert.IsTrue(w.Execute(new DeleteArea(marketArea)).IsAccepted);
            Assert.AreEqual(0, w.Markets.Count);
            Assert.IsFalse(w.Operations.Get(a).SuppliesMarket);
            Assert.IsFalse(w.Operations.Get(b).SuppliesMarket);
            CommandResult r = w.Execute(new SetGranarySupplies(a, true));
            Assert.AreEqual("No market yet", r.Text);
        }

        [Test]
        public void RemovingOneGranary_ClearsItsOwnFlag_AndLeavesTheOthers()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int a = BuiltGranary(w, 50);
            int b = BuiltGranary(w, 80);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(a, true)).IsAccepted);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(b, true)).IsAccepted);
            w.Tuning.DeconstructTimeShare = 0;
            int aFootprint = w.Operations.Get(a).ModuleIds.Single();
            Assert.IsTrue(w.Execute(new MarkForDeconstruction(aFootprint)).IsAccepted);
            Assert.IsNull(w.Operations.Get(a), "granary operation removed with its footprint");
            Assert.IsTrue(w.Operations.Get(b).SuppliesMarket, "the other granary keeps supplying");
            Assert.IsNotNull(w.Markets.Current, "market still stands");
        }

        [Test]
        public void SaveRoundTrip_SuppliesFlag_IsByteIdentical_AndKeepsFlagPerGranary()
        {
            var (w, map) = Make();
            DesignateMarket(w);
            int a = BuiltGranary(w, 50);
            int b = BuiltGranary(w, 80);
            Assert.IsTrue(w.Execute(new SetGranarySupplies(a, true)).IsAccepted);
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(first, map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.IsTrue(back.Operations.Get(a).SuppliesMarket);
            Assert.IsFalse(back.Operations.Get(b).SuppliesMarket);
        }

        [Test]
        public void SaveVersion_IsBumpedTo13_ForTheSuppliesFlag()
        {
            Assert.AreEqual(14, SaveWriter.CurrentVersion);
        }

        [Test]
        public void Registry_SetSuppliesMarket_BumpsVersionOnlyOnChange()
        {
            var (w, _) = Make();
            DesignateMarket(w);
            int a = BuiltGranary(w, 50);
            long v = w.Operations.Version;
            Assert.IsTrue(w.Operations.SetSuppliesMarket(a, true));
            Assert.AreEqual(v + 1, w.Operations.Version);
            Assert.IsFalse(w.Operations.SetSuppliesMarket(a, true));
            Assert.IsFalse(w.Operations.SetSuppliesMarket(999, true));
        }
    }
}
