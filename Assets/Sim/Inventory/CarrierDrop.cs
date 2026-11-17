using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Inventory
{
    /// <summary>Shared helper for emptying a carrier onto the ground (moved from ForagingSystem, T-501).</summary>
    public static class CarrierDrop
    {
        /// <summary>Moves everything a carrier holds onto a ground pile at <paramref name="at"/>.</summary>
        public static void DropAll(SimWorld world, LedgerLocation carrier, PointMm at)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (carrier == null) throw new ArgumentNullException(nameof(carrier));
            LedgerLocation pile = world.Inventory.GetOrAddGroundPile(at, null);
            foreach (ItemDef item in Items.All)
            {
                int n = carrier.CountOf(item.Id);
                if (n > 0) world.Inventory.Transfer(carrier, pile, item, n);
            }
        }
    }
}
