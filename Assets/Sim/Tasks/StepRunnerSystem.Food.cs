using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Sunday food trip steps (FD-14, FOOD-A7): WalkTo, CollectFood (reserved allotment to the family's carrier) and
    /// DepositFood (carrier to larder). The trip ignores shifts; the carrier is a ledger location, so food in transit is
    /// saved with the Ledger section. A failed trip puts what is carried on the ground where the walker stands.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepFoodTrip(SimWorld world, TaskRecord t, Citizen c)
        {
            Family f = FoodTripSystem.FamilyOf(world, t);
            if (f == null) { Fail(world, t, c); return; }
            TaskStep step = t.Def.Steps[t.CurrentStep];
            switch (step.Kind)
            {
                case StepKind.WalkTo:
                    if (SkipRemovedStall(world, t, c, f)) return;
                    c.Activity = Activity.Walking;
                    if (!_orders.TryGetValue(t.Id, out MoveOrder order) || c.Move != order)
                    {
                        _orders[t.Id] = c.Move = new MoveOrder(step.X, step.Z);
                        return;
                    }
                    if (order.Status == MoveStatus.Failed) { Fail(world, t, c); return; }
                    if (order.Status != MoveStatus.Done) return;
                    c.Move = null;
                    _orders.Remove(t.Id);
                    Advance(world, t, c);
                    return;
                case StepKind.CollectFood:
                    Collect(world, f, step.LocationId);
                    Advance(world, t, c);
                    return;
                case StepKind.DepositFood:
                    Deposit(world, f);
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        // MK-33: a stall removed before or during the walk to it is skipped (walk and collect) and its reservations freed.
        private bool SkipRemovedStall(SimWorld world, TaskRecord t, Citizen c, Family f)
        {
            if (t.CurrentStep + 1 >= t.Def.Steps.Count) return false;
            TaskStep next = t.Def.Steps[t.CurrentStep + 1];
            if (next.Kind != StepKind.CollectFood || next.LocationId == 0) return false;
            // A removed stall leaves its (emptied) ledger location behind, so also check the footprint it was bound to.
            LedgerLocation loc = world.Inventory.Get(next.LocationId);
            if (loc != null && (loc.ModuleId == 0 || world.Footprints.Get(loc.ModuleId) != null)) return false;
            foreach (int rid in new List<int>(f.AllotmentReservationIds))
            {
                Reservation r = world.Inventory.GetReservation(rid);
                if (r != null && r.LocationId == next.LocationId) world.Inventory.Release(rid);
            }
            c.Move = null;
            _orders.Remove(t.Id);
            Advance(world, t, c);
            Advance(world, t, c);
            return true;
        }

        private static LedgerLocation CarrierOf(SimWorld world, Family f)
        {
            LedgerLocation l = world.Inventory.Get(f.TripCarrierId);
            if (l != null) return l;
            l = world.Inventory.AddCarrier(int.MaxValue);
            f.TripCarrierId = l.Id;
            return l;
        }

        // Ledger transfer: every reserved allotment line moves from the market stock to the carrier (the reservations
        // are consumed by the transfer). Anything the stock could no longer cover is released.
        private static void Collect(SimWorld world, Family f, int onlyLocation)
        {
            LedgerLocation carrier = CarrierOf(world, f);
            foreach (int rid in new List<int>(f.AllotmentReservationIds))
            {
                Reservation r = world.Inventory.GetReservation(rid);
                if (r == null || (onlyLocation != 0 && r.LocationId != onlyLocation)) continue;
                LedgerLocation from = world.Inventory.Get(r.LocationId);
                ItemDef item = Items.Find(r.ItemId);
                if (from != null && item != null) world.Inventory.Transfer(from, carrier, item, r.Count, r.Owner);
            }
            if (onlyLocation != 0)
            {
                // Keep only the reservations still held (the rest is freed when the trip ends or fails): a save needs live ids.
                var keep = new List<int>();
                foreach (int rid in f.AllotmentReservationIds)
                    if (world.Inventory.GetReservation(rid) != null) keep.Add(rid);
                f.ClearAllotment();
                foreach (int rid in keep) f.AddAllotment(rid);
                return;
            }
            world.Inventory.CancelOwner(AllotmentSystem.OwnerFor(f.Id));
            f.ClearAllotment();
        }

        // Ledger transfer: everything carried goes to the house larder (or the family's camp larder while homeless).
        private static void Deposit(SimWorld world, Family f)
        {
            world.Inventory.CancelOwner(AllotmentSystem.OwnerFor(f.Id));
            f.ClearAllotment();
            LedgerLocation carrier = world.Inventory.Get(f.TripCarrierId);
            if (carrier == null) return;
            Homes.HomeRecord home = f.HomeHouseId.HasValue ? world.Homes.Get(f.HomeHouseId.Value) : null;
            LedgerLocation larder = home != null ? Larders.ForHome(world, home) : Larders.ForFamily(world, f);
            MoveAll(world, carrier, larder);
        }

        private static void MoveAll(SimWorld world, LedgerLocation from, LedgerLocation to)
        {
            foreach (ItemDef item in Items.All)
            {
                int n = from.CountOf(item.Id);
                if (n > 0) world.Inventory.Transfer(from, to, item, n);
            }
        }

        // A failed trip: carried food is dropped as a ground pile where the walker stands; the allotment is released.
        private void FailFoodTrip(SimWorld world, TaskRecord t, Citizen c)
        {
            Family f = FoodTripSystem.FamilyOf(world, t);
            if (f == null) return;
            world.Inventory.CancelOwner(AllotmentSystem.OwnerFor(f.Id));
            f.ClearAllotment();
            LedgerLocation carrier = world.Inventory.Get(f.TripCarrierId);
            if (carrier == null || carrier.Total == 0) return;
            LedgerLocation to;
            if (c != null) to = world.Inventory.GetOrAddGroundPile(PointMm.FromMetres(c.X, c.Z), null);
            else
            {
                Homes.HomeRecord home = f.HomeHouseId.HasValue ? world.Homes.Get(f.HomeHouseId.Value) : null;
                to = home != null ? Larders.ForHome(world, home) : Larders.ForFamily(world, f);
            }
            MoveAll(world, carrier, to);
        }
    }
}
