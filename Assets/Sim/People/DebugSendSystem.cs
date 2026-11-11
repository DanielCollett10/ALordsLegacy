using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// Movement-phase bookkeeping for debug "send to" orders (PPL-33). Runs after <see cref="MovementSystem"/>, so it
    /// sees an arrival in the same tick. On arrival the citizen stands at the target and lingers for
    /// <c>DebugLingerMinutes</c>; then <see cref="Citizen.DebugSend"/> is cleared and the routine resumes. A walk that
    /// fails (no route any more) ends at once. <see cref="RoutineSystem"/> skips citizens that have a send in progress.
    /// </summary>
    public sealed class DebugSendSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            foreach (Citizen c in world.People.Citizens)
            {
                DebugSendState d = c.DebugSend;
                if (d == null) continue;

                if (d.LingerUntilTick < 0)
                {
                    if (d.Order.Status == MoveStatus.Done)
                    {
                        d.LingerUntilTick = world.Tick + world.Tuning.DebugLingerMinutes;
                        c.Move = null;
                        c.Activity = Activity.MillingAbout;
                    }
                    else if (d.Order.Status == MoveStatus.Failed)
                    {
                        c.Move = null;
                        c.DebugSend = null;
                    }
                }
                else if (world.Tick >= d.LingerUntilTick)
                {
                    c.DebugSend = null;
                }
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }
    }
}
