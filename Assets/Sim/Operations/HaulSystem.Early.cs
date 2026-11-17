using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Settles haul tasks that wait for a Fell task (T-291, WC-23, WC-35). Early fetch is off (T-491): hauls are opened
    /// only from piles that exist, so no haul is opened before its tree is down. The awaiting machinery is kept, so a
    /// saved task that still waits for a Fell task is settled or cancelled as before: its stock is reserved once the
    /// Fell task is Done, and it is dropped if the Fell task is cancelled.
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

        private static double Dist(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1, dz = z2 - z1;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
