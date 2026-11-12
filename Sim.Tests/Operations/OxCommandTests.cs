using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Operations
{
    /// <summary>AssignOx and UnassignOx (T-294, WC-30).</summary>
    public class OxCommandTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static Operation Op(SimWorld w, double x0)
        {
            var shape = AreaShape.Straight(new[] { M(x0, 100), M(x0 + 20, 100), M(x0 + 20, 120), M(x0, 120) });
            CommandResult r = w.Execute(new DesignateArea(shape, AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return w.Operations.FindByArea((int)r.Created[0]);
        }

        [Test]
        public void Assign_FreeOx_ListsIt()
        {
            var w = new SimWorld(new SimTuning()); var op = Op(w, 100);
            w.People.AddOx(1, "Bess", 0, 0);
            Assert.IsTrue(w.Execute(new AssignOx(op.Id, 1)).IsAccepted);
            CollectionAssert.AreEqual(new[] { 1 }, op.OxIds);
        }

        [Test]
        public void Assign_OxOfAnotherOperation_NotAllowed()
        {
            var w = new SimWorld(new SimTuning()); var a = Op(w, 100); var b = Op(w, 200);
            w.People.AddOx(1, "Bess", 0, 0);
            w.Execute(new AssignOx(a.Id, 1));
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new AssignOx(b.Id, 1)).Reason);
            Assert.AreEqual(0, b.OxIds.Count);
        }

        [Test]
        public void Assign_UnknownOxOrOperation_NotFound()
        {
            var w = new SimWorld(new SimTuning()); var op = Op(w, 100);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new AssignOx(op.Id, 9)).Reason);
            w.People.AddOx(1, "Bess", 0, 0);
            Assert.AreEqual(CommandRejectCode.NotFound, w.Execute(new AssignOx(99, 1)).Reason);
        }

        [Test]
        public void Unassign_ReturnsOxToPool_AndRefusesUnassigned()
        {
            var w = new SimWorld(new SimTuning()); var op = Op(w, 100);
            w.People.AddOx(1, "Bess", 0, 0);
            Assert.AreEqual(CommandRejectCode.NotAllowed, w.Execute(new UnassignOx(op.Id, 1)).Reason);
            w.Execute(new AssignOx(op.Id, 1));
            Assert.IsTrue(w.Execute(new UnassignOx(op.Id, 1)).IsAccepted);
            Assert.IsNull(w.Operations.FindOxOwner(1));
        }
    }
}
