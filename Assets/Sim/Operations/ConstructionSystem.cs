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
    /// Each Open task asks <see cref="OxPool.FindFor"/> for an ox of the site's operation every tick (a house has none, so
    /// its tasks ask for a pool ox with <c>HouseOwnerId</c>). Helpers do the work (the task def is HelpersOnly and heavy).
    /// The site moves to Building when its first log is on site (CN-23). Houses (HM-06) get the same deliver and build tasks.
    /// Cancelled tasks give their logs back to the site's reservations; tasks of a removed site are cancelled.
    /// </summary>
    public sealed class ConstructionSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Construction.Count == 0 && world.Deconstruction.Count == 0 && !AnyLiveDeliver(world)) return;
            Sweep(world);
            SweepBuilds(world);
            ManageDeconstruction(world);
            foreach (ConstructionSite site in new List<ConstructionSite>(world.Construction.Sites))
            {
                Footprint fp = world.Footprints.Get(site.FootprintId);
                if (fp == null) continue;
                ConstructionRecipe recipe = fp.Type.ConstructionRecipe;
                LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
                if (loc == null) continue;
                if (site.State == ConstructionState.Delivering && AnyDelivered(world, site, recipe))
                    site.State = ConstructionState.Building; // CN-23: from the first item on site
                int owner = OwnerOf(world, site.FootprintId);
                if (site.State == ConstructionState.Building && !ConstructionProgress.TryFinish(world, site, recipe))
                    ManageBuilders(world, site, fp, owner);
                if (recipe.Items.Count == 0) continue;
                foreach (var kv in recipe.Items)
                {
                    ItemDef item = Items.Find(kv.Key);
                    if (item != null) TopUp(world, site, fp, item, kv.Value, loc);
                }
                OpenTasks(world, site, fp, owner);
            }
            AttachOxen(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        // CN-23: a site with nothing to deliver, or with its first item on site (or already used up), is Building.
        private static bool AnyDelivered(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            if (recipe.Items.Count == 0) return true;
            foreach (var kv in recipe.Items)
                if (ConstructionProgress.DeliveredOf(world, site, kv.Key) > 0) return true;
            return false;
        }

        private static bool AnyLiveDeliver(SimWorld world)
        {
            foreach (TaskRecord t in world.Tasks.All)
                if (!t.IsFinished && ConstructionTaskDefs.IsConstruction(t.Def.Id)) return true;
            return false;
        }

        // Build tasks (T-347, CN-23/23c): offered only while cap − progress > 0, up to MaxBuildersPerSite at once and never
        // more than there are minutes left. Open ones are withdrawn when the site is capped.
        private static void ManageBuilders(SimWorld world, ConstructionSite site, Footprint fp, int owner)
        {
            int remaining = ConstructionProgress.Remaining(world, site, fp.Type.ConstructionRecipe);
            int live = 0;
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
            {
                if (t.IsFinished || t.Def.Id != ConstructionTaskDefs.BuildId || t.SiteFootprintId != site.FootprintId) continue;
                if (remaining <= 0 && t.State == TaskState.Open) world.Tasks.Cancel(t.Id);
                else live++;
            }
            int want = Math.Min(Math.Max(1, world.Tuning.MaxBuildersPerSite), remaining) - live;
            TaskPriority priority = site.Priority == ConstructionPriority.High ? TaskPriority.High : TaskPriority.Normal;
            for (int i = 0; i < want; i++)
            {
                TaskDef def = ConstructionTaskDefs.Build(Math.Max(1, fp.Type.ConstructionRecipe.BuildWorkerMinutes));
                TaskRecord task = world.Tasks.Create(def, owner, null, priority, fp.Type.Id);
                world.Tasks.SetSite(task.Id, fp.Id);
            }
        }

        // Deconstruct tasks (T-394, CN-53): up to MaxBuildersPerSite live per marked module, never more than the minutes
        // left. Tasks of an entry that is gone (cancelled, CN-57) are cancelled. A 0-minute module is removed at once.
        private static void ManageDeconstruction(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
                if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.DeconstructId && t.SiteFootprintId.HasValue &&
                    world.Deconstruction.Get(t.SiteFootprintId.Value) == null)
                    world.Tasks.Cancel(t.Id);
            foreach (DeconstructionEntry e in new List<DeconstructionEntry>(world.Deconstruction.Entries))
            {
                Footprint fp = world.Footprints.Get(e.FootprintId);
                if (fp == null) { world.Deconstruction.Remove(e.FootprintId); continue; }
                int remaining = DeconstructionWork.Remaining(world, e, fp);
                if (remaining <= 0) { DeconstructionWork.Finish(world, e.FootprintId); continue; }
                int owner = OwnerOf(world, e.FootprintId);
                int live = 0;
                foreach (TaskRecord t in world.Tasks.All)
                    if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.DeconstructId && t.SiteFootprintId == e.FootprintId) live++;
                int want = Math.Min(Math.Max(1, world.Tuning.MaxBuildersPerSite), remaining) - live;
                for (int i = 0; i < want; i++)
                {
                    TaskRecord task = world.Tasks.Create(ConstructionTaskDefs.Deconstruct(remaining), owner, null,
                        TaskPriority.Normal, fp.Type.Id);
                    world.Tasks.SetSite(task.Id, fp.Id);
                }
            }
        }

        // Cancels live build tasks whose site is Built or gone.
        private static void SweepBuilds(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
                if (!t.IsFinished && t.Def.Id == ConstructionTaskDefs.BuildId && t.SiteFootprintId.HasValue &&
                    world.Construction.Get(t.SiteFootprintId.Value) == null)
                    world.Tasks.Cancel(t.Id);
        }

        private static Operation OperationOf(SimWorld world, int footprintId)
        {
            foreach (Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                    if (id == footprintId) return op;
            return null;
        }

        // Owner id for the tasks of a site or deconstruction entry: its operation's id, or HouseOwnerId for a house
        // (HM-06, HM-14), which belongs to no operation.
        private static int OwnerOf(SimWorld world, int footprintId)
        {
            Operation op = OperationOf(world, footprintId);
            // A granary operation has no area: its tasks stay helpers-only like a house's.
            return op != null && op.TypeId != OperationTypes.GranaryId ? op.Id : DeconstructionWork.HouseOwnerId;
        }

        private static TaskStep StepOf(TaskRecord t, StepKind kind, out int index)
        {
            for (int i = 0; i < t.Def.Steps.Count; i++)
                if (t.Def.Steps[i].Kind == kind) { index = i; return t.Def.Steps[i]; }
            index = -1;
            return default;
        }

        // The step that puts the task's load into a store: UnloadOx for an ox delivery, DepositItems for a hand carry.
        private static TaskStep DropStepOf(TaskRecord t, out int index)
        {
            TaskStep s = StepOf(t, StepKind.UnloadOx, out index);
            return index >= 0 ? s : StepOf(t, StepKind.DepositItems, out index);
        }

        // Site whose store the task unloads into, or null.
        private static ConstructionSite SiteOf(SimWorld world, TaskRecord t)
        {
            TaskStep unload = DropStepOf(t, out int idx);
            if (idx < 0) return null;
            LedgerLocation l = world.Inventory.Get(unload.LocationId);
            return l == null || l.Kind != LocationKind.Site ? null : world.Construction.Get(l.ModuleId);
        }

        private static bool IsSiteDelivery(SimWorld world, TaskRecord t)
        {
            TaskStep unload = DropStepOf(t, out int idx);
            if (idx < 0) return false;
            LedgerLocation l = world.Inventory.Get(unload.LocationId);
            return l != null && l.Kind == LocationKind.Site;
        }

        // Hand carries (T-517): those of a removed site are cancelled unless a walker is already on the way (the step
        // runner fails that one itself, so what it carries is dropped); a cancelled one frees its reservations.
        private static void SweepCarries(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
            {
                if (t.Def.Id != ConstructionTaskDefs.CarryId || !IsSiteDelivery(world, t)) continue;
                if (!t.IsFinished)
                {
                    if (SiteOf(world, t) != null || t.State == TaskState.Active) continue;
                    world.Tasks.Cancel(t.Id);
                    StepRunnerSystem.ReleaseCarryReservations(world, t); // the system may sleep next tick: free them now
                    continue;
                }
                if (t.State != TaskState.Cancelled) continue;
                StepRunnerSystem.ReleaseCarryReservations(world, t);
                StepRunnerSystem.DropCarried(world, t, null); // cancelled from outside the runner mid-walk
            }
        }

        // Cancels tasks of removed sites, and hands the logs of finished-but-undelivered tasks back to their site.
        private static void Sweep(SimWorld world)
        {
            SweepCarries(world);
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

        private static int Reserved(SimWorld world, ConstructionSite site, ItemDef item)
        {
            int n = 0;
            foreach (int id in site.ReservationIds)
            {
                Reservation r = world.Inventory.GetReservation(id);
                if (r != null && r.ItemId == item.Id) n += r.Count;
            }
            return n;
        }

        // Units of an item the site still needs from outside: recipe - delivered (on site + already consumed by progress,
        // T-347) - carried by live tasks (ox deliveries for logs, hand carries for the rest) that have not unloaded yet.
        // Consumed units must count as delivered, or the site orders them again from the stock (T-350).
        private static int Outstanding(SimWorld world, ConstructionSite site, ItemDef item, int recipeCount, LedgerLocation loc)
        {
            int n = recipeCount - loc.CountOf(item.Id) - site.ConsumedOf(item.Id);
            bool isLog = item.Id == Items.Log.Id;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished) continue;
                bool ours = isLog ? t.Def.Id == ConstructionTaskDefs.DeliverId : t.Def.Id == ConstructionTaskDefs.CarryId;
                if (!ours || SiteOf(world, t) != site) continue;
                TaskStep drop = DropStepOf(t, out int idx);
                if (!isLog && drop.ItemId != item.Id) continue;
                if (t.CurrentStep <= idx) n -= drop.Count;
            }
            return n;
        }

        // Re-reserves units the site lost when a task was cancelled (the items may now sit elsewhere).
        private static void TopUp(SimWorld world, ConstructionSite site, Footprint fp, ItemDef item, int recipeCount, LedgerLocation loc)
        {
            int missing = Outstanding(world, site, item, recipeCount, loc) - Reserved(world, site, item);
            if (missing <= 0) return;
            var one = new ConstructionRecipe(new[] { new KeyValuePair<string, int>(item.Id, missing) }, 0);
            List<int> ids = SiteMaterials.TryReserveAll(world, one, fp.Centre, fp.Id);
            if (ids != null) site.ReservationIds.AddRange(ids);
        }

        // Turns the site's reservations into tasks: logs go by ox (OxLoadLogs per trip), every other item is carried by
        // hand (HandCarryPlanks per trip, PL-16).
        private static void OpenTasks(SimWorld world, ConstructionSite site, Footprint fp, int owner)
        {
            TaskPriority priority = site.Priority == ConstructionPriority.High ? TaskPriority.High : TaskPriority.Normal;
            while (site.ReservationIds.Count > 0)
            {
                int at = FirstIndex(world, site);
                int resId = site.ReservationIds[at];
                Reservation r = world.Inventory.GetReservation(resId);
                ItemDef item = r == null ? null : Items.Find(r.ItemId);
                if (item == null) { site.ReservationIds.RemoveAt(at); continue; }
                bool isLog = item.Id == Items.Log.Id;
                int load = Math.Max(1, isLog ? world.Tuning.OxLoadLogs : world.Tuning.HandCarryPlanks);
                LedgerLocation source = world.Inventory.Get(r.LocationId);
                int n = Math.Min(load, r.Count);
                int remainder = r.Count - n;

                // Hand n items from the site's reservation to the task.
                world.Inventory.Release(resId);
                site.ReservationIds.RemoveAt(at);
                if (remainder > 0)
                {
                    int rest = world.Inventory.ReserveStock(source, item, remainder, fp.Id);
                    site.ReservationIds.Insert(at, rest);
                }
                PathPoint pick = PointOf(world, source);
                PathPoint drop = new PathPoint(fp.AccessPoint.XMetres, fp.AccessPoint.ZMetres);
                TaskDef def = isLog
                    ? ConstructionTaskDefs.Deliver(pick, source.Id, drop, site.LedgerLocationId, n,
                        Math.Max(1, world.Tuning.OxLoadMinutes), Math.Max(1, world.Tuning.OxUnloadMinutes))
                    : ConstructionTaskDefs.Carry(pick, source.Id, item.Id, drop, site.LedgerLocationId, n);
                TaskRecord task = world.Tasks.Create(def, owner, null, priority, fp.Type.Id);
                world.Tasks.SetSite(task.Id, fp.Id); // lets a priority change (CN-30) reach this task
                if (world.Inventory.ReserveStock(source, item, n, task.Id) == 0)
                {
                    world.Tasks.Cancel(task.Id);
                    throw new InvalidOperationException("Could not re-reserve " + item.Id + " for a delivery task.");
                }
            }
        }

        // CN-25b: reservations on the site's own piles (left by a sunset wind-down) are used before the others.
        private static int FirstIndex(SimWorld world, ConstructionSite site)
        {
            for (int i = 0; i < site.ReservationIds.Count; i++)
            {
                Reservation r = world.Inventory.GetReservation(site.ReservationIds[i]);
                LedgerLocation l = r == null ? null : world.Inventory.Get(r.LocationId);
                if (l != null && l.Kind == LocationKind.GroundPile && l.OwnerOperationId == site.FootprintId) return i;
            }
            return 0;
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
