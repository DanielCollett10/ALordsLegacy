using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Ox step kinds (T-289, TSK-11, WCO-06): FetchOx, LeadTo, LoadOx, UnloadOx, ReturnOx. The handler's own
    /// <see cref="MoveOrder"/> does the walking; the ox follows through <see cref="MovementSystem.Attach"/> (MOV-16).
    /// If the ox is removed or unassigned mid-task the task cancels and the logs it carried are dropped as a ground pile
    /// where the ox was.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private struct OxTrack { public double X, Z; public int CarrierId; }
        private struct RestTarget { public PointMm Spot; public int StandId; }

        // Last known ox position and carrier per task, so a removed ox can still drop its load.
        private readonly Dictionary<int, OxTrack> _oxTrack = new Dictionary<int, OxTrack>();
        private readonly Dictionary<int, RestTarget> _rest = new Dictionary<int, RestTarget>();

        // False when the task must cancel: ox removed, or its operation assignment changed.
        private bool CheckOx(SimWorld world, TaskRecord t, Citizen c, out Ox ox)
        {
            ox = world.People.FindOx(t.OxId.Value);
            if (ox == null) return false;
            Operation owner = world.Operations.FindOxOwner(ox.Id);
            if (owner != null && owner.Id != t.OwnerOperationId) return false;
            if (owner == null && t.OxWasAssigned) return false;
            _oxTrack[t.Id] = new OxTrack { X = ox.X, Z = ox.Z, CarrierId = ox.CarrierId };
            return true;
        }

        private void StepOxWalk(SimWorld world, TaskRecord t, Citizen c, Ox ox, TaskStep step)
        {
            if (ox == null) { Fail(world, t, c); return; }
            double tx, tz;
            if (step.Kind == StepKind.FetchOx)
            {
                if (ox.State.Kind == OxStateKind.Led && ox.State.HandlerId != c.Id) { Fail(world, t, c); return; }
                tx = ox.X; tz = ox.Z;
            }
            else
            {
                if (ox.Follow == null || !ReferenceEquals(ox.Follow.Leader, c)) { Fail(world, t, c); return; }
                if (step.Kind == StepKind.LeadTo) { tx = step.X; tz = step.Z; }
                else
                {
                    if (!_rest.TryGetValue(t.Id, out RestTarget r))
                    {
                        PointMm spot = new OxPool(world).RestSpot(ox, out int stand);
                        r = new RestTarget { Spot = spot, StandId = stand };
                        _rest[t.Id] = r;
                    }
                    tx = r.Spot.XMetres; tz = r.Spot.ZMetres;
                }
            }

            c.Activity = Activity.Walking;
            if (!_orders.TryGetValue(t.Id, out MoveOrder order) || c.Move != order)
            {
                _orders[t.Id] = c.Move = new MoveOrder(tx, tz);
                return;
            }
            if (order.Status == MoveStatus.Failed) { Fail(world, t, c); return; }
            if (order.Status != MoveStatus.Done) return;

            c.Move = null;
            _orders.Remove(t.Id);
            if (step.Kind == StepKind.FetchOx)
            {
                MovementSystem.Attach(ox, c);
                ox.State = OxState.Led(c.Id);
            }
            else if (step.Kind == StepKind.ReturnOx)
            {
                RestTarget r = _rest[t.Id];
                MovementSystem.Detach(ox);
                ox.X = r.Spot.XMetres;
                ox.Z = r.Spot.ZMetres;
                ox.State = OxState.Resting(r.Spot.X, r.Spot.Z, r.StandId);
            }
            Advance(world, t, c);
        }

        private static bool TransferLogs(SimWorld world, TaskRecord t, Ox ox, TaskStep step)
        {
            if (ox == null) return false;
            LedgerLocation carrier = world.OxCarrier(ox);
            LedgerLocation other = world.Inventory.Get(step.LocationId);
            if (other == null) return false;
            return step.Kind == StepKind.LoadOx
                ? world.Inventory.Transfer(other, carrier, Items.Log, step.Count, t.Id)
                : world.Inventory.Transfer(carrier, other, Items.Log, step.Count, t.Id);
        }

        // Cancel clean-up: drop what the ox carries as a ground pile where it stood, let it go, free reservations.
        private void ReleaseOx(SimWorld world, TaskRecord t, Citizen c)
        {
            if (!t.OxId.HasValue) return;
            world.Inventory.CancelOwner(t.Id);
            Ox ox = world.People.FindOx(t.OxId.Value);
            double x, z;
            int carrierId;
            if (ox != null) { x = ox.X; z = ox.Z; carrierId = ox.CarrierId; }
            else if (_oxTrack.TryGetValue(t.Id, out OxTrack k)) { x = k.X; z = k.Z; carrierId = k.CarrierId; }
            else if (c != null) { x = c.X; z = c.Z; carrierId = 0; }
            else return;

            LedgerLocation carrier = carrierId != 0 ? world.Inventory.Get(carrierId) : null;
            if (carrier != null)
            {
                foreach (ItemDef item in Items.All)
                {
                    int n = carrier.CountOf(item.Id);
                    if (n <= 0) continue;
                    LedgerLocation pile = world.Inventory.GetOrAddGroundPile(PointMm.FromMetres(x, z), t.OwnerOperationId);
                    world.Inventory.Transfer(carrier, pile, item, n);
                }
            }

            if (ox != null)
            {
                MovementSystem.Detach(ox);
                ox.Move = null;
                ox.State = OxState.Free;
            }
            _oxTrack.Remove(t.Id);
            _rest.Remove(t.Id);
        }
    }
}
