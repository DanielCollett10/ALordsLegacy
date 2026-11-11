using System;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Placeholder walking motion (MOV-09): a vertical bob whose rate follows how fast the agent really moves.
    /// Steps per real second = metres walked per real second × steps per metre, so an agent on a 1.25× road cell
    /// steps 1.25× as fast as one off-road. Pure maths; no Unity types, so it is covered by EditMode tests.
    /// </summary>
    public static class WalkBob
    {
        /// <summary>Distance below which an agent counts as standing still (metres moved in one tick).</summary>
        public const double StillThresholdM = 1e-6;

        /// <summary>Metres the agent covered in its latest Sim tick (previous position to current position).</summary>
        public static double MetresPerTick(IMovable agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            double dx = agent.X - agent.PreviousX, dz = agent.Z - agent.PreviousZ;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Steps per real second: metres per tick × game minutes (ticks) per real second × steps per metre.
        /// Zero when the agent did not move in its latest tick or when paused.
        /// </summary>
        public static double StepsPerRealSecond(IMovable agent, double gameMinutesPerRealSecond, double stepsPerMetre,
            bool paused = false)
        {
            if (paused) return 0;
            double d = MetresPerTick(agent);
            if (d <= StillThresholdM) return 0;
            return d * gameMinutesPerRealSecond * stepsPerMetre;
        }

        /// <summary>
        /// Vertical offset in metres for a gait phase measured in steps: each step is one hump, so the bob is
        /// |sin(pi * phase)| × amplitude. Zero at whole steps.
        /// </summary>
        public static double Offset(double phaseSteps, double amplitudeM)
        {
            return Math.Abs(Math.Sin(Math.PI * phaseSteps)) * amplitudeM;
        }
    }
}
