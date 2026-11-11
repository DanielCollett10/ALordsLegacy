namespace LordsLegacy.Sim.Core
{
    /// <summary>
    /// The random source part of <see cref="SimWorld"/> (T-250). Seeded from <c>SimTuning.WorldSeed</c>.
    /// Routine choices (CLK-07) draw from here, never from <c>System.Random</c>.
    /// </summary>
    public sealed partial class SimWorld
    {
        /// <summary>
        /// The world's one deterministic random source. Never null. Built from <c>Tuning.WorldSeed</c>, so two worlds
        /// with the same tuning draw the same sequence. Its <see cref="SimRandom.State"/> is what a save stores.
        /// </summary>
        public SimRandom Random { get; private set; }

        private void InitRandom()
        {
            Random = new SimRandom(unchecked((ulong)(long)Tuning.WorldSeed));
        }
    }
}
