using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Start;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Structures save section (decision 0034): footprints (type id, pose, id counter), the camp info,
    /// operations (with counter) and construction sites. It is read before the Ledger section, so the camp stockpile
    /// and site locations are stored as ledger location ids only.
    /// </summary>
    public sealed class StructuresSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 3;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            WriteFootprints(world.Footprints, w);
            WriteCamp(world.Camp, w);
            w.WriteBool(world.RoughSleepSpot.HasValue); // T-468
            if (world.RoughSleepSpot.HasValue) WritePoint(w, world.RoughSleepSpot.Value);
            WriteOperations(world.Operations, w);
            WriteSites(world.Construction, w);
            WriteDeconstruction(world.Deconstruction, w);
            WritePendingOperations(world.Deconstruction, w);
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            try
            {
                ReadFootprints(world.Footprints, r);
                ReadCamp(world, r);
                world.RoughSleepSpot = r.ReadBool() ? ReadPoint(r) : (PointMm?)null;
                ReadOperations(world.Operations, r);
                ReadSites(world, r);
                ReadDeconstruction(world, r);
                ReadPendingOperations(world, r);
            }
            catch (InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            catch (ArgumentException e) { throw new SaveFormatException(e.Message); }
        }

        private static void WriteFootprints(FootprintRegistry fps, SaveWriter w)
        {
            w.WriteInt32(fps.NextId());
            w.WriteInt32(fps.Count);
            foreach (Footprint f in fps.All)
            {
                w.WriteInt32(f.Id);
                w.WriteString(f.Type.Id);
                WritePoint(w, f.Centre);
                w.WriteDouble(f.RotationDegrees);
            }
        }

        private static void ReadFootprints(FootprintRegistry fps, SaveReader r)
        {
            int next = r.ReadInt32();
            int count = r.ReadCount(28);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                string typeId = r.ReadString();
                PointMm centre = ReadPoint(r);
                double rot = r.ReadDouble();
                FootprintTypeDef type = typeId == FootprintTypeDef.CampId ? FootprintTypeDef.Camp : ModuleTypes.Find(typeId);
                if (type == null) throw new SaveFormatException("Unknown footprint type '" + typeId + "'.");
                fps.Add(id, type, centre, rot);
            }
            fps.RestoreNextId(next);
        }

        private static void WriteCamp(CampInfo c, SaveWriter w)
        {
            w.WriteBool(c != null);
            if (c == null) return;
            w.WriteInt32(c.FootprintId);
            WritePoint(w, c.Fire);
            WritePoint(w, c.Stockpile);
            WritePoint(w, c.OxStand);
            w.WriteInt32(c.StockpileLocationId);
            w.WriteInt32(c.GeneralStoreLocationId); // T-513
        }

        private static void ReadCamp(SimWorld world, SaveReader r)
        {
            if (!r.ReadBool()) { world.Camp = null; return; }
            int fp = r.ReadInt32();
            PointMm fire = ReadPoint(r), stock = ReadPoint(r), stand = ReadPoint(r);
            int locId = r.ReadInt32(), generalId = r.ReadInt32();
            if (world.Footprints.Get(fp) == null) throw new SaveFormatException("Camp footprint " + fp + " is missing.");
            var camp = new CampInfo(fp, fire, stock, stand, world.Inventory.Get(locId))
                { StockpileLocationId = locId, GeneralStoreLocationId = generalId };
            world.Camp = camp;
        }

        private static void WriteOperations(OperationRegistry ops, SaveWriter w)
        {
            w.WriteInt32(ops.NextIdValue);
            w.WriteInt32(ops.Count);
            foreach (Operation o in ops.All)
            {
                w.WriteInt32(o.Id);
                w.WriteString(o.TypeId);
                w.WriteInt32(o.AreaId);
                WriteInts(w, o.ModuleIds);
                WriteInts(w, o.FamilyIds);
                WriteInts(w, o.OxIds);
                w.WriteBool(o.WorkAreaId.HasValue);
                w.WriteInt32(o.WorkAreaId ?? 0);
                w.WriteBool(o.Paused);
                w.WriteBool(o.SuppliesMarket);
                w.WriteInt32(o.ReserveOverrides.Count);
                var keys = new List<string>(o.ReserveOverrides.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys) { w.WriteString(key); w.WriteInt32(o.ReserveOverrides[key]); }
            }
        }

        private static void ReadOperations(OperationRegistry ops, SaveReader r)
        {
            int next = r.ReadInt32();
            int count = r.ReadCount(31);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                string type = r.ReadString();
                int area = r.ReadInt32();
                int[] modules = ReadInts(r), families = ReadInts(r), oxen = ReadInts(r);
                bool hasWork = r.ReadBool();
                int work = r.ReadInt32();
                bool paused = r.ReadBool();
                bool supplies = r.ReadBool();
                int nRes = r.ReadCount(8);
                var reserves = new List<KeyValuePair<string, int>>();
                for (int k = 0; k < nRes; k++) { string item = r.ReadString(); reserves.Add(new KeyValuePair<string, int>(item, r.ReadInt32())); }
                ops.RestoreOperation(id, type, area, modules, families, oxen, hasWork ? work : (int?)null, paused, supplies, reserves);
            }
            ops.RestoreNextId(next);
        }

        private static void WriteSites(ConstructionRegistry reg, SaveWriter w)
        {
            w.WriteInt32(reg.Count);
            foreach (ConstructionSite s in reg.Sites)
            {
                w.WriteInt32(s.FootprintId);
                w.WriteInt32((int)s.State);
                w.WriteInt32((int)s.Priority);
                w.WriteInt32(s.LedgerLocationId);
                w.WriteInt32(s.ProgressMinutes);
                w.WriteInt32(s.Consumed.Count);
                foreach (KeyValuePair<string, int> kv in s.Consumed) { w.WriteString(kv.Key); w.WriteInt32(kv.Value); }
                WriteInts(w, s.ReservationIds);
            }
        }

        private static void ReadSites(SimWorld world, SaveReader r)
        {
            int count = r.ReadCount(28);
            for (int i = 0; i < count; i++)
            {
                int fp = r.ReadInt32();
                int state = r.ReadInt32();
                int prio = r.ReadInt32();
                int loc = r.ReadInt32();
                int progress = r.ReadInt32();
                int nConsumed = r.ReadCount(8);
                var consumed = new List<KeyValuePair<string, int>>();
                for (int k = 0; k < nConsumed; k++)
                {
                    string item = r.ReadString();
                    consumed.Add(new KeyValuePair<string, int>(item, r.ReadInt32()));
                }
                int[] reservations = ReadInts(r);
                if (state < 0 || state > (int)ConstructionState.Building) throw new SaveFormatException("Bad construction state " + state + ".");
                if (prio < 0 || prio > (int)ConstructionPriority.High) throw new SaveFormatException("Bad construction priority " + prio + ".");
                if (world.Footprints.Get(fp) == null) throw new SaveFormatException("Construction site for missing footprint " + fp + ".");
                var site = new ConstructionSite(fp, loc, (ConstructionPriority)prio)
                {
                    State = (ConstructionState)state, ProgressMinutes = progress
                };
                foreach (KeyValuePair<string, int> kv in consumed) site.SetConsumed(kv.Key, kv.Value);
                site.ReservationIds.AddRange(reservations);
                world.Construction.Add(site);
            }
        }

        private static void WriteDeconstruction(DeconstructionRegistry reg, SaveWriter w)
        {
            w.WriteInt32(reg.Count);
            foreach (DeconstructionEntry e in reg.Entries)
            {
                w.WriteInt32(e.FootprintId);
                w.WriteInt32(e.ProgressMinutes);
            }
        }

        private static void ReadDeconstruction(SimWorld world, SaveReader r)
        {
            int count = r.ReadCount(8);
            for (int i = 0; i < count; i++)
            {
                int fp = r.ReadInt32();
                int progress = r.ReadInt32();
                if (world.Footprints.Get(fp) == null) throw new SaveFormatException("Deconstruction of missing footprint " + fp + ".");
                if (!world.IsBuilt(fp)) throw new SaveFormatException("Deconstruction of unbuilt footprint " + fp + ".");
                world.Deconstruction.Add(new DeconstructionEntry(fp) { ProgressMinutes = progress });
            }
        }

        private static void WritePendingOperations(DeconstructionRegistry reg, SaveWriter w)
        {
            var ids = new List<int>(reg.PendingOperationIds);
            WriteInts(w, ids);
        }

        private static void ReadPendingOperations(SimWorld world, SaveReader r)
        {
            foreach (int id in ReadInts(r))
            {
                if (world.Operations.Get(id) == null) throw new SaveFormatException("Pending removal of missing operation " + id + ".");
                world.Deconstruction.AddPendingOperation(id);
            }
        }

        private static void WriteInts(SaveWriter w, IReadOnlyList<int> v)
        {
            w.WriteInt32(v.Count);
            for (int i = 0; i < v.Count; i++) w.WriteInt32(v[i]);
        }

        private static int[] ReadInts(SaveReader r)
        {
            int n = r.ReadCount(4);
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = r.ReadInt32();
            return a;
        }

        private static void WritePoint(SaveWriter w, PointMm p) { w.WriteInt64(p.X); w.WriteInt64(p.Z); }

        private static PointMm ReadPoint(SaveReader r)
        {
            long x = r.ReadInt64();
            long z = r.ReadInt64();
            return new PointMm(x, z);
        }
    }
}
