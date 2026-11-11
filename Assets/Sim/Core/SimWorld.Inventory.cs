using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The inventory part of <see cref="SimWorld"/> (T-273).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The item ledger. Never null; empty at start.</summary>
        public Ledger Inventory { get; } = new Ledger();
    }
}
