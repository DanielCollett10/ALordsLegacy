using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Dummy command: validation outcome is configurable and Apply records a counter.</summary>
    internal sealed class DummyCommand : ISimCommand
    {
        private readonly CommandResult _validateResult;
        private readonly long[] _created;
        private readonly long[] _removed;

        public int ValidateCalls;
        public int ApplyCalls;
        public long[] Created => _created;

        public DummyCommand(CommandResult validateResult, long[] created = null, long[] removed = null)
        {
            _validateResult = validateResult;
            _created = created;
            _removed = removed;
        }

        public CommandResult Validate(SimWorld world)
        {
            ValidateCalls++;
            return _validateResult;
        }

        public CommandResult Apply(SimWorld world)
        {
            ApplyCalls++;
            return CommandResult.Accepted(_created, _removed);
        }
    }

    public class CommandPipelineTests
    {
        private static SimWorld New() => new SimWorld(new SimTuning());

        private static CommandResult Accept() => CommandResult.Accepted();

        private static CommandResult Reject() =>
            CommandResult.Rejected(CommandRejectCode.InvalidArgument, "dummy says no");

        [Test]
        public void AcceptedResultHasNoReasonAndIsAccepted()
        {
            var r = CommandResult.Accepted();
            Assert.That(r.IsAccepted, Is.True);
            Assert.That(r.IsRejected, Is.False);
            Assert.That(r.Text, Is.EqualTo(string.Empty));
            Assert.That(r.Created, Is.Empty);
            Assert.That(r.Removed, Is.Empty);
        }

        [Test]
        public void RejectedResultCarriesReasonAndTextAndNoIds()
        {
            var r = CommandResult.Rejected(CommandRejectCode.Conflict, "overlaps area 4");
            Assert.That(r.IsRejected, Is.True);
            Assert.That(r.Reason, Is.EqualTo(CommandRejectCode.Conflict));
            Assert.That(r.Text, Is.EqualTo("overlaps area 4"));
            Assert.That(r.Created, Is.Empty);
            Assert.That(r.Removed, Is.Empty);
        }

        [Test]
        public void AcceptedResultCopiesIdArrays()
        {
            var created = new long[] { 7, 8 };
            var r = CommandResult.Accepted(created, new long[] { 3 });
            created[0] = 99;
            Assert.That(r.Created, Is.EqualTo(new long[] { 7, 8 }));
            Assert.That(r.Removed, Is.EqualTo(new long[] { 3 }));
        }

        [Test]
        public void ExecuteAcceptedRunsApplyAndReturnsItsIds()
        {
            var world = New();
            var cmd = new DummyCommand(Accept(), created: new long[] { 42 });
            var result = world.Execute(cmd);
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Created, Is.EqualTo(new long[] { 42 }));
            Assert.That(cmd.ValidateCalls, Is.EqualTo(1));
            Assert.That(cmd.ApplyCalls, Is.EqualTo(1));
        }

        [Test]
        public void RejectedCommandDoesNotRunApply()
        {
            var world = New();
            var cmd = new DummyCommand(Reject());
            var result = world.Execute(cmd);
            Assert.That(result.IsRejected, Is.True);
            Assert.That(result.Reason, Is.EqualTo(CommandRejectCode.InvalidArgument));
            Assert.That(result.Text, Is.EqualTo("dummy says no"));
            Assert.That(cmd.ApplyCalls, Is.EqualTo(0));
        }

        [Test]
        public void RejectedCommandChangesNothingInTheWorld()
        {
            var world = New();
            world.Step(10);
            world.Pause();
            var tickBefore = world.Tick;
            var pausedBefore = world.Paused;
            var treesBefore = world.Trees.Count;
            var terrainBefore = world.Terrain;

            world.Execute(new DummyCommand(Reject()));

            Assert.That(world.Tick, Is.EqualTo(tickBefore));
            Assert.That(world.Paused, Is.EqualTo(pausedBefore));
            Assert.That(world.Trees.Count, Is.EqualTo(treesBefore));
            Assert.That(world.Terrain, Is.SameAs(terrainBefore));
        }

        [Test]
        public void RejectedCommandIsStillLogged()
        {
            var world = New();
            var cmd = new DummyCommand(Reject());
            world.Execute(cmd);
            Assert.That(world.CommandLog.Count, Is.EqualTo(1));
            Assert.That(world.CommandLog.Entries[0].Command, Is.SameAs(cmd));
        }

        [Test]
        public void ExecuteWorksWhilePaused()
        {
            var world = New();
            world.Pause();
            var cmd = new DummyCommand(Accept(), created: new long[] { 1 });
            var result = world.Execute(cmd);
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(cmd.ApplyCalls, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteNeverChangesTick()
        {
            var world = New();
            world.Step(5);
            world.Execute(new DummyCommand(Accept()));
            world.Execute(new DummyCommand(Reject()));
            Assert.That(world.Tick, Is.EqualTo(5));
        }

        [Test]
        public void ExecuteLogsCommandWithCurrentTick()
        {
            var world = New();
            world.Step(3);
            var first = new DummyCommand(Accept());
            world.Execute(first);
            world.Step(4);
            world.Pause();
            var second = new DummyCommand(Accept());
            world.Execute(second);

            var entries = world.CommandLog.Entries;
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries[0].Tick, Is.EqualTo(3));
            Assert.That(entries[0].Command, Is.SameAs(first));
            Assert.That(entries[1].Tick, Is.EqualTo(7));
            Assert.That(entries[1].Command, Is.SameAs(second));
        }

        [Test]
        public void LogKeepsExecutionOrder()
        {
            var world = New();
            var a = new DummyCommand(Accept());
            var b = new DummyCommand(Reject());
            var c = new DummyCommand(Accept());
            world.Execute(a);
            world.Execute(b);
            world.Execute(c);
            Assert.That(world.CommandLog.Entries[0].Command, Is.SameAs(a));
            Assert.That(world.CommandLog.Entries[1].Command, Is.SameAs(b));
            Assert.That(world.CommandLog.Entries[2].Command, Is.SameAs(c));
        }

        [Test]
        public void NewWorldHasEmptyLog()
        {
            Assert.That(New().CommandLog.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExecuteNullCommandIsRejectedWithArgumentNull()
        {
            var world = New();
            Assert.Throws<System.ArgumentNullException>(() => world.Execute(null));
            Assert.That(world.CommandLog.Count, Is.EqualTo(0));
        }

        [Test]
        public void ValidateReturningNullThrowsAndChangesNothing()
        {
            var world = New();
            var cmd = new DummyCommand(null);
            Assert.Throws<System.InvalidOperationException>(() => world.Execute(cmd));
            Assert.That(cmd.ApplyCalls, Is.EqualTo(0));
        }

        [Test]
        public void RejectedResultToStringNamesTheReason()
        {
            var r = CommandResult.Rejected(CommandRejectCode.NotFound, "no such tree");
            Assert.That(r.ToString(), Does.Contain("NotFound").And.Contain("no such tree"));
        }
    }
}
