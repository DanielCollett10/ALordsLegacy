using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Food;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Growth
{
    /// <summary>
    /// Starts a family leaving (GR-10..12, GR-14, decision 0045). <see cref="Begin"/> takes the family off its
    /// operation, fails every open task its members hold through the step runner's normal fail path, empties any
    /// remaining carrier on the ground, frees the house with its larder food left in it, releases allotment
    /// reservations, sets Passage to Leaving (HungryDays kept) and writes a settlement log line.
    /// </summary>
    public static class FamilyDeparture
    {
        /// <summary>Begins the departure of a family. Does nothing for an unknown or already Leaving family.</summary>
        public static void Begin(SimWorld world, int familyId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Family f = world.People.FindFamily(familyId);
            if (f == null || f.Passage == FamilyPassage.Leaving) return;

            StepRunnerSystem runner = world.FindSystem<StepRunnerSystem>();
            var carriers = new List<int>();
            foreach (int memberId in f.MemberIds)
                foreach (TaskRecord t in world.Tasks.ByAssignee(memberId))
                {
                    if (t.CarrierId.HasValue) carriers.Add(t.CarrierId.Value);
                    if (runner != null) runner.FailTask(world, t);
                    else world.Tasks.Cancel(t.Id);
                }

            // Tasks are failed first (their drops run); then the shared unassign finds nothing left to cancel.
            if (f.WorkplaceId.HasValue) UnassignFamily.Unassign(world, f, f.WorkplaceId.Value);

            world.Inventory.CancelOwner(AllotmentSystem.OwnerFor(f.Id));
            f.ClearAllotment();

            if (f.TripCarrierId != 0) carriers.Add(f.TripCarrierId);
            PointMm at = DropPoint(world, f);
            foreach (int id in carriers)
            {
                LedgerLocation carrier = world.Inventory.Get(id);
                if (carrier != null && carrier.Total > 0) CarrierDrop.DropAll(world, carrier, at);
            }

            if (f.HomeHouseId.HasValue) HouseRemoval.Evict(world, f.HomeHouseId.Value, true);

            f.Passage = FamilyPassage.Leaving;
            world.SettlementLog.Add(world.Tick, "The " + f.Surname + " family is leaving: hungry for " + f.HungryDays + " days");
            world.Log.Write(LogCategory.Growth, "departure: family " + f.Id + " " + f.Surname + " leaving, hungry " + f.HungryDays + " days");
        }

        private static PointMm DropPoint(SimWorld world, Family f)
        {
            foreach (int id in f.MemberIds)
            {
                Citizen c = world.People.FindCitizen(id);
                if (c != null) return PointMm.FromMetres(c.X, c.Z);
            }
            return PointMm.FromMetres(f.HomeX, f.HomeZ);
        }
    }
}
