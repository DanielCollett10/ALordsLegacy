using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Start
{
    /// <summary>Dossing about with no camp, and every camp reader coping with World.Camp == null (T-468, CMP-25, HM-11, HM-14).</summary>
    public class NoCampTests
    {
        private const long Adult = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(MapData map = null)
        {
            var w = SimWorld.FromMapData(map ?? SyntheticMap.CreateRamp(), new SimTuning { StartHour = 8 });
            var layout = new StartLayout(M(100, 100), 0, new PointMm(0, 0), new PointMm(6000, 0), new PointMm(-6000, 0),
                new PointMm[0], new StartFamily[0], 2);
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static Family AddFamily(SimWorld w, int id)
        {
            Family f = w.People.AddFamily(id, "F" + id, 100, 100);
            w.People.AddCitizen(id, "C" + id, Sex.Male, Adult, id, 100, 100);
            return f;
        }

        /// <summary>A world whose camp is gone, with one family housed in house 900.</summary>
        private static SimWorld CampGone(out Family family)
        {
            SimWorld w = Make();
            family = AddFamily(w, 1);
            w.Footprints.Add(900, ModuleTypes.House, M(60, 60), 0);
            w.Homes.Add(900, 0);
            Assert.IsTrue(w.Homes.MoveIn(w.Homes.Get(900), family));
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            Assert.IsNull(w.Camp);
            return w;
        }

        private static void AddMarket(SimWorld w)
        {
            var shape = AreaShape.Straight(new[] { M(30, 30), M(50, 30), M(50, 50), M(30, 50) });
            Assert.IsTrue(w.Execute(new DesignateArea(shape, AreaTypes.MarketId)).IsAccepted);
        }

        [Test]
        public void Evict_AfterCampGone_NoMarket_FamilyDossesAtTheOldFireSpot()
        {
            SimWorld w = CampGone(out Family f);
            PointMm fire = w.RoughSleepSpot.Value;
            HouseRemoval.Evict(w, 900);
            Assert.IsTrue(f.IsHomeless);
            Assert.AreEqual(fire.XMetres, f.HomeX, 1e-6);
            Assert.AreEqual(fire.ZMetres, f.HomeZ, 1e-6);
            Assert.AreEqual("Homeless (sleeping rough)", RoughSleep.Status(w, f));
            Assert.IsNull(w.Camp);
        }

        [Test]
        public void Evict_AfterCampGone_WithMarket_FamilyDossesAtTheMarketCentre()
        {
            SimWorld w = CampGone(out Family f);
            AddMarket(w);
            HouseRemoval.Evict(w, 900);
            Assert.AreEqual(40.0, f.HomeX, 1e-6);
            Assert.AreEqual(40.0, f.HomeZ, 1e-6);
        }

        [Test]
        public void Evict_WhileCampStands_StillUsesTheCampFireAndCampStatus()
        {
            SimWorld w = Make();
            Family f = AddFamily(w, 1);
            w.Footprints.Add(900, ModuleTypes.House, M(60, 60), 0);
            w.Homes.Add(900, 0);
            w.Homes.MoveIn(w.Homes.Get(900), f);
            HouseRemoval.Evict(w, 900);
            Assert.AreEqual(w.Camp.Fire.XMetres, f.HomeX, 1e-6);
            Assert.AreEqual("Homeless (camp)", RoughSleep.Status(w, f));
        }

        [Test]
        public void Evict_WithNoCampAndNoSpot_KeepsTheHomePoint()
        {
            SimWorld w = Make();
            Family f = AddFamily(w, 1);
            w.Footprints.Add(900, ModuleTypes.House, M(60, 60), 0);
            w.Homes.Add(900, 0);
            w.Homes.MoveIn(w.Homes.Get(900), f);
            f.HomeX = 61; f.HomeZ = 62;
            w.Camp = null;
            Assert.DoesNotThrow(() => HouseRemoval.Evict(w, 900));
            Assert.AreEqual(61, f.HomeX);
            Assert.AreEqual(62, f.HomeZ);
        }

        [Test]
        public void HomelessFamily_NoCamp_IsMovedIntoTheNextFreeHouse_AndCampNeverReturns()
        {
            SimWorld w = CampGone(out Family f);
            HouseRemoval.RemoveWithPlot(w, 900);
            Assert.IsTrue(f.IsHomeless);
            w.Step(1);
            Assert.IsTrue(f.IsHomeless, "no free house yet");
            w.Footprints.Add(901, ModuleTypes.House, M(80, 80), 0);
            w.Homes.Add(901, 0);
            w.Step(2);
            Assert.IsFalse(f.IsHomeless);
            Assert.AreEqual(901, f.HomeHouseId);
            Assert.IsNull(w.Camp);
        }

        [Test]
        public void RoutineSystem_HomelessNoCamp_MillsAndSleepsAroundTheRoughSpot()
        {
            SimWorld w = CampGone(out Family f);
            AddMarket(w);
            HouseRemoval.Evict(w, 900);
            Citizen c = w.People.Citizens.First();
            w.Step(60 * 24); // a full day: wakes, mills, goes home, sleeps
            double radius = w.Tuning.MillRadiusM + 2;
            double d = System.Math.Sqrt((c.X - 40) * (c.X - 40) + (c.Z - 40) * (c.Z - 40));
            Assert.LessOrEqual(d, radius);
        }

        [Test]
        public void CampFoodMarket_NoCamp_HasNoStockAndANeutralOrigin()
        {
            SimWorld w = CampGone(out _);
            var m = new CampFoodMarket(w);
            Assert.AreEqual(0, m.Stocks.Count);
            Assert.AreEqual(0, m.Origin.X);
            Assert.AreEqual(0, m.Origin.Z);
        }

        [Test]
        public void FoodTripSystem_NoCamp_NoReservedStops_OpensNoTripAndDoesNotThrow()
        {
            SimWorld w = CampGone(out Family f);
            TaskRecordHolder.AssertNoTrip(w, f);
        }

        [Test]
        public void FoodSection_NoCamp_RoundTripsAndKeepsTheHandoverFlag()
        {
            MapData map = SyntheticMap.CreateRamp();
            SimWorld w = Make(map);
            AddFamily(w, 1).HomeHouseId = 900;
            w.FoodHandedOver = true;
            Assert.IsTrue(CampRemovalSystem.TryRemove(w));
            byte[] first = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld loaded = SimSave.Read(first, map);
            Assert.IsNull(loaded.Camp);
            Assert.IsTrue(loaded.FoodHandedOver);
            Assert.AreEqual(w.RoughSleepSpot, loaded.RoughSleepSpot);
            Assert.AreEqual(first, SimSave.Write(loaded, "s", 1, map.MapId, map.ContentHash));
        }

        [Test]
        public void SaveVersion_IsBumpedTo18_ForTheRoughSleepSpot()
        {
            Assert.AreEqual(18, SaveWriter.CurrentVersion);
        }

        [Test]
        public void ModuleTypes_NoCamp_StillResolveEveryModuleAndNeverTheCamp()
        {
            SimWorld w = CampGone(out _);
            foreach (FootprintTypeDef t in ModuleTypes.All) Assert.AreSame(t, ModuleTypes.Find(t.Id));
            Assert.IsNull(ModuleTypes.Find(FootprintTypeDef.CampId));
            Assert.IsNull(w.Camp);
        }

        [Test]
        public void ThirtyDayRun_NoCamp_OneEvictedFamily_RunsWithoutErrors()
        {
            SimWorld w = CampGone(out Family f);
            HouseRemoval.RemoveWithPlot(w, 900);
            Assert.DoesNotThrow(() => w.Step(60 * 24 * 30));
            Assert.IsTrue(f.IsHomeless);
            Assert.IsNull(w.Camp);
            Citizen c = w.People.Citizens.First();
            Assert.IsFalse(double.IsNaN(c.X));
        }
    }

    internal static class TaskRecordHolder
    {
        public static void AssertNoTrip(SimWorld w, Family f)
        {
            Assert.IsNull(FoodTripSystem.OpenTrip(w, f));
        }
    }
}
