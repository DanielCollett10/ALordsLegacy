namespace LordsLegacy.Sim.Inventory
{
    /// <summary>What a reservation holds.</summary>
    public enum ReservationKind
    {
        /// <summary>Items at the location promised to leave it.</summary>
        Stock = 0,
        /// <summary>Space at the location promised to items arriving.</summary>
        Capacity = 1,
    }

    /// <summary>A promise on stock or capacity at one location, held by an owner (LED-04).</summary>
    public sealed class Reservation
    {
        /// <summary>Ledger-unique id (starts at 1).</summary>
        public int Id { get; }
        /// <summary>Stock or capacity.</summary>
        public ReservationKind Kind { get; }
        /// <summary>Location id the reservation is on.</summary>
        public int LocationId { get; }
        /// <summary>Item id.</summary>
        public string ItemId { get; }
        /// <summary>Items still reserved.</summary>
        public int Count { get; internal set; }
        /// <summary>Owner id (an operation or task id).</summary>
        public int Owner { get; }

        internal Reservation(int id, ReservationKind kind, int locationId, string itemId, int count, int owner)
        {
            Id = id; Kind = kind; LocationId = locationId; ItemId = itemId; Count = count; Owner = owner;
        }
    }
}
