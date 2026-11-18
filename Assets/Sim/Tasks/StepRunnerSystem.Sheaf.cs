using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Collect trip steps (MAT-39d): WalkTo the dry pile, CollectItems (one sheaf onto the carrier), WalkTo the hut,
    /// DepositItems into the hut store. A trip that ends mid-carry drops the sheaf as an operation pile
    /// (<see cref="FailCollectThatch"/>).
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepCollectThatch(SimWorld world, TaskRecord t, Citizen c)
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
                case StepKind.CollectItems:
                    if (!TakeSheaf(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                case StepKind.DepositItems:
                    if (!StoreSheaf(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        private static bool TakeSheaf(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation pile = world.Inventory.Get(step.LocationId);
            if (pile == null || pile.CountOf(Items.Thatch.Id) < step.Count) return false;
            LedgerLocation carrier = world.Inventory.AddCarrier(System.Math.Max(1, step.Count));
            world.Tasks.SetCarrier(t.Id, carrier.Id);
            return world.Inventory.Transfer(pile, carrier, Items.Thatch, step.Count);
        }

        private static bool StoreSheaf(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation store = world.Inventory.Get(step.LocationId);
            if (carrier == null || store == null || !world.IsWorking(store.ModuleId)) return false;
            int n = carrier.CountOf(Items.Thatch.Id);
            if (n <= 0 || n > store.FreeSpaceFor(Items.Thatch.Id)) return false;
            return world.Inventory.Transfer(carrier, store, Items.Thatch, n);
        }

        // The sheaf in hand goes to an operation pile where the carrier stands (the hut when nobody is known).
        private static void FailCollectThatch(SimWorld world, TaskRecord t, Citizen c)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            PointMm at = c != null ? PointMm.FromMetres(c.X, c.Z) : default;
            if (c == null)
            {
                Operations.Operation op = world.Operations.Get(t.OwnerOperationId);
                Footprints.Footprint hut = op == null ? null : Operations.OperationStatus.Hut(world, op);
                if (hut != null) at = hut.Centre;
            }
            LedgerLocation pile = world.Inventory.GetOrAddGroundPile(at, t.OwnerOperationId);
            world.Inventory.Transfer(carrier, pile, Items.Thatch, carrier.CountOf(Items.Thatch.Id));
        }
    }
}
