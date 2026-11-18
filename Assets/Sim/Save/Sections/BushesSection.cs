using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Bushes save section (T-421, decision 0034, id 10, version 10): forage bushes with position, kind, baskets,
    /// emptied tick and reservation, plus the id counter. Read right after Land. Replaces the new-game scatter.
    /// </summary>
    public sealed class BushesSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 10;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            BushRegistry b = world.Bushes;
            w.WriteInt32(b.NextIdValue);
            w.WriteInt32(b.Count);
            foreach (BushRecord k in b.All)
            {
                w.WriteInt32(k.Id);
                w.WriteDouble(k.X);
                w.WriteDouble(k.Z);
                w.WriteInt32((int)k.Kind);
                w.WriteInt32(k.Baskets);
                w.WriteInt64(k.EmptiedAtTick);
                w.WriteBool(k.ReservedBy.HasValue);
                w.WriteInt32(k.ReservedBy ?? 0);
            }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            BushRegistry b = world.Bushes;
            b.RestoreClear();
            int next = r.ReadInt32();
            int n = r.ReadCount(33);
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                float x = (float)r.ReadDouble();
                float z = (float)r.ReadDouble();
                int kind = r.ReadInt32();
                int baskets = r.ReadInt32();
                long emptied = r.ReadInt64();
                bool reserved = r.ReadBool();
                int owner = r.ReadInt32();
                if (kind < 0 || kind > (int)BushKind.WillowStool) throw new SaveFormatException("Bad bush kind " + kind + ".");
                if (baskets < 0 || baskets > BushKinds.FullUnits((BushKind)kind, world.Tuning)) throw new SaveFormatException("Bad bush baskets " + baskets + ".");
                if (reserved && baskets == 0) throw new SaveFormatException("Bush " + id + " is reserved but empty.");
                try { b.RestoreBush(id, x, z, (BushKind)kind, baskets, emptied, reserved ? owner : (int?)null); }
                catch (System.InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            }
            b.RestoreNextId(next);
        }
    }
}
