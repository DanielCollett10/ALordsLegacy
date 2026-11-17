using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Ledger save section (decision 0034, 0032): every location (kind, position, owner, module id, capacity, counts),
    /// every reservation, the produced and consumed journals and both id counters. Reserved and incoming totals are
    /// rebuilt from the reservations. After loading it re-attaches the camp stockpile, checks site location ids and
    /// runs <see cref="Ledger.Audit"/>; a fault is a <see cref="SaveFormatException"/>.
    /// </summary>
    public sealed class LedgerSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 4;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            Ledger led = world.Inventory;
            w.WriteInt32(led.NextLocationId);
            w.WriteInt32(led.NextReservationIdValue);
            int n = 0;
            foreach (LedgerLocation unused in led.Locations) n++;
            w.WriteInt32(n);
            foreach (LedgerLocation l in led.Locations)
            {
                w.WriteInt32(l.Id);
                w.WriteInt32((int)l.Kind);
                w.WriteInt64(l.Position.X);
                w.WriteInt64(l.Position.Z);
                w.WriteBool(l.OwnerOperationId.HasValue);
                w.WriteInt32(l.OwnerOperationId ?? 0);
                w.WriteInt32(l.ModuleId);
                w.WriteInt32(l.Capacity);
                w.WriteString(l.InputItemId ?? "");
                WriteMap(w, l.Counts);
            }
            n = 0;
            foreach (Reservation unused in led.Reservations) n++;
            w.WriteInt32(n);
            foreach (Reservation r in led.Reservations)
            {
                w.WriteInt32(r.Id);
                w.WriteInt32((int)r.Kind);
                w.WriteInt32(r.LocationId);
                w.WriteString(r.ItemId);
                w.WriteInt32(r.Count);
                w.WriteInt32(r.Owner);
            }
            WritePairs(w, led.ProducedJournal);
            WritePairs(w, led.ConsumedJournal);
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
            catch (OverflowException e) { throw new SaveFormatException(e.Message); }
        }

        private static void ReadInto(SimWorld world, SaveReader r)
        {
            Ledger led = world.Inventory;
            int nextLoc = r.ReadInt32();
            int nextRes = r.ReadInt32();
            int count = r.ReadCount(41);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                int kind = r.ReadInt32();
                long x = r.ReadInt64();
                long z = r.ReadInt64();
                bool hasOwner = r.ReadBool();
                int owner = r.ReadInt32();
                int module = r.ReadInt32();
                int capacity = r.ReadInt32();
                string input = r.ReadString();
                List<KeyValuePair<string, int>> counts = ReadPairs(r);
                if (kind < (int)LocationKind.GroundPile || kind > (int)LocationKind.WorkshopStock) throw new SaveFormatException("Bad location kind " + kind + ".");
                if (id >= nextLoc) throw new SaveFormatException("Location id " + id + " is not below the id counter " + nextLoc + ".");
                led.RestoreLocation(id, (LocationKind)kind, new PointMm(x, z), hasOwner ? owner : (int?)null, module, capacity, counts, input.Length == 0 ? null : input);
            }
            int resCount = r.ReadCount(24);
            for (int i = 0; i < resCount; i++)
            {
                int id = r.ReadInt32();
                int kind = r.ReadInt32();
                int loc = r.ReadInt32();
                string item = r.ReadString();
                int n = r.ReadInt32();
                int owner = r.ReadInt32();
                if (kind < (int)ReservationKind.Stock || kind > (int)ReservationKind.Capacity) throw new SaveFormatException("Bad reservation kind " + kind + ".");
                if (id >= nextRes) throw new SaveFormatException("Reservation id " + id + " is not below the id counter " + nextRes + ".");
                led.RestoreReservation(id, (ReservationKind)kind, loc, item, n, owner);
            }
            led.RestoreJournalAndCounters(ReadPairs(r), ReadPairs(r), nextLoc, nextRes);

            if (world.Camp != null && world.Camp.StockpileLocationId != 0)
            {
                LedgerLocation stock = led.Get(world.Camp.StockpileLocationId);
                if (stock == null) throw new SaveFormatException("Camp stockpile location " + world.Camp.StockpileLocationId + " is missing.");
                world.Camp.StockpileLocation = stock;
            }
            if (world.Camp != null && world.Camp.GeneralStoreLocationId != 0)
            {
                LedgerLocation general = led.Get(world.Camp.GeneralStoreLocationId);
                if (general == null || general.Kind != LocationKind.GoodsStore)
                    throw new SaveFormatException("Camp General store " + world.Camp.GeneralStoreLocationId + " is missing.");
                world.Camp.GeneralStoreLocation = general;
            }
            foreach (ConstructionSite s in world.Construction.Sites)
                if (s.LedgerLocationId != 0 && led.Get(s.LedgerLocationId) == null)
                    throw new SaveFormatException("Construction site " + s.FootprintId + " uses missing location " + s.LedgerLocationId + ".");

            string fault = led.Audit();
            if (fault != null) throw new SaveFormatException("Ledger audit failed: " + fault);
        }

        private static void WriteMap(SaveWriter w, IEnumerable<KeyValuePair<string, int>> map) => WritePairs(w, map);

        private static void WritePairs(SaveWriter w, IEnumerable<KeyValuePair<string, int>> pairs)
        {
            int n = 0;
            foreach (KeyValuePair<string, int> unused in pairs) n++;
            w.WriteInt32(n);
            foreach (KeyValuePair<string, int> kv in pairs)
            {
                w.WriteString(kv.Key);
                w.WriteInt32(kv.Value);
            }
        }

        private static List<KeyValuePair<string, int>> ReadPairs(SaveReader r)
        {
            int n = r.ReadCount(8);
            var list = new List<KeyValuePair<string, int>>(n);
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                int v = r.ReadInt32();
                list.Add(new KeyValuePair<string, int>(key, v));
            }
            return list;
        }
    }
}
