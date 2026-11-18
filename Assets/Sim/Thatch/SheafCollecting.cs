using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Thatch
{
    /// <summary>
    /// Collect trips of a thatcher's hut (MAT-32, MAT-35, MAT-39d): one dry sheaf per trip from an operation pile of the
    /// hut to the hut's own store. Pure queries and the task builder; <see cref="ThatchingSystem"/> plans them before new
    /// cutting. The yard never collects the hut store (ST-42b).
    /// </summary>
    public static class SheafCollecting
    {
        /// <summary>Id of the collect task definition.</summary>
        public const string CollectTaskId = "task.collect_thatch";

        /// <summary>Sheaves one collect trip carries (MAT-35).</summary>
        public const int PerTrip = 1;

        /// <summary>Dry piles of the operation holding thatch, nearest to <paramref name="from"/> first (ties by id).</summary>
        public static List<LedgerLocation> DryPiles(SimWorld world, Operation op, PointMm from)
        {
            var list = new List<LedgerLocation>();
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id && l.CountOf(Items.Thatch.Id) > 0) list.Add(l);
            list.Sort((a, b) =>
            {
                int c = a.Position.DistanceTo(from).CompareTo(b.Position.DistanceTo(from));
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            return list;
        }

        /// <summary>Sheaves unfinished collect trips of this operation have promised to pick up from one pile.</summary>
        public static int PendingFrom(SimWorld world, int operationId, int pileId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(operationId))
            {
                if (t.IsFinished || t.Def.Id != CollectTaskId) continue;
                if (t.CurrentStep > 1) continue; // already on the carrier, no longer in the pile
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.CollectItems && s.LocationId == pileId) n += s.Count;
            }
            return n;
        }

        /// <summary>Sheaves unfinished collect trips of this operation have promised to a store but not yet put in it.</summary>
        public static int PendingTo(SimWorld world, int operationId, int storeId)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(operationId))
            {
                if (t.IsFinished || t.Def.Id != CollectTaskId) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositItems && s.LocationId == storeId) n += s.Count;
            }
            return n;
        }

        /// <summary>Dry sheaves lying in the operation's piles (promised or not).</summary>
        public static int DryCount(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (LedgerLocation l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == op.Id) n += l.CountOf(Items.Thatch.Id);
            return n;
        }

        /// <summary>Room left in the hut store after trips already on their way (never negative).</summary>
        public static int Room(SimWorld world, Operation op, LedgerLocation store)
        {
            int r = store.FreeSpaceFor(Items.Thatch.Id) - PendingTo(world, op.Id, store.Id);
            return r < 0 ? 0 : r;
        }

        /// <summary>Plans one collect trip if a dry sheaf and room exist; false when nothing was planned.</summary>
        public static bool PlanOne(SimWorld world, Operation op, Footprint hut, LedgerLocation store)
        {
            if (Room(world, op, store) < PerTrip) return false;
            foreach (LedgerLocation pile in DryPiles(world, op, hut.Centre))
            {
                if (pile.CountOf(Items.Thatch.Id) - PendingFrom(world, op.Id, pile.Id) < PerTrip) continue;
                var steps = new List<TaskStep>
                {
                    TaskStep.WalkTo(new PathPoint(pile.Position.XMetres, pile.Position.ZMetres)),
                    TaskStep.CollectItems(pile.Id, Items.Thatch.Id, PerTrip),
                    TaskStep.WalkTo(new PathPoint(hut.AccessPoint.XMetres, hut.AccessPoint.ZMetres)),
                    TaskStep.DepositItems(store.Id, Items.Thatch.Id, PerTrip),
                };
                world.Tasks.Create(new TaskDef(CollectTaskId, steps, EligibilityPolicy.AssignedFamilyOnly), op.Id);
                return true;
            }
            return false;
        }
    }
}
