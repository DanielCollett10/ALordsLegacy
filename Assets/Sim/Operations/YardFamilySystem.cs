using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Operations-phase planner for storage yard families (ST-43, ST-44; modelled on StoreFamilySystem, decision 0044).
    /// For each yard with a working module, every idle member (age <c>ForagerMinAge</c> or more) of an assigned family not
    /// in passage and not already promised a trip gets one open <c>task.haul_by_hand</c>, planned by asking the
    /// <see cref="YardJobs"/> sources in order. The system holds no job rules. A trip carries at most the item's hand load,
    /// and stock at the source and room at the destination are reserved before the carrier leaves, so a full destination
    /// means no trip. A sweep frees the reservations of ended trips and drops what they carried as an unowned pile.
    /// Nothing is saved besides the tasks and the ledger.
    /// </summary>
    public sealed class YardFamilySystem : ISimSystem
    {
        private readonly YardJobs _jobs;

        /// <summary>Creates the planner over an ordered source list (null or empty: it does nothing).</summary>
        public YardFamilySystem(YardJobs jobs = null)
        {
            _jobs = jobs ?? new YardJobs();
        }

        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Sweep(world);
            if (_jobs.Sources.Count == 0) return;
            foreach (Operation op in new List<Operation>(world.Operations.All))
                if (op.TypeId == OperationTypes.StorageYardOpId) Plan(world, op);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        private void Plan(SimWorld world, Operation op)
        {
            bool built = false;
            foreach (int m in op.ModuleIds) if (world.IsWorking(m)) built = true;
            if (!built) return;
            int wanted = -OpenTrips(world, op);
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam != null && !fam.IsInPassage) wanted += CountIdleWorkers(world, fam);
            }
            for (int i = 0; i < wanted; i++)
                if (!PlanOne(world, op)) break;
        }

        // Asks the sources in order; the first usable trip becomes a task. False when no source has one.
        private bool PlanOne(SimWorld world, Operation op)
        {
            foreach (IYardJobSource source in _jobs.Sources)
            {
                YardTrip trip = source.NextTrip(world, op);
                if (trip == null || trip.Source == null || trip.Destination == null || trip.Item == null) continue;
                int? load = HandLoads.For(trip.Item, world.Tuning);
                if (!load.HasValue) continue;
                int n = Math.Min(Math.Min(trip.Count, Math.Max(1, load.Value)),
                    Math.Min(trip.Source.AvailableOf(trip.Item.Id), trip.Destination.FreeSpace));
                if (n <= 0) continue;
                TaskRecord task = world.Tasks.Create(HandHaulTaskDefs.Build(world, trip.Source, trip.Destination, trip.Item, n), op.Id);
                int owner = HandHaulTaskDefs.OwnerFor(task.Id);
                if (world.Inventory.ReserveStock(trip.Source, trip.Item, n, owner) == 0 ||
                    world.Inventory.ReserveCapacity(trip.Destination, trip.Item, n, owner) == 0)
                {
                    world.Inventory.CancelOwner(owner);
                    world.Tasks.Cancel(task.Id);
                    continue;
                }
                return true;
            }
            return false;
        }

        private static int OpenTrips(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.State == TaskState.Open && t.Def.Id == HandHaulTaskDefs.HaulId) n++;
            return n;
        }

        private static int CountIdleWorkers(SimWorld world, Family fam)
        {
            int n = 0;
            foreach (int cid in fam.MemberIds)
            {
                Citizen c = world.People.FindCitizen(cid);
                if (c == null || c.AgeYears(world.Tick, world.Tuning.AgeingRate) < world.Tuning.ForagerMinAge) continue;
                if (DispatchSystem.IsIdle(world, c)) n++;
            }
            return n;
        }

        // A haul that ended (done, or cancelled from outside) leaves nothing behind: reservations freed, load dropped.
        private static void Sweep(SimWorld world)
        {
            foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
            {
                if (!t.IsFinished || t.Def.Id != HandHaulTaskDefs.HaulId) continue;
                world.Inventory.CancelOwner(HandHaulTaskDefs.OwnerFor(t.Id));
                if (!t.CarrierId.HasValue) continue;
                LedgerLocation carrier = world.Inventory.Get(t.CarrierId.Value);
                if (carrier == null || carrier.Total == 0) continue;
                PointMm at = default;
                foreach (TaskStep s in t.Def.Steps)
                {
                    if (s.Kind != StepKind.DepositItems) continue;
                    LedgerLocation dest = world.Inventory.Get(s.LocationId);
                    if (dest != null) at = dest.Position;
                    break;
                }
                CarrierDrop.DropAll(world, carrier, at);
            }
        }
    }
}
