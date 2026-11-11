using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The operations part of <see cref="SimWorld"/> (T-268).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>All production operations. Never null; empty at start.</summary>
        public OperationRegistry Operations { get; } = new OperationRegistry();
    }
}
