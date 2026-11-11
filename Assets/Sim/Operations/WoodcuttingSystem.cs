using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Processes;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Operations-phase system (WC-10..15, WC-25, TSK-06, TMB-05..07). For every Working operation it keeps one open
    /// Fell task per idle worker of the assigned families. Each task targets the nearest standing, unreserved tree to
    /// the hut access point inside the work region (linked work area polygon, or the default circle) and reserves it
    /// from creation (TMB-08). Steps: MoveTo, then the effective timber stages for that tree's log count. When the
    /// operation is not Working, its still-open Fell tasks are cancelled. New tasks stop once the logs on hand plus the
    /// yield of unfinished tasks would fill the storage (WC-25), so storage is not overshot. Stateless.
    /// </summary>
    public sealed class WoodcuttingSystem : ISimSystem
    {
        /// <summary>Id of the Fell task definition.</summary>
        public const string FellTaskId = "task.fell_tree";

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (Operation op in new List<Operation>(world.Operations.All))
            {
                if (op.TypeId != OperationTypes.WoodcuttingId) continue;
                if (OperationStatus.Of(world, op) != OperationState.Working) CancelOpen(world, op);
                else Refill(world, op);
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static void CancelOpen(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == FellTaskId) world.Tasks.Cancel(t.Id);
        }

        private static void Refill(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            TreeRegion region = OperationStatus.WorkRegion(world, op);
            if (hut == null || region == null) return;

            int open = 0, pendingLogs = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (t.IsFinished || t.Def.Id != FellTaskId) continue;
                if (t.State == TaskState.Open) open++;
                pendingLogs += YieldOf(world, t);
            }

            int wanted = CountIdleWorkers(world, op) - open;
            int capacity = OperationStatus.LogCapacity(world, op);
            float ax = (float)hut.AccessPoint.XMetres, az = (float)hut.AccessPoint.ZMetres;
            int load = OperationStatus.LogLoad(world, op);
            for (int i = 0; i < wanted; i++)
            {
                if (load + pendingLogs >= capacity) return;
                TreeRecord tree = world.Trees.NearestUnreservedStanding(ax, az, region);
                if (tree == null) return;
                TaskRecord task = world.Tasks.Create(BuildDef(world, tree), op.Id, tree.Id);
                if (!world.Tasks.Reserve(task.Id, tree.Id)) { world.Tasks.Cancel(task.Id); return; }
                pendingLogs += tree.LogYield(world.Tuning);
            }
        }

        private static int YieldOf(SimWorld world, TaskRecord t)
        {
            return t.Target.HasValue && world.Trees.TryGet(t.Target.Value, out TreeRecord tree) && tree.State != TreeState.Stump
                ? tree.LogYield(world.Tuning)
                : 0;
        }

        private static int CountIdleWorkers(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                    if (DispatchSystem.IsIdle(world, c)) n++;
                }
            }
            return n;
        }

        private static TaskDef BuildDef(SimWorld world, TreeRecord tree)
        {
            var steps = new List<TaskStep> { TaskStep.MoveTo() };
            foreach (EffectiveStage s in ProcessDefs.Timber.EffectiveStages(tree.LogYield(world.Tuning)))
                steps.Add(TaskStep.Work(Math.Max(1, s.Minutes), s.Heavy, s.StageId));
            return new TaskDef(FellTaskId, steps, EligibilityPolicy.AssignedFamilyOnly);
        }
    }
}
