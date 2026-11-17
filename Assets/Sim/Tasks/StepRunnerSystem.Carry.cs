using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Carry-materials steps (T-517, PL-16): WalkTo the source, CollectItems (source reservation to the person carrier),
    /// WalkTo the site, DepositItems (carrier to the site store). The carrier is a ledger location, so items in transit
    /// are saved with the Ledger section. A failed or wound-down carry puts what is carried on a site-owned pile where the
    /// carrier stands and reserves it for the site again (CN-25b), like an ox delivery.
    /// </summary>
    public sealed partial class StepRunnerSystem
    {
        private void StepCarry(SimWorld world, TaskRecord t, Citizen c)
        {
            if (SiteOfCarry(world, t) == null) { Fail(world, t, c); return; } // the site was removed or finished
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
                    if (!CollectItems(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                case StepKind.DepositItems:
                    if (!DepositItems(world, t, step)) { Fail(world, t, c); return; }
                    Advance(world, t, c);
                    return;
                default:
                    Fail(world, t, c);
                    return;
            }
        }

        // End of work for a carry (WC-35 style): after today's shift end the carrier drops what it holds where it stands
        // and the task is cancelled. Returns true when it handled the task this tick.
        private bool TryWindDownCarry(SimWorld world, TaskRecord t, Citizen c)
        {
            if (t.Def.Id != ConstructionTaskDefs.CarryId) return false;
            if (!t.WindingDown)
            {
                if (world.Tick < world.WorkDay.WindowOf(world, WorkDay.DayStart(world)).End) return false; // not yet today's end
                world.Tasks.SetWindingDown(t.Id);
            }
            Fail(world, t, c);
            return true;
        }

        // Ledger: the reserved items move from the source onto a new person carrier (the reservation is consumed).
        // One collect for every item trip: a step with an item id moves that item; without one it takes a proportional
        // share of whatever the source holds (stock-the-stall).
        private static bool CollectItems(SimWorld world, TaskRecord t, TaskStep step)
        {
            if (step.ItemId == null) return CollectShare(world, t, step);
            LedgerLocation source = world.Inventory.Get(step.LocationId);
            ItemDef item = Items.Find(step.ItemId);
            if (source == null || item == null) return false;
            LedgerLocation carrier = world.Inventory.AddCarrier(System.Math.Max(1, step.Count));
            world.Tasks.SetCarrier(t.Id, carrier.Id);
            return world.Inventory.Transfer(source, carrier, item, step.Count, t.Id);
        }

        // Ledger: what the carrier holds goes into the site store.
        private static bool DepositItems(SimWorld world, TaskRecord t, TaskStep step)
        {
            if (step.ItemId == null) return DepositShare(world, t, step);
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            LedgerLocation site = world.Inventory.Get(step.LocationId);
            ItemDef item = Items.Find(step.ItemId);
            if (carrier == null || site == null || item == null) return false;
            int n = carrier.CountOf(item.Id);
            return n > 0 && world.Inventory.Transfer(carrier, site, item, n);
        }

        // The construction site a carry deposits into, or null (removed, finished or not a site).
        private static ConstructionSite SiteOfCarry(SimWorld world, TaskRecord t)
        {
            foreach (TaskStep st in t.Def.Steps)
            {
                if (st.Kind != StepKind.DepositItems) continue;
                LedgerLocation l = world.Inventory.Get(st.LocationId);
                return l == null || l.Kind != LocationKind.Site ? null : world.Construction.Get(l.ModuleId);
            }
            return null;
        }

        // A failed carry: its reservations are freed and carried items lie on a site-owned pile where the carrier stands,
        // reserved for the site so the next carry picks them up first.
        private static void FailCarry(SimWorld world, TaskRecord t, Citizen c)
        {
            ReleaseCarryReservations(world, t);
            DropCarried(world, t, c);
        }

        /// <summary>Frees the stock reservations a carry task holds. The site's own reservations are kept, even if a
        /// footprint id happens to equal the task id.</summary>
        internal static void ReleaseCarryReservations(SimWorld world, TaskRecord t)
        {
            ConstructionSite site = SiteOfCarry(world, t);
            var mine = new List<Reservation>();
            foreach (Reservation r in world.Inventory.Reservations)
                if (r.Owner == t.Id && (site == null || !site.ReservationIds.Contains(r.Id))) mine.Add(r);
            foreach (Reservation r in mine) world.Inventory.Release(r.Id);
        }

        /// <summary>Puts what a carry task's carrier holds on a site-owned pile (where the walker stands, else at the
        /// site) and reserves it for the site. Does nothing when the carrier is empty.</summary>
        internal static void DropCarried(SimWorld world, TaskRecord t, Citizen c)
        {
            if (c == null && t.AssigneeId.HasValue) c = world.People.FindCitizen(t.AssigneeId.Value);
            LedgerLocation carrier = t.CarrierId.HasValue ? world.Inventory.Get(t.CarrierId.Value) : null;
            if (carrier == null || carrier.Total == 0) return;
            ConstructionSite site = SiteOfCarry(world, t);
            PointMm at = default;
            if (c != null) at = PointMm.FromMetres(c.X, c.Z);
            else
            {
                Footprint fp = site == null ? null : world.Footprints.Get(site.FootprintId);
                if (fp != null) at = fp.AccessPoint;
            }
            int? owner = site != null ? (int?)site.FootprintId : PileOwner(t);
            LedgerLocation pile = world.Inventory.GetOrAddGroundPile(at, owner);
            foreach (ItemDef item in Items.All)
            {
                int n = carrier.CountOf(item.Id);
                if (n <= 0 || !world.Inventory.Transfer(carrier, pile, item, n)) continue;
                if (site == null) continue;
                int rid = world.Inventory.ReserveStock(pile, item, n, site.FootprintId);
                if (rid != 0) site.ReservationIds.Add(rid);
            }
        }
    }
}
