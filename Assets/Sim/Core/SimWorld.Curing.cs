using LordsLegacy.Sim.Thatch;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The curing stook part of <see cref="SimWorld"/> (T-573, MAT-32).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>Curing reed stooks on the ground. Never null. Saved in the Curing section.</summary>
        public CuringRegistry Curing { get; } = new CuringRegistry();
    }
}
