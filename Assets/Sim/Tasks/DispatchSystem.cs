using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Dispatch-phase system (TSK-07, TSK-08, PPL-44, PPL-45). Every <c>DispatchIntervalMinutes</c> (10) game minutes it
    /// matches idle workers to Open tasks, workers in id order. A worker takes the best eligible task: higher priority
    /// first, then shorter travel (straight-line distance at walking speed), then lower task id. It reserves the task's
    /// target tree, assigns and starts the task. The step runner does the rest.
    /// <para>
    /// Two passes: first workers of the family assigned to the task's owner operation, then helper families (no
    /// workplace). A family with a workplace never takes other work (PPL-45). Tasks with a heavy step need a worker aged
    /// 14 or over (PPL-44). Tasks that require an ox are left open (no ox handling yet). A task whose target tree has
    /// gone is cancelled (TSK-13). No state, so one instance per world is not required but is customary.
    /// </para>
    /// </summary>
    public sealed class DispatchSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            int interval = Math.Max(1, world.Tuning.DispatchIntervalMinutes);
            if (world.Tick % interval != 0) return;
            if (world.Tasks.InState(TaskState.Open).Count == 0) return;

            RunPass(world, helpers: false);
            RunPass(world, helpers: true);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        /// <summary>True when the citizen can be given a task now (WC-26: a worker holding a task is not idle).</summary>
        public static bool IsIdle(SimWorld world, Citizen c)
        {
            if (world.Tasks.IsBusy(c.Id)) return false;
            if (c.DebugSend != null) return false;
            // A sleeping worker of an assigned family can take tomorrow's task now (commute in the dark, PPL-25).
            if (c.Activity == Activity.Sleeping) return WorkDay.AppliesTo(world, c) && (c.Move == null || c.Move.IsFinished);
            return c.Move == null || c.Move.IsFinished || c.Activity == Activity.MillingAbout;
        }

        private static void RunPass(SimWorld world, bool helpers)
        {
            double walk = Math.Max(1e-9, world.Tuning.WalkSpeedMPerGameMinute);
            var citizens = new List<Citizen>(world.People.Citizens); // id order
            AssignOxTasks(world, citizens, helpers);
            foreach (Citizen c in citizens)
            {
                if (!IsIdle(world, c)) continue;
                Family f = world.People.FindFamily(c.FamilyId);
                if (f == null) continue;
                if (helpers != !f.WorkplaceId.HasValue) continue;
                bool adult = c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears;

                TaskRecord best = null;
                double bestTravel = 0;
                foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
                {
                    if (!Eligible(t, f, helpers, adult)) continue;
                    if (t.Def.Required == RequiredResource.Ox) continue; // handled by AssignOxTasks
                    double travel = 0;
                    if (t.Target.HasValue)
                    {
                        if (!world.Trees.TryGet(t.Target.Value, out TreeRecord tree) || tree.State == TreeState.Stump)
                        {
                            world.Tasks.Cancel(t.Id); // TSK-13: target gone
                            continue;
                        }
                        if (tree.ReservedBy.HasValue && tree.ReservedBy.Value != t.Id) continue;
                        double dx = tree.X - c.X, dz = tree.Z - c.Z;
                        travel = Math.Sqrt(dx * dx + dz * dz) / walk;
                    }
                    if (best == null || t.Priority > best.Priority ||
                        (t.Priority == best.Priority && travel < bestTravel - 1e-9))
                    {
                        best = t; bestTravel = travel;
                    }
                }
                if (best == null) continue;
                if (best.Target.HasValue && !world.Tasks.Reserve(best.Id, best.Target.Value)) continue;
                if (!world.Tasks.Assign(best.Id, c.Id)) continue;
                world.Tasks.Start(best.Id);
            }
        }

        // Ox tasks (WC-21/22): only once an ox is reserved; the free eligible worker nearest the ox takes it (ties: lowest id).
        private static void AssignOxTasks(SimWorld world, List<Citizen> citizens, bool helpers)
        {
            // After the end of work no haul starts: it would only be dropped again at once (T-291, WC-35).
            if (world.Tick >= world.WorkDay.WindowOf(world, WorkDay.DayStart(world)).End) return;
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
            {
                if (t.Def.Required != RequiredResource.Ox || !t.OxId.HasValue) continue;
                Ox ox = world.People.FindOx(t.OxId.Value);
                if (ox == null) { world.Tasks.Cancel(t.Id); continue; }
                Citizen best = null;
                double bestD = double.MaxValue;
                foreach (Citizen c in citizens)
                {
                    if (!IsIdle(world, c)) continue;
                    Family f = world.People.FindFamily(c.FamilyId);
                    if (f == null || helpers != !f.WorkplaceId.HasValue) continue;
                    bool adult = c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears;
                    if (!Eligible(t, f, helpers, adult)) continue;
                    double dx = ox.X - c.X, dz = ox.Z - c.Z;
                    double d = dx * dx + dz * dz;
                    if (d < bestD - 1e-9) { bestD = d; best = c; }
                }
                if (best == null) continue;
                if (!world.Tasks.Assign(t.Id, best.Id)) continue;
                world.Tasks.Start(t.Id);
            }
        }

        private static bool Eligible(TaskRecord t, Family f, bool helpers, bool adult)
        {
            if (t.State != TaskState.Open) return false;
            if (!adult && HasHeavyStep(t.Def)) return false;
            switch (t.Def.Eligibility)
            {
                case EligibilityPolicy.AssignedFamilyOnly:
                    return !helpers && f.WorkplaceId == t.OwnerOperationId;
                case EligibilityPolicy.AssignedThenHelpers:
                    return helpers || f.WorkplaceId == t.OwnerOperationId;
                default:
                    return helpers;
            }
        }

        private static bool HasHeavyStep(TaskDef def)
        {
            foreach (TaskStep s in def.Steps) if (s.Heavy) return true;
            return false;
        }
    }
}
