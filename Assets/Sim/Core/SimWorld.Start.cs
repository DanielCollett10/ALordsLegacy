using LordsLegacy.Sim.Start;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The start-layout part of <see cref="SimWorld"/> (T-253).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The camp placed by <see cref="StartLayoutApplier"/>, or null if no layout was applied.</summary>
        public CampInfo Camp { get; internal set; }

        /// <summary>The old camp fire spot, kept at camp removal as the rough-sleeping fallback (CMP-25). Saved in the Structures section.</summary>
        public Geometry.PointMm? RoughSleepSpot { get; internal set; }

        /// <summary>
        /// True once a built granary has supplied the market (CMP-20): the camp is no longer a food supply and its food
        /// store is emptied into granaries (CMP-21). Set once, never cleared (CMP-22). Saved in the Food section.
        /// </summary>
        public bool FoodHandedOver { get; internal set; }

        /// <summary>Households fully topped up at the last Sunday allotment (T-465). Saved in the Food section.</summary>
        public int LastAllotmentServed { get; internal set; }

        /// <summary>Eating households left short (out of range, or stock ran out) at the last Sunday allotment. Saved in the Food section.</summary>
        public int LastAllotmentShort { get; internal set; }
    }
}
