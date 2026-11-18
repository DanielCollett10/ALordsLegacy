using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Tasks
{
    /// <summary>
    /// Dispatch-phase system (TSK-07, TSK-08, PPL-44, PPL-45). Every <c>DispatchIntervalMinutes</c> (10) game minutes it
    /// matches idle workers to Open tasks, workers in id order. A worker takes the best eligible task: higher priority
    /// first, then shorter travel, then lower task id. Travel is the straight-line distance at walking speed from the
    /// worker to the job: the target tree for a fell task (TSK-07), the ox for a delivery (CN-31), the site for a
    /// build (CN-31). It reserves the task's target tree, assigns and starts the task. The step runner does the rest.
    /// <para>
    /// Two passes: first workers of the family assigned to the task's owner operation, then helper families (no
    /// workplace). A family with a workplace never takes other work (PPL-45). Tasks with a heavy step need a worker aged
    /// 14 or over (PPL-44). A task that requires an ox is offered only once it has one (WC-21/22), and not after the
    /// end of work (T-291). A task whose target tree has gone, or whose ox has gone, is cancelled (TSK-13). No state,
    /// so one instance per world is not required but is customary.
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

            RunFamilyTrips(world);
            bool oxOpen = OxTasksOpen(world);
            if (oxOpen) CancelTasksOfGoneOxen(world);
            RunPass(world, helpers: false, oxOpen);
            RunPass(world, helpers: true, oxOpen);
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

        // Ox tasks are offered only before the end of work: after it no haul starts (T-291, WC-35).
        private static bool OxTasksOpen(SimWorld world) =>
            world.Tick < world.WorkDay.WindowOf(world, WorkDay.DayStart(world)).End;

        // An ox task whose ox has gone is cancelled (TSK-13).
        private static void CancelTasksOfGoneOxen(SimWorld world)
        {
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
                if (t.Def.Required == RequiredResource.Ox && t.OxId.HasValue && world.People.FindOx(t.OxId.Value) == null)
                    world.Tasks.Cancel(t.Id);
        }

        private static void RunPass(SimWorld world, bool helpers, bool oxOpen)
        {
            var citizens = new List<Citizen>(world.People.Citizens); // id order
            foreach (Citizen c in citizens)
            {
                if (!IsIdle(world, c)) continue;
                Family f = world.People.FindFamily(c.FamilyId);
                if (f == null || f.IsInPassage) continue; // decision 0045, GR-13
                if (helpers !=!f.WorkplaceId.HasValue) continue;
                // An odd-jobber off shift is offered nothing (T-346, CN-25); assigned workers may take tomorrow's task.
                if (helpers && !world.WorkDay.OnShift(world, c)) continue;
                double age = c.AgeYears(world.Tick, world.Tuning.AgeingRate);
                bool adult = age >= OperationStatus.WorkingAgeYears;
                bool forager = age >= world.Tuning.ForagerMinAge;

                TaskRecord best = null;
                double bestTravel = 0;
                foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
                {
                    if (!Eligible(world, t, f, helpers, adult, forager)) continue;
                    if (!TravelTo(world, t, c, helpers, oxOpen, out double travel)) continue;
                    // T-518: at equal priority a stall carry beats stock_stall (no stall, no selling), whatever the travel.
                    if (best == null || t.Priority > best.Priority ||
                        (t.Priority == best.Priority && (IsStallCarry(t) != IsStallCarry(best)
                            ? IsStallCarry(t) : travel < bestTravel - 1e-9)))
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

        // Walking time (minutes) from the citizen to the job of an Open task, and whether the task can be offered to
        // this citizen now. A fell task with a gone tree is cancelled here (TSK-13).
        private static bool TravelTo(SimWorld world, TaskRecord t, Citizen c, bool helpers, bool oxOpen, out double travel)
        {
            double walk = Math.Max(1e-9, world.Tuning.WalkSpeedMPerGameMinute);
            travel = 0;
            if (t.Def.Required == RequiredResource.Ox)
            {
                // WC-21/22: offered only once an ox is reserved, only before the end of work, and only to the free
                // eligible worker nearest the ox (ties: lowest id).
                if (!oxOpen || !t.OxId.HasValue) return false;
                Ox ox = world.People.FindOx(t.OxId.Value);
                if (ox == null) return false;
                if (!IsNearestFreeToOx(world, t, c, helpers, ox)) return false;
                travel = DistanceM(ox.X, ox.Z, c.X, c.Z) / walk;
                return true;
            }
            if (t.BushId.HasValue)
            {
                if (!world.Bushes.TryGet(t.BushId.Value, out Foraging.BushRecord bush) || bush.ReservedBy != t.Id)
                {
                    world.Tasks.Cancel(t.Id); // the bush is gone or no longer ours
                    return false;
                }
                travel = DistanceM(bush.X, bush.Z, c.X, c.Z) / walk;
                return true;
            }
            if (t.Target.HasValue)
            {
                if (!world.Trees.TryGet(t.Target.Value, out TreeRecord tree) || tree.State == TreeState.Stump)
                {
                    world.Tasks.Cancel(t.Id); // TSK-13: target gone
                    return false;
                }
                if (tree.ReservedBy.HasValue && tree.ReservedBy.Value != t.Id) return false;
                travel = DistanceM(tree.X, tree.Z, c.X, c.Z) / walk;
                return true;
            }
            if (t.SiteFootprintId.HasValue)
            {
                Footprint site = world.Footprints.Get(t.SiteFootprintId.Value);
                if (site == null) { world.Tasks.Cancel(t.Id); return false; }
                travel = DistanceM(site.Centre.XMetres, site.Centre.ZMetres, c.X, c.Z) / walk; // CN-31: nearest site
            }
            return true;
        }

        // True when no other free, eligible worker of this pass stands nearer the ox than c (ties: lower id wins).
        private static bool IsNearestFreeToOx(SimWorld world, TaskRecord t, Citizen c, bool helpers, Ox ox)
        {
            double mine = SquaredDistance(ox.X, ox.Z, c.X, c.Z);
            foreach (Citizen other in world.People.Citizens)
            {
                if (other.Id == c.Id || !CanTake(world, other, t, helpers)) continue;
                double theirs = SquaredDistance(ox.X, ox.Z, other.X, other.Z);
                if (theirs < mine - 1e-9 || (theirs <= mine + 1e-9 && other.Id < c.Id)) return false;
            }
            return true;
        }

        // Free, in this pass, and eligible for the task (same checks as the worker loop).
        private static bool CanTake(SimWorld world, Citizen c, TaskRecord t, bool helpers)
        {
            if (!IsIdle(world, c)) return false;
            Family f = world.People.FindFamily(c.FamilyId);
            if (f == null || f.IsInPassage || helpers != !f.WorkplaceId.HasValue) return false;
            if (helpers && !world.WorkDay.OnShift(world, c)) return false;
            double age = c.AgeYears(world.Tick, world.Tuning.AgeingRate);
            return Eligible(world, t, f, helpers, age >= OperationStatus.WorkingAgeYears, age >= world.Tuning.ForagerMinAge);
        }

        // A carry that only the stall's granary family may take (T-518).
        private static bool IsStallCarry(TaskRecord t) =>
            t.Def.Id == ConstructionTaskDefs.CarryId && t.Def.Eligibility == EligibilityPolicy.AssignedFamilyOnly;

        private static double SquaredDistance(double ax, double az, double bx, double bz)
        {
            double dx = ax - bx, dz = az - bz;
            return dx * dx + dz * dz;
        }

        private static double DistanceM(double ax, double az, double bx, double bz)
        {
            double dx = ax - bx, dz = az - bz;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool Eligible(SimWorld world, TaskRecord t, Family f, bool helpers, bool adult, bool forager)
        {
            if (t.State != TaskState.Open) return false;
            if (!adult && HasHeavyStep(t.Def)) return false;
            if (IsStallCarry(t))
            {
                // T-518, PL-16: only the family staffing that stall, from age 8 (MK-24).
                if (!forager || helpers || f.WorkplaceId != t.OwnerOperationId || !t.SiteFootprintId.HasValue) return false;
                return world.Markets.Current != null && world.Markets.Current.FamilyOf(t.SiteFootprintId.Value) == f.Id;
            }
            if (!forager && t.Def.Id == Foraging.ForagingSystem.ForageTaskId) return false; // FD-23: 8 and over
            if (!forager && t.Def.Id == Coppice.CoppiceSystem.CoppiceTaskId) return false; // MAT-17: 8 and over
            if (!forager && t.Def.Id == Markets.StoreFamilySystem.StockStallTaskId) return false; // MK-24: 8 and over
            if (!forager && t.Def.Id == HandHaulTaskDefs.HaulId) return false; // same age rule as the stall trip
            switch (t.Def.Eligibility)
            {
                case EligibilityPolicy.AssignedFamilyOnly:
                    return !helpers && f.WorkplaceId == t.OwnerOperationId;
                case EligibilityPolicy.AssignedThenHelpers:
                    return helpers || f.WorkplaceId == t.OwnerOperationId;
                case EligibilityPolicy.OwnerFamily:
                    return false; // handed out by RunFamilyTrips
                default:
                    return helpers;
            }
        }

        // Sunday food trip (FD-14): each Open owner-family task goes to the lowest-id awake, idle adult of that family.
        private static void RunFamilyTrips(SimWorld world)
        {
            foreach (TaskRecord t in world.Tasks.InState(TaskState.Open))
            {
                if (t.Def.Eligibility != EligibilityPolicy.OwnerFamily) continue;
                Family f = Food.FoodTripSystem.FamilyOf(world, t);
                if (f == null) { world.Tasks.Cancel(t.Id); continue; }
                if (f.IsInPassage) continue; // decision 0045, GR-13
                foreach (int id in f.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(id);
                    if (c == null || c.Activity == Activity.Sleeping || !IsIdle(world, c)) continue;
                    if (c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                    if (world.Tasks.Assign(t.Id, c.Id)) world.Tasks.Start(t.Id);
                    break;
                }
            }
        }

        private static bool HasHeavyStep(TaskDef def)
        {
            foreach (TaskStep s in def.Steps) if (s.Heavy) return true;
            return false;
        }
    }
}
