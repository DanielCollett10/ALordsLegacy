using System.Collections.Generic;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.People
{
    /// <summary>Where a <see cref="MoveOrder"/> stands.</summary>
    public enum MoveStatus
    {
        /// <summary>Not planned yet; <c>MovementSystem</c> plans it on its next step.</summary>
        Pending,
        /// <summary>Walking along <see cref="MoveOrder.Path"/>.</summary>
        Moving,
        /// <summary>Arrived at the target.</summary>
        Done,
        /// <summary>Could not get there; see <see cref="MoveOrder.FailReason"/> (MOV-13).</summary>
        Failed
    }

    /// <summary>
    /// A request for one citizen or ox to walk to a point (MOV-14, MOV-15): the target, the current route and how far
    /// along it the agent is. Created by whoever sends the agent (dispatch, M8); advanced and finished by
    /// <see cref="MovementSystem"/>. A finished order stays on the agent until its owner clears it.
    /// </summary>
    public sealed class MoveOrder
    {
        private static readonly PathPoint[] NoPath = new PathPoint[0];

        /// <summary>The reason text of a failed order whose target cannot be reached (MOV-13).</summary>
        public const string NoRouteReason = "No route";

        /// <summary>Creates a pending order to walk to a point in metres.</summary>
        public MoveOrder(double targetX, double targetZ)
        {
            TargetX = targetX;
            TargetZ = targetZ;
            Path = NoPath;
            PlannedNavVersion = -1;
        }

        /// <summary>Target x in metres.</summary>
        public double TargetX { get; }

        /// <summary>Target z in metres.</summary>
        public double TargetZ { get; }

        /// <summary>Current state of the order.</summary>
        public MoveStatus Status { get; internal set; }

        /// <summary>Why the order failed; empty unless <see cref="Status"/> is <see cref="MoveStatus.Failed"/>.</summary>
        public string FailReason { get; internal set; } = string.Empty;

        /// <summary>The current route in metres (empty until planned).</summary>
        public IReadOnlyList<PathPoint> Path { get; internal set; }

        /// <summary>Index of the path point the agent has last passed.</summary>
        public int SegmentIndex { get; internal set; }

        /// <summary>Metres walked along the current path (resets to 0 on a re-plan).</summary>
        public double ProgressM { get; internal set; }

        /// <summary>The <c>NavGrid.Version</c> the path was planned against (-1 before planning).</summary>
        public int PlannedNavVersion { get; internal set; }

        /// <summary>True once the order is <see cref="MoveStatus.Done"/> or <see cref="MoveStatus.Failed"/>.</summary>
        public bool IsFinished => Status == MoveStatus.Done || Status == MoveStatus.Failed;
    }
}
