using LordsLegacy.Sim.Markets;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The markets part of <see cref="SimWorld"/> (T-456, markets.md).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The town's market records. Never null; empty at start.</summary>
        public MarketRegistry Markets { get; } = new MarketRegistry();
    }
}
