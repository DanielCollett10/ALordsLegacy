using LordsLegacy.Sim.Coppice;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Coppice task steps (MAT-17): WalkTo the stool, one Work step per rod (each hands one rod to the cutter's carrier),
    /// WalkTo the hut, then DepositForage into the hut store (the step kind is shared with foraging; the checks differ:
    /// the per-kind cap applies). A failed trip drops the rods as an operation pile (<see cref="CoppiceDrop"/>).
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepCoppice(SimWorld world, TaskRecord t, Citizen c)
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
                        if (!t.BushId.HasValue || !world.Bushes.TryGet(t.BushId.Value, out BushRecord stool)) { Fail(world, t, c); return; }
                        if (_returning.Contains(t.Id))
                        {
                            int r = Walk(c, t, stool.X, stool.Z);
                            if (r == 2) Fail(world, t, c);
                            else if (r == 1) _returning.Remove(t.Id);
                            return;
                        }
                        c.Activity = Activity.Working;
                        world.Tasks.AddProgress(t.Id, 1);
                        if (t.StepProgress < step.DurationTicks) return;
                        if (!CutRod(world, t, stool)) { Fail(world, t, c); return; }
                        Advance(world, t, c);
                        return;
                    }
                case StepKind.DepositForage:
                    if (!DepositRods(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        // Ledger: one rod of the stool leaves it and lands on the task's carrier.
        private static bool CutRod(SimWorld world, TaskRecord t, BushRecord stool)
        {
            ItemDef item = CoppiceStores.RodOf(stool.Kind);
            if (!world.Bushes.TakeBasket(stool.Id, t.Id, world.Tick)) return false;
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null)
            {
                carrier = world.Inventory.AddCarrier(System.Math.Max(1, world.Tuning.HandCarryRods));
                world.Tasks.SetCarrier(t.Id, carrier.Id);
            }
            world.Inventory.Produce(carrier, item, 1);
            return true;
        }

        // Ledger: everything carried goes to the hut store, which must still be built and have room for that kind.
        private static bool DepositRods(SimWorld world, TaskRecord t, TaskStep step)
        {
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation store = world.Inventory.Get(step.LocationId);
            if (carrier == null || store == null || !world.IsWorking(store.ModuleId)) return false;
            foreach (ItemDef item in Items.All)
                if (carrier.CountOf(item.Id) > store.FreeSpaceFor(item.Id)) return false;
            MoveAll(world, carrier, store);
            return true;
        }

        private static void FailCoppice(SimWorld world, TaskRecord t, Citizen c)
        {
            CoppiceDrop.DropCarried(world, t, c != null ? PointMm.FromMetres(c.X, c.Z) : (PointMm?)null);
        }
    }
}
