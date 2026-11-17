using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Restore path used by the save loader (decision 0034). Rebuilds exact state without producing items.</summary>
    public sealed partial class Ledger
    {
        /// <summary>The next location id that will be handed out.</summary>
        internal int NextLocationId => _nextId;

        /// <summary>The next reservation id that will be handed out.</summary>
        internal int NextReservationIdValue => _nextReservationId;

        /// <summary>Produced journal (item id to total), in id order.</summary>
        internal IEnumerable<KeyValuePair<string, int>> ProducedJournal => _produced;

        /// <summary>Consumed journal (item id to total), in id order.</summary>
        internal IEnumerable<KeyValuePair<string, int>> ConsumedJournal => _consumed;

        /// <summary>Adds a location with exactly the given fields and counts. Throws on a duplicate id or bad count.</summary>
        internal LedgerLocation RestoreLocation(int id, LocationKind kind, PointMm position, int? owner, int moduleId, int capacity,
            IEnumerable<KeyValuePair<string, int>> counts, string inputItemId = null)
        {
            if (id <= 0) throw new ArgumentException("Location id must be positive.");
            if (_locations.ContainsKey(id)) throw new ArgumentException("Duplicate location id " + id + ".");
            var l = new LedgerLocation(id, kind, position, owner, moduleId, capacity);
            l.InputItemId = inputItemId;
            foreach (var kv in counts)
            {
                if (kv.Value <= 0) throw new ArgumentException("Location " + id + " holds " + kv.Value + " of " + kv.Key + ".");
                l.Counts[kv.Key] = kv.Value;
            }
            return Add(l);
        }

        /// <summary>Adds a reservation with its original id and updates the location's reserved / incoming totals.</summary>
        internal void RestoreReservation(int id, ReservationKind kind, int locationId, string itemId, int count, int owner)
        {
            if (id <= 0 || count <= 0) throw new ArgumentException("Bad reservation " + id + ".");
            if (_reservations.ContainsKey(id)) throw new ArgumentException("Duplicate reservation id " + id + ".");
            LedgerLocation l = Get(locationId);
            if (l == null) throw new ArgumentException("Reservation " + id + " is on missing location " + locationId + ".");
            Bump(kind == ReservationKind.Stock ? l.ReservedCounts : l.IncomingCounts, itemId, count);
            _reservations.Add(id, new Reservation(id, kind, locationId, itemId, count, owner));
            Version++;
        }

        /// <summary>Restores the journals and id counters.</summary>
        internal void RestoreJournalAndCounters(IEnumerable<KeyValuePair<string, int>> produced,
            IEnumerable<KeyValuePair<string, int>> consumed, int nextLocationId, int nextReservationId)
        {
            _produced.Clear();
            _consumed.Clear();
            foreach (var kv in produced) _produced[kv.Key] = kv.Value;
            foreach (var kv in consumed) _consumed[kv.Key] = kv.Value;
            _nextId = nextLocationId;
            _nextReservationId = nextReservationId;
            Version++;
        }
    }
}
