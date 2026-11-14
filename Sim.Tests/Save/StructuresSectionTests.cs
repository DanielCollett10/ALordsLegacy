using System.Linq;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class StructuresSectionTests
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
            return back;
        }

        private static (SimWorld w, MapData map, int opId, int hutId) MakeWithHut()
        {
            var (w, map) = Make();
            CommandResult r = w.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            int opId = w.Operations.FindByArea((int)r.Created[0]).Id;
            w.Inventory.Produce(w.Inventory.AddStockpile(M(95, 95)), LordsLegacy.Sim.Inventory.Items.Log, 8);
            CommandResult p = w.Execute(new PlaceModule(opId, ModuleTypes.WoodcuttingHutId, M(110, 100), 0));
            Assert.IsTrue(p.IsAccepted, p.Text);
            return (w, map, opId, (int)p.Created[0]);
        }

        [Test]
        public void HalfBuiltHighPriorityHut_KeepsStatusIsBuiltAndProgress()
        {
            var (w, map, _, hut) = MakeWithHut();
            ConstructionSite s = w.Construction.Get(hut);
            s.Priority = ConstructionPriority.High;
            s.State = ConstructionState.Building;
            s.ProgressMinutes = 480;
            s.LogsConsumed = 4;
            s.ReservationIds.AddRange(new[] { 5, 9 });

            SimWorld back = RoundTrip(w, map);
            ConstructionSite b = back.Construction.Get(hut);
            Assert.IsNotNull(b);
            Assert.AreEqual(ConstructionPriority.High, b.Priority);
            Assert.AreEqual(ConstructionState.Building, b.State);
            Assert.AreEqual(480, b.ProgressMinutes);
            Assert.AreEqual(4, b.LogsConsumed);
            Assert.AreEqual(s.LedgerLocationId, b.LedgerLocationId);
            Assert.AreEqual(s.ReservationIds.ToArray(), b.ReservationIds.ToArray());
            Assert.AreEqual(3, b.ReservationIds.Count);
            Assert.AreEqual(w.IsBuilt(hut), back.IsBuilt(hut));
            Assert.IsFalse(back.IsBuilt(hut));
            ConstructionProgress a = ConstructionProgress.Of(w, hut), c = ConstructionProgress.Of(back, hut);
            Assert.AreEqual(a.State, c.State);
            Assert.AreEqual(a.Fraction, c.Fraction);
            Assert.AreEqual(a.LogsNeeded, c.LogsNeeded);
        }

        [Test]
        public void BuiltFootprint_HasNoSiteAfterLoad()
        {
            var (w, map, _, hut) = MakeWithHut();
            w.Construction.Remove(hut);
            SimWorld back = RoundTrip(w, map);
            Assert.IsTrue(back.IsBuilt(hut));
            Assert.IsNull(ConstructionProgress.Of(back, hut));
        }

        [Test]
        public void Footprints_PoseAndIdCounterKept_RemovedIdNotReused()
        {
            var (w, map) = Make();
            w.Footprints.Add(1, ModuleTypes.LogStorage, M(20, 20), 33.5);
            w.Footprints.Add(2, ModuleTypes.OxenStand, M(40, 40), 0);
            w.Footprints.Remove(2);
            SimWorld back = RoundTrip(w, map);
            Footprint f = back.Footprints.Get(1);
            Assert.AreEqual(ModuleTypes.LogStorageId, f.Type.Id);
            Assert.AreEqual(33.5, f.RotationDegrees);
            Assert.AreEqual(w.Footprints.Get(1).Polygon, f.Polygon);
            Assert.AreEqual(w.Footprints.Get(1).AccessPoint, f.AccessPoint);
            Assert.IsNull(back.Footprints.Get(2));
            Assert.AreEqual(3, back.Footprints.NextId());
        }

        [Test]
        public void Camp_RoundTripsIncludingStockpileLocationId()
        {
            var (w, map) = Make();
            w.Footprints.Add(1, FootprintTypeDef.Camp, M(50, 50), 10);
            var loc = w.Inventory.AddStockpile(M(52, 52));
            w.Camp = new CampInfo(1, M(51, 51), M(52, 52), M(53, 53), loc);
            SimWorld back = RoundTrip(w, map);
            Assert.AreEqual(1, back.Camp.FootprintId);
            Assert.AreEqual(M(51, 51), back.Camp.Fire);
            Assert.AreEqual(M(52, 52), back.Camp.Stockpile);
            Assert.AreEqual(M(53, 53), back.Camp.OxStand);
            Assert.AreEqual(loc.Id, back.Camp.StockpileLocationId);
        }

        [Test]
        public void NoCamp_StaysNull()
        {
            var (w, map) = Make();
            Assert.IsNull(RoundTrip(w, map).Camp);
        }

        [Test]
        public void Operations_AllFieldsAndCounterKept()
        {
            var (w, map, opId, hut) = MakeWithHut();
            w.Operations.AddModule(opId, 77);
            w.Operations.AssignFamily(opId, 3);
            w.Operations.AssignFamily(opId, 1);
            w.Operations.AssignOx(opId, 12);
            w.Operations.SetWorkArea(opId, 42);
            w.Operations.SetPaused(opId, true);
            SimWorld back = RoundTrip(w, map);
            var o = back.Operations.Get(opId);
            var e = w.Operations.Get(opId);
            Assert.AreEqual(e.TypeId, o.TypeId);
            Assert.AreEqual(e.AreaId, o.AreaId);
            Assert.AreEqual(e.ModuleIds, o.ModuleIds);
            Assert.AreEqual(new[] { 3, 1 }, o.FamilyIds);
            Assert.AreEqual(new[] { 12 }, o.OxIds);
            Assert.AreEqual(42, o.WorkAreaId);
            Assert.IsTrue(o.Paused);
            Assert.IsTrue(o.ModuleIds.Contains(hut));
            Assert.AreEqual(w.Operations.NextIdValue, back.Operations.NextIdValue);
        }

        [Test]
        public void OperationWithoutWorkArea_KeepsNull()
        {
            var (w, map, opId, _) = MakeWithHut();
            Assert.IsNull(RoundTrip(w, map).Operations.Get(opId).WorkAreaId);
        }

        [Test]
        public void SiteForMissingFootprint_IsRejectedOnRead()
        {
            var (w, map) = Make();
            w.Construction.Add(new ConstructionSite(9, 1));
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }
    }
}
