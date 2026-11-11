using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Navigation
{
    /// <summary>
    /// Movement-phase system (MOV-12, MOV-14, MOV-15, decision 0022). Each tick, for every citizen and then every ox
    /// in ascending id order:
    /// <list type="bullet">
    /// <item><description>When <see cref="NavGrid.Version"/> changed since the last step, anyone standing on a blocked
    /// cell is moved to the nearest walkable point, and every unfinished order is re-planned from where the agent stands.</description></item>
    /// <item><description>Records <see cref="IMovable.PreviousX"/>/<c>PreviousZ</c> and the multiplier of the cell under the agent.</description></item>
    /// <item><description>Advances an active order by speed (walk or ox, from tuning) × that multiplier along its path and
    /// turns the agent to face the way it went. Arrival sets the order Done; no route sets it Failed ("No route").</description></item>
    /// </list>
    /// </summary>
    public sealed class MovementSystem : ISimSystem
    {
        private int _seenNavVersion = -1;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            NavGrid nav = world.Nav;
            bool navChanged = nav.Version != _seenNavVersion;
            _seenNavVersion = nav.Version;

            foreach (Citizen c in world.People.Citizens) StepAgent(world, c, world.Tuning.WalkSpeedMPerGameMinute, navChanged);
            foreach (Ox o in world.People.Oxen) StepAgent(world, o, world.Tuning.OxSpeedMPerGameMinute, navChanged);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private static void StepAgent(SimWorld world, IMovable a, double speed, bool navChanged)
        {
            NavGrid nav = world.Nav;
            if (navChanged && nav.IsBlocked(nav.CellX(a.X), nav.CellZ(a.Z))
                && GridPathfinder.TryNearestWalkable(nav, new PathPoint(a.X, a.Z), out PathPoint safe))
            {
                a.X = safe.X;
                a.Z = safe.Z;
            }

            a.PreviousX = a.X;
            a.PreviousZ = a.Z;
            a.CurrentMultiplier = nav.SpeedMultiplier(nav.CellX(a.X), nav.CellZ(a.Z));

            MoveOrder order = a.Move;
            if (order == null || order.IsFinished) return;

            if (order.Status == MoveStatus.Pending || order.PlannedNavVersion != nav.Version)
            {
                if (!Plan(nav, a, order)) return;
            }
            Advance(a, order, speed * a.CurrentMultiplier);
        }

        // Plans from the agent's position. False when the order failed.
        private static bool Plan(NavGrid nav, IMovable a, MoveOrder order)
        {
            PathResult r = GridPathfinder.Find(nav, new PathPoint(a.X, a.Z), new PathPoint(order.TargetX, order.TargetZ));
            order.PlannedNavVersion = nav.Version;
            order.SegmentIndex = 0;
            order.ProgressM = 0;
            if (!r.Ok)
            {
                order.Status = MoveStatus.Failed;
                order.FailReason = MoveOrder.NoRouteReason;
                return false;
            }
            order.Path = r.Points;
            order.Status = MoveStatus.Moving;
            return true;
        }

        private static void Advance(IMovable a, MoveOrder order, double budget)
        {
            var pts = order.Path;
            int last = pts.Count - 1;
            while (budget > 0 && order.SegmentIndex < last)
            {
                PathPoint to = pts[order.SegmentIndex + 1];
                double dx = to.X - a.X, dz = to.Z - a.Z;
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (d <= budget)
                {
                    a.X = to.X;
                    a.Z = to.Z;
                    budget -= d;
                    order.ProgressM += d;
                    order.SegmentIndex++;
                }
                else
                {
                    a.X += dx / d * budget;
                    a.Z += dz / d * budget;
                    order.ProgressM += budget;
                    budget = 0;
                }
                if (d > 0) a.FacingRad = Math.Atan2(dx, dz);
            }
            if (order.SegmentIndex >= last) order.Status = MoveStatus.Done;
        }
    }
}
