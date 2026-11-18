using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Kit;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Foraging
{
    /// <summary>What a forager hut panel shows, read from the Sim in one go (T-566, pattern of GranaryReport). Immutable. Read only.</summary>
    public sealed class ForagerHutReport
    {
        /// <summary>The forager hut footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>True once the hut is built.</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>The foraging status (only meaningful when <see cref="HasOperation"/>).</summary>
        public OperationState State { get; private set; }
        /// <summary>True when a foraging operation uses this hut.</summary>
        public bool HasOperation { get; private set; }
        /// <summary>Short status text, e.g. "Working" or "No bushes".</summary>
        public string StatusText { get; private set; }
        /// <summary>Food items in the hut store.</summary>
        public int FoodTotal { get; private set; }
        /// <summary>Food store capacity.</summary>
        public int FoodCapacity { get; private set; }
        /// <summary>Baskets held in the hut's kit store.</summary>
        public int BasketsHeld { get; private set; }
        /// <summary>Basket slots of a forager hut (tuning).</summary>
        public int BasketSlots { get; private set; }
        /// <summary>"Baskets: 1 / 2".</summary>
        public string BasketsText => "Baskets: " + BasketsHeld + " / " + BasketSlots;

        private ForagerHutReport() { }

        /// <summary>The report for a forager hut footprint, or null when the id is not a forager hut.</summary>
        public static ForagerHutReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || fp.Type.Id != ModuleTypes.ForagerHutId) return null;
            var r = new ForagerHutReport
            {
                FootprintId = footprintId,
                IsBuilt = world.IsBuilt(footprintId),
                BasketsHeld = KitStores.BasketsHeld(world, footprintId),
                BasketSlots = KitStores.BasketSlots(world, fp.Type),
            };
            LedgerLocation food = world.Inventory.FindFoodStore(footprintId);
            if (food != null) { r.FoodTotal = food.Total; r.FoodCapacity = food.Capacity; }
            Operation op = world.Operations.FindByModule(footprintId);
            r.HasOperation = op != null;
            if (!r.IsBuilt) r.StatusText = "Under construction";
            else if (op == null) { r.State = OperationState.NoWorkers; r.StatusText = "No workers"; }
            else { r.State = ForagingStatus.Of(world, op); r.StatusText = OperationText.Of(r.State); }
            return r;
        }

    }
}
