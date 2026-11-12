using System;
using System.Collections.Generic;
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

            double walk = world.Tuning.WalkSpeedMPerGameMinute, oxSpeed = world.Tuning.OxSpeedMPerGameMinute;

            // Leaders with followers move at the slowest pair speed (MOV-16).
            Dictionary<IMovable, double> pair = null;
            foreach (Citizen c in world.People.Citizens) NotePair(ref pair, c, walk);
            foreach (Ox o in world.People.Oxen) NotePair(ref pair, o, oxSpeed);

            foreach (Citizen c in world.People.Citizens) StepAgent(world, c, SpeedOf(pair, c, walk), navChanged);
            foreach (Ox o in world.People.Oxen) StepAgent(world, o, SpeedOf(pair, o, oxSpeed), navChanged);

            if (pair == null) return;
            foreach (Citizen c in world.People.Citizens) PlaceFollower(nav, c);
            foreach (Ox o in world.People.Oxen) PlaceFollower(nav, o);
        }

        /// <summary>Default distance a follower trails its leader, in metres (MOV-16).</summary>
        public const double DefaultFollowOffsetM = 1.5;

        /// <summary>
        /// Ties <paramref name="follower"/> to <paramref name="leader"/> (MOV-16). While attached the leader moves at
        /// min(own speed, follower speed) × the cell multiplier, and the follower is placed <paramref name="offsetM"/>
        /// behind the leader along the leader's facing (clamped to a walkable cell) with the same facing. Any move order
        /// on the follower is dropped.
        /// </summary>
        public static void Attach(IMovable follower, IMovable leader, double offsetM = DefaultFollowOffsetM)
        {
            if (follower == null) throw new ArgumentNullException(nameof(follower));
            if (leader == null) throw new ArgumentNullException(nameof(leader));
            if (ReferenceEquals(follower, leader)) throw new ArgumentException("An agent cannot follow itself.");
            if (offsetM < 0 || double.IsNaN(offsetM) || double.IsInfinity(offsetM))
                throw new ArgumentOutOfRangeException(nameof(offsetM));
            follower.Move = null;
            follower.Follow = new FollowLink(leader, offsetM);
        }

        /// <summary>Ends a follow tie; the follower stays where it is. Does nothing if not attached.</summary>
        public static void Detach(IMovable follower)
        {
            if (follower == null) throw new ArgumentNullException(nameof(follower));
            follower.Follow = null;
        }

        private static void NotePair(ref Dictionary<IMovable, double> pair, IMovable follower, double followerSpeed)
        {
            if (follower.Follow == null) return;
            if (pair == null) pair = new Dictionary<IMovable, double>();
            IMovable leader = follower.Follow.Leader;
            if (!pair.TryGetValue(leader, out double s) || followerSpeed < s) pair[leader] = followerSpeed;
        }

        private static double SpeedOf(Dictionary<IMovable, double> pair, IMovable a, double own)
        {
            return pair != null && pair.TryGetValue(a, out double s) ? Math.Min(own, s) : own;
        }

        private static void PlaceFollower(NavGrid nav, IMovable f)
        {
            FollowLink link = f.Follow;
            if (link == null) return;
            IMovable l = link.Leader;
            double sin = Math.Sin(l.FacingRad), cos = Math.Cos(l.FacingRad);
            double x = l.X, z = l.Z;
            for (int i = 0; i < 4; i++)
            {
                double d = link.OffsetM * (1.0 - i * 0.25); // 100%, 75%, 50%, 25%, else on the leader
                double px = l.X - d * sin, pz = l.Z - d * cos;
                if (!nav.IsBlocked(nav.CellX(px), nav.CellZ(pz))) { x = px; z = pz; break; }
            }
            f.X = x;
            f.Z = z;
            f.FacingRad = l.FacingRad;
            f.CurrentMultiplier = nav.SpeedMultiplier(nav.CellX(x), nav.CellZ(z));
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

            if (a.Follow != null) return; // placed behind its leader after everyone has moved

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
