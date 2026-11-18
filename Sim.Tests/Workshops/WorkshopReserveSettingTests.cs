using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>The player-set workshop input reserve (T-538, PL-30b, PL-32).</summary>
    public class WorkshopReserveSettingTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static MapData _map;

        private static SimWorld Make(out int hewOp, out int hewFp)
        {
            _map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            SimWorld w = SimWorld.FromMapData(_map, new SimTuning { StartHour = 8 });
            w.People.AddFamily(3, "F3", 40, 40);
            for (int i = 0; i < 2; i++) w.People.AddCitizen(30 + i, "C" + i, Sex.Male, AdultBirth, 3, 40, 40);
            for (int i = 0; i < 3; i++) w.People.AddOx(50 + i, "O" + i, 40, 44 + i).State = OxState.Resting(40000, 44000 + 1000 * i);
            hewFp = w.Footprints.Add(100, ModuleTypes.Find(ModuleTypes.HewingStationId), M(60, 60), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(ModuleTypes.HewingStationId), 0);
            w.Operations.AddModule(op.Id, hewFp);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(hewFp));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, 3)).IsAccepted);
            hewOp = op.Id;
            return w;
        }

        private static void Logs(SimWorld w, int n) => w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, n);

        private static int Fetches(SimWorld w) => w.Tasks.All.Count(t => t.Def.Id == WorkshopSystem.FetchTaskId && !t.IsFinished);

        [Test]
        public void Reserve_Default_IsTuningDefault_UntilSet()
        {
            SimWorld w = Make(out int op, out _);
            Assert.AreEqual(w.Tuning.WorkshopDefaultReserve, InputReserve.ReserveOf(w, w.Operations.Get(op), Items.Log));
            Assert.AreEqual(0, w.Operations.Get(op).Reserves.Count);
        }

        [Test]
        public void Command_AcceptsOwnInput_ZeroAndMax()
        {
            SimWorld w = Make(out int op, out _);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 0)).IsAccepted);
            Assert.AreEqual(0, InputReserve.ReserveOf(w, w.Operations.Get(op), Items.Log));
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 9999)).IsAccepted);
            Assert.AreEqual(9999, InputReserve.ReserveOf(w, w.Operations.Get(op), Items.Log));
        }

        [TestCase(-1)]
        [TestCase(10000)]
        public void Command_RefusesAmountOutOfRange_WithReason_AndChangesNothing(int amount)
        {
            SimWorld w = Make(out int op, out _);
            CommandResult r = w.Execute(new SetWorkshopReserve(op, Items.Log.Id, amount));
            Assert.IsTrue(r.IsRejected);
            Assert.IsNotEmpty(r.Text);
            Assert.AreEqual(0, w.Operations.Get(op).Reserves.Count);
        }

        [Test]
        public void Command_RefusesOtherItem_UnknownOperation_AndNonWorkshop()
        {
            SimWorld w = Make(out int op, out _);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Beam.Id, 3)).IsRejected, "beam is not the hewing station's input");
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(999, Items.Log.Id, 3)).IsRejected, "unknown operation");
            Operation granary = w.Operations.Create(OperationTypes.GranaryId, 7);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(granary.Id, Items.Log.Id, 3)).IsRejected, "not a workshop");
            Assert.AreEqual(0, w.Operations.Get(op).Reserves.Count);
        }

        [Test]
        public void Command_WorksWhilePaused_AndDoesNotAdvanceTick()
        {
            SimWorld w = Make(out int op, out _);
            w.Pause();
            long tick = w.Tick;
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 2)).IsAccepted);
            Assert.AreEqual(tick, w.Tick);
            Assert.AreEqual(2, InputReserve.ReserveOf(w, w.Operations.Get(op), Items.Log));
        }

        [Test]
        public void LoweringReserve_OpensTheNextFetch()
        {
            SimWorld w = Make(out int op, out _);
            Logs(w, 5);
            w.Step(1);
            Assert.AreEqual(0, Fetches(w), "5 logs at the default reserve 5");
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 2)).IsAccepted);
            w.Step(1);
            Assert.AreEqual(3, Fetches(w), "3 oxen, 5 logs, reserve 2");
        }

        [Test]
        public void RaisingReserve_StopsTheNextFetch()
        {
            SimWorld w = Make(out int op, out _);
            Logs(w, 6);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 6)).IsAccepted);
            w.Step(1);
            Assert.AreEqual(0, Fetches(w));
        }

        [Test]
        public void Save_RoundTrip_KeepsChangedReserve_ByteIdentical_AndDefaultForOthers()
        {
            SimWorld w = Make(out int op, out _);
            Assert.IsTrue(w.Execute(new SetWorkshopReserve(op, Items.Log.Id, 42)).IsAccepted);
            byte[] first = SimSave.Write(w, "s", 1, _map.MapId, _map.ContentHash);
            SimWorld back = SimSave.Read(first, _map);
            Assert.AreEqual(first, SimSave.Write(back, "s", 1, _map.MapId, _map.ContentHash));
            Assert.AreEqual(42, InputReserve.ReserveOf(back, back.Operations.Get(op), Items.Log));
        }

        [Test]
        public void Status_ReserveTheOnlyBlock_SaysWaitingTotalOverReserve()
        {
            SimWorld w = Make(out _, out int fp);
            Logs(w, 5);
            w.Step(1);
            Assert.AreEqual("Waiting: 5 / 5 logs in storage", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void Status_NothingInStorage_StaysWaitingForLogs()
        {
            SimWorld w = Make(out _, out int fp);
            w.Step(1);
            Assert.AreEqual("Waiting for logs", WorkshopReport.Of(w, fp).StatusText);
        }

        [Test]
        public void SaveVersion_IsCurrent_AfterReserveBump()
        {
            Assert.GreaterOrEqual(SaveWriter.CurrentVersion, 31);
        }
    }
}
