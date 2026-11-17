using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Growth
{
    /// <summary>
    /// Routine-phase system (GR-03, GR-11, GR-13, decisions 0030 and 0045), run right after eating. At midnight it
    /// begins the departure of every family with HungryDays at or over the tuning limit. Every tick, each member of a
    /// Leaving family is sent to <see cref="MapExits.Nearest"/> from where they stand, by day or night; a failed order
    /// is retried on the next hour. A member whose order is done is removed; the family and its empty camp larder go
    /// with the last member. The exit lives in the member's move order, so there is no state of its own to save.
    /// </summary>
    public sealed class DepartureSystem : ISimSystem
    {
        private const int RetryMinutes = 60;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            bool midnight = world.Tick > 0 && world.Now.Hour == 0 && world.Now.Minute == 0;
            var leaving = new List<Family>();
            foreach (Family f in world.People.Families)
            {
                if (midnight && f.Passage == FamilyPassage.None && f.MemberIds.Count > 0
                    && f.HungryDays >= world.Tuning.HungryDaysToLeave)
                    FamilyDeparture.Begin(world, f.Id);
                if (f.Passage == FamilyPassage.Leaving) leaving.Add(f);
            }
            foreach (Family f in leaving) Walk(world, f);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        private static void Walk(SimWorld world, Family f)
        {
            foreach (int id in new List<int>(f.MemberIds))
            {
                Citizen c = world.People.FindCitizen(id);
                if (c == null) continue;
                MoveOrder order = c.Move;
                bool toExit = order != null && MapExits.IsOnEdge(world, PointMm.FromMetres(order.TargetX, order.TargetZ));
                if (toExit && order.Status == MoveStatus.Done) { Remove(world, f, c); continue; }
                if (toExit && order.Status != MoveStatus.Failed) continue; // still walking or waiting to be planned
                if (toExit && world.Tick % RetryMinutes != 0) continue;    // failed: try again on the next hour
                PointMm? exit = MapExits.Nearest(world, PointMm.FromMetres(c.X, c.Z));
                if (!exit.HasValue) { c.Move = null; continue; }
                c.Move = new MoveOrder(exit.Value.XMetres, exit.Value.ZMetres);
                c.Activity = Activity.Walking;
            }
        }

        private static void Remove(SimWorld world, Family f, Citizen c)
        {
            world.People.RemoveCitizen(c.Id);
            if (f.MemberIds.Count > 0) return;
            int larder = f.LarderLocationId;
            world.People.RemoveFamily(f.Id);
            if (larder != 0) world.Inventory.RemoveEmptyLocation(larder);
        }
    }
}
