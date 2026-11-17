namespace LordsLegacy.Sim.Core
{
    /// <summary>The settlement message log part of <see cref="SimWorld"/> (T-498, decision 0045). Saved by the Core section.</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The last few settlement events. Never null; empty at the start.</summary>
        public SettlementLog SettlementLog { get; } = new SettlementLog();
    }
}
