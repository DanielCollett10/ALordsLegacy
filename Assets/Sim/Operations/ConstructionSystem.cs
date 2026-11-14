using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Operations-phase system for construction sites (T-345, CN-20..23; decisions 0031, 0033). Part 1: delivery.
    /// For each site whose logs are not all on site, it opens <c>task.deliver_materials</c> tasks (up to
    /// <c>OxLoadLogs</c> logs each, from one reserved source), at the site's priority. Opening a task moves that many logs
    /// from the site's reservations to a reservation owned by the task, so the step runner's Transfer can consume it.
    /// Each Open task asks <see cref="OxPool.FindFor"/> for an ox of the site's operation every tick. Helpers do the work
    /// (the task def is HelpersOnly and heavy). The site moves to Building when its first log is on site (CN-23).
    /// Cancelled tasks give their logs back to the site's reservations; tasks of a removed site are cancelled.
    /// </summary>
    public sealed class ConstructionSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Construction.Count == 0 && !AnyLiveDeliver(world)) return;
            Sweep(world);
            foreach (ConstructionSite site in new List<ConstructionSite>(world.Construction.Sites))
            {
                Footprint fp = world.Footprints.Get(site.FootprintId);
                if (fp == null) continue;
                int logs = fp.Type.ConstructionRecipe.Logs;
                LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
                if (loc == null) continue;
                if (site.State == ConstructionState.Delivering && (logs == 0 || loc.CountOf(Items.Log.Id) > 0))
                    site.State = ConstructionState.Building; // CN-23
                if (logs == 0) continue;
                Operation op = OperationOf(world, site.FootprintId);
                if (op == null) continue;
                TopUp(world, site, fp, logs, loc);
                OpenTasks(world, site, fp, op);
            }
            AttachOxen(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static bool AnyLiveDeliver(SimWorld world)
        {
            foreach (TaskRecord t in world.Tasks.All)
                if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.DeliverId) return true;
            return false;
        }

        private static Operation OperationOf(SimWorld world, int footprintId)
        {
            foreach (Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                    if (id == footprintId) return op;
            return null;
        }

        private static TaskStep StepOf(TaskRecord t, StepKind kind, out int index)
        {
            for (int i = 0; i < t.Def.Steps.Count; i++)
                if (t.Def.Steps[i].Kind == kind) { index = i; return t.Def.Steps[i]; }
            index = -1;
            return default;
        }

        // Site whose store the task unloads into, or null.
        private static ConstructionSite SiteOf(SimWorld world, TaskRecord t)
        {
            TaskStep unload = StepOf(t, StepKind.UnloadOx, out int idx);
            if (idx < 0) return null;
            LedgerLocation l = world.Inventory.Get(unload.LocationId);
            return l == null || l.Kind != LocationKind.Site ? null : world.Construction.Get(l.ModuleId);
        }

        private static bool IsSiteDelivery(SimWorld world, TaskRecord t)
        {
            TaskStep unload = StepOf(t, StepKind.UnloadOx, out int idx);
            if (idx < 0) return false;
            LedgerLocation l = world.Inventory.Get(unload.LocationId);
            return l != null && l.Kind == LocationKind.Site;
        }

        // Cancels tasks of removed sites, and hands the logs of finished-but-undelivered tasks back to their site.
        private static void Sweep(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
            {
                if (t.Def.Id != ConstructionTaskDefs.DeliverId) continue;
                ConstructionSite site = SiteOf(world, t);
                if (!IsSiteDelivery(world, t)) continue; // a hand-made task into some other location
                if (!t.IsFinished)
                {
                    if (site == null) world.Tasks.Cancel(t.Id); // the site was removed
                    continue;
                }
                if (t.State != TaskState.Cancelled) continue;
                TaskStep load = StepOf(t, StepKind.LoadOx, out int idx);
                if (idx < 0) continue;
                var mine = new List<Reservation>();
                foreach (Reservation r in world.Inventory.Reservations)
                    if (r.Owner == t.Id && r.Kind == ReservationKind.Stock && r.LocationId == load.LocationId &&
                        (site == null || !site.ReservationIds.Contains(r.Id)))
                        mine.Add(r);
                foreach (Reservation r in mine) world.Inventory.Release(r.Id);
            }
            foreach (ConstructionSite site in world.Construction.Sites) site.ReservationIds.RemoveAll(
                id => world.Inventory.GetReservation(id) == null);
            foreach (Ox ox in world.People.Oxen)
            {
                if (ox.State.Kind != OxStateKind.Reserved) continue;
                TaskRecord t = world.Tasks.Get(ox.State.TaskId);
                if (t == null || t.IsFinished) ox.State = OxState.Free;
            }
        }

        private static int Reserved(SimWorld world, ConstructionSite site)
        {
            int n = 0;
            foreach (int id in site.ReservationIds) n += world.Inventory.GetReservation(id)?.Count ?? 0;
            return n;
        }

        // Logs the site still needs from outside: recipe - on site - carried by live tasks that have not unloaded yet.
        private static int Outstanding(SimWorld world, ConstructionSite site, int recipeLogs, LedgerLocation loc)
        {
            int n = recipeLogs - loc.CountOf(Items.Log.Id);
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != ConstructionTaskDefs.DeliverId) continue;
                if (SiteOf(world, t) != site) continue;
                TaskStep unload = StepOf(t, StepKind.UnloadOx, out int idx);
                if (t.CurrentStep <= idx) n -= unload.Count;
            }
            return n;
        }

        // Re-reserves logs the site lost when a task was cancelled (the logs may now sit elsewhere).
        private static void TopUp(SimWorld world, ConstructionSite site, Footprint fp, int recipeLogs, LedgerLocation loc)
        {
            int missing = Outstanding(world, site, recipeLogs, loc) - Reserved(world, site);
            if (missing <= 0) return;
            List<int> ids = ConstructionSources.TryReserve(world, Items.Log, missing, fp.Centre, fp.Id);
            if (ids != null) site.ReservationIds.AddRange(ids);
        }

        private static void OpenTasks(SimWorld world, ConstructionSite site, Footprint fp, Operation op)
        {
            int load = Math.Max(1, world.Tuning.OxLoadLogs);
            TaskPriority priority = site.Priority == ConstructionPriority.High ? TaskPriority.High : TaskPriority.Normal;
            while (site.ReservationIds.Count > 0)
            {
                int resId = site.ReservationIds[0];
                Reservation r = world.Inventory.GetReservation(resId);
                if (r == null) { site.ReservationIds.RemoveAt(0); continue; }
                LedgerLocation source = world.Inventory.Get(r.LocationId);
                int n = Math.Min(load, r.Count);
                int remainder = r.Count - n;

                // Hand n logs from the site's reservation to the task.
                world.Inventory.Release(resId);
                site.ReservationIds.RemoveAt(0);
                if (remainder > 0)
                {
                    int rest = world.Inventory.ReserveStock(source, Items.Log, remainder, fp.Id);
                    site.ReservationIds.Insert(0, rest);
                }
                PathPoint pick = PointOf(world, source);
                PathPoint drop = new PathPoint(fp.AccessPoint.XMetres, fp.AccessPoint.ZMetres);
                TaskDef def = ConstructionTaskDefs.Deliver(pick, source.Id, drop, site.LedgerLocationId, n,
                    Math.Max(1, world.Tuning.OxLoadMinutes), Math.Max(1, world.Tuning.OxUnloadMinutes));
                TaskRecord task = world.Tasks.Create(def, op.Id, null, priority, fp.Type.Id);
                if (world.Inventory.ReserveStock(source, Items.Log, n, task.Id) == 0)
                {
                    world.Tasks.Cancel(task.Id);
                    throw new InvalidOperationException("Could not re-reserve logs for a delivery task.");
                }
            }
        }

        private static PathPoint PointOf(SimWorld world, LedgerLocation l)
        {
            PointMm p = l.Position;
            if (l.Kind == LocationKind.Storage)
            {
                Footprint f = world.Footprints.Get(l.ModuleId);
                if (f != null) p = f.AccessPoint;
            }
            return new PathPoint(p.XMetres, p.ZMetres);
        }

        // Open delivery tasks without an ox ask the pool, in task id order.
        private static void AttachOxen(SimWorld world)
        {
            var pool = new OxPool(world);
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
            {
                if (t.Def.Id != ConstructionTaskDefs.DeliverId || t.OxId.HasValue) continue;
                TaskStep load = StepOf(t, StepKind.LoadOx, out int idx);
                LedgerLocation source = idx < 0 ? null : world.Inventory.Get(load.LocationId);
                if (source == null) { world.Tasks.Cancel(t.Id); continue; }
                Ox ox = pool.FindFor(t.OwnerOperationId, PointMm.FromMetres(t.Def.Steps[1].X, t.Def.Steps[1].Z));
                if (ox == null) continue;
                bool assigned = world.Operations.FindOxOwner(ox.Id) != null;
                world.Tasks.SetOx(t.Id, ox.Id, assigned);
                ox.State = OxState.Reserved(t.Id);
            }
        }
    }
}
