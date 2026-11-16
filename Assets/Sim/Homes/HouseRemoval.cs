using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>
    /// A house leaving the world (homes.md HM-14, HM-14b). <see cref="Evict"/> runs when a built house is marked for
    /// deconstruction: its family becomes Homeless and goes back to the camp tent. <see cref="RemoveWithPlot"/> runs when
    /// the house is gone (its deconstruction finished, or an unbuilt site was cancelled): its home and plot area go too.
    /// </summary>
    public static class HouseRemoval
    {
        /// <summary>
        /// Moves the family out of a house (HM-14). The house becomes Empty and the family Homeless, with its home point at the
        /// camp fire (the tent spot is not saved, so the fire stands in for it). Without a camp the point is kept.
        /// Does nothing for a house with no family.
        /// </summary>
        public static void Evict(SimWorld world, int houseFootprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            HomeRecord home = world.Homes.Get(houseFootprintId);
            if (home == null) return;
            if (home.FamilyId != 0)
            {
                Family f = world.People.FindFamily(home.FamilyId);
                if (f != null && f.HomeHouseId == houseFootprintId)
                {
                    Food.Larders.OnEvict(world, home, f);
                    f.HomeHouseId = null;
                    PointMm? spot = RoughSleep.HomelessPoint(world); // camp fire, or the rough-sleeping spot (CMP-25)
                    if (spot.HasValue)
                    {
                        f.HomeX = spot.Value.XMetres;
                        f.HomeZ = spot.Value.ZMetres;
                    }
                }
            }
            world.Homes.Vacate(home);
        }

        /// <summary>
        /// Removes a house's home record and its plot area (HM-14b), evicting any family first. Returns the removed plot
        /// area id, or null when the footprint has no home (nothing changes).
        /// </summary>
        public static int? RemoveWithPlot(SimWorld world, int houseFootprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            HomeRecord home = world.Homes.Get(houseFootprintId);
            if (home == null) return null;
            Evict(world, houseFootprintId);
            int plot = home.PlotAreaId;
            world.Homes.Remove(houseFootprintId);
            world.Areas.Remove(plot);
            return plot;
        }
    }
}
