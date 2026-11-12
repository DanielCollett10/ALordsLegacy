using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Early fetch (T-291, WC-23, WC-35). For a tree that will yield a full load, the Haul opens before the tree is down:
    /// as soon as the tree's time left is no more than a free worker's walk to the ox plus the ox's walk to the tree.
    /// The haul reserves its ox and room in storage at once and waits at the pile (before LoadOx) until the Fell task is
    /// Done; the pile's stock is then reserved. If the Fell task is cancelled the haul is cancelled with it.
    /// </summary>
    public sealed partial class HaulSystem
    {
        /// <summary>
        /// Game minutes of work left on an Active Fell task: the rest of the current step plus the later steps, plus the
        /// straight-line walk to the tree while the MoveTo step is current.
        /// </summary>
        public static double FellMinutesLeft(SimWorld world, TaskRecord fell)
        {
            double left = 0;
            double walk = Math.Max(1e-9, world.Tuning.WalkSpeedMPerGameMinute);
            for (int i = fell.CurrentStep; i < fell.Def.Steps.Count; i++)
            {
                TaskStep s = fell.Def.Steps[i];
                if (s.Kind == StepKind.Work)
                    left += s.DurationTicks - (i == fell.CurrentStep ? fell.StepProgress : 0);
                else if (s.Kind == StepKind.MoveTo && i == fell.CurrentStep && fell.Target.HasValue &&
                         fell.AssigneeId.HasValue && world.Trees.TryGet(fell.Target.Value, out TreeRecord tree))
                {
                    Citizen c = world.People.FindCitizen(fell.AssigneeId.Value);
                    if (c != null) left += Dist(c.X, c.Z, tree.X, tree.Z) / walk;
                }
            }
            return Math.Max(0, left);
        }

        // Settles haul tasks that wait for a Fell task: reserve the pile's stock when it is Done, drop them if it is not coming.
        private static void ResolveAwaiting(SimWorld world, Operation op)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
            {
                if (t.IsFinished || t.Def.Id != HaulTaskId || !t.AwaitsTaskId.HasValue) continue;
                TaskRecord fell = world.Tasks.Get(t.AwaitsTaskId.Value);
                if (fell == null || fell.State == TaskState.Cancelled)
                {
                    // An Active haul is cancelled by the step runner (it must also release the ox); an Open one here.
                    if (t.State == TaskState.Open) world.Tasks.Cancel(t.Id);
                    continue;
                }
                if (fell.State != TaskState.Done) continue;
                LedgerLocation pile = PileOf(world, t);
                int count = 0;
                foreach (TaskStep s in t.Def.Steps) if (s.Kind == StepKind.LoadOx) count = s.Count;
                if (pile == null || world.Inventory.ReserveStock(pile, Items.Log, count, t.Id) == 0)
                {
                    // No logs to carry (should not happen): give up, freeing the ox where it stands.
                    world.Inventory.CancelOwner(t.Id);
                    Ox held = t.OxId.HasValue ? world.People.FindOx(t.OxId.Value) : null;
                    if (held != null)
                    {
                        MovementSystem.Detach(held);
                        held.State = OxState.Free;
                    }
                    world.Tasks.Cancel(t.Id);
                    continue;
                }
                world.Tasks.SetAwaiting(t.Id, null);
            }
        }

        private static void OpenEarly(SimWorld world, Operation op)
        {
            int load = Math.Max(1, world.Tuning.OxLoadLogs);
            double walk = Math.Max(1e-9, world.Tuning.WalkSpeedMPerGameMinute);
            double oxSpeed = Math.Max(1e-9, world.Tuning.OxSpeedMPerGameMinute);
            var pool = new OxPool(world);
            foreach (TaskRecord fell in world.Tasks.ByOwner(op.Id))
            {
                if (fell.State != TaskState.Active || fell.Def.Id != WoodcuttingSystem.FellTaskId || !fell.Target.HasValue) continue;
                if (!world.Trees.TryGet(fell.Target.Value, out TreeRecord tree) || tree.State == TreeState.Stump) continue;
                if (tree.LogYield(world.Tuning) < load || HasHaulFor(world, op, fell.Id)) continue;
                Citizen feller = fell.AssigneeId.HasValue ? world.People.FindCitizen(fell.AssigneeId.Value) : null;
                if (feller == null || !world.WorkDay.OnShift(world, feller)) continue; // time only passes on shift

                Ox ox = pool.FindFor(op.Id, PointMm.FromMetres(tree.X, tree.Z));
                if (ox == null) continue;
                Citizen worker = FreeWorkerNearest(world, op, ox);
                if (worker == null) continue;
                double toOx = Dist(worker.X, worker.Z, ox.X, ox.Z) / walk;
                double toTree = Dist(ox.X, ox.Z, tree.X, tree.Z) / oxSpeed; // led, so at the ox's pace
                if (FellMinutesLeft(world, fell) > toOx + toTree) continue;

                LedgerLocation pile = world.Inventory.GetOrAddGroundPile(PointMm.FromMetres(tree.X, tree.Z), op.Id);
                if (!TryCreate(world, op, pile, load, fell.Id, out TaskRecord haul)) continue;
                bool assigned = world.Operations.FindOxOwner(ox.Id) != null;
                world.Tasks.SetOx(haul.Id, ox.Id, assigned);
                ox.State = OxState.Reserved(haul.Id);
            }
        }

        private static bool HasHaulFor(SimWorld world, Operation op, int fellId)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (!t.IsFinished && t.Def.Id == HaulTaskId && t.AwaitsTaskId == fellId) return true;
            return false;
        }

        // The idle adult of an assigned family nearest the ox (the one the dispatcher would pick), or null.
        private static Citizen FreeWorkerNearest(SimWorld world, Operation op, Ox ox)
        {
            Citizen best = null;
            double bestD = double.MaxValue;
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < OperationStatus.WorkingAgeYears) continue;
                    if (!DispatchSystem.IsIdle(world, c)) continue;
                    double d = Dist(c.X, c.Z, ox.X, ox.Z);
                    if (d < bestD) { bestD = d; best = c; }
                }
            }
            return best;
        }

        private static double Dist(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1, dz = z2 - z1;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
