using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Construction
{
    /// <summary>
    /// Read side and build rules of a construction site (T-347, CN-23/23b). <see cref="Of"/> is what the UI reads;
    /// the other members are used by the step runner and <c>ConstructionSystem</c>.
    /// </summary>
    public sealed class ConstructionProgress
    {
        /// <summary>Delivering or Building.</summary>
        public ConstructionState State { get; }
        /// <summary>Logs that have reached the site so far, counting those already used up by the build work.</summary>
        public int LogsDelivered { get; }
        /// <summary>Logs the recipe needs.</summary>
        public int LogsNeeded { get; }
        /// <summary>Build progress as a fraction of the recipe's worker-minutes, 0..1.</summary>
        public double Fraction { get; }

        private ConstructionProgress(ConstructionState state, int delivered, int needed, double fraction)
        {
            State = state; LogsDelivered = delivered; LogsNeeded = needed; Fraction = fraction;
        }

        /// <summary>Progress of a footprint's site, or null when it is Built (or unknown).</summary>
        public static ConstructionProgress Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            ConstructionSite site = world.Construction.Get(footprintId);
            Footprint fp = site == null ? null : world.Footprints.Get(footprintId);
            if (fp == null) return null;
            ConstructionRecipe recipe = SiteRecipe.Of(world, fp);
            double f = recipe.BuildWorkerMinutes > 0
                ? (double)site.ProgressMinutes / recipe.BuildWorkerMinutes
                : (recipe.Logs > 0 ? (double)Delivered(world, site) / recipe.Logs : 0);
            return new ConstructionProgress(site.State, Delivered(world, site), recipe.Logs, Math.Min(1.0, Math.Max(0.0, f)));
        }

        /// <summary>Logs delivered to the site so far: those on site plus those consumed.</summary>
        public static int Delivered(SimWorld world, ConstructionSite site)
        {
            LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
            return site.LogsConsumed + (loc == null ? 0 : loc.CountOf(Items.Log.Id));
        }

        /// <summary>Units of one item delivered to the site: on site plus consumed.</summary>
        public static int DeliveredOf(SimWorld world, ConstructionSite site, string itemId)
        {
            LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
            return site.ConsumedOf(itemId) + (loc == null ? 0 : loc.CountOf(itemId));
        }

        private static int NeededUnits(ConstructionRecipe recipe)
        {
            int n = 0;
            foreach (int c in recipe.Items.Values) n += c;
            return n;
        }

        private static bool AllDelivered(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            foreach (var kv in recipe.Items)
                if (DeliveredOf(world, site, kv.Key) < kv.Value) return false;
            return true;
        }

        /// <summary>Progress cap in worker-minutes: recipe minutes × delivered units ÷ needed units over all items (CN-23, decision 0046).</summary>
        public static int Cap(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            int needed = NeededUnits(recipe);
            if (needed == 0) return recipe.BuildWorkerMinutes;
            int delivered = 0;
            foreach (var kv in recipe.Items) delivered += Math.Min(kv.Value, DeliveredOf(world, site, kv.Key));
            return (int)((long)recipe.BuildWorkerMinutes * delivered / needed);
        }

        /// <summary>Worker-minutes of build work still allowed now: cap − progress, never negative.</summary>
        public static int Remaining(SimWorld world, ConstructionSite site, ConstructionRecipe recipe) =>
            Math.Max(0, Cap(world, site, recipe) - site.ProgressMinutes);

        /// <summary>
        /// One builder's minute of work. Returns false, changing nothing, when the site is at its cap (CN-23c).
        /// Consumes each log as progress passes its share (CN-23b) and removes the site record at the full total.
        /// </summary>
        public static bool Work(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            if (Remaining(world, site, recipe) <= 0) return false;
            site.ProgressMinutes++;
            ConsumeDue(world, site, recipe);
            TryFinish(world, site, recipe);
            return true;
        }

        /// <summary>Removes the site record when all logs are delivered and the work total is reached.</summary>
        public static bool TryFinish(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            if (site.ProgressMinutes < recipe.BuildWorkerMinutes || !AllDelivered(world, site, recipe)) return false;
            ConsumeDue(world, site, recipe);
            world.Construction.Remove(site.FootprintId);
            ConstructionLog.Done(world, site.FootprintId);
            Operations.Workshops.WorkshopBuildings.OnBuilt(world, world.Footprints.Get(site.FootprintId));
            return true;
        }

        private static void ConsumeDue(SimWorld world, ConstructionSite site, ConstructionRecipe recipe)
        {
            LedgerLocation loc = world.Inventory.Get(site.LedgerLocationId);
            foreach (var kv in recipe.Items)
            {
                int due = recipe.BuildWorkerMinutes <= 0 || site.ProgressMinutes >= recipe.BuildWorkerMinutes
                    ? kv.Value
                    : (int)((long)site.ProgressMinutes * kv.Value / recipe.BuildWorkerMinutes);
                ItemDef item = Items.Find(kv.Key);
                int done = site.ConsumedOf(kv.Key);
                while (done < due && loc != null && world.Inventory.Consume(loc, item, 1)) done++;
                site.SetConsumed(kv.Key, done);
            }
        }
    }
}
