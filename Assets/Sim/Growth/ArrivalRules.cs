using System;
using LordsLegacy.Sim.Arrivals;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Navigation;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Growth
{
    /// <summary>Outcome of <see cref="ArrivalRules.Check"/>: Allowed with a house id, or one reason.</summary>
    public sealed class ArrivalCheck
    {
        /// <summary>True if the family may arrive.</summary>
        public bool Allowed { get; }
        /// <summary>The house footprint id to reserve when Allowed, else 0.</summary>
        public int HouseId { get; }
        /// <summary>Why not, or null when Allowed.</summary>
        public string Reason { get; }

        private ArrivalCheck(bool allowed, int houseId, string reason) { Allowed = allowed; HouseId = houseId; Reason = reason; }

        internal static ArrivalCheck Yes(int houseId) => new ArrivalCheck(true, houseId, null);
        internal static ArrivalCheck No(string reason) => new ArrivalCheck(false, 0, reason);
    }

    /// <summary>Dawn arrival check (GR-20, GR-21, GR-22, GR-24, GR-24b, decision 0045). Pure: changes nothing.</summary>
    public static class ArrivalRules
    {
        /// <summary>Reason text: no free house exists.</summary>
        public const string NoEmptyHouse = "No empty house";
        /// <summary>Reason text: free houses exist but none can be walked to from the entry point.</summary>
        public const string NoHouseReachable = "No house can be reached";
        /// <summary>Reason text: village food would not cover the village plus the new family.</summary>
        public const string NotEnoughFood = "Not enough food";
        /// <summary>Reason text: a household went hungry within the no-hunger window.</summary>
        public const string HungryThisWeek = "Someone was hungry this week";
        /// <summary>Reason text: the last arrival was too recent.</summary>
        public const string TooSoon = "Too soon since the last family";

        private const long TicksPerDay = 1440;

        /// <summary>
        /// May the family in <paramref name="plan"/> arrive now? Checks in order: house, reach, food, hunger, gap. The
        /// food check uses village stock before the arrival and never counts the family's own month (GR-8).
        /// </summary>
        public static ArrivalCheck Check(SimWorld world, FamilyPlan plan)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            bool anyFree = false;
            int best = 0;
            double bestCost = double.MaxValue;
            bool homelessWaiting = HomelessWaiting(world);
            if (!homelessWaiting)
            {
                foreach (HomeRecord home in world.Homes.All)
                {
                    if (home.FamilyId != 0 || home.ReservedForFamilyId != 0) continue;
                    Footprint house = world.Footprints.Get(home.HouseFootprintId);
                    if (house == null || !world.IsWorking(house.Id)) continue;
                    anyFree = true;
                    if (!world.EntryPoint.HasValue) continue;
                    PathResult r = GridPathfinder.Find(world.Nav,
                        new PathPoint(world.EntryPoint.Value.XMetres, world.EntryPoint.Value.ZMetres),
                        new PathPoint(house.AccessPoint.XMetres, house.AccessPoint.ZMetres));
                    if (!r.Ok || r.TravelMinutes >= bestCost) continue; // ascending id: the lowest id wins a tie
                    bestCost = r.TravelMinutes;
                    best = home.HouseFootprintId;
                }
            }
            if (!anyFree) return ArrivalCheck.No(NoEmptyHouse);
            if (best == 0) return ArrivalCheck.No(NoHouseReachable);

            SimTuning t = world.Tuning;
            double need = FoodStats.DailyNeed(world) + PlanNeed(world, plan);
            if (FoodStats.TotalRations(world) < t.ArrivalFoodDays * need) return ArrivalCheck.No(NotEnoughFood);
            if (world.LastHungryTick.HasValue && world.Tick - world.LastHungryTick.Value <= t.ArrivalNoHungerDays * TicksPerDay)
                return ArrivalCheck.No(HungryThisWeek);
            if (world.LastArrivalTick.HasValue && world.Tick - world.LastArrivalTick.Value <= t.ArrivalGapDays * TicksPerDay)
                return ArrivalCheck.No(TooSoon);
            return ArrivalCheck.Yes(best);
        }

        /// <summary>Daily rations the planned family will eat: adults (14+) AdultRations, children ChildRations. Pure.</summary>
        public static double PlanNeed(SimWorld world, FamilyPlan plan)
        {
            double need = 0;
            foreach (FamilyMemberPlan m in plan.Members)
                need += m.Age >= OperationStatus.WorkingAgeYears ? world.Tuning.AdultRations : world.Tuning.ChildRations;
            return need;
        }

        private static bool HomelessWaiting(SimWorld world)
        {
            foreach (Family f in world.People.Families)
                if (f.IsHomeless && f.MemberIds.Count > 0 && !f.IsInPassage) return true;
            return false;
        }
    }
}
