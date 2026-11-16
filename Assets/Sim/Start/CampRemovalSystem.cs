using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Start
{
    /// <summary>
    /// Operations-phase system (CMP-23, CMP-24, CN-11). Checked once a day at midnight: when the food handover has
    /// happened and no family is Homeless, the camp is removed. Its footprint goes, anything left in the camp stockpile
    /// or food store moves to an unowned ground pile at the stockpile spot (unowned log piles stay construction sources),
    /// and oxen resting at the camp stand move to the nearest oxen stand, or rest where they are. If a camp location
    /// still has reserved or incoming items the removal waits for the next day. <c>World.Camp</c> becomes null.
    /// </summary>
    public sealed class CampRemovalSystem : ISimSystem
    {
        /// <inheritdoc />
        public void Step(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (world.Now.Hour != 0 || world.Now.Minute != 0) return;
            TryRemove(world);
        }

        /// <inheritdoc />
        public void OnCommandApplied(SimWorld world, CommandResult result) { }

        /// <summary>True when the camp may go: handover done, a camp exists and no family is Homeless.</summary>
        public static bool IsDue(SimWorld world)
        {
            if (world.Camp == null || !world.FoodHandedOver) return false;
            foreach (Family f in world.People.Families)
                if (f.MemberIds.Count > 0 && f.IsHomeless) return false;
            return true;
        }

        /// <summary>Removes the camp now if it is due and nothing is promised from its stores. Returns true when removed.</summary>
        public static bool TryRemove(SimWorld world)
        {
            if (!IsDue(world)) return false;
            CampInfo camp = world.Camp;
            LedgerLocation stock = camp.StockpileLocation, food = camp.FoodStoreLocation;
            if (Promised(stock) || Promised(food)) return false;

            LedgerLocation pile = null;
            foreach (LedgerLocation from in new[] { stock, food })
            {
                if (from == null) continue;
                foreach (string id in new List<string>(from.Counts.Keys))
                {
                    int n = from.CountOf(id);
                    if (n <= 0) continue;
                    if (pile == null) pile = world.Inventory.GetOrAddGroundPile(camp.Stockpile, null);
                    world.Inventory.Transfer(from, pile, Items.Find(id), n);
                }
                world.Inventory.RemoveEmptyLocation(from.Id);
            }

            PointMm stand = camp.OxStand;
            world.Footprints.Remove(camp.FootprintId);
            world.RoughSleepSpot = camp.Fire;
            world.Camp = null;

            var oxPool = new OxPool(world);
            foreach (Ox ox in world.People.Oxen)
            {
                if (ox.State.Kind != OxStateKind.Resting || ox.State.StandId != OxState.NoStand) continue;
                if (ox.State.SpotXMm != stand.X || ox.State.SpotZMm != stand.Z) continue;
                ox.HomeX = ox.X; ox.HomeZ = ox.Z; // CMP-24: with no camp, an ox without a stand rests (and is homed) where it is; saved with the ox
                oxPool.Rest(ox);
            }
            return true;
        }

        private static bool Promised(LedgerLocation l) =>
            l != null && (l.ReservedCounts.Count != 0 || l.IncomingCounts.Count != 0);
    }
}
