using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>Ox pool, assignment, stand occupancy and rest spots (WC-30..32, T-288).</summary>
    public class OxPoolTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld World()
        {
            var w = new SimWorld(new SimTuning());
            w.Camp = new CampInfo(0, M(0, 0), M(0, 0), M(5, 5));
            return w;
        }

        private static Operation Op(SimWorld w, double x)
        {
            var shape = AreaShape.Straight(new[] { M(x, 100), M(x + 20, 100), M(x + 20, 120), M(x, 120) });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        private static Footprint Module(SimWorld w, Operation op, FootprintTypeDef type, double x, double z)
        {
            int id = w.Footprints.NextId();
            Footprint f = w.Footprints.Add(id, type, M(x, z), 0);
            w.Operations.AddModule(op.Id, id);
            return f;
        }

        private static Ox AddOx(SimWorld w, int id, double x, double z) => w.People.AddOx(id, "ox" + id, x, z);

        [Test]
        public void AssignOx_AssignedOxServesOnlyThatOperation()
        {
            var w = World(); var a = Op(w, 100); var b = Op(w, 200);
            var ox = AddOx(w, 1, 0, 0);
            Assert.IsTrue(w.Operations.AssignOx(a.Id, 1));
            var pool = new OxPool(w);
            Assert.AreSame(ox, pool.FindFor(a.Id, M(0, 0)));
            Assert.IsNull(pool.FindFor(b.Id, M(0, 0)));
        }

        [Test]
        public void AssignOx_RefusedWhenAssignedElsewhere()
        {
            var w = World(); var a = Op(w, 100); var b = Op(w, 200);
            AddOx(w, 1, 0, 0);
            Assert.IsTrue(w.Operations.AssignOx(a.Id, 1));
            Assert.IsFalse(w.Operations.AssignOx(b.Id, 1));
            Assert.IsFalse(w.Operations.AssignOx(a.Id, 1));
            Assert.IsEmpty(b.OxIds);
        }

        [Test]
        public void UnassignOx_ReturnsOxToPool_AndAllowsReassign()
        {
            var w = World(); var a = Op(w, 100); var b = Op(w, 200);
            var ox = AddOx(w, 1, 0, 0);
            w.Operations.AssignOx(a.Id, 1);
            Assert.IsTrue(w.Operations.UnassignOx(a.Id, 1));
            Assert.IsFalse(w.Operations.UnassignOx(a.Id, 1));
            Assert.AreSame(ox, new OxPool(w).FindFor(b.Id, M(0, 0)));
            Assert.IsTrue(w.Operations.AssignOx(b.Id, 1));
        }

        [Test]
        public void OxState_FactoriesCarryTheirData()
        {
            Assert.AreEqual(OxStateKind.Free, OxState.Free.Kind);
            Assert.AreEqual(7, OxState.Reserved(7).TaskId);
            Assert.AreEqual(OxStateKind.Led, OxState.Led(3).Kind);
            Assert.AreEqual(3, OxState.Led(3).HandlerId);
            var r = OxState.Resting(1000, 2000, 9);
            Assert.AreEqual(OxStateKind.Resting, r.Kind);
            Assert.AreEqual(9, r.StandId);
            Assert.AreEqual(2000, r.SpotZMm);
            Assert.AreEqual(OxStateKind.Free, AddOx(World(), 1, 0, 0).State.Kind);
        }

        [Test]
        public void FindFor_ReservedAndLedOxenAreNotAvailable()
        {
            var w = World(); var a = Op(w, 100);
            AddOx(w, 1, 0, 0).State = OxState.Reserved(5);
            AddOx(w, 2, 1, 0).State = OxState.Led(1);
            var free = AddOx(w, 3, 50, 0);
            Assert.AreSame(free, new OxPool(w).FindFor(a.Id, M(0, 0)));
        }

        [Test]
        public void FindFor_AssignedFirst_EvenIfFartherThanPoolOx()
        {
            var w = World(); var a = Op(w, 100);
            AddOx(w, 1, 1, 0);
            var assigned = AddOx(w, 2, 90, 0);
            w.Operations.AssignOx(a.Id, 2);
            Assert.AreSame(assigned, new OxPool(w).FindFor(a.Id, M(0, 0)));
        }

        [Test]
        public void FindFor_FallsBackToNearestFreePoolOx_WhenAssignedBusy()
        {
            var w = World(); var a = Op(w, 100);
            AddOx(w, 1, 30, 0);
            var near = AddOx(w, 2, 10, 0);
            AddOx(w, 3, 5, 0).State = OxState.Led(1);
            AddOx(w, 4, 2, 0); w.Operations.AssignOx(a.Id, 4); w.People.FindOx(4).State = OxState.Reserved(1);
            Assert.AreSame(near, new OxPool(w).FindFor(a.Id, M(0, 0)));
        }

        [Test]
        public void FindFor_NearestAssignedOxFirst_TieBreaksByLowestId()
        {
            var w = World(); var a = Op(w, 100);
            AddOx(w, 1, 20, 0); var near = AddOx(w, 2, 5, 0); AddOx(w, 3, -5, 0);
            foreach (int i in new[] { 1, 2, 3 }) w.Operations.AssignOx(a.Id, i);
            Assert.AreSame(near, new OxPool(w).FindFor(a.Id, M(0, 0)));
            Assert.AreEqual(2, new OxPool(w).FindFor(a.Id, M(0, 0)).Id);
            // equidistant from x = 0: ids 2 (5) and 3 (-5) -> lowest id wins
            w.People.FindOx(1).State = OxState.Reserved(1);
            Assert.AreEqual(2, new OxPool(w).FindFor(a.Id, M(0, 0)).Id);
        }

        [Test]
        public void FindFor_NoOxen_ReturnsNull()
        {
            var w = World(); var a = Op(w, 100);
            Assert.IsNull(new OxPool(w).FindFor(a.Id, M(0, 0)));
        }

        [Test]
        public void Stand_HoldsOneOx_Occupancy()
        {
            var w = World(); var a = Op(w, 100);
            var stand = Module(w, a, ModuleTypes.OxenStand, 110, 110);
            var pool = new OxPool(w);
            Assert.IsTrue(pool.HasRoom(stand));
            var ox = AddOx(w, 1, 108, 108);
            pool.Rest(ox);
            Assert.AreEqual(1, pool.Occupancy(stand.Id));
            Assert.IsFalse(pool.HasRoom(stand));
            Assert.IsTrue(pool.HasRoom(stand, ox), "the occupant itself does not block its own stand");
            ox.State = OxState.Led(1);
            Assert.AreEqual(0, pool.Occupancy(stand.Id));
        }

        [Test]
        public void RestSpot_AssignedOx_GoesToItsOperationsStand()
        {
            var w = World(); var a = Op(w, 100); var b = Op(w, 200);
            var stand = Module(w, a, ModuleTypes.OxenStand, 110, 110);
            Module(w, a, ModuleTypes.LogStorage, 105, 105);
            Module(w, b, ModuleTypes.OxenStand, 210, 110);
            var ox = AddOx(w, 1, 0, 0); w.Operations.AssignOx(a.Id, 1);
            PointMm spot = new OxPool(w).RestSpot(ox, out int standId);
            Assert.AreEqual(stand.Centre, spot);
            Assert.AreEqual(stand.Id, standId);
        }

        [Test]
        public void RestSpot_AssignedOx_NoStand_GoesBesideLogStorage()
        {
            var w = World(); var a = Op(w, 100);
            var storage = Module(w, a, ModuleTypes.LogStorage, 105, 105);
            var ox = AddOx(w, 1, 0, 0); w.Operations.AssignOx(a.Id, 1);
            PointMm spot = new OxPool(w).RestSpot(ox, out int standId);
            Assert.AreEqual(storage.AccessPoint, spot);
            Assert.AreEqual(OxState.NoStand, standId);
        }

        [Test]
        public void RestSpot_AssignedOx_StandTaken_GoesBesideLogStorage()
        {
            var w = World(); var a = Op(w, 100);
            Module(w, a, ModuleTypes.OxenStand, 110, 110);
            var storage = Module(w, a, ModuleTypes.LogStorage, 105, 105);
            var pool = new OxPool(w);
            var first = AddOx(w, 1, 0, 0); w.Operations.AssignOx(a.Id, 1); pool.Rest(first);
            var second = AddOx(w, 2, 0, 0); w.Operations.AssignOx(a.Id, 2);
            Assert.AreEqual(storage.AccessPoint, pool.RestSpot(second, out _));
        }

        [Test]
        public void RestSpot_PoolOx_NearestFreeStand()
        {
            var w = World(); var a = Op(w, 100); var b = Op(w, 200);
            Module(w, a, ModuleTypes.OxenStand, 110, 110);
            var nearer = Module(w, b, ModuleTypes.OxenStand, 20, 20);
            var ox = AddOx(w, 1, 15, 15);
            // camp stand at (5,5) is nearer than either; move the camp away so a stand wins
            w.Camp = new CampInfo(0, M(0, 0), M(0, 0), M(-300, -300));
            PointMm spot = new OxPool(w).RestSpot(ox, out int standId);
            Assert.AreEqual(nearer.Centre, spot);
            Assert.AreEqual(nearer.Id, standId);
        }

        [Test]
        public void RestSpot_PoolOx_AllStandsTaken_GoesToCampStand_WithNoLimit()
        {
            var w = World(); var a = Op(w, 100);
            w.Camp = new CampInfo(0, M(0, 0), M(0, 0), M(-300, -300));
            var stand = Module(w, a, ModuleTypes.OxenStand, 110, 110);
            var pool = new OxPool(w);
            var ox1 = AddOx(w, 1, 100, 100); pool.Rest(ox1);
            Assert.AreEqual(stand.Id, ox1.State.StandId);
            for (int i = 2; i <= 6; i++)
            {
                var ox = AddOx(w, i, 100, 100);
                PointMm spot = pool.Rest(ox);
                Assert.AreEqual(M(-300, -300), spot);
                Assert.AreEqual(OxState.NoStand, ox.State.StandId);
                Assert.AreEqual(OxStateKind.Resting, ox.State.Kind);
            }
        }

        [Test]
        public void RestSpot_PoolOx_CampStandIsACandidateForNearest()
        {
            var w = World(); var a = Op(w, 100);
            Module(w, a, ModuleTypes.OxenStand, 110, 110);
            var ox = AddOx(w, 1, 4, 4);
            Assert.AreEqual(M(5, 5), new OxPool(w).RestSpot(ox, out int standId));
            Assert.AreEqual(OxState.NoStand, standId);
        }
    }
}
