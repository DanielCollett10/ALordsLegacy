using LordsLegacy.Sim.Start;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The start-layout part of <see cref="SimWorld"/> (T-253).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The camp placed by <see cref="StartLayoutApplier"/>, or null if no layout was applied.</summary>
        public CampInfo Camp { get; internal set; }
    }
}
