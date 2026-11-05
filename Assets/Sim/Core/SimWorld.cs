namespace NetSim.Sim.Core
{
    /// <summary>
    /// Root of the simulation. Owns the clock and, later, every device, link and packet.
    /// Pure C#: no UnityEngine. The Unity layer calls <see cref="Step"/> on a fixed timestep
    /// and reads state to render it.
    /// </summary>
    public sealed class SimWorld
    {
        /// <summary>Number of ticks simulated since the world was created.</summary>
        public long Tick { get; private set; }

        /// <summary>Simulated seconds per tick.</summary>
        public double SecondsPerTick { get; }

        public SimWorld(double secondsPerTick = 0.05)
        {
            if (secondsPerTick <= 0) throw new System.ArgumentOutOfRangeException(nameof(secondsPerTick));
            SecondsPerTick = secondsPerTick;
        }

        /// <summary>Total simulated time in seconds.</summary>
        public double SimulatedSeconds => Tick * SecondsPerTick;

        /// <summary>Advance the simulation by <paramref name="ticks"/> ticks.</summary>
        public void Step(int ticks = 1)
        {
            if (ticks < 0) throw new System.ArgumentOutOfRangeException(nameof(ticks));
            for (var i = 0; i < ticks; i++) Tick++;
        }
    }
}
