using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Markets
{
    /// <summary>
    /// The whole M15 loop through real commands and ticks (T-470): foragers fill a hut store, a granary family carries it (and the
    /// camp's meat after the handover) to the granary and on to a stall, households draw their Sunday allotment from the stall,
    /// and the emptied camp goes. Houses are the one shortcut: they are designated, then finished with Construction.Remove.
    /// </summary>
    public class MarketEndToEndTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private sealed class Fx
        {
            public SimWorld W; public MapData Map;
            public int Hut, Granary, ForageOp, GranaryOp;
            public LedgerLocation HutStore => W.Inventory.FindFoodStore(Hut);
            public LedgerLocation GranaryStore => W.Inventory.FindFoodStore(Granary);
            public IEnumerable<Family> Households => W.People.Families;
        }

        private static bool At(SimWorld w, Weekday d, int hour) => w.Now.Weekday == d && w.Now.Hour == hour && w.Now.Minute == 0;

        private static void StepUntil(SimWorld w, System.Func<bool> done, int maxMinutes, string what)
        {
            for (int i = 0; i < maxMinutes && !done(); i++) w.Step(1);
            Assert.IsTrue(done(), what + " (now " + w.Now.Weekday + " " + w.Now.Hour + ":" + w.Now.Minute + ")");
        }

        // Wednesday 08:00. Flat map, camp with 60 meat, three families (two adults each) with larders for the first week,
        // a road with three house plots, a market, a granary and a forager hut with bushes. Built, assigned, ready to switch on.
        private static Fx Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, BushCount = 0, StartingMeat = 60, StartWeekday = Weekday.Wednesday });
            StartLayoutApplier.Apply(w, new StartLayout(M(140, 140), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2));
            w.Inventory.Produce(w.Camp.StockpileLocation, Items.Log, 80);
            w.Inventory.Consume(w.Camp.GeneralStoreLocation, Items.Plank, w.Camp.GeneralStoreLocation.CountOf(Items.Plank.Id)); // T-513: planks used up, so the camp can go (ST-30)
            w.Roads.AddSegment(RoadTypes.DirtPathId, CurveKind.CatmullRomCentripetal, new[] { M(20, 50), M(110, 50) }, new[] { M(20, 50), M(65, 50), M(110, 50) });
            for (int fam = 1; fam <= 3; fam++)
            {
                Family f = w.People.AddFamily(fam, "F" + fam, 140, 140);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, Adult, fam, 140, 140);
                w.Inventory.Produce(Larders.ForFamily(w, f), Items.Meat, 5);
            }
            w.People.AddOx(50, "O", 140, 144).State = OxState.Resting(140000, 144000);

            int road = w.Roads.Segments.First().Id;
            foreach (double x in new[] { 30.0, 55.0, 80.0 })
            {
                var plot = new AreaShape(new[] { M(x, 51.25), M(x + 20, 51.25), M(x + 20, 61.25), M(x, 61.25) },
                    new[] { AreaEdge.FollowsRoad(road, RoadSide.Left), AreaEdge.Straight(), AreaEdge.Straight(), AreaEdge.Straight() });
                CommandResult p = w.Execute(new DesignatePlot(plot));
                Assert.IsTrue(p.IsAccepted, p.Text);
                int house = (int)p.Created[1];
                foreach (int res in w.Construction.Get(house).ReservationIds.ToList()) w.Inventory.Release(res); // the site never consumes its logs
                w.Construction.Remove(house);
            }
            Assert.IsTrue(w.Execute(new DesignateArea(AreaShape.Straight(new[] { M(10, 10), M(30, 10), M(30, 30), M(10, 30) }), AreaTypes.MarketId)).IsAccepted);

            var fx = new Fx { W = w, Map = map };
            CommandResult g = w.Execute(new PlaceStructure(ModuleTypes.GranaryId, M(60, 110), 0));
            Assert.IsTrue(g.IsAccepted, g.Text);
            fx.Granary = (int)g.Created[0];
            var hut = new List<ModuleSpec> { new ModuleSpec(ModuleTypes.ForagerHutId, M(160, 80), 0) };
            var square = AreaShape.Straight(new[] { M(145, 65), M(175, 65), M(175, 95), M(145, 95) });
            CommandResult d = w.Execute(new DesignateOperation(square, OperationTypes.ForagingId, hut));
            Assert.IsTrue(d.IsAccepted, d.Text);
            fx.Hut = (int)d.Created[1];
            fx.ForageOp = w.Operations.FindByArea((int)d.Created[0]).Id;
            for (int i = 0; i < 6; i++)
                w.Bushes.Add(150 + i * 4, 70 + (i % 2) * 20, i % 2 == 0 ? BushKind.Berries : BushKind.Mushrooms, 3);

            StepUntil(w, () => w.IsBuilt(fx.Granary) && w.IsBuilt(fx.Hut), 3 * 1440, "granary and hut built by workers");
            fx.GranaryOp = w.Operations.FindByModule(fx.Granary).Id;
            fx.GranaryStore.Capacity = 100;
            Assert.IsTrue(w.Execute(new AssignFamily(fx.ForageOp, 1)).IsAccepted);
            Assert.IsTrue(w.Execute(new AssignFamily(fx.GranaryOp, 2)).IsAccepted);
            return fx;
        }

        /// <summary>Steps minute by minute until done, checking the one-way food path on every tick.</summary>
        private static void RunUntil(Fx f, System.Func<bool> done, int maxMinutes, string what)
        {
            var hutStores = new HashSet<int>();
            for (int i = 0; i < maxMinutes && !done(); i++)
            {
                f.W.Step(1);
                hutStores.Add(f.HutStore.Id);
                int granary = f.GranaryStore.Id;
                var stalls = new HashSet<int>();
                if (f.W.Markets.Current != null)
                    foreach (int sf in f.W.Markets.Current.StallFootprintIds)
                    {
                        LedgerLocation l = f.W.Inventory.FindFoodStore(sf);
                        if (l != null) stalls.Add(l.Id);
                    }
                foreach (TaskRecord t in f.W.Tasks.All.Where(t => !t.IsFinished))
                    foreach (TaskStep s in t.Def.Steps)
                    {
                        if (s.Kind == StepKind.DepositForage)
                            Assert.IsTrue(hutStores.Contains(s.LocationId), "foragers deliver to their hut store only");
                        if (s.Kind == StepKind.DepositStock)
                            Assert.IsTrue(s.LocationId == granary || stalls.Contains(s.LocationId), "stock goes to the granary or a stall");
                        if (s.Kind == StepKind.CollectStock && s.LocationId == granary)
                            Assert.IsTrue(t.Def.Steps.Where(x => x.Kind == StepKind.DepositStock).All(x => stalls.Contains(x.LocationId)), "granary stock goes to a stall");
                    }
            }
            Assert.IsTrue(done(), what + " (now " + f.W.Now.Weekday + " " + f.W.Now.Hour + ":" + f.W.Now.Minute + ")");
        }

        private static int StallFood(Fx f) =>
            f.W.Markets.Current.StallFootprintIds.Where(s => f.W.IsBuilt(s)).Sum(s => f.W.Inventory.FindFoodStore(s).Total);

        [Test]
        public void CaseA_SwitchOnMonday_StallReadyBySunday_CampHandedOverAndGone_NobodyHungryTwoWeeksLater()
        {
            Fx f = Make();
            StepUntil(f.W, () => At(f.W, Weekday.Monday, 5), 8 * 1440, "reach Monday 05:00");
            Assert.IsNotNull(f.W.Camp, "camp still stands before the switch");
            Assert.IsTrue(f.Households.All(x => !x.IsHomeless), "three households housed");
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, true)).IsAccepted);
            int rationsBefore = FoodStats.TotalRations(f.W);

            RunUntil(f, () => At(f.W, Weekday.Sunday, 2), 7 * 1440, "reach Sunday 02:00");
            Assert.IsTrue(f.W.FoodHandedOver);
            Assert.IsNull(f.W.Camp, "camp food moved into the granary and the camp removed; camp food left "
                + (f.W.Camp?.FoodStoreLocation?.Total ?? -1) + ", granary " + f.GranaryStore.Total + ", unhoused "
                + f.Households.Count(x => x.IsHomeless) + ", stock reserved " + (f.W.Camp?.StockpileLocation?.ReservedCounts.Count ?? -1)
                + ", incoming " + (f.W.Camp?.StockpileLocation?.IncomingCounts.Count ?? -1) + ", handed " + f.W.FoodHandedOver);
            Assert.AreEqual(1, f.W.Markets.Current.StallFootprintIds.Count(s => f.W.IsBuilt(s)), "one stall built");
            Assert.Greater(StallFood(f), 0, "stall stocked before the Sunday dawn allotment");

            RunUntil(f, () => At(f.W, Weekday.Sunday, 12), 1440, "Sunday noon");
            Assert.AreEqual(3, f.W.LastAllotmentServed);
            Assert.AreEqual(0, f.W.LastAllotmentShort);

            RunUntil(f, () => At(f.W, Weekday.Monday, 0), 1440, "midnight");
            long end = f.W.Tick + 14L * 1440;
            RunUntil(f, () => f.W.Tick >= end, 14 * 1440, "14 more days");
            Assert.AreEqual(0, f.W.Now.Hour * 60 + f.W.Now.Minute, "stopped on a midnight, when eating sets Hungry");
            Assert.IsFalse(f.Households.Any(x => x.IsHungry), "nobody Hungry on the final day");
            Assert.IsNull(f.W.Inventory.Audit());
            Assert.IsTrue(rationsBefore > 0 && FoodStats.TotalRations(f.W) > 0);
        }

        [Test]
        public void CaseB_SwitchOnSaturdayNight_FirstSundayIsShortForEveryHousehold()
        {
            Fx f = Make();
            StepUntil(f.W, () => At(f.W, Weekday.Saturday, 22), 8 * 1440, "reach Saturday 22:00");
            Assert.IsTrue(f.Households.All(x => !x.IsHomeless));
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, true)).IsAccepted);
            RunUntil(f, () => At(f.W, Weekday.Sunday, 12), 1440, "Sunday noon");
            Assert.AreEqual(0, f.W.LastAllotmentServed, "no stall is built and stocked yet (MK-31)");
            Assert.AreEqual(3, f.W.LastAllotmentShort, "every household is short");
        }

        [Test]
        public void SaveMidRun_ReloadedWorldIsByteIdenticalAfterTwoMoreDays()
        {
            Fx f = Make();
            StepUntil(f.W, () => At(f.W, Weekday.Monday, 5), 8 * 1440, "reach Monday 05:00");
            Assert.IsTrue(f.W.Execute(new SetGranarySupplies(f.GranaryOp, true)).IsAccepted);
            RunUntil(f, () => At(f.W, Weekday.Wednesday, 9), 3 * 1440, "mid-run, stall working");
            byte[] saved = SimSave.Write(f.W, "s", 1, f.Map.MapId, f.Map.ContentHash);
            SimWorld back = SimSave.Read(saved, f.Map);
            Assert.IsNull(SaveDiff.FirstDifference(saved, SimSave.Write(back, "s", 1, f.Map.MapId, f.Map.ContentHash)));
            f.W.Step(2 * 1440);
            back.Step(2 * 1440);
            Assert.IsNull(SaveDiff.FirstDifference(SimSave.Write(f.W, "s", 1, f.Map.MapId, f.Map.ContentHash),
                SimSave.Write(back, "s", 1, f.Map.MapId, f.Map.ContentHash)));
        }
    }
}
