using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>Houses split by walking range from a market centre. Both lists are in ascending house footprint id.</summary>
    public sealed class HouseRangeResult
    {
        /// <summary>Houses whose walking distance is within range (MK-34).</summary>
        public IReadOnlyList<int> InRange { get; }

        /// <summary>Houses further than the range, or with no walking route at all.</summary>
        public IReadOnlyList<int> OutOfRange { get; }

        internal HouseRangeResult(List<int> inRange, List<int> outOfRange)
        {
            InRange = inRange;
            OutOfRange = outOfRange;
        }
    }

    /// <summary>
    /// Pure market range queries (MK-34, MK-41). Walking distance is the same measure the Sunday allotment uses
    /// (<see cref="AllotmentSystem.WalkingDistanceM"/>): the shortest walk over the nav grid from the centre to each
    /// house's access point, in metres. The range is <c>SimTuning.MarketRangeM</c>, inclusive.
    /// </summary>
    public static class MarketRange
    {
        /// <summary>
        /// Splits every house (a footprint of type <see cref="ModuleTypes.HouseId"/>) by walking distance from
        /// <paramref name="centre"/> to its access point. A house with no walking route counts as out of range.
        /// The centre may be a draft point that is not a market yet (MK-41 preview). Brings the nav grid up to date
        /// first, so a house placed this tick is measured against current footprints; that cache is the only write.
        /// </summary>
        public static HouseRangeResult HousesInRange(SimWorld world, PointMm centre)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.Nav.Sync(world);
            double range = world.Tuning.MarketRangeM;
            var from = new PathPoint(centre.XMetres, centre.ZMetres);
            var inRange = new List<int>();
            var outOfRange = new List<int>();
            foreach (Footprint fp in world.Footprints.All)
            {
                if (fp.Type.Id != ModuleTypes.HouseId) continue;
                var to = new PathPoint(fp.AccessPoint.XMetres, fp.AccessPoint.ZMetres);
                double d = AllotmentSystem.WalkingDistanceM(world, from, to);
                if (d >= 0 && d <= range) inRange.Add(fp.Id);
                else outOfRange.Add(fp.Id);
            }
            return new HouseRangeResult(inRange, outOfRange);
        }

        /// <summary>
        /// The area centre point of a market: the centroid of its area polygon (degenerate shapes use the bounding box
        /// centre). Throws if the market is null or its area is missing from the world.
        /// </summary>
        public static PointMm CentreOf(SimWorld world, Market market)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (market == null) throw new ArgumentNullException(nameof(market));
            AreaRecord area = world.Areas.Get(market.AreaId);
            if (area == null) throw new InvalidOperationException("Market " + market.Id + " has no area " + market.AreaId);
            return Polygons.Centroid(area.Polygon);
        }
    }
}
