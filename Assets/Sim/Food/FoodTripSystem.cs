using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Food
{
    /// <summary>
    /// Routine-phase system for the Sunday food trip (FD-14, FOOD-A7). After the allotment reserved each household's food,
    /// one <c>task.fetch_food</c> per household with an allotment is opened. Opening times are spread over the morning:
    /// household number r of n (by family id) opens at dawn + r × <see cref="MorningMinutes"/> ÷ n minutes. The dispatcher
    /// gives the task to an adult of that household; the step runner walks to the camp food store, moves the reserved
    /// food into the family's carrier (a ledger transfer), walks home and moves it into the larder.
    /// </summary>
    public sealed class FoodTripSystem : ISimSystem
    {
        /// <summary>Task def id of the Sunday trip.</summary>
        public const string FetchFoodTaskId = "task.fetch_food";

        /// <summary>Length of the morning the trip starts are spread over, in game minutes.</summary>
        public const int MorningMinutes = 240;

        private Daylight _daylight;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.Now.IsSunday) return;
            if (_daylight == null) _daylight = new Daylight(world.Tuning.MinDaylightHours, world.Tuning.MaxDaylightHours);
            int dawn = (int)Math.Ceiling(_daylight.For(world.Now.DayOfYear).Sunrise * 60.0);
            int since = world.Now.Hour * 60 + world.Now.Minute - dawn;
            if (since < 0 || since >= MorningMinutes) return;
            // Rank among every household that eats, so a household's start time does not move when others finish their trip.
            var eaters = new List<Family>();
            foreach (Family f in world.People.Families)
                if (EatingSystem.DailyNeed(world, f) > 0) eaters.Add(f);
            for (int r = 0; r < eaters.Count; r++)
            {
                Family f = eaters[r];
                if (r * MorningMinutes / eaters.Count == since && f.AllotmentReservationIds.Count > 0 && !HasTrip(world, f))
                    OpenTrip(world, f);
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        /// <summary>True when the family has an unfinished fetch task.</summary>
        public static bool HasTrip(SimWorld world, Family f)
        {
            foreach (TaskRecord t in world.Tasks.ByOwner(AllotmentSystem.OwnerFor(f.Id)))
                if (!t.IsFinished && t.Def.Id == FetchFoodTaskId) return true;
            return false;
        }

        /// <summary>
        /// Opens the household's fetch task. Camp-supplied households walk to the camp, collect, walk home, deposit (M14).
        /// Stall-supplied households walk to the nearest reserved stall, collect there, go on to the next nearest, then
        /// walk home (MK-33). With no camp there is no camp supply: only stall stops count, and the result is null when
        /// the family has no reserved stop at all.
        /// </summary>
        public static TaskRecord OpenTrip(SimWorld world, Family f)
        {
            var home = new PathPoint(f.HomeX, f.HomeZ);
            var camp = new CampFoodMarket(world).Origin;
            var stops = new List<LedgerLocation>();
            bool allCamp = true;
            foreach (int rid in f.AllotmentReservationIds)
            {
                Reservation r = world.Inventory.GetReservation(rid);
                LedgerLocation l = r == null ? null : world.Inventory.Get(r.LocationId);
                if (l == null || stops.Contains(l)) continue;
                stops.Add(l);
                if (world.Camp == null || l != world.Camp.FoodStoreLocation) allCamp = false;
            }
            if (world.Camp == null && stops.Count == 0) return null;            var steps = new List<TaskStep>();
            if (allCamp)
            {
                steps.Add(TaskStep.WalkTo(camp));
                steps.Add(TaskStep.CollectFood());
            }
            else
            {
                PathPoint at = home;
                while (stops.Count > 0)
                {
                    int best = 0;
                    double bestD = double.MaxValue;
                    for (int i = 0; i < stops.Count; i++)
                    {
                        PathPoint p = StopPoint(world, stops[i], camp);
                        double d = (p.X - at.X) * (p.X - at.X) + (p.Z - at.Z) * (p.Z - at.Z);
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    LedgerLocation s = stops[best];
                    stops.RemoveAt(best);
                    at = StopPoint(world, s, camp);
                    steps.Add(TaskStep.WalkTo(at));
                    steps.Add(TaskStep.CollectFood(s.Id));
                }
            }
            steps.Add(TaskStep.WalkTo(home));
            steps.Add(TaskStep.DepositFood());
            TaskDef def = TaskDefCatalog.Build(FetchFoodTaskId, steps.ToArray());
            return world.Tasks.Create(def, AllotmentSystem.OwnerFor(f.Id));
        }

        // The camp store is reached at the fire; a stall store at its footprint (the location's own position is unset).
        private static PathPoint StopPoint(SimWorld world, LedgerLocation l, PathPoint camp)
        {
            if (world.Camp != null && l == world.Camp.FoodStoreLocation) return camp;
            Footprint fp = l.ModuleId == 0 ? null : world.Footprints.Get(l.ModuleId);
            PointMm p = fp != null ? fp.Centre : l.Position;
            return new PathPoint(p.XMetres, p.ZMetres);
        }

        /// <summary>The family a fetch task serves, or null.</summary>
        public static Family FamilyOf(SimWorld world, TaskRecord t) => world.People.FindFamily(-1000 - t.OwnerOperationId);
    }
}
