namespace LordsLegacy.Sim.Inventory
{
    /// <summary>One item summed over an operation's storage modules (WC-37).</summary>
    public readonly struct OperationPool
    {
        /// <summary>Items held in the operation's storage.</summary>
        public int Total { get; }
        /// <summary>Of those, reserved to leave.</summary>
        public int Reserved { get; }
        /// <summary>Promised to arrive.</summary>
        public int Incoming { get; }
        /// <summary>Combined storage capacity.</summary>
        public int Capacity { get; }
        /// <summary>Available = total − reserved.</summary>
        public int Available => Total - Reserved;

        /// <summary>Creates a pool summary.</summary>
        public OperationPool(int total, int reserved, int incoming, int capacity)
        {
            Total = total; Reserved = reserved; Incoming = incoming; Capacity = capacity;
        }
    }
}
