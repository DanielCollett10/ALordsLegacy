using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Save.Sections;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class CuringSectionTests
    {
        private const int Day = 1440;
        private static readonly PointMm At = PointMm.FromMetres(40, 40);

        private static (SimWorld world, MapData map) Make()
        {
            var map = SyntheticMap.CreateRamp();
            return (SimWorld.FromMapData(map, new SimTuning()), map);
        }

        private static long Ready(SimWorld w) => w.Tick + (long)w.Tuning.ThatchCureDays * Day;

        [Test]
        public void Stook_StaysGreenForSevenDays_ThenBecomesThatch_ThroughSimTicks()
        {
            var (w, _) = Make();
            w.Curing.Add(At, 5, 6, Ready(w));
            w.Step(7 * Day - 1);
            Assert.AreEqual(1, w.Curing.Count, "still curing at 6 days 23:59");
            Assert.AreEqual(0, w.Inventory.TotalOf(Items.Thatch), "green bundles are not in the ledger");
            w.Step(1);
            Assert.AreEqual(0, w.Curing.Count);
            Assert.AreEqual(6, w.Inventory.TotalOf(Items.Thatch));
        }

        [Test]
        public void Dry_ProducesOperationOwnedPileAtStook_Journalled_AuditClean()
        {
            var (w, _) = Make();
            int before = w.Curing.Version;
            w.Curing.Add(At, 5, 4, w.Tick + 3);
            Assert.Greater(w.Curing.Version, before);
            w.Step(3);
            LedgerLocation pile = w.Inventory.Locations.Single(l => l.Kind == LocationKind.GroundPile && l.CountOf(Items.Thatch.Id) > 0);
            Assert.AreEqual(5, pile.OwnerOperationId);
            Assert.AreEqual(At, pile.Position);
            Assert.AreEqual(4, pile.CountOf(Items.Thatch.Id));
            Assert.IsNull(w.Inventory.Audit());
            Assert.IsNull(w.Curing.Audit());
        }

        [Test]
        public void QueryByOperation_ReturnsOnlyThatOperationsStooks()
        {
            var (w, _) = Make();
            w.Curing.Add(At, 5, 2, 100);
            w.Curing.Add(At, 6, 3, 100);
            w.Curing.Add(At, 5, 4, 100);
            Assert.AreEqual(new[] { 1, 3 }, w.Curing.ForOperation(5).Select(s => s.Id).ToArray());
            Assert.AreEqual(6, w.Curing.CuringCount(5));
            Assert.AreEqual(0, w.Curing.ForOperation(9).Count);
        }

        [Test]
        public void Add_ZeroCount_Throws()
        {
            var (w, _) = Make();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => w.Curing.Add(At, 5, 0, 10));
        }

        [Test]
        public void RoundTrip_CuringAndDriedStooksSurvive()
        {
            var (w, map) = Make();
            w.Curing.Add(At, 5, 2, w.Tick + 10);
            w.Curing.Add(PointMm.FromMetres(50, 50), 6, 3, w.Tick + 5 * Day);
            w.Step(20); // first dries into a pile
            Assert.AreEqual(1, w.Curing.Count);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash), "byte for byte");
            Assert.AreEqual(w.Curing.NextIdValue, back.Curing.NextIdValue);
            Assert.AreEqual(
                w.Curing.All.Select(s => (s.Id, s.Position, s.OperationId, s.Count, s.ReadyTick)).ToList(),
                back.Curing.All.Select(s => (s.Id, s.Position, s.OperationId, s.Count, s.ReadyTick)).ToList());
            Assert.AreEqual(2, back.Inventory.TotalOf(Items.Thatch));
            back.Step(5 * Day);
            Assert.AreEqual(0, back.Curing.Count);
            Assert.AreEqual(5, back.Inventory.TotalOf(Items.Thatch));
            Assert.IsNull(back.Inventory.Audit());
        }

        [Test]
        public void SaveVersion_Is37OrMore_AndSectionRegistered()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 37);
            Assert.AreEqual(12, CuringSection.SectionId);
            Assert.IsTrue(SaveSections.CreateDefault().Any(s => s is CuringSection));
        }
    }
}
