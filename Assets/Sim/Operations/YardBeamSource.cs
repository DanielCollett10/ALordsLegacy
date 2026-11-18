using System;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// ST-43, source 4: beams go to the yard by ox, never by hand. From a hewing store with a free beam (at or above
    /// <c>YardCollectThreshold</c> full, 0 by default), or an unowned loose beam pile, a free pool ox (<see cref="OxPool.FindFor"/>) is led
    /// by the yard family: one <c>OxLoadBeams</c> load per trip, to the nearest yard with room. The trip reuses the
    /// <see cref="WorkshopSystem.FetchTaskId"/> steps (FetchOx, LeadTo, LoadOx, LeadTo, UnloadOx, ReturnOx), so its reservations
    /// and ox are freed by the workshop sweep. The yard system runs after construction, so deliveries take free oxen first
    /// (HW-06), and an ox assigned to another operation is never borrowed. With no free ox no task is made.
    /// This source makes its own ox task and returns no hand trip.
    /// </summary>
    public sealed class YardBeamSource : IYardJobSource
    {
        /// <inheritdoc />
        public YardTrip NextTrip(SimWorld world, Operation yard)
        {
            if (world == null || yard == null) return null;
            LedgerLocation dest = YardRoute.StoreOf(world, yard);
            if (dest == null || HasOpenBeamTask(world, yard)) return null;
            LedgerLocation from = Pick(world, yard, dest);
            if (from == null) return null;

            int load = Math.Min(Math.Max(1, world.Tuning.OxLoadBeams), Math.Min(from.AvailableOf(Items.Beam.Id), dest.FreeSpace));
            if (load <= 0) return null;
            PointMm pick = PointOf(world, from);
            Ox ox = new OxPool(world).FindFor(yard.Id, pick);
            if (ox == null) return null; // Waiting for ox: nothing is opened until one is free

            Footprint yardFp = world.Footprints.Get(dest.ModuleId);
            PointMm drop = yardFp != null ? yardFp.AccessPoint : dest.Position;
            string item = Items.Beam.Id;
            var steps = new[]
            {
                TaskStep.FetchOx(),
                TaskStep.LeadTo(new PathPoint(pick.XMetres, pick.ZMetres)),
                TaskStep.LoadOx(from.Id, load, Math.Max(1, world.Tuning.OxLoadMinutes), false, item),
                TaskStep.LeadTo(new PathPoint(drop.XMetres, drop.ZMetres)),
                TaskStep.UnloadOx(dest.Id, load, Math.Max(1, world.Tuning.OxUnloadMinutes), false, item),
                TaskStep.ReturnOx(),
            };
            var def = new TaskDef(WorkshopSystem.FetchTaskId, steps, EligibilityPolicy.AssignedFamilyOnly, RequiredResource.Ox);
            TaskRecord task = world.Tasks.Create(def, yard.Id, null, TaskPriority.Normal);
            if (world.Inventory.ReserveStock(from, Items.Beam, load, task.Id) == 0 ||
                world.Inventory.ReserveCapacity(dest, Items.Beam, load, task.Id) == 0)
            {
                world.Inventory.CancelOwner(task.Id);
                world.Tasks.Cancel(task.Id);
                return null;
            }
            bool assigned = world.Operations.FindOxOwner(ox.Id) != null;
            world.Tasks.SetOx(task.Id, ox.Id, assigned);
            ox.State = OxState.Reserved(task.Id);
            return null;
        }

        /// <summary>
        /// True when a beam load is ready for <paramref name="yard"/> but no pool ox is free (the Waiting for ox status,
        /// ST-45). Read only: same source and room rules as <see cref="NextTrip"/>, without making a task.
        /// </summary>
        public static bool WaitingForOx(SimWorld world, Operation yard)
        {
            if (world == null || yard == null) return false;
            LedgerLocation dest = YardRoute.StoreOf(world, yard);
            if (dest == null || HasOpenBeamTask(world, yard)) return false;
            LedgerLocation from = Pick(world, yard, dest);
            if (from == null || Math.Min(from.AvailableOf(Items.Beam.Id), dest.FreeSpace) <= 0) return false;
            return new OxPool(world).FindFor(yard.Id, PointOf(world, from)) == null;
        }

        // One waiting trip per yard at a time; the next is made once a family member has picked it up.
        private static bool HasOpenBeamTask(SimWorld world, Operation yard)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(yard.Id))
                if (t.State == TaskState.Open && t.Def.Id == WorkshopSystem.FetchTaskId) return true;
            return false;
        }

        // The fullest qualifying hewing store, else the nearest loose beam pile; only when this yard is the nearest with room.
        private static LedgerLocation Pick(SimWorld world, Operation yard, LedgerLocation dest)
        {
            LedgerLocation best = null;
            double bestFill = -1;
            foreach (Operation op in world.Operations.All)
            {
                WorkshopDef def = OperationTypes.WorkshopOf(op.TypeId, world.Tuning);
                if (def == null || def.Output.Id != Items.Beam.Id) continue;
                foreach (int m in op.ModuleIds)
                {
                    if (!world.IsWorking(m)) continue;
                    LedgerLocation store = world.Inventory.FindGoodsStore(m);
                    if (store == null || store.Capacity <= 0 || store.AvailableOf(Items.Beam.Id) <= 0) continue;
                    double fill = (double)store.Total / store.Capacity;
                    if (fill < world.Tuning.YardCollectThreshold || fill <= bestFill || !IsNearest(world, yard, store)) continue;
                    best = store; bestFill = fill;
                }
            }
            if (best != null) return best;

            double bestD = double.MaxValue;
            PointMm yardAt = ConstructionSources.StorePosition(world, dest);
            foreach (LedgerLocation pile in world.Inventory.Locations)
            {
                if (pile.Kind != LocationKind.GroundPile || pile.OwnerOperationId != null) continue;
                if (pile.AvailableOf(Items.Beam.Id) <= 0 || !IsNearest(world, yard, pile)) continue;
                double d = pile.Position.DistanceTo(yardAt);
                if (d < bestD) { best = pile; bestD = d; }
            }
            return best;
        }

        private static bool IsNearest(SimWorld world, Operation yard, LedgerLocation from)
        {
            Operation nearest = YardRoute.NearestYardWithRoom(world, ConstructionSources.StorePosition(world, from));
            return nearest != null && nearest.Id == yard.Id;
        }

        private static PointMm PointOf(SimWorld world, LedgerLocation l)
        {
            if (l.Kind == LocationKind.GroundPile) return l.Position;
            Footprint f = world.Footprints.Get(l.ModuleId);
            return f != null ? f.AccessPoint : l.Position;
        }
    }
}
