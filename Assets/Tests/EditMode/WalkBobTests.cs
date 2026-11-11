using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>EditMode tests for the placeholder walking bob rate (T-262, MOV-09).</summary>
    public class WalkBobTests
    {
        private static Citizen WalkerOnCell(double multiplier, SimTuning t)
        {
            // What MovementSystem leaves behind after one tick of walking at the cell's multiplier.
            var c = new Citizen(1, "A", Sex.Male, 0, 1, 10, 10);
            c.PreviousX = 10; c.PreviousZ = 10;
            c.CurrentMultiplier = multiplier;
            c.X = 10 + t.WalkSpeedMPerGameMinute * multiplier;
            return c;
        }

        [Test]
        public void RoadCellStepsOnePointTwoFiveTimesFasterThanOffRoad()
        {
            var t = new SimTuning();
            const double spm = 0.7, minutesPerSecond = 2.0;
            double off = WalkBob.StepsPerRealSecond(WalkerOnCell(1.0, t), minutesPerSecond, spm);
            double road = WalkBob.StepsPerRealSecond(WalkerOnCell(1.25, t), minutesPerSecond, spm);
            Assert.Greater(off, 0);
            Assert.AreEqual(1.25 * off, road, 1e-9);
        }

        [Test]
        public void StandingOrPausedAgentDoesNotBob()
        {
            var c = new Citizen(1, "A", Sex.Male, 0, 1, 5, 5);
            Assert.AreEqual(0, WalkBob.StepsPerRealSecond(c, 2.0, 0.7));
            Assert.AreEqual(0, WalkBob.StepsPerRealSecond(WalkerOnCell(1.0, new SimTuning()), 2.0, 0.7, paused: true));
        }

        [Test]
        public void RateScalesWithStepsPerMetre()
        {
            var w = WalkerOnCell(1.0, new SimTuning());
            Assert.AreEqual(2 * WalkBob.StepsPerRealSecond(w, 1.0, 0.5), WalkBob.StepsPerRealSecond(w, 1.0, 1.0), 1e-9);
        }

        [Test]
        public void OffsetIsZeroAtWholeStepsAndPeaksBetween()
        {
            Assert.AreEqual(0, WalkBob.Offset(1.0, 0.1), 1e-9);
            Assert.AreEqual(0.1, WalkBob.Offset(0.5, 0.1), 1e-9);
        }
    }
}
