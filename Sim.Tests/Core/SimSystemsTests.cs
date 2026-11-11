using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    /// <summary>Test system that appends its name and the world tick to a shared log on every call.</summary>
    internal sealed class RecordingSystem : ISimSystem
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingSystem(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public void Step(SimWorld world) => _log.Add(_name + ".Step@" + world.Tick);

        public void OnCommandApplied(SimWorld world, CommandResult result) =>
            _log.Add(_name + ".Cmd:" + (result.IsAccepted ? "accepted" : "rejected"));
    }

    public class SimSystemsTests
    {
        private static SimTuning Tuning() => new SimTuning();

        [Test]
        public void EachTickRunsSystemsInListOrder()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[]
            {
                new RecordingSystem("sync", log),
                new RecordingSystem("routine", log),
                new RecordingSystem("operations", log),
            });
            world.Step(1);
            Assert.That(log, Is.EqualTo(new[] { "sync.Step@1", "routine.Step@1", "operations.Step@1" }));
        }

        [Test]
        public void StepNRunsEverySystemOncePerTickWithTickAlreadyAdvanced()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[]
            {
                new RecordingSystem("a", log),
                new RecordingSystem("b", log),
            });
            world.Step(2);
            Assert.That(log, Is.EqualTo(new[]
            {
                "a.Step@1", "b.Step@1",
                "a.Step@2", "b.Step@2",
            }));
            Assert.That(world.Tick, Is.EqualTo(2));
        }

        [Test]
        public void StepZeroRunsNoSystems()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[] { new RecordingSystem("a", log) });
            world.Step(0);
            Assert.That(log, Is.Empty);
            Assert.That(world.Tick, Is.EqualTo(0));
        }

        [Test]
        public void PausedWorldRunsNoSystemsOnStep()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[] { new RecordingSystem("a", log) });
            world.Pause();
            world.Step(10);
            Assert.That(log, Is.Empty);
            Assert.That(world.Tick, Is.EqualTo(0));
        }

        [Test]
        public void ResumedWorldRunsSystemsAgain()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[] { new RecordingSystem("a", log) });
            world.Pause();
            world.Step(5);
            world.Resume();
            world.Step(1);
            Assert.That(log, Is.EqualTo(new[] { "a.Step@1" }));
        }

        [Test]
        public void AcceptedCommandNotifiesSystemsInListOrder()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[]
            {
                new RecordingSystem("a", log),
                new RecordingSystem("b", log),
            });
            world.Execute(new DummyCommand(CommandResult.Accepted()));
            Assert.That(log, Is.EqualTo(new[] { "a.Cmd:accepted", "b.Cmd:accepted" }));
        }

        [Test]
        public void RejectedCommandNotifiesNoSystem()
        {
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[] { new RecordingSystem("a", log) });
            world.Execute(new DummyCommand(CommandResult.Rejected(CommandRejectCode.Conflict, "no")));
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void CommandWhilePausedStillNotifiesSystems()
        {
            // Commands work while paused (decision 0011); only Step is gated by pause.
            var log = new List<string>();
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[] { new RecordingSystem("a", log) });
            world.Pause();
            world.Execute(new DummyCommand(CommandResult.Accepted()));
            Assert.That(log, Is.EqualTo(new[] { "a.Cmd:accepted" }));
        }

        [Test]
        public void ListPassedToWorldIsCopiedSoLaterChangesDoNotReorderSystems()
        {
            var log = new List<string>();
            var list = new List<ISimSystem> { new RecordingSystem("a", log) };
            var world = new SimWorld(Tuning(), null, null, list);
            list.Insert(0, new RecordingSystem("late", log));
            world.Step(1);
            Assert.That(log, Is.EqualTo(new[] { "a.Step@1" }));
        }

        [Test]
        public void WorldWithoutSystemsStillAdvancesTick()
        {
            var world = new SimWorld(Tuning(), null, null, new ISimSystem[0]);
            world.Step(7);
            Assert.That(world.Tick, Is.EqualTo(7));
        }

        [Test]
        public void DefaultSystemListStartsWithNavSync()
        {
            Assert.That(SimSystems.CreateDefault()[0], Is.InstanceOf<LordsLegacy.Sim.Navigation.NavSyncSystem>());
        }

        [Test]
        public void DefaultSystemListIsNewEachCall()
        {
            Assert.That(SimSystems.CreateDefault(), Is.Not.SameAs(SimSystems.CreateDefault()));
        }

        [Test]
        public void WorldBuiltWithoutSystemsArgumentRunsDefaultListAndStepsNormally()
        {
            var world = new SimWorld(Tuning());
            world.Step(3);
            Assert.That(world.Tick, Is.EqualTo(3));
            Assert.That(world.Paused, Is.False);
        }
    }
}
