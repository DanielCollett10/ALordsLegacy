using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>
    /// The grown grain (straw, chaff) source on the thatcher's hut panel (MAT-31). Derived, never stored. Straw comes
    /// from fields, and no field exists in the Sim yet, so the source is always unavailable and shows "Needs farming".
    /// </summary>
    public static class GrownGrainSource
    {
        /// <summary>The greyed-out label shown while the source is unavailable.</summary>
        public const string NeedsFarmingText = "Needs farming";

        /// <summary>True when grown grain can be used. Always false until farming (fields) exists in the Sim.</summary>
        public static bool Available(SimWorld world) => false;

        /// <summary>The label for the source: the grain name when available, else <see cref="NeedsFarmingText"/>.</summary>
        public static string Text(SimWorld world) => Available(world) ? "Grown grain" : NeedsFarmingText;
    }
}
