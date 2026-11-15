using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Homes save section (T-405, decision 0034): each house with its plot area id and family id (0 = Empty).
    /// Read after Structures (footprints, sites) and Population (families); it sets each family's home link.
    /// </summary>
    public sealed class HomesSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 8;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            w.WriteInt32(world.Homes.Count);
            foreach (HomeRecord h in world.Homes.All)
            {
                w.WriteInt32(h.HouseFootprintId);
                w.WriteInt32(h.PlotAreaId);
                w.WriteInt32(h.FamilyId);
            }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            try
            {
                int n = r.ReadCount(12);
                for (int i = 0; i < n; i++)
                {
                    int house = r.ReadInt32(), plot = r.ReadInt32(), fam = r.ReadInt32();
                    if (world.Footprints.Get(house) == null) throw new SaveFormatException("Home " + house + " has no house footprint.");
                    if (world.Areas.Get(plot) == null) throw new SaveFormatException("Home " + house + " has missing plot " + plot + ".");
                    world.Homes.Restore(house, plot, fam);
                    if (fam == 0) continue;
                    Family f = world.People.FindFamily(fam);
                    if (f == null) throw new SaveFormatException("Home " + house + " has missing family " + fam + ".");
                    if (f.HomeHouseId.HasValue) throw new SaveFormatException("Family " + fam + " has two homes.");
                    f.HomeHouseId = house;
                }
            }
            catch (ArgumentException e) { throw new SaveFormatException(e.Message); }
        }
    }
}
