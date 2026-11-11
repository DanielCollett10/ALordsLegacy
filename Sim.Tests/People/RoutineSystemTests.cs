using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class RoutineSystemTests
    {
        private static PointMm P(long x, long z) => new PointMm(x, z);

        // 200 m flat map; camp at (100,100); default start 1 Mar 06:00 (before sunrise, ~06:42).
        private static SimWorld NewWorld(int seed = 12345, int startHour = 6)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { WorldSeed = seed, StartHour = startHour });
            var layout = new StartLayout(P(100000, 100000), 0, P(0, 0), P(6000, 0), P(-6000, 0), new PointMm[0],
                new[]
                {
                    new StartFamily("Ash", P(-8000, -4000), new[] { new StartCitizen("A", Sex.Male, -100), new StartCitizen("B", Sex.Female, -90) }),
                    new StartFamily("Birch", P(-8000, 4000), new[] { new StartCitizen("C", Sex.Female, -80) }),
                    new StartFamily("Cedar", P(-3000, 9000), new[] { new StartCitizen("D", Sex.Male, -70), new StartCitizen("E", Sex.Male, -60) }),
                }, 2);
            StartLayoutApplier.Apply(w, layout);
            return w;
        }

        private static void StepTo(SimWorld w, int hour, int minute)
        {
            int guard = 0;
            while (!(w.Now.Hour == hour && w.Now.Minute == minute) && guard++ < 3 * 1440) w.Step();
        }

        [Test]
        public void Default_Systems_RoutineRunsBeforeMovement()
        {
            var list = SimSystems.CreateDefault();
            int r = -1, m = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is RoutineSystem) r = i;
                if (list[i] is MovementSystem) m = i;
            }
            Assert.That(r, Is.GreaterThanOrEqualTo(0));
            Assert.That(m, Is.GreaterThan(r));
        }

        [Test]
        public void At0200_EveryoneIsHomeAndSleeping()
        {
            SimWorld w = NewWorld();
            StepTo(w, 2, 0);
            foreach (Citizen c in w.People.Citizens)
            {
                Family f = w.People.FindFamily(c.FamilyId);
                Assert.AreEqual(Activity.Sleeping, c.Activity, c.Name);
                Assert.AreEqual(f.HomeX, c.X, 0.01, c.Name);
                Assert.AreEqual(f.HomeZ, c.Z, 0.01, c.Name);
            }
        }

        [Test]
        public void At0200_AfterADayOfMilling_EveryoneIsHomeAndSleeping()
        {
            SimWorld w = NewWorld();
            w.Step(15 * 60); // 21:00 on day 1, after a day of milling
            StepTo(w, 2, 0);
            foreach (Citizen c in w.People.Citizens)
            {
                Family f = w.People.FindFamily(c.FamilyId);
                Assert.AreEqual(Activity.Sleeping, c.Activity, c.Name);
                Assert.AreEqual(f.HomeX, c.X, 0.01, c.Name);
                Assert.AreEqual(f.HomeZ, c.Z, 0.01, c.Name);
            }
        }

        [Test]
        public void AtNoon_NobodyIsSleeping()
        {
            SimWorld w = NewWorld();
            StepTo(w, 12, 0);
            foreach (Citizen c in w.People.Citizens)
                Assert.AreNotEqual(Activity.Sleeping, c.Activity, c.Name);
        }

        [Test]
        public void AtNoon_AfterAFullNight_NobodyIsSleeping()
        {
            SimWorld w = NewWorld();
            StepTo(w, 2, 0);
            StepTo(w, 12, 0);
            Assert.That(w.People.Citizens.All(c => c.Activity != Activity.Sleeping));
        }

        [Test]
        public void Sleepers_WakeStaggeredAfterSunrise_NotBefore()
        {
            SimWorld w = NewWorld();
            StepTo(w, 2, 0);
            var sun = new LordsLegacy.Sim.Calendar.Daylight(w.Tuning.MinDaylightHours, w.Tuning.MaxDaylightHours);
            long sunriseMin = (long)Math.Ceiling(sun.For(w.Now.DayOfYear).Sunrise * 60);
            var woke = new Dictionary<int, long>();
            for (int i = 0; i < 24 * 60 && woke.Count < w.People.CitizenCount; i++)
            {
                w.Step();
                long minuteOfDay = w.Now.Hour * 60 + w.Now.Minute;
                foreach (Citizen c in w.People.Citizens)
                    if (c.Activity != Activity.Sleeping && !woke.ContainsKey(c.Id)) woke[c.Id] = minuteOfDay;
            }
            Assert.AreEqual(w.People.CitizenCount, woke.Count);
            foreach (long m in woke.Values)
                Assert.That(m, Is.InRange(sunriseMin, sunriseMin + w.Tuning.WakeStaggerMaxMinutes));
            Assert.Greater(woke.Values.Distinct().Count(), 1, "wake times should be spread");
        }

        [Test]
        public void MillingPoints_AreWithinRadiusOfFire_AndWalkable()
        {
            SimWorld w = NewWorld();
            var targets = new List<(double x, double z)>();
            var seen = new HashSet<MoveOrder>();
            double fx = w.Camp.Fire.XMetres, fz = w.Camp.Fire.ZMetres;
            for (int i = 0; i < 3 * 1440; i++)
            {
                w.Step();
                foreach (Citizen c in w.People.Citizens)
                    if (c.Move != null && c.Activity == Activity.MillingAbout && seen.Add(c.Move))
                        targets.Add((c.Move.TargetX, c.Move.TargetZ));
            }
            Assert.Greater(targets.Count, 10);
            foreach (var t in targets)
            {
                double d = Math.Sqrt((t.x - fx) * (t.x - fx) + (t.z - fz) * (t.z - fz));
                Assert.LessOrEqual(d, w.Tuning.MillRadiusM + 1e-9);
                Assert.IsFalse(w.Nav.IsBlocked(w.Nav.CellX(t.x), w.Nav.CellZ(t.z)));
            }
        }

        [Test]
        public void Stays_AreBetweenMinAndMaxMinutes()
        {
            SimWorld w = NewWorld();
            Citizen c = w.People.FindCitizen(1);
            StepTo(w, 2, 0);
            long arrived = -1;
            var stays = new List<long>();
            bool wasMoving = false;
            for (int i = 0; i < 2 * 1440; i++)
            {
                w.Step();
                bool moving = c.Move != null && !c.Move.IsFinished;
                bool milling = c.Activity == Activity.MillingAbout;
                if (milling && wasMoving && !moving) arrived = w.Tick;
                if (milling && !wasMoving && moving && arrived >= 0) { stays.Add(w.Tick - arrived); arrived = -1; }
                if (!milling) arrived = -1;
                wasMoving = moving;
            }
            Assert.Greater(stays.Count, 3);
            foreach (long s in stays)
                Assert.That(s, Is.InRange(w.Tuning.MillStayMinMinutes, w.Tuning.MillStayMaxMinutes + 2));
        }

        [Test]
        public void SameSeed_GivesSameDay_AndDifferentSeed_Differs()
        {
            string Run(int seed)
            {
                SimWorld w = NewWorld(seed);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 1440; i++)
                {
                    w.Step();
                    foreach (Citizen c in w.People.Citizens)
                        sb.Append(c.X.ToString("R")).Append(',').Append(c.Z.ToString("R")).Append(',').Append((int)c.Activity).Append(';');
                }
                return sb.ToString();
            }
            Assert.AreEqual(Run(7), Run(7));
            Assert.AreNotEqual(Run(7), Run(8));
        }

        [Test]
        public void Oxen_StayAtStand_Waiting()
        {
            SimWorld w = NewWorld();
            w.Step(2 * 1440);
            foreach (Ox o in w.People.Oxen)
            {
                Assert.AreEqual(Activity.Waiting, o.Activity);
                Assert.AreEqual(o.HomeX, o.X, 1e-9);
                Assert.AreEqual(o.HomeZ, o.Z, 1e-9);
            }
        }

        [Test]
        public void ForeignMoveOrder_IsLeftAlone_UntilItFinishes()
        {
            SimWorld w = NewWorld();
            w.Step(8 * 60); // morning, everyone milling
            Citizen c = w.People.FindCitizen(1);
            var order = new MoveOrder(60, 60);
            c.Move = order;
            w.Step();
            Assert.AreSame(order, c.Move);
            for (int i = 0; i < 400 && !order.IsFinished; i++) w.Step();
            Assert.AreEqual(MoveStatus.Done, order.Status);
        }

        [Test]
        public void NoCamp_UsesFamilyHomeAsCentre_WithoutThrowing()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning());
            w.People.AddFamily(1, "X", 50, 50);
            w.People.AddCitizen(1, "Z", Sex.Male, 0, 1, 50, 50);
            Assert.DoesNotThrow(() => w.Step(1440));
        }
    }
}
