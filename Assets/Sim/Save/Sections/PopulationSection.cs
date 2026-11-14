using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Population save section (decision 0034): families (name, home, workplace, member ids), citizens, oxen
    /// (including <see cref="OxState"/>) and the sets of every id ever used, so ids are never reused after load.
    /// Move orders, follow links and debug sends belong to the Movement section.
    /// </summary>
    public sealed class PopulationSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 5;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            Population p = world.People;
            w.WriteInt32(p.FamilyCount);
            foreach (Family f in p.Families)
            {
                w.WriteInt32(f.Id);
                w.WriteString(f.Surname);
                w.WriteDouble(f.HomeX);
                w.WriteDouble(f.HomeZ);
                w.WriteBool(f.WorkplaceId.HasValue);
                w.WriteInt32(f.WorkplaceId ?? 0);
                w.WriteInt32(f.MemberIds.Count);
                foreach (int m in f.MemberIds) w.WriteInt32(m);
            }
            w.WriteInt32(p.CitizenCount);
            foreach (Citizen c in p.Citizens)
            {
                w.WriteInt32(c.Id);
                w.WriteString(c.Name);
                w.WriteInt32((int)c.Sex);
                w.WriteInt64(c.BirthTick);
                w.WriteInt32(c.FamilyId);
                w.WriteDouble(c.X);
                w.WriteDouble(c.Z);
                w.WriteDouble(c.FacingRad);
                w.WriteInt32((int)c.Activity);
                w.WriteDouble(c.PreviousX);
                w.WriteDouble(c.PreviousZ);
                w.WriteDouble(c.CurrentMultiplier);
            }
            w.WriteInt32(p.OxCount);
            foreach (Ox o in p.Oxen)
            {
                w.WriteInt32(o.Id);
                w.WriteString(o.Name);
                w.WriteDouble(o.HomeX);
                w.WriteDouble(o.HomeZ);
                w.WriteDouble(o.X);
                w.WriteDouble(o.Z);
                w.WriteDouble(o.FacingRad);
                w.WriteInt32((int)o.Activity);
                w.WriteInt32((int)o.State.Kind);
                w.WriteInt32(o.State.TaskId);
                w.WriteInt32(o.State.HandlerId);
                w.WriteInt64(o.State.SpotXMm);
                w.WriteInt64(o.State.SpotZMm);
                w.WriteInt32(o.State.StandId);
                w.WriteDouble(o.PreviousX);
                w.WriteDouble(o.PreviousZ);
                w.WriteDouble(o.CurrentMultiplier);
                w.WriteInt32(o.CarrierId);
            }
            WriteIds(w, p.UsedFamilyIds);
            WriteIds(w, p.UsedCitizenIds);
            WriteIds(w, p.UsedOxIds);
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            try
            {
                ReadInto(world, r);
            }
            catch (InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            catch (ArgumentException e) { throw new SaveFormatException(e.Message); }
        }

        private static void ReadInto(SimWorld world, SaveReader r)
        {
            Population p = world.People;
            int nf = r.ReadCount(33);
            for (int i = 0; i < nf; i++)
            {
                int id = r.ReadInt32();
                string surname = r.ReadString();
                double hx = r.ReadDouble();
                double hz = r.ReadDouble();
                bool hasWork = r.ReadBool();
                int work = r.ReadInt32();
                int nm = r.ReadCount(4);
                var members = new List<int>(nm);
                for (int j = 0; j < nm; j++) members.Add(r.ReadInt32());
                p.RestoreFamily(id, surname, hx, hz, hasWork ? work : (int?)null, members);
            }
            int nc = r.ReadCount(76);
            for (int i = 0; i < nc; i++)
            {
                int id = r.ReadInt32();
                string name = r.ReadString();
                int sex = r.ReadInt32();
                long birth = r.ReadInt64();
                int fam = r.ReadInt32();
                double x = r.ReadDouble(), z = r.ReadDouble(), facing = r.ReadDouble();
                int act = r.ReadInt32();
                double px = r.ReadDouble(), pz = r.ReadDouble(), mult = r.ReadDouble();
                if (!Enum.IsDefined(typeof(Sex), sex)) throw new SaveFormatException("Bad sex " + sex + ".");
                if (!Enum.IsDefined(typeof(Activity), act)) throw new SaveFormatException("Bad activity " + act + ".");
                p.RestoreCitizen(id, name, (Sex)sex, birth, fam, x, z, facing, (Activity)act, px, pz, mult);
            }
            int no = r.ReadCount(100);
            for (int i = 0; i < no; i++)
            {
                int id = r.ReadInt32();
                string name = r.ReadString();
                double hx = r.ReadDouble(), hz = r.ReadDouble(), x = r.ReadDouble(), z = r.ReadDouble(), facing = r.ReadDouble();
                int act = r.ReadInt32();
                int kind = r.ReadInt32();
                int task = r.ReadInt32();
                int handler = r.ReadInt32();
                long sx = r.ReadInt64(), sz = r.ReadInt64();
                int stand = r.ReadInt32();
                double px = r.ReadDouble(), pz = r.ReadDouble(), mult = r.ReadDouble();
                int carrier = r.ReadInt32();
                if (!Enum.IsDefined(typeof(Activity), act)) throw new SaveFormatException("Bad activity " + act + ".");
                OxState state;
                switch ((OxStateKind)kind)
                {
                    case OxStateKind.Free: state = OxState.Free; break;
                    case OxStateKind.Reserved: state = OxState.Reserved(task); break;
                    case OxStateKind.Led: state = OxState.Led(handler); break;
                    case OxStateKind.Resting: state = OxState.Resting(sx, sz, stand); break;
                    default: throw new SaveFormatException("Bad ox state " + kind + ".");
                }
                p.RestoreOx(id, name, hx, hz, x, z, facing, (Activity)act, state, px, pz, mult, carrier);
            }
            p.RestoreUsedIds(ReadIds(r), ReadIds(r), ReadIds(r));
            string fault = p.CheckLinks();
            if (fault != null) throw new SaveFormatException(fault);
        }

        private static void WriteIds(SaveWriter w, IEnumerable<int> ids)
        {
            var list = new List<int>(ids);
            w.WriteInt32(list.Count);
            foreach (int i in list) w.WriteInt32(i);
        }

        private static List<int> ReadIds(SaveReader r)
        {
            int n = r.ReadCount(4);
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
            return list;
        }
    }
}
