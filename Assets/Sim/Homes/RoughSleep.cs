using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>
    /// Where a family made homeless after the camp has gone dosses about the town (camp.md CMP-25): the market centre,
    /// or the old camp fire spot when there is no market. No tent; HM-11 still moves the family into the next free house.
    /// </summary>
    public static class RoughSleep
    {
        /// <summary>Status text for a Homeless family with no camp (CMP-25).</summary>
        public const string StatusText = "Homeless (sleeping rough)";

        /// <summary>Status text for a Homeless family while the camp stands (HM-22).</summary>
        public const string CampStatusText = "Homeless (camp)";

        /// <summary>The rough-sleeping spot: market centre, else the saved old fire spot, else null.</summary>
        public static PointMm? Spot(SimWorld world)
        {
            Market m = world.Markets.Current;
            if (m != null && world.Areas.Get(m.AreaId) != null) return MarketRange.CentreOf(world, m);
            return world.RoughSleepSpot;
        }

        /// <summary>True when the family is Homeless and the camp is gone, so it sleeps rough.</summary>
        public static bool IsSleepingRough(SimWorld world, Family f) => f != null && f.IsHomeless && world.Camp == null;

        /// <summary>The home-line text for a family: its status when Homeless, else null.</summary>
        public static string Status(SimWorld world, Family f) =>
            f == null || !f.IsHomeless ? null : world.Camp == null ? StatusText : CampStatusText;

        /// <summary>The point a Homeless family lives at: camp fire while the camp stands, else the rough spot, else null.</summary>
        public static PointMm? HomelessPoint(SimWorld world)
        {
            if (world.Camp != null) return world.Camp.Fire;
            return Spot(world);
        }
    }
}
