using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Homes
{
    /// <summary>T-590: household needs report, eligibility and the UpgradeHousehold command (HL-21..23).</summary>
    public class UpgradeHouseholdTests
    {
        private const int House = 100;
        private const int Fence = 101;

        private static SimWorld Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
        }

        // A housed family; food types and fence set by the caller. Built fence = footprint with no site.
        private static Family Housed(SimWorld w, int foodTypes, bool fenceBuilt, bool hungry = false)
        {
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            HomeRecord home = w.Homes.Add(House, 0);
            Assert.IsTrue(w.Homes.MoveIn(home, f));
            f.AllotmentFoodTypes = foodTypes;
            f.IsHungry = hungry;
            if (fenceBuilt)
            {
                w.Footprints.Add(Fence, ModuleTypes.Fence, PointMm.FromMetres(5, 5), 0);
                w.Homes.SetFence(home, Fence);
            }
            return f;
        }

        private static NeedStatus Need(SimWorld w, NeedKind kind) =>
            HouseholdNeedsReport.Of(w, 1).Needs.First(n => n.Kind == kind);

        [Test]
        public void Report_UnknownFamily_IsNull() => Assert.IsNull(HouseholdNeedsReport.Of(Make(), 99));

        [Test]
        public void Report_AllMet_ListsFourNeedsAndEligible()
        {
            SimWorld w = Make();
            Housed(w, w.Tuning.DiverseFoodTypes, true);
            HouseholdNeedsReport r = HouseholdNeedsReport.Of(w, 1);
            Assert.AreEqual(4, r.Needs.Count);
            Assert.IsTrue(r.Needs.All(n => n.Met && n.Reason.Length > 0));
            Assert.IsTrue(r.Eligible);
            Assert.AreEqual(2, r.NextLevel);
        }

        [Test]
        public void Need_DiverseFood_OneTypeBelowThreshold_NotMetWithReason()
        {
            SimWorld w = Make();
            Housed(w, w.Tuning.DiverseFoodTypes - 1, true);
            NeedStatus n = Need(w, NeedKind.DiverseFood);
            Assert.IsFalse(n.Met);
            StringAssert.Contains("food types", n.Reason);
            Assert.IsFalse(HouseholdNeedsReport.Of(w, 1).Eligible);
        }

        [Test]
        public void Need_DiverseFood_HungryFamily_NotMetEvenWithTypes()
        {
            SimWorld w = Make();
            Housed(w, w.Tuning.DiverseFoodTypes + 1, true, hungry: true);
            Assert.IsFalse(Need(w, NeedKind.DiverseFood).Met);
            Assert.IsFalse(HouseholdNeedsReport.Of(w, 1).Eligible);
        }

        [Test]
        public void Need_FencedPlot_NoFence_NotMet()
        {
            SimWorld w = Make();
            Housed(w, w.Tuning.DiverseFoodTypes, false);
            Assert.IsFalse(Need(w, NeedKind.FencedPlot).Met);
            Assert.IsFalse(HouseholdNeedsReport.Of(w, 1).Eligible);
        }

        [Test]
        public void Need_FencedPlot_FenceStillBuilding_NotMet()
        {
            SimWorld w = Make();
            Housed(w, w.Tuning.DiverseFoodTypes, true);
            w.Construction.Add(new ConstructionSite(Fence, w.Inventory.GetOrAddSite(Fence).Id));
            Assert.IsFalse(Need(w, NeedKind.FencedPlot).Met);
        }

        [Test]
        public void Need_WaterAndChurch_CountedAsMet()
        {
            SimWorld w = Make();
            Housed(w, 0, false);
            var always = HouseholdNeedsReport.Of(w, 1).Needs.Where(n => n.Kind == NeedKind.AlwaysMet).ToList();
            Assert.AreEqual(2, always.Count);
            Assert.IsTrue(always.All(n => n.Met));
        }

        [Test]
        public void Upgrade_Eligible_RaisesToLevel2_LogsAndCostsNothing()
        {
            SimWorld w = Make();
            Family f = Housed(w, w.Tuning.DiverseFoodTypes, true);
            int entries = w.SettlementLog.Entries.Count;
            CommandResult r = w.Execute(new UpgradeHousehold(1));
            Assert.IsTrue(r.IsAccepted, r.Text);
            Assert.AreEqual(2, f.Level);
            Assert.AreEqual(entries + 1, w.SettlementLog.Entries.Count);
            Assert.AreEqual("The Ash household reached level 2", w.SettlementLog.Entries.Last().Text);
        }

        [Test]
        public void Upgrade_Twice_SecondRefused_LevelStaysTwo()
        {
            SimWorld w = Make();
            Family f = Housed(w, w.Tuning.DiverseFoodTypes, true);
            Assert.IsTrue(w.Execute(new UpgradeHousehold(1)).IsAccepted);
            CommandResult again = w.Execute(new UpgradeHousehold(1));
            Assert.IsTrue(again.IsRejected);
            Assert.AreEqual(2, f.Level);
            Assert.IsFalse(HouseholdNeedsReport.Of(w, 1).Eligible);
        }

        [Test]
        public void Upgrade_NotEligible_RefusedWithReasonAndNothingChanges()
        {
            SimWorld w = Make();
            Family f = Housed(w, w.Tuning.DiverseFoodTypes, false);
            int entries = w.SettlementLog.Entries.Count;
            CommandResult r = w.Execute(new UpgradeHousehold(1));
            Assert.IsTrue(r.IsRejected);
            StringAssert.Contains("Fenced plot", r.Text);
            Assert.AreEqual(1, f.Level);
            Assert.AreEqual(entries, w.SettlementLog.Entries.Count);
        }

        [Test]
        public void Upgrade_Homeless_Refused()
        {
            SimWorld w = Make();
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            f.AllotmentFoodTypes = 5;
            CommandResult r = w.Execute(new UpgradeHousehold(1));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(1, f.Level);
            Assert.IsFalse(HouseholdNeedsReport.Of(w, 1).Eligible);
        }

        [Test]
        public void Upgrade_InPassage_Refused()
        {
            SimWorld w = Make();
            Family f = Housed(w, w.Tuning.DiverseFoodTypes, true);
            f.Passage = FamilyPassage.Leaving;
            Assert.IsTrue(w.Execute(new UpgradeHousehold(1)).IsRejected);
            Assert.AreEqual(1, f.Level);
        }

        [Test]
        public void Upgrade_UnknownFamily_NotFound()
        {
            CommandResult r = Make().Execute(new UpgradeHousehold(42));
            Assert.IsTrue(r.IsRejected);
            Assert.AreEqual(CommandRejectCode.NotFound, r.Reason);
        }

        [Test]
        public void Level_NeverRaisedWithoutCommand_AndNeverDropsWhenNeedsLapse()
        {
            SimWorld w = Make();
            Family f = Housed(w, w.Tuning.DiverseFoodTypes, true);
            for (int i = 0; i < 2000; i++) w.Step();
            Assert.AreEqual(1, f.Level, "eligible but not upgraded: stays level 1");
            Assert.IsTrue(w.Execute(new UpgradeHousehold(1)).IsAccepted);
            f.AllotmentFoodTypes = 0;
            f.IsHungry = true;
            for (int i = 0; i < 100; i++) w.Step();
            Assert.AreEqual(2, f.Level, "level never drops when needs lapse");
        }
    }
}
