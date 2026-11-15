using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
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

        /// <summary>Opens the household's fetch task: walk to the camp, collect, walk home, deposit. Returns null with no camp.</summary>
        public static TaskRecord OpenTrip(SimWorld world, Family f)
        {
            if (world.Camp == null) return null;
            var camp = new CampFoodMarket(world).Origin;
            TaskDef def = TaskDefCatalog.Build(FetchFoodTaskId, new[]
            {
                TaskStep.WalkTo(camp), TaskStep.CollectFood(),
                TaskStep.WalkTo(new PathPoint(f.HomeX, f.HomeZ)), TaskStep.DepositFood(),
            });
            return world.Tasks.Create(def, AllotmentSystem.OwnerFor(f.Id));
        }

        /// <summary>The family a fetch task serves, or null.</summary>
        public static Family FamilyOf(SimWorld world, TaskRecord t) => world.People.FindFamily(-1000 - t.OwnerOperationId);
    }
}
