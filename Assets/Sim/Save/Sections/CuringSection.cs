using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Thatch;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>The Curing save section (T-573, decision 0034, id 12, version 37): curing stooks and the id counter.</summary>
    public sealed class CuringSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 12;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            CuringRegistry c = world.Curing;
            w.WriteInt32(c.NextIdValue);
            w.WriteInt32(c.Count);
            foreach (StookRecord s in c.All)
            {
                w.WriteInt32(s.Id);
                w.WriteInt64(s.Position.X);
                w.WriteInt64(s.Position.Z);
                w.WriteInt32(s.OperationId);
                w.WriteInt32(s.Count);
                w.WriteInt64(s.ReadyTick);
            }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            CuringRegistry c = world.Curing;
            c.RestoreClear();
            int next = r.ReadInt32();
            int n = r.ReadCount(36);
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                long x = r.ReadInt64();
                long z = r.ReadInt64();
                int op = r.ReadInt32();
                int count = r.ReadInt32();
                long ready = r.ReadInt64();
                if (count <= 0) throw new SaveFormatException("Bad stook count " + count + ".");
                try { c.RestoreStook(id, new PointMm(x, z), op, count, ready); }
                catch (System.InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            }
            c.RestoreNextId(next);
        }
    }
}
