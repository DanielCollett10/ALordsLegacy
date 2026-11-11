using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// The working day of workers in an assigned family (PPL-21, PPL-24..26). A worker is on shift from a departure
    /// tick until the end of work. The end is sunset, or <c>SundayMealMinutes</c> earlier on a Sunday. The departure is
    /// chosen so the worker reaches the task's first target at sunrise (commute in the dark, PPL-25), but never so early
    /// that sleep at home falls below <c>MinSleepHours</c> (PPL-26); then the worker leaves later and starts after
    /// sunrise. The commute time is the route's travel time from where the worker sleeps. A plan is made once per
    /// "morning" per citizen and cached. Workers of other families are always on shift (M7 routine and helper work).
    /// Per-world state, owned by <see cref="SimWorld.WorkDay"/>.
    /// </summary>
    public sealed class WorkDay
    {
        private const int TicksPerDay = 1440;

        private readonly Dictionary<int, Plan> _plans = new Dictionary<int, Plan>();
        private Daylight _daylight;

        private struct Plan
        {
            public long KeyDayStart;
            public long Depart;
        }

        /// <summary>First and last (exclusive) tick of work on one calendar day.</summary>
        public readonly struct Window
        {
            /// <summary>Tick of sunrise.</summary>
            public long Sunrise { get; }
            /// <summary>Tick work stops (sunset, minus the Sunday meal).</summary>
            public long End { get; }

            /// <summary>Creates a window.</summary>
            public Window(long sunrise, long end) { Sunrise = sunrise; End = end; }
        }

        /// <summary>True when the citizen belongs to a family with a workplace and is old enough to work.</summary>
        public static bool AppliesTo(SimWorld world, Citizen c)
        {
            Family f = world.People.FindFamily(c.FamilyId);
            return f != null && f.WorkplaceId.HasValue &&
                   c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= OperationStatus.WorkingAgeYears;
        }

        /// <summary>Sunrise and end-of-work ticks for the day starting at <paramref name="dayStartTick"/> (midnight).</summary>
        public Window WindowOf(SimWorld world, long dayStartTick)
        {
            if (_daylight == null)
                _daylight = new Daylight(world.Tuning.MinDaylightHours, world.Tuning.MaxDaylightHours);
            // Offset from today by whole days (the calendar rejects ticks before the start, so do not ask it).
            long offsetDays = (dayStartTick - DayStart(world)) / TicksPerDay;
            GameTime now = world.Now;
            int doy = (int)(((now.DayOfYear - 1 + offsetDays) % 365 + 365) % 365) + 1;
            int weekday = (int)(((int)now.Weekday + offsetDays) % 7 + 7) % 7;
            SunTimes sun = _daylight.For(doy);
            long end = dayStartTick + (long)Math.Ceiling(sun.Sunset * 60.0);
            if (weekday == (int)Weekday.Sunday) end -= Math.Max(0, world.Tuning.SundayMealMinutes);
            return new Window(dayStartTick + (long)Math.Ceiling(sun.Sunrise * 60.0), end);
        }

        /// <summary>Tick of midnight that starts the current calendar day.</summary>
        public static long DayStart(SimWorld world)
        {
            GameTime n = world.Now;
            return world.Tick - (n.Hour * 60 + n.Minute);
        }

        /// <summary>True from sunrise to the end of work on the current day.</summary>
        public bool IsWorkingDaylight(SimWorld world)
        {
            Window w = WindowOf(world, DayStart(world));
            return world.Tick >= w.Sunrise && world.Tick < w.End;
        }

        /// <summary>
        /// True when the citizen should be working (or walking to work) now. Always true for citizens the work day does
        /// not apply to.
        /// </summary>
        public bool OnShift(SimWorld world, Citizen c)
        {
            if (!AppliesTo(world, c)) return true;
            long day = DayStart(world);
            Window today = WindowOf(world, day);
            long key = world.Tick >= today.End ? day + TicksPerDay : day;
            Window w = key == day ? today : WindowOf(world, key);
            return world.Tick >= PlanFor(world, c, key, w).Depart && world.Tick < w.End;
        }

        /// <summary>The departure tick planned for the citizen's next (or current) shift.</summary>
        public long DepartTick(SimWorld world, Citizen c)
        {
            long day = DayStart(world);
            Window today = WindowOf(world, day);
            long key = world.Tick >= today.End ? day + TicksPerDay : day;
            return PlanFor(world, c, key, WindowOf(world, key)).Depart;
        }

        private Plan PlanFor(SimWorld world, Citizen c, long key, Window w)
        {
            if (_plans.TryGetValue(c.Id, out Plan p) && p.KeyDayStart == key) return p;
            long commute = (long)Math.Ceiling(CommuteMinutes(world, c));
            Window prev = WindowOf(world, key - TicksPerDay);
            long ideal = w.Sunrise - commute;
            long minDepart = prev.End + commute + (long)Math.Round(world.Tuning.MinSleepHours * 60.0);
            long depart = Math.Max(ideal, minDepart);
            // Already up and about (e.g. milling when the task came in): no reason to wait.
            if (c.Activity != Activity.Sleeping) depart = Math.Min(depart, Math.Max(world.Tick, w.Sunrise));
            p = new Plan { KeyDayStart = key, Depart = depart };
            _plans[c.Id] = p;
            return p;
        }

        private static double CommuteMinutes(SimWorld world, Citizen c)
        {
            TaskRecord task = null;
            foreach (TaskRecord t in world.Tasks.ByAssignee(c.Id)) { task = t; break; }
            if (task == null || !task.Target.HasValue || !world.Trees.TryGet(task.Target.Value, out var tree)) return 0;
            double walk = Math.Max(1e-9, world.Tuning.WalkSpeedMPerGameMinute);
            PathResult r = GridPathfinder.Find(world.Nav, new PathPoint(c.X, c.Z), new PathPoint(tree.X, tree.Z));
            if (r.Ok) return r.TravelMinutes / walk;
            double dx = tree.X - c.X, dz = tree.Z - c.Z;
            return Math.Sqrt(dx * dx + dz * dz) / walk;
        }
    }
}
