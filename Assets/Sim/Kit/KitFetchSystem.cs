using System;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Kit
{
    /// <summary>
    /// Fills kit stores by hand (decision 0052, ST-21, ST-22, ST-24). A forager hut or granary with free basket slots opens
    /// a claim (a full one closes it). Claims are served oldest first: when a basket is available in the nearest camp
    /// General store, working storage yard or weaver store, the building's own family gets one
    /// <see cref="HandHaulTaskDefs.HaulId"/> trip (one open trip per working-age family). Cheap checks only, no random.
    /// </summary>
    public sealed class KitFetchSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            SyncClaims(world);
            if (world.KitClaims.Count == 0) return;
            foreach (KitClaim claim in world.KitClaims.Ordered()) Serve(world, claim.BuildingId);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        // A building with free slots claims; a full or vanished one closes its claim.
        private static void SyncClaims(SimWorld world)
        {
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                if (!KitStores.IsKitStore(world, l)) continue;
                if (l.Total < l.Capacity) world.KitClaims.Claim(l.ModuleId, world.Tick);
                else world.KitClaims.Release(l.ModuleId);
            }
            foreach (KitClaim c in world.KitClaims.Ordered())
                if (KitStores.Find(world, c.BuildingId) == null) world.KitClaims.Release(c.BuildingId);
        }

        private static void Serve(SimWorld world, int buildingId)
        {
            Footprint fp = world.Footprints.Get(buildingId);
            LedgerLocation store = KitStores.Find(world, buildingId);
            if (fp == null || store == null || !world.IsWorking(fp.Id)) return;
            Operation op = OwnerOf(world, buildingId);
            if (op == null) return;
            int open = 0;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.Def.Id == HandHaulTaskDefs.HaulId && !t.IsFinished) open++;
            int families = WorkingFamilies(world, op);
            ItemDef basket = Items.Basket;
            while (open < families)
            {
                int free = store.FreeSpaceFor(basket.Id);
                if (free <= 0) return;
                LedgerLocation src = NearestSource(world, fp.AccessPoint);
                if (src == null) return;
                int load = HandLoads.For(basket, world.Tuning) ?? 1;
                int n = Math.Min(Math.Min(Math.Max(1, load), src.AvailableOf(basket.Id)), free);
                if (n <= 0) return;
                TaskRecord task = world.Tasks.Create(HandHaulTaskDefs.Build(world, src, store, basket, n), op.Id);
                int owner = HandHaulTaskDefs.OwnerFor(task.Id);
                if (world.Inventory.ReserveStock(src, basket, n, owner) == 0 ||
                    world.Inventory.ReserveCapacity(store, basket, n, owner) == 0)
                {
                    world.Inventory.CancelOwner(owner);
                    world.Tasks.Cancel(task.Id);
                    return;
                }
                open++;
            }
        }

        /// <summary>Nearest General store with an unreserved basket: the camp store, a working yard's or a working weaver's store.</summary>
        internal static LedgerLocation NearestSource(SimWorld world, PointMm from)
        {
            LedgerLocation best = null;
            double bestD = double.MaxValue;
            LedgerLocation camp = world.Camp?.GeneralStoreLocation;
            if (camp != null && camp.AvailableOf(Items.Basket.Id) > 0) { best = camp; bestD = camp.Position.DistanceTo(from); }
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                if (l.Kind != LocationKind.GoodsStore || l.ModuleId == 0 || l.AvailableOf(Items.Basket.Id) < 1) continue;
                Footprint f = world.Footprints.Get(l.ModuleId);
                if (f == null || !world.IsWorking(f.Id)) continue;
                if (f.Type.Id != ModuleTypes.StorageYardId && f.Type.Id != ModuleTypes.WeaverId) continue;
                double d = f.AccessPoint.DistanceTo(from);
                if (d < bestD || (d == bestD && best != null && l.Id < best.Id)) { best = l; bestD = d; }
            }
            return best;
        }

        private static Operation OwnerOf(SimWorld world, int buildingId)
        {
            foreach (Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds) if (id == buildingId) return op;
            return null;
        }

        // Families of the operation with a member old enough to carry (same age rule as other hand trips).
        private static int WorkingFamilies(SimWorld world, Operation op)
        {
            int n = 0;
            foreach (int fid in op.FamilyIds)
            {
                Family fam = world.People.FindFamily(fid);
                if (fam == null || fam.IsInPassage) continue;
                foreach (int cid in fam.MemberIds)
                {
                    Citizen c = world.People.FindCitizen(cid);
                    if (c != null && c.AgeYears(world.Tick, world.Tuning.AgeingRate) >= world.Tuning.ForagerMinAge) { n++; break; }
                }
            }
            return n;
        }
    }
}
