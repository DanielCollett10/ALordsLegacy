using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Arrivals;
using LordsLegacy.Sim.Calendar;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Growth
{
    /// <summary>
    /// Routine-phase system, right after <see cref="DepartureSystem"/> (GR-22b, GR-24, GR-26, decision 0045). At dawn it
    /// runs <see cref="ArrivalRules.Check"/>; when allowed it draws a family, reserves the house, spawns the members one
    /// metre apart at the entry point with Passage Arriving, produces a month of food into the family larder and sends
    /// everyone to the house access point. When the first member arrives the family moves in. If the house goes away on
    /// the way, the family becomes Homeless with Passage None (the camp, or rough sleeping once the camp is gone).
    /// All state it uses (Passage, reservation, move orders, larders) is already saved, so it has none of its own.
    /// </summary>
    public sealed class ArrivalSystem : ISimSystem
    {
        private const int RetryMinutes = 60;
        private Daylight _daylight;
        private ArrivalCatalog _catalog;

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var arriving = new List<Family>();
            foreach (Family f in world.People.Families)
                if (f.Passage == FamilyPassage.Arriving) arriving.Add(f);
            foreach (Family f in arriving) Walk(world, f);
            if (world.Tick > 0 && IsDawn(world)) TryArrive(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        private bool IsDawn(SimWorld world)
        {
            if (_daylight == null) _daylight = new Daylight(world.Tuning.MinDaylightHours, world.Tuning.MaxDaylightHours);
            int dawn = (int)Math.Ceiling(_daylight.For(world.Now.DayOfYear).Sunrise * 60.0);
            return world.Now.Hour * 60 + world.Now.Minute == dawn;
        }

        private void TryArrive(SimWorld world)
        {
            if (!MayTry(world)) return; // cheap pre-check: keeps world.Random untouched on days nothing can arrive
            if (_catalog == null) _catalog = new ArrivalCatalog();
            var used = new List<string>();
            foreach (Family f in world.People.Families) used.Add(f.Surname);
            FamilyPlan plan = FamilyGenerator.Make(_catalog, world.Random, used);
            ArrivalCheck check = ArrivalRules.Check(world, plan);
            if (!check.Allowed) return;
            HomeRecord home = world.Homes.Get(check.HouseId);
            Footprint house = world.Footprints.Get(check.HouseId);
            PointMm entry = world.EntryPoint.Value;

            Family fam = world.People.AddFamily(world.People.NextFamilyId, plan.Surname, entry.XMetres, entry.ZMetres);
            fam.Passage = FamilyPassage.Arriving;
            world.Homes.Reserve(home, fam.Id);
            world.LastArrivalTick = world.Tick;

            Inward(world, entry, out double dx, out double dz);
            for (int i = 0; i < plan.Members.Count; i++)
            {
                FamilyMemberPlan m = plan.Members[i];
                Citizen c = world.People.AddCitizen(world.People.NextCitizenId, m.Name, m.Sex,
                    m.BirthTick(world.Tick, world.Tuning.AgeingRate), fam.Id,
                    entry.XMetres + dx * i, entry.ZMetres + dz * i, 0, Activity.Walking);
                c.Move = new MoveOrder(house.AccessPoint.XMetres, house.AccessPoint.ZMetres);
            }

            LedgerLocation larder = Larders.ForFamily(world, fam);
            double perKind = world.Tuning.ArrivalFoodDays * ArrivalRules.PlanNeed(world, plan) / 3.0;
            foreach (ItemDef item in new[] { Items.Meat, Items.Berries, Items.Mushrooms })
                world.Inventory.Produce(larder, item, (int)Math.Ceiling(perKind / item.RationsPerItem));

            world.SettlementLog.Add(world.Tick, "The " + fam.Surname + " family has arrived");
            world.Log.Write(LogCategory.Growth, "arrival: family " + fam.Id + " " + fam.Surname + ", " + plan.Members.Count + " members, house " + check.HouseId);
        }

        // The plan-independent parts of ArrivalRules.Check: entry point, a free working house, hunger window, gap.
        private static bool MayTry(SimWorld world)
        {
            if (!world.EntryPoint.HasValue) return false;
            SimTuning t = world.Tuning;
            if (world.LastHungryTick.HasValue && world.Tick - world.LastHungryTick.Value <= t.ArrivalNoHungerDays * 1440L) return false;
            if (world.LastArrivalTick.HasValue && world.Tick - world.LastArrivalTick.Value <= t.ArrivalGapDays * 1440L) return false;
            foreach (HomeRecord h in world.Homes.All)
            {
                if (h.FamilyId != 0 || h.ReservedForFamilyId != 0) continue;
                if (world.Footprints.Get(h.HouseFootprintId) != null && world.IsWorking(h.HouseFootprintId)) return true;
            }
            return false;
        }

        private static void Inward(SimWorld world, PointMm p, out double dx, out double dz)
        {
            var b = world.Terrain.Bounds;
            double vx = (b.MinX + b.MaxX) / 2.0 - p.XMetres, vz = (b.MinZ + b.MaxZ) / 2.0 - p.ZMetres;
            double len = Math.Sqrt(vx * vx + vz * vz);
            if (len < 1e-6) { dx = 1; dz = 0; return; }
            dx = vx / len; dz = vz / len;
        }

        private static void Walk(SimWorld world, Family f)
        {
            if (f.MemberIds.Count == 0) { f.Passage = FamilyPassage.None; return; }
            HomeRecord home = FindReserved(world, f.Id);
            // No reservation and nobody walking: nothing was ever sent, so there is nothing to undo (a bare Arriving
            // flag, e.g. a test fixture). A removed house leaves the members' walk orders behind.
            if (home == null && !HasOrder(world, f)) return;
            Footprint house = home == null ? null : world.Footprints.Get(home.HouseFootprintId);
            if (house == null || !world.IsWorking(house.Id))
            {
                if (home != null) world.Homes.ReleaseReservation(home);
                BecomeHomeless(world, f);
                return;
            }
            bool retry = world.Tick % RetryMinutes == 0;
            foreach (int id in new List<int>(f.MemberIds))
            {
                Citizen c = world.People.FindCitizen(id);
                if (c == null) continue;
                MoveOrder o = c.Move;
                if (o != null && o.Status == MoveStatus.Done) { MoveIn(world, f, home, house); return; }
                if ((o == null || o.Status == MoveStatus.Failed) && retry)
                {
                    c.Move = new MoveOrder(house.AccessPoint.XMetres, house.AccessPoint.ZMetres);
                    c.Activity = Activity.Walking;
                }
            }
        }

        private static void MoveIn(SimWorld world, Family f, HomeRecord home, Footprint house)
        {
            f.Passage = FamilyPassage.None;
            if (!world.Homes.MoveIn(home, f)) { world.Homes.ReleaseReservation(home); BecomeHomeless(world, f); return; }
            Larders.OnMoveIn(world, home, f);
            f.HomeX = house.AccessPoint.XMetres;
            f.HomeZ = house.AccessPoint.ZMetres;
        }

        private static void BecomeHomeless(SimWorld world, Family f)
        {
            f.Passage = FamilyPassage.None;
            PointMm? spot = RoughSleep.HomelessPoint(world);
            if (spot.HasValue) { f.HomeX = spot.Value.XMetres; f.HomeZ = spot.Value.ZMetres; }
            foreach (int id in f.MemberIds)
            {
                Citizen c = world.People.FindCitizen(id);
                if (c == null) continue;
                c.Move = null;
                c.Activity = Activity.MillingAbout;
            }
        }

        private static bool HasOrder(SimWorld world, Family f)
        {
            foreach (int id in f.MemberIds)
            {
                Citizen c = world.People.FindCitizen(id);
                if (c != null && c.Move != null) return true;
            }
            return false;
        }

        private static HomeRecord FindReserved(SimWorld world, int familyId)
        {
            foreach (HomeRecord h in world.Homes.All) if (h.ReservedForFamilyId == familyId) return h;
            return null;
        }
    }
}
