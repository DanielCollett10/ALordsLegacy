using System;
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

        // Hauls ending their day at sunset (WC-35) and the unload minutes spent so far.
        private readonly Dictionary<int, int> _drop = new Dictionary<int, int>();

        // End of work for a haul that is still fetching, leading to the pile, loading or leading to storage: unload where
        // it stands (unload time applies), lead the ox to its rest spot, release the reservations, cancel the task. The
        // worker is then free and the routine walks them home. Returns true when it handled the task this tick.
        private bool TryWindDown(SimWorld world, TaskRecord t, Citizen c, Ox ox)
        {
            if (t.Def.Id != HaulSystem.HaulTaskId || ox == null) return false;
            if (!t.WindingDown)
            {
                StepKind k = t.Def.Steps[t.CurrentStep].Kind;
                if (k != StepKind.FetchOx && k != StepKind.LeadTo && k != StepKind.LoadOx) return false;
                if (world.Tick < world.WorkDay.WindowOf(world, WorkDay.DayStart(world)).End) return false; // not yet today's end
                world.Tasks.SetWindingDown(t.Id);
                _orders.Remove(t.Id);
                c.Move = null;
            }
            if (ox.Follow == null || !ReferenceEquals(ox.Follow.Leader, c))
            {
                Fail(world, t, c); // ox not collected yet: let it go, nothing to drop
                return true;
            }
            LedgerLocation carrier = world.OxCarrier(ox);
            int carried = carrier.CountOf(Items.Log.Id);
            if (carried > 0)
            {
                c.Activity = Activity.Working;
                _drop.TryGetValue(t.Id, out int done);
                _drop[t.Id] = ++done;
                if (done < Math.Max(1, world.Tuning.OxUnloadMinutes)) return true;
                LedgerLocation pile = world.Inventory.GetOrAddGroundPile(PointMm.FromMetres(ox.X, ox.Z), t.OwnerOperationId);
                world.Inventory.Transfer(carrier, pile, Items.Log, carried);
            }
            StepOxWalk(world, t, c, ox, TaskStep.ReturnOx());
            return true;
        }

        private void EndWindDown(SimWorld world, TaskRecord t, Citizen c)
        {
            _drop.Remove(t.Id);
            _rest.Remove(t.Id);
            _oxTrack.Remove(t.Id);
            _orders.Remove(t.Id);
            world.Inventory.CancelOwner(t.Id); // reservations on the old pile and in storage
            world.Tasks.Cancel(t.Id);
            c.Move = null;
            c.Activity = Activity.MillingAbout;
        }

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
            if (t.WindingDown) EndWindDown(world, t, c);
            else Advance(world, t, c);
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
