using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The population part of <see cref="SimWorld"/> (T-251): families, citizens and oxen.</summary>
    public sealed partial class SimWorld
    {
        /// <summary>All families, citizens and oxen. Never null; empty until a start layout fills it.</summary>
        public Population People { get; } = new Population();
    }
}
