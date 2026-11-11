using System;

namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// A debug "send to" in progress for one citizen (PPL-33). While it is set, the routine leaves the citizen alone.
    /// <see cref="Order"/> is the walk to the clicked point. <see cref="LingerUntilTick"/> is -1 until the citizen
    /// arrives, then the tick from which the citizen may go back to the routine. Set by <c>SendCitizenTo</c>; advanced
    /// by <see cref="DebugSendSystem"/>.
    /// </summary>
    public sealed class DebugSendState
    {
        /// <summary>Creates the state for a walk that has just been ordered.</summary>
        public DebugSendState(MoveOrder order)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
            LingerUntilTick = -1;
        }

        /// <summary>The walk to the target point.</summary>
        public MoveOrder Order { get; }

        /// <summary>Tick at which the citizen returns to its routine; -1 while still walking.</summary>
        public long LingerUntilTick { get; internal set; }
    }
}
