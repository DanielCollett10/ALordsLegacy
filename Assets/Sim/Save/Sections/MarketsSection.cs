using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Markets;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Markets save section (T-456, decision 0034, id 11, version 12): each market with its area id and stall
    /// footprint ids, plus the id counter. Read after Homes (areas and footprints exist by then).
    /// </summary>
    public sealed class MarketsSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 11;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            MarketRegistry reg = world.Markets;
            w.WriteInt32(reg.NextIdValue);
            w.WriteInt32(reg.Count);
            foreach (Market m in reg.All)
            {
                w.WriteInt32(m.Id);
                w.WriteInt32(m.AreaId);
                w.WriteInt32(m.StallFootprintIds.Count);
                foreach (int s in m.StallFootprintIds) w.WriteInt32(s);
            }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            MarketRegistry reg = world.Markets;
            reg.RestoreClear();
            int next = r.ReadInt32();
            int n = r.ReadCount(12);
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32(), area = r.ReadInt32();
                if (world.Areas.Get(area) == null) throw new SaveFormatException("Market " + id + " has missing area " + area + ".");
                Market m;
                try { m = reg.Restore(id, area); }
                catch (System.ArgumentException e) { throw new SaveFormatException(e.Message); }
                int stalls = r.ReadCount(4);
                for (int k = 0; k < stalls; k++)
                {
                    int fp = r.ReadInt32();
                    if (world.Footprints.Get(fp) == null) throw new SaveFormatException("Market " + id + " has missing stall " + fp + ".");
                    m.StallFootprintIds.Add(fp);
                }
            }
            if (reg.Count > 1) throw new SaveFormatException("More than one market.");
            reg.RestoreNextId(next);
        }
    }
}
