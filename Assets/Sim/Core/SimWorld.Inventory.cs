using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Core
{
    /// <summary>The inventory part of <see cref="SimWorld"/> (T-273, T-286).</summary>
    public sealed partial class SimWorld
    {
        /// <summary>The item ledger. Never null; empty at start.</summary>
        public Ledger Inventory { get; } = new Ledger();

        /// <summary>The ox's ledger carrier, created on first use with capacity <c>SimTuning.OxLoadLogs</c>.</summary>
        public LedgerLocation OxCarrier(People.Ox ox)
        {
            if (ox == null) throw new System.ArgumentNullException(nameof(ox));
            LedgerLocation l = ox.CarrierId != 0 ? Inventory.Get(ox.CarrierId) : null;
            if (l == null)
            {
                l = Inventory.AddCarrier(Tuning.OxLoadLogs);
                ox.CarrierId = l.Id;
            }
            return l;
        }

        /// <summary>
        /// The operation pool (WC-37): the sum of one item over the operation's storage modules only. Ground piles,
        /// carriers and other operations' storage are excluded. Unknown operation gives an empty pool.
        /// </summary>
        public OperationPool OperationPool(int operationId, ItemDef item)
        {
            int total = 0, reserved = 0, incoming = 0, capacity = 0;
            Operation op = Operations.Get(operationId);
            if (op == null) return new OperationPool(0, 0, 0, 0);
            foreach (Footprint f in OperationStatus.StorageModules(this, op))
            {
                capacity += f.Type.LogCapacity;
                LedgerLocation l = Inventory.FindStorage(f.Id);
                if (l == null) continue;
                total += l.CountOf(item.Id);
                reserved += l.ReservedOf(item.Id);
                incoming += l.IncomingOf(item.Id);
            }
            return new OperationPool(total, reserved, incoming, capacity);
        }
    }
}
