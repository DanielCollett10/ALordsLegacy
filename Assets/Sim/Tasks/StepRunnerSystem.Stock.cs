using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Markets;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Stock-the-stall steps (MK-25): WalkTo the granary, Work (load, 10 min), CollectStock (reserve unreserved granary food in
    /// proportion to stock and the stall's room, move it to the carrier), WalkTo the stall, Work (unload, 10 min), DepositStock
    /// (carrier to stall). The carrier is a ledger location, so food in transit is saved with the Ledger section. A failed
    /// trip frees the reservations and drops what is carried on the ground where the carrier stands.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepStock(SimWorld world, TaskRecord t, Citizen c)
        {
            TaskStep step = t.Def.Steps[t.CurrentStep];
            switch (step.Kind)
            {
                case StepKind.WalkTo:
                    {
                        int r = Walk(c, t, step.X, step.Z);
                        if (r == 2) Fail(world, t, c);
                        else if (r == 1) Advance(world, t, c);
                        return;
                    }
                case StepKind.Work:
                    c.Activity = Activity.Working;
                    world.Tasks.AddProgress(t.Id, 1);
                    if (t.StepProgress >= step.DurationTicks) Advance(world, t, c);
                    return;
                case StepKind.CollectStock:
                    if (!CollectStock(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                case StepKind.DepositStock:
                    if (!DepositStock(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        // Ledger: reserve stock at the granary and room at the stall for the share, then move it to the carrier. The stock
        // reservation only covers unreserved food (MK-27); the capacity reservation is consumed when the stall is stocked.
        private static bool CollectStock(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation granary = world.Inventory.Get(step.LocationId);
            LedgerLocation stall = StallOf(world, t);
            // A source without a module (the camp food store, CMP-21) is always usable.
            if (granary == null || stall == null || (granary.ModuleId != 0 && !world.IsWorking(granary.ModuleId)) || !world.IsWorking(stall.ModuleId)) return false;
            int owner = StoreFamilySystem.OwnerFor(t.Id);
            int room = stall.FreeSpace; // already net of items other trips have reserved room for
            int n = System.Math.Min(System.Math.Min(step.Count, room), System.Math.Max(1, world.Tuning.HandCarryItems));
            List<KeyValuePair<ItemDef, int>> share = StoreFamilySystem.Proportions(granary, n);
            if (share.Count == 0) return false;
            LedgerLocation carrier = world.Inventory.AddCarrier(System.Math.Max(1, world.Tuning.HandCarryItems));
            world.Tasks.SetCarrier(t.Id, carrier.Id);
            foreach (KeyValuePair<ItemDef, int> kv in share)
            {
                if (world.Inventory.ReserveStock(granary, kv.Key, kv.Value, owner) == 0) return false;
                if (world.Inventory.ReserveCapacity(stall, kv.Key, kv.Value, owner) == 0) return false;
                if (!world.Inventory.Transfer(granary, carrier, kv.Key, kv.Value, owner)) return false;
            }
            return true;
        }

        // Ledger: everything carried goes onto the stall, which must still be working.
        private static bool DepositStock(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation stall = world.Inventory.Get(step.LocationId);
            if (carrier == null || stall == null || !world.IsWorking(stall.ModuleId)) return false;
            int owner = StoreFamilySystem.OwnerFor(t.Id);
            foreach (ItemDef item in Items.All)
            {
                int n = carrier.CountOf(item.Id);
                if (n > 0 && !world.Inventory.Transfer(carrier, stall, item, n, owner)) return false;
            }
            world.Inventory.CancelOwner(owner);
            return true;
        }

        private static LedgerLocation StallOf(SimWorld world, TaskRecord t)
        {
            foreach (TaskStep s in t.Def.Steps)
                if (s.Kind == StepKind.DepositStock) return world.Inventory.Get(s.LocationId);
            return null;
        }

        // A failed stock trip: reservations are freed and carried food lies where the carrier stands.
        private static void FailStock(SimWorld world, TaskRecord t, Citizen c)
        {
            world.Inventory.CancelOwner(StoreFamilySystem.OwnerFor(t.Id));
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            PointMm at = default;
            if (c != null) at = PointMm.FromMetres(c.X, c.Z);
            else
            {
                LedgerLocation stall = StallOf(world, t);
                Footprints.Footprint fp = stall == null ? null : world.Footprints.Get(stall.ModuleId);
                if (fp != null) at = fp.Centre;
            }
            Inventory.CarrierDrop.DropAll(world, carrier, at);
        }
    }
}
