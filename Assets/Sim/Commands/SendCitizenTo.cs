using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Debug only (PPL-33): sends one citizen to a point, overriding its routine. The citizen walks there, lingers
    /// <c>DebugLingerMinutes</c> after arriving, then the routine resumes. Works while paused; the walk starts on the
    /// next tick. Rejected with <see cref="CommandRejectCode.NotFound"/> for an unknown citizen and
    /// <see cref="CommandRejectCode.NotAllowed"/> ("No route") when no walkable route exists from where the citizen
    /// stands. Rejected commands change nothing.
    /// </summary>
    public sealed class SendCitizenTo : ISimCommand
    {
        /// <summary>Id of the citizen to send.</summary>
        public int CitizenId { get; }

        /// <summary>Target point in metres.</summary>
        public PathPoint Point { get; }

        /// <summary>Creates the command.</summary>
        public SendCitizenTo(int citizenId, PathPoint point)
        {
            CitizenId = citizenId;
            Point = point;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Citizen c = world.People.FindCitizen(CitizenId);
            if (c == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no citizen " + CitizenId);
            if (double.IsNaN(Point.X) || double.IsInfinity(Point.X) || double.IsNaN(Point.Z) || double.IsInfinity(Point.Z))
                return CommandResult.Rejected(CommandRejectCode.InvalidArgument, "point is not a finite position");

            PathResult route = GridPathfinder.Find(world.Nav, new PathPoint(c.X, c.Z), Point);
            if (!route.Ok) return CommandResult.Rejected(CommandRejectCode.NotAllowed, MoveOrder.NoRouteReason);
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;

            Citizen c = world.People.FindCitizen(CitizenId);
            var order = new MoveOrder(Point.X, Point.Z);
            c.DebugSend = new DebugSendState(order);
            c.Move = order;
            c.Activity = Activity.Walking;
            return CommandResult.Accepted();
        }
    }
}
