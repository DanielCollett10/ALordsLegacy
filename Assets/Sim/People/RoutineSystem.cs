using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Navigation;

namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// Routine-phase system (PPL-24, PPL-27, PPL-30..32). A small per-citizen state machine:
    /// <c>Asleep</c> (at home) → at sunrise plus a seeded 0–<c>WakeStaggerMaxMinutes</c> delay → <c>Milling</c> (walk to a
    /// seeded random walkable spot within <c>MillRadiusM</c> of the camp fire, stay <c>MillStayMinMinutes</c>..
    /// <c>MillStayMaxMinutes</c>, repeat) → at sunset <c>GoingHome</c> → <c>Asleep</c> on arrival. Oxen stay at the stand
    /// (Activity Waiting). Sunrise and sunset come from <see cref="Daylight"/> for the current date.
    /// <para>
    /// It only issues <see cref="MoveOrder"/>s; <see cref="Navigation.MovementSystem"/> (later in the system list)
    /// walks them. A citizen whose <see cref="IMovable.Move"/> was set by someone else is left alone until that order
    /// finishes, so later phases (work, debug commands) can take over. Add a new phase by extending the switch in
    /// <see cref="StepCitizen"/>. Holds per-world state, so each world needs its own instance.
    /// </para>
    /// </summary>
    public sealed class RoutineSystem : ISimSystem
    {
        private const int PickAttempts = 24;

        private enum Phase { Asleep, Milling, GoingHome }

        private sealed class State
        {
            public Phase Phase;
            public long WakeTick = -1;     // Asleep: tick to get up, or -1 when not yet drawn today
            public long StayUntilTick;     // Milling: tick to pick the next spot
            public MoveOrder Order;        // the order this system issued, or null
        }

        private readonly Dictionary<int, State> _states = new Dictionary<int, State>();
        private Daylight _daylight;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (_daylight == null)
                _daylight = new Daylight(world.Tuning.MinDaylightHours, world.Tuning.MaxDaylightHours);

            bool day = IsDay(world.Now);
            foreach (Citizen c in world.People.Citizens) StepCitizen(world, c, day);
            foreach (Ox o in world.People.Oxen)
            {
                if (o.Move == null || o.Move.IsFinished) o.Activity = Activity.Waiting;
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private bool IsDay(GameTime t)
        {
            SunTimes sun = _daylight.For(t.DayOfYear);
            int minute = t.Hour * 60 + t.Minute;
            return minute >= (int)Math.Ceiling(sun.Sunrise * 60.0) && minute < (int)Math.Ceiling(sun.Sunset * 60.0);
        }

        private void StepCitizen(SimWorld world, Citizen c, bool day)
        {
            long now = world.Tick;
            _states.TryGetValue(c.Id, out State st);
            if (st != null && c.Move != st.Order)
            {
                if (c.Move != null && !c.Move.IsFinished) return; // someone else is sending this citizen
                if (c.Move != null) { c.Move = null; st = null; } // foreign order done: start again
                else st.Order = null;                              // our order was cleared
            }
            if (st == null)
            {
                if (c.Move != null && !c.Move.IsFinished) return; // already being sent somewhere
                st = Init(world, c, day);
                _states[c.Id] = st;
            }

            switch (st.Phase)
            {
                case Phase.Asleep: StepAsleep(world, c, st, day, now); break;
                case Phase.Milling: StepMilling(world, c, st, day, now); break;
                case Phase.GoingHome: StepGoingHome(world, c, st); break;
            }
        }

        private static State Init(SimWorld world, Citizen c, bool day)
        {
            if (c.Move != null && c.Move.IsFinished) c.Move = null;
            var st = new State();
            if (c.Activity == Activity.Sleeping) st.Phase = Phase.Asleep;
            else if (day) { st.Phase = Phase.Milling; st.StayUntilTick = world.Tick; }
            else st.Phase = Phase.GoingHome;
            return st;
        }

        private void StepAsleep(SimWorld world, Citizen c, State st, bool day, long now)
        {
            c.Activity = Activity.Sleeping;
            if (!day) { st.WakeTick = -1; return; }
            if (st.WakeTick < 0) st.WakeTick = now + world.Random.NextInt(0, world.Tuning.WakeStaggerMaxMinutes + 1);
            if (now < st.WakeTick) return;
            st.Phase = Phase.Milling;
            st.WakeTick = -1;
            BeginMill(world, c, st, now);
        }

        private void StepMilling(SimWorld world, Citizen c, State st, bool day, long now)
        {
            if (!day) { GoHome(world, c, st); return; }
            if (st.Order != null)
            {
                if (!st.Order.IsFinished) return;
                c.Move = null;
                st.Order = null;
                c.Activity = Activity.MillingAbout;
                st.StayUntilTick = now + DrawStay(world);
                return;
            }
            if (now >= st.StayUntilTick) BeginMill(world, c, st, now);
        }

        private void StepGoingHome(SimWorld world, Citizen c, State st)
        {
            if (st.Order == null) { GoHome(world, c, st); return; } // order was cleared by someone else
            if (!st.Order.IsFinished) return;
            // Arrived (or no route: sleep where we stand rather than loop forever).
            c.Move = null;
            st.Order = null;
            FallAsleep(c, st);
        }

        private static void FallAsleep(Citizen c, State st)
        {
            st.Phase = Phase.Asleep;
            st.WakeTick = -1;
            c.Activity = Activity.Sleeping;
        }

        private static void GoHome(SimWorld world, Citizen c, State st)
        {
            Family f = world.People.FindFamily(c.FamilyId);
            double hx = f != null ? f.HomeX : c.X, hz = f != null ? f.HomeZ : c.Z;
            c.Move = null;
            st.Order = null;
            double dx = hx - c.X, dz = hz - c.Z;
            if (dx * dx + dz * dz < 1e-6)
            {
                FallAsleep(c, st);
                return;
            }
            st.Phase = Phase.GoingHome;
            st.Order = new MoveOrder(hx, hz);
            c.Move = st.Order;
            c.Activity = Activity.Walking;
        }

        private static long DrawStay(SimWorld world)
        {
            int lo = world.Tuning.MillStayMinMinutes, hi = Math.Max(lo, world.Tuning.MillStayMaxMinutes);
            return world.Random.NextInt(lo, hi + 1);
        }

        private static void BeginMill(SimWorld world, Citizen c, State st, long now)
        {
            c.Activity = Activity.MillingAbout;
            if (TryPickMillSpot(world, c, out double x, out double z))
            {
                st.Order = new MoveOrder(x, z);
                c.Move = st.Order;
            }
            else
            {
                st.StayUntilTick = now + DrawStay(world);
            }
        }

        /// <summary>
        /// Draws a point uniformly in the disc of <c>MillRadiusM</c> around the camp fire (the family home when the
        /// world has no camp), retrying a few times until the point is on walkable ground. False when none was found.
        /// </summary>
        private static bool TryPickMillSpot(SimWorld world, Citizen c, out double x, out double z)
        {
            double cx, cz;
            if (world.Camp != null) { cx = world.Camp.Fire.XMetres; cz = world.Camp.Fire.ZMetres; }
            else
            {
                Family f = world.People.FindFamily(c.FamilyId);
                cx = f != null ? f.HomeX : c.X; cz = f != null ? f.HomeZ : c.Z;
            }
            double radius = world.Tuning.MillRadiusM;
            NavGrid nav = world.Nav;
            for (int i = 0; i < PickAttempts; i++)
            {
                double r = radius * Math.Sqrt(world.Random.NextDouble());
                double a = world.Random.NextDouble() * 2.0 * Math.PI;
                x = cx + r * Math.Cos(a);
                z = cz + r * Math.Sin(a);
                if (!nav.IsBlocked(nav.CellX(x), nav.CellZ(z))) return true;
            }
            x = 0; z = 0;
            return false;
        }
    }
}
