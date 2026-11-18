using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Hand-haul steps (ST-43, ST-44): WalkTo the source, Work (load), CollectItems (the stock reserved at planning moves
    /// onto a person carrier), WalkTo the destination, Work (unload), DepositItems (carrier to the destination, using the
    /// room reserved at planning). A failed haul frees its reservations and drops the load as an unowned pile where the
    /// carrier stands.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepHaul(SimWorld world, TaskRecord t, Citizen c)
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
                case StepKind.CollectItems:
                    if (!HaulCollect(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                case StepKind.DepositItems:
                    if (!HaulDeposit(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        private static bool HaulCollect(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation source = world.Inventory.Get(step.LocationId);
            ItemDef item = Items.Find(step.ItemId);
            if (source == null || item == null) return false;
            LedgerLocation carrier = world.Inventory.AddCarrier(System.Math.Max(1, step.Count));
            world.Tasks.SetCarrier(t.Id, carrier.Id);
            return world.Inventory.Transfer(source, carrier, item, step.Count, HandHaulTaskDefs.OwnerFor(t.Id));
        }

        private static bool HaulDeposit(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation dest = world.Inventory.Get(step.LocationId);
            ItemDef item = Items.Find(step.ItemId);
            if (carrier == null || dest == null || item == null) return false;
            int n = carrier.CountOf(item.Id);
            int owner = HandHaulTaskDefs.OwnerFor(t.Id);
            if (n <= 0 || !world.Inventory.Transfer(carrier, dest, item, n, owner)) return false;
            world.Inventory.CancelOwner(owner);
            return true;
        }

        private static void FailHaul(SimWorld world, TaskRecord t, Citizen c)
        {
            world.Inventory.CancelOwner(HandHaulTaskDefs.OwnerFor(t.Id));
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            PointMm at = default;
            if (c != null) at = PointMm.FromMetres(c.X, c.Z);
            CarrierDrop.DropAll(world, carrier, at);
        }
    }
}
