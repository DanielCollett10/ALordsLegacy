using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Reserve, release and atomic transfer (spec section 7 part 2, LED-02..06).</summary>
    public sealed partial class Ledger
    {
        private readonly SortedDictionary<int, Reservation> _reservations = new SortedDictionary<int, Reservation>();
        private int _nextReservationId = 1;

        /// <summary>All live reservations in id order.</summary>
        public IEnumerable<Reservation> Reservations => _reservations.Values;

        /// <summary>
        /// Reserves <paramref name="n"/> items of stock at <paramref name="location"/> (needs available ≥ n).
        /// Returns the reservation id, or 0 when there is not enough available stock.
        /// </summary>
        public int ReserveStock(LedgerLocation location, ItemDef item, int n, int owner)
        {
            Check(location, item, n);
            if (location.AvailableOf(item.Id) < n) return 0;
            Bump(location.ReservedCounts, item.Id, n);
            return AddReservation(ReservationKind.Stock, location, item, n, owner);
        }

        /// <summary>
        /// Reserves room for <paramref name="n"/> incoming items at <paramref name="location"/>
        /// (needs capacity − total − incoming ≥ n). Returns the reservation id, or 0 when full.
        /// </summary>
        public int ReserveCapacity(LedgerLocation location, ItemDef item, int n, int owner)
        {
            Check(location, item, n);
            if (location.FreeSpace < n) return 0;
            Bump(location.IncomingCounts, item.Id, n);
            return AddReservation(ReservationKind.Capacity, location, item, n, owner);
        }

        /// <summary>Releases one reservation. Returns false if the id is unknown.</summary>
        public bool Release(int reservationId)
        {
            if (!_reservations.TryGetValue(reservationId, out var r)) return false;
            Remove(r, r.Count);
            return true;
        }

        /// <summary>Releases every reservation held by an owner (LED-04). Returns how many were released.</summary>
        public int CancelOwner(int owner)
        {
            var mine = new List<Reservation>();
            foreach (var r in _reservations.Values) if (r.Owner == owner) mine.Add(r);
            foreach (var r in mine) Remove(r, r.Count);
            return mine.Count;
        }

        /// <summary>Finds a reservation by id, or null.</summary>
        public Reservation GetReservation(int id) => _reservations.TryGetValue(id, out var r) ? r : null;

        /// <summary>
        /// Moves <paramref name="n"/> items atomically, consuming the matching reservations: stock reservations on
        /// <paramref name="from"/> and capacity reservations on <paramref name="to"/> for this item (only those of
        /// <paramref name="owner"/> when given). Whatever is not covered by a reservation must be available stock /
        /// free space. Returns false, changing nothing, if either side cannot take it.
        /// </summary>
        public bool Transfer(LedgerLocation from, LedgerLocation to, ItemDef item, int n, int? owner = null)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            Check(from, item, n);
            Check(to, item, n);
            if (from == to) return false;
            if (from.CountOf(item.Id) < n) return false;
            int stockCovered = Math.Min(n, Matching(ReservationKind.Stock, from, item, owner));
            if (from.AvailableOf(item.Id) < n - stockCovered) return false;
            int roomCovered = Math.Min(n, Matching(ReservationKind.Capacity, to, item, owner));
            if (to.Capacity != int.MaxValue && to.FreeSpace < n - roomCovered) return false;

            Consume(ReservationKind.Stock, from, item, stockCovered, owner);
            Consume(ReservationKind.Capacity, to, item, roomCovered, owner);
            int left = from.Counts[item.Id] - n;
            if (left == 0) from.Counts.Remove(item.Id); else from.Counts[item.Id] = left;
            to.Counts.TryGetValue(item.Id, out int have);
            to.Counts[item.Id] = checked(have + n);
            Version++;
            return true;
        }

        private static void Check(LedgerLocation location, ItemDef item, int n)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Must be positive.");
        }

        private int AddReservation(ReservationKind kind, LedgerLocation location, ItemDef item, int n, int owner)
        {
            int id = _nextReservationId++;
            _reservations.Add(id, new Reservation(id, kind, location.Id, item.Id, n, owner));
            Version++;
            return id;
        }

        private static void Bump(SortedDictionary<string, int> d, string key, int delta)
        {
            d.TryGetValue(key, out int v);
            v += delta;
            if (v == 0) d.Remove(key); else d[key] = v;
        }

        private void Remove(Reservation r, int count)
        {
            LedgerLocation l = Get(r.LocationId);
            Bump(r.Kind == ReservationKind.Stock ? l.ReservedCounts : l.IncomingCounts, r.ItemId, -count);
            r.Count -= count;
            if (r.Count == 0) _reservations.Remove(r.Id);
            Version++;
        }

        private int Matching(ReservationKind kind, LedgerLocation loc, ItemDef item, int? owner)
        {
            int t = 0;
            foreach (var r in _reservations.Values)
                if (r.Kind == kind && r.LocationId == loc.Id && r.ItemId == item.Id && (owner == null || r.Owner == owner)) t += r.Count;
            return t;
        }

        private void Consume(ReservationKind kind, LedgerLocation loc, ItemDef item, int n, int? owner)
        {
            var list = new List<Reservation>();
            foreach (var r in _reservations.Values)
                if (r.Kind == kind && r.LocationId == loc.Id && r.ItemId == item.Id && (owner == null || r.Owner == owner)) list.Add(r);
            foreach (var r in list)
            {
                if (n == 0) break;
                int take = Math.Min(n, r.Count);
                Remove(r, take);
                n -= take;
            }
        }

        private string AuditReservations()
        {
            foreach (var l in _locations.Values)
            {
                if (l.Capacity != int.MaxValue && l.Total > l.Capacity)
                    return "Location " + l.Id + " holds " + l.Total + " over capacity " + l.Capacity;
                foreach (var kv in l.ReservedCounts)
                    if (kv.Value > l.CountOf(kv.Key)) return "Location " + l.Id + " reserves " + kv.Value + " of " + kv.Key + " but holds " + l.CountOf(kv.Key);
                if (l.Capacity != int.MaxValue && (long)l.Total + l.IncomingTotal > l.Capacity)
                    return "Location " + l.Id + " has incoming beyond capacity";
            }
            var stock = new Dictionary<string, int>();
            var room = new Dictionary<string, int>();
            foreach (var r in _reservations.Values)
            {
                if (r.Count <= 0) return "Reservation " + r.Id + " has count " + r.Count;
                string key = r.LocationId + "|" + r.ItemId;
                var d = r.Kind == ReservationKind.Stock ? stock : room;
                d.TryGetValue(key, out int v);
                d[key] = v + r.Count;
            }
            foreach (var l in _locations.Values)
            {
                foreach (var kv in l.ReservedCounts)
                {
                    stock.TryGetValue(l.Id + "|" + kv.Key, out int v);
                    if (v != kv.Value) return "Location " + l.Id + " reserved total for " + kv.Key + " disagrees with reservations";
                }
                foreach (var kv in l.IncomingCounts)
                {
                    room.TryGetValue(l.Id + "|" + kv.Key, out int v);
                    if (v != kv.Value) return "Location " + l.Id + " incoming total for " + kv.Key + " disagrees with reservations";
                }
            }
            return null;
        }
    }
}
