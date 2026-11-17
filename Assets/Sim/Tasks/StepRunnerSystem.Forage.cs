using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Forage task steps (FD-24): WalkTo the bush, one Work step per basket (each hands one item to the forager's
    /// carrier when it completes), WalkTo the store, then DepositForage. The carrier is a ledger location, so food in
    /// transit is saved with the Ledger section. A failed trip drops what is carried on the ground where the forager
    /// stands. After a night the forager walks back to the bush before picking on.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepForage(SimWorld world, TaskRecord t, Citizen c)
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
                    {
                        if (!t.BushId.HasValue || !world.Bushes.TryGet(t.BushId.Value, out BushRecord bush)) { Fail(world, t, c); return; }
                        if (_returning.Contains(t.Id))
                        {
                            int r = Walk(c, t, bush.X, bush.Z);
                            if (r == 2) Fail(world, t, c);
                            else if (r == 1) _returning.Remove(t.Id);
                            return;
                        }
                        c.Activity = Activity.Working;
                        world.Tasks.AddProgress(t.Id, 1);
                        if (t.StepProgress < step.DurationTicks) return;
                        if (!PickBasket(world, t, bush)) { Fail(world, t, c); return; }
                        Advance(world, t, c);
                        return;
                    }
                case StepKind.DepositForage:
                    if (!DepositForage(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        // 0 = still walking, 1 = arrived, 2 = no route.
        private int Walk(Citizen c, TaskRecord t, double x, double z)
        {
            c.Activity = Activity.Walking;
            if (!_orders.TryGetValue(t.Id, out MoveOrder order) || c.Move != order)
            {
                _orders[t.Id] = c.Move = new MoveOrder(x, z);
                return 0;
            }
            if (order.Status == MoveStatus.Failed) return 2;
            if (order.Status != MoveStatus.Done) return 0;
            c.Move = null;
            _orders.Remove(t.Id);
            return 1;
        }

        // Ledger: one basket becomes one item on the task's carrier.
        private static bool PickBasket(SimWorld world, TaskRecord t, BushRecord bush)
        {
            ItemDef item = bush.Kind == BushKind.Berries ? Items.Berries : Items.Mushrooms;
            if (!world.Bushes.TakeBasket(bush.Id, t.Id, world.Tick)) return false;
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null)
            {
                carrier = world.Inventory.AddCarrier(System.Math.Max(1, world.Tuning.HandCarryItems));
                world.Tasks.SetCarrier(t.Id, carrier.Id);
            }
            world.Inventory.Produce(carrier, item, 1);
            return true;
        }

        // Ledger: everything carried goes to the chosen store, which must still be built and have room.
        private static bool DepositForage(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation store = world.Inventory.Get(step.LocationId);
            if (carrier == null || store == null || !world.IsWorking(store.ModuleId)) return false;
            if (store.FreeSpace < carrier.Total) return false;
            MoveAll(world, carrier, store);
            return true;
        }

        // A failed forage trip: carried food lies where the forager stands (or at the bush if the forager is gone).
        private static void FailForage(SimWorld world, TaskRecord t, Citizen c)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            PointMm at = default;
            if (c != null) at = PointMm.FromMetres(c.X, c.Z);
            else if (t.BushId.HasValue && world.Bushes.TryGet(t.BushId.Value, out BushRecord b)) at = PointMm.FromMetres(b.X, b.Z);
            Inventory.CarrierDrop.DropAll(world, carrier, at);
        }
    }
}
