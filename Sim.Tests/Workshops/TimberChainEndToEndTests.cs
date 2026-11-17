using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>
    /// The timber chain through real commands and ticks only (T-525, PL-05, PL-10, PL-16): a woodcutter fells logs, an ox
    /// carries them to the hewing station, the hewn beams go to the sawpit, and the sawn planks are carried to the sixth
    /// market stall once the 10 starting planks have gone into the first five. No Build() or SetState shortcut: the only
    /// set-up shortcut is the start stock of logs and the families.
    /// </summary>
    public class TimberChainEndToEndTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        [Test]
        public void LogsBecomeBeamsBecomePlanks_AndTheSixthStallIsBuilt_AuditCleanThroughout()
        {
            var trees = new List<MapTree>();
            int n = 1;
            for (double x = 92; x <= 128; x += 4)
                for (double z = 92; z <= 108; z += 4) trees.Add(new MapTree(new TreeId(n++), (float)x, (float)z, 16));
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), trees.ToArray());
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartingMeat = 400 });
            // The builds reserve 44 logs. A few spare logs let a site that loses a carried load at sunset (the load is dropped
            // on a pile owned by the site) reserve again; the woodcutter feeds the hewing station after that.
            var stock = new[] { new StartStock(Items.Log, 8 + 6 + 6 + 3 * 8 + 4) };
            StartLayoutApplier.Apply(w, new StartLayout(M(150, 150), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2, stock: stock));
            Assert.AreEqual(10, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id), "the 10 starting planks");

            for (int fam = 1; fam <= 11; fam++) // 1 woodcutter, 2 hewing, 3 sawpit, 4-9 granaries, 10-11 odd-jobbers who build the stalls
            {
                Family f = w.People.AddFamily(fam, "F" + fam, 150, 150);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, Adult, fam, 150, 150);
                w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 60);
            }

            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);
            var mods = new List<ModuleSpec>
            {
                new ModuleSpec(ModuleTypes.WoodcuttingHutId, M(100, 85), 0),
                new ModuleSpec(ModuleTypes.LogStorageId, M(120, 85), 0),
            };
            CommandResult cut = w.Execute(new DesignateOperation(
                AreaShape.Straight(new[] { M(88, 78), M(132, 78), M(132, 112), M(88, 112) }), OperationTypes.WoodcuttingId, mods));
            Assert.IsTrue(cut.IsAccepted, cut.Text);
            int cutHut = (int)cut.Created[1];
            int cutOp = w.Operations.FindByArea((int)cut.Created[0]).Id;
            int hewing = Place(w, ModuleTypes.HewingStationId, M(50, 120));
            int sawpit = Place(w, ModuleTypes.SawpitId, M(50, 150));
            int[] granaries = { Place(w, ModuleTypes.GranaryId, M(60, 50)), Place(w, ModuleTypes.GranaryId, M(90, 50)), Place(w, ModuleTypes.GranaryId, M(120, 50)) };

            int[] all = new[] { cutHut, hewing, sawpit }.Concat(granaries).ToArray();
            Step(w, () => all.All(w.IsBuilt), 12 * 1440, "everything built by workers");


            Assert.IsTrue(w.Execute(new AssignFamily(cutOp, 1)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(hewing).Id, 2)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(w.Operations.FindByModule(sawpit).Id, 3)).IsAccepted);
            for (int g = 0; g < 3; g++)
            {
                int op = w.Operations.FindByModule(granaries[g]).Id;
                Assert.IsTrue(w.Execute(new AssignFamily(op, 4 + 2 * g)).IsAccepted);
                Assert.IsTrue(w.Execute(new AssignFamily(op, 5 + 2 * g)).IsAccepted);
                CommandResult sup = w.Execute(new SetGranarySupplies(op, true));
                Assert.IsTrue(sup.IsAccepted, sup.Text);
            }
            int logsBefore = w.Inventory.ConsumedOf(Items.Log.Id);

            int stallsFromStart = -1;
            WorkshopReport hr = null;
            bool sawBeams = false, sawPlanks = false;
            int ticks = 0;
            var stalls = new List<int>();
            while (ticks++ < 40 * 1440)
            {
                w.Step(1);
                if (ticks % 5 == 0) Assert.IsNull(w.Inventory.Audit(), "tick " + ticks);
                stalls = w.Markets.Current.StallFootprintIds.Where(w.IsBuilt).ToList();
                if (stallsFromStart < 0 && w.Camp != null && w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id) == 0 && stalls.Count + w.Markets.Current.StallFootprintIds.Count > 0)
                    stallsFromStart = w.Markets.Current.StallFootprintIds.Count;
                hr = WorkshopReport.Of(w, hewing);
                if (hr.OutputCount > 0) sawBeams = true;
                if (WorkshopReport.Of(w, sawpit).OutputCount > 0) sawPlanks = true;
                if (stalls.Count >= 6) break;
            }
            Assert.IsNull(w.Inventory.Audit());
            Assert.AreEqual(6, stalls.Count, "six stalls built (day " + ticks / 1440 + ", planks " + GoodsStats.Planks(w) + ", beams "
                + GoodsStats.Beams(w) + ", logs " + GoodsStats.Logs(w) + ", sawpit " + WorkshopReport.Of(w, sawpit).StatusText + ")");
            Assert.IsTrue(sawBeams, "the hewing station made beams from logs");
            Assert.IsTrue(sawPlanks, "the sawpit made planks from beams");
            Assert.AreEqual(5, stallsFromStart, "the 10 starting planks went into 5 stalls before any sawn plank was used");
            Assert.AreEqual(12, w.Inventory.ConsumedOf(Items.Plank.Id), "6 stalls x 2 planks: 10 starting + 2 sawn");
            Assert.Greater(w.Inventory.ConsumedOf(Items.Beam.Id), 0, "beams were consumed by the sawpit");
            Assert.Greater(w.Inventory.ConsumedOf(Items.Log.Id), logsBefore, "the hewing station consumed logs");
            Assert.IsFalse(w.People.Families.Any(f => f.IsHungry));
        }

        private static int Place(SimWorld w, string type, PointMm at)
        {
            CommandResult r = w.Execute(new PlaceStructure(type, at, 0));
            Assert.IsTrue(r.IsAccepted, type + ": " + r.Text);
            return (int)r.Created[0];
        }

        private static void Step(SimWorld w, System.Func<bool> done, int max, string what)
        {
            for (int i = 0; i < max && !done(); i++)
            {
                w.Step(1);
                if (i % 50 == 0) Assert.IsNull(w.Inventory.Audit(), what + " tick " + i);
            }
            Assert.IsTrue(done(), what + " (tick " + w.Tick + ", unbuilt " + string.Join(",", w.Footprints.All.Where(f => !w.IsBuilt(f.Id)).Select(f => f.Type.Id + "#" + f.Id)) + ")");
        }
    }
}
