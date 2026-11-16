using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Markets
{
    /// <summary>
    /// Operations-phase system that gives every store family one stall (T-459, MK-20..23). A store family is a family
    /// assigned to a built granary that supplies the market. Each such family without a stall takes an unstaffed stall
    /// first (MK-23, reuse), else a new stall is queued as an ordinary construction site on the free slot nearest the
    /// market centre (MK-21; <see cref="StallGrid.Slots"/> is ordered that way). No free slot: the family is listed in
    /// <see cref="Market.NoRoomFamilyIds"/> and nothing is built. Stalls of families that are unassigned, or whose granary
    /// stopped supplying, become unstaffed and keep their food (MK-23). Footprints removed elsewhere (deconstruction)
    /// leave the market's list here.
    /// </summary>
    public sealed class StallSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Market market = world.Markets.Current;
            if (market == null) return;
            market.ClearNoRoom();
            market.StallFootprintIds.RemoveAll(id => world.Footprints.Get(id) == null);

            var families = StoreFamilies(world);
            var wanted = new HashSet<int>(families);
            foreach (int stall in new List<int>(market.StallFootprintIds))
                if (market.FamilyOf(stall) != 0 && !wanted.Contains(market.FamilyOf(stall))) market.SetFamily(stall, 0); // MK-23
            foreach (int stall in new List<int>(market.StallFootprintIds)) // a family keeps only one stall
            {
                int f = market.FamilyOf(stall);
                if (f != 0 && market.StallOf(f) != stall) market.SetFamily(stall, 0);
            }

            List<StallSlot> slots = null;
            foreach (int familyId in families)
            {
                if (market.StallOf(familyId) != 0) continue;
                int unstaffed = FirstUnstaffed(market);
                if (unstaffed != 0) { market.SetFamily(unstaffed, familyId); continue; }
                if (slots == null)
                {
                    Areas.AreaRecord area = world.Areas.Get(market.AreaId);
                    slots = area == null ? new List<StallSlot>() : StallGrid.Slots(area.Polygon, world.Tuning);
                }
                StallSlot free = FirstFreeSlot(world, market, slots);
                if (free == null) { market.AddNoRoom(familyId); continue; }
                int stallId = BuildStall(world, free);
                if (stallId == 0) continue; // no logs yet: try again next tick
                market.StallFootprintIds.Add(stallId);
                market.SetFamily(stallId, familyId);
            }
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result)
        {
        }

        /// <summary>
        /// Takes down every stall of the market because its area is gone (MK-04): food on a stall (and any logs on its site)
        /// drops as a ground pile at the stall (CN-55), site reservations are released and the footprints are removed.
        /// </summary>
        public static void RemoveStalls(SimWorld world, Market market)
        {
            Ledger inv = world.Inventory;
            foreach (int id in new List<int>(market.StallFootprintIds))
            {
                Footprint fp = world.Footprints.Get(id);
                if (fp == null) continue;
                foreach (TaskRecord t in new List<TaskRecord>(world.Tasks.All))
                    if (!t.IsFinished && t.SiteFootprintId == id) world.Tasks.Cancel(t.Id);
                ConstructionSite site = world.Construction.Get(id);
                if (site != null)
                {
                    foreach (int resId in new List<int>(site.ReservationIds)) inv.Release(resId);
                    world.Construction.Remove(id);
                    Drop(inv, inv.Get(site.LedgerLocationId), fp);
                }
                world.Deconstruction.Remove(id);
                Drop(inv, inv.FindFoodStore(id), fp);
                world.Footprints.Remove(id);
                market.SetFamily(id, 0);
            }
            market.StallFootprintIds.Clear();
        }

        private static void Drop(Ledger inv, LedgerLocation from, Footprint fp)
        {
            if (from == null || from.Total <= 0) return;
            LedgerLocation pile = inv.GetOrAddGroundPile(fp.Centre, null);
            foreach (KeyValuePair<string, int> kv in new List<KeyValuePair<string, int>>(from.Counts))
            {
                int n = from.AvailableOf(kv.Key);
                if (n > 0) inv.Transfer(from, pile, Items.Find(kv.Key), n);
            }
        }

        // Families assigned to a built granary that supplies the market: operation id order, then assignment order.
        private static List<int> StoreFamilies(SimWorld world)
        {
            var result = new List<int>();
            foreach (Operation op in world.Operations.All)
            {
                if (op.TypeId != OperationTypes.GranaryId || !op.SuppliesMarket) continue;
                bool built = false;
                foreach (int m in op.ModuleIds) if (world.IsBuilt(m)) built = true;
                if (!built) continue;
                foreach (int fid in op.FamilyIds)
                    if (world.People.FindFamily(fid) != null && !result.Contains(fid)) result.Add(fid);
            }
            return result;
        }

        private static int FirstUnstaffed(Market market)
        {
            foreach (int id in market.StallFootprintIds) if (market.FamilyOf(id) == 0) return id;
            return 0;
        }

        private static StallSlot FirstFreeSlot(SimWorld world, Market market, List<StallSlot> slots)
        {
            foreach (StallSlot slot in slots)
            {
                bool taken = false;
                foreach (int id in market.StallFootprintIds)
                {
                    Footprint fp = world.Footprints.Get(id);
                    if (fp != null && fp.Centre.Equals(slot.Centre)) { taken = true; break; }
                }
                if (!taken) return slot;
            }
            return null;
        }

        // Queues a stall site on the slot like PlaceStructure does. Returns 0 when the logs cannot be reserved yet.
        private static int BuildStall(SimWorld world, StallSlot slot)
        {
            FootprintTypeDef type = ModuleTypes.MarketStall;
            int fpId = world.Footprints.NextId();
            var site = new ConstructionSite(fpId, world.Inventory.GetOrAddSite(fpId).Id);
            if (type.ConstructionRecipe.Logs > 0)
            {
                var ids = ConstructionSources.TryReserve(world, Items.Log, type.ConstructionRecipe.Logs, slot.Centre, fpId);
                if (ids == null) return 0;
                site.ReservationIds.AddRange(ids);
            }
            world.Construction.Add(site);
            // The stall's front (local -Z) faces the aisle, which lies on the slot's +v side (StallGrid).
            double ux = slot.Stall[1].X - slot.Stall[0].X, uz = slot.Stall[1].Z - slot.Stall[0].Z;
            double rotation = Math.Atan2(-uz, -ux) * 180.0 / Math.PI;
            Footprint fp = world.Footprints.Add(fpId, type, slot.Centre, rotation);
            world.Inventory.AddFoodStore(type.FoodCapacity, fp.Id); // MK-22
            return fpId;
        }
    }
}
