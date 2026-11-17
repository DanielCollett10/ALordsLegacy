using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>
    /// Operations-phase system: moves Homeless families into Built, Empty houses (HM-11, HM-13). Houses are served in
    /// ascending footprint id. Homeless families with a workplace come first, nearest workplace to the house
    /// (straight line, centre of its area) first; if none has a workplace, any Homeless family; ties by lowest family id.
    /// A reserved house is never filled here. A house with no Homeless family stays Empty and is tried again every tick, so it fills when one exists.
    /// The family's home point becomes the house's access point. Families with no members never move in.
    /// </summary>
    public sealed class MoveInSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Homes.Count == 0) return;
            foreach (HomeRecord home in new System.Collections.Generic.List<HomeRecord>(world.Homes.All))
            {
                if (home.FamilyId != 0 || home.ReservedForFamilyId != 0) continue; // reserved for an arriving family (GR-22)
                Footprint house = world.Footprints.Get(home.HouseFootprintId);
                if (house == null || !world.IsWorking(house.Id)) continue;
                Family f = Choose(world, house);
                if (f == null) continue;
                if (world.Homes.MoveIn(home, f)) Food.Larders.OnMoveIn(world, home, f);
                f.HomeX = house.AccessPoint.XMetres;
                f.HomeZ = house.AccessPoint.ZMetres;
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        /// <summary>The family HM-11 picks for a house, or null when no Homeless family exists. Pure.</summary>
        public static Family Choose(SimWorld world, Footprint house)
        {
            Family anyHomeless = null, best = null;
            double bestDist = 0;
            foreach (Family f in world.People.Families) // ascending id, so the first of equal distance wins
            {
                if (f.IsInPassage || !f.IsHomeless || f.MemberIds.Count == 0) continue;
                if (anyHomeless == null) anyHomeless = f;
                if (!f.WorkplaceId.HasValue) continue;
                Operation op = world.Operations.Get(f.WorkplaceId.Value);
                var area = op == null ? null : world.Areas.Get(op.AreaId);
                if (area == null) continue;
                double cx = (area.Bounds.Min.XMetres + area.Bounds.Max.XMetres) / 2.0;
                double cz = (area.Bounds.Min.ZMetres + area.Bounds.Max.ZMetres) / 2.0;
                double dx = cx - house.Centre.XMetres, dz = cz - house.Centre.ZMetres;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (best == null || d < bestDist) { best = f; bestDist = d; }
            }
            return best ?? anyHomeless;
        }
    }
}
