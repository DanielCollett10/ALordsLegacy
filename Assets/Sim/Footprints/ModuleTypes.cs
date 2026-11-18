using System.Collections.Generic;

namespace LordsLegacy.Sim.Footprints
{
    /// <summary>
    /// The module footprint types of the woodcutting operation (woodcutting.md WC-02, WC-05, WC-06), built in code
    /// like <see cref="FootprintTypeDef.Camp"/> until the definition pipeline exists (decision 0019).
    /// </summary>
    public static class ModuleTypes
    {
        /// <summary>Id of the woodcutting hut module.</summary>
        public const string WoodcuttingHutId = "module.woodcutting_hut";
        /// <summary>Id of the log storage module.</summary>
        public const string LogStorageId = "module.log_storage";
        /// <summary>Id of the oxen stand module.</summary>
        public const string OxenStandId = "module.oxen_stand";

        /// <summary>Id of the house footprint (HM-04).</summary>
        public const string HouseId = "module.house";

        /// <summary>Id of the granary structure (FD-30).</summary>
        public const string GranaryId = "module.granary";

        /// <summary>Short player-facing name of a module type id ("Hut", "Log storage", "Oxen stand"); other ids pass through.</summary>
        public static string DisplayName(string typeId)
        {
            switch (typeId)
            {
                case WoodcuttingHutId: return "Hut";
                case LogStorageId: return "Log storage";
                case OxenStandId: return "Oxen stand";
                case HouseId: return "House";
                case GranaryId: return "Granary";
                case ForagerHutId: return "Forager hut";
                case MarketStallId: return "Market stall";
                case HewingStationId: return "Hewing station";
                case SawpitId: return "Sawpit";
                case WeaverId: return "Weaver";
                case WattleYardId: return "Wattle yard";
                case StorageYardId: return "Storage yard";
                case CoppiceHutId: return "Coppice hut";
                case ThatcherHutId: return "Thatcher's hut";
                default: return typeId;
            }
        }

        /// <summary>Woodcutting hut: 6 × 4 m, blocks movement, clears trees (WC-02, WC-06, WC-08).</summary>
        public static readonly FootprintTypeDef WoodcuttingHut =
            new FootprintTypeDef(WoodcuttingHutId, 6000, 4000, true, true, recipe: new ConstructionRecipe(8, 960));

        /// <summary>Log storage: 8 × 6 m, walkable, clears trees, holds 20 logs (WC-02, WC-06, INV-A3).</summary>
        public static readonly FootprintTypeDef LogStorage =
            new FootprintTypeDef(LogStorageId, 8000, 6000, false, true, logCapacity: 20);

        /// <summary>Oxen stand: 3 × 3 m, walkable, clears trees, holds 1 ox (WC-02, WC-05, WC-06).</summary>
        public static readonly FootprintTypeDef OxenStand =
            new FootprintTypeDef(OxenStandId, 3000, 3000, false, true, oxCapacity: 1, recipe: new ConstructionRecipe(1, 60));

        /// <summary>House: 6 × 8 m, blocks movement, clears trees, 10 logs / 720 worker-minutes (HM-04, HM-06).</summary>
        public static readonly FootprintTypeDef House =
            new FootprintTypeDef(HouseId, 6000, 8000, true, true, recipe: new ConstructionRecipe(10, 720));

        /// <summary>
        /// Granary: 8 × 6 m building that blocks movement, with a walkable 5 m yard (plot 18 × 16 m), clears trees,
        /// holds 100 food items, 8 logs / 600 worker-minutes (FD-30, FD-30b, FOOD-04, CON-A1).
        /// </summary>
        public static readonly FootprintTypeDef Granary =
            new FootprintTypeDef(GranaryId, 8000, 6000, true, true, recipe: new ConstructionRecipe(8, 600),
                foodCapacity: 100, yardMarginMm: 5000);

        /// <summary>Id of the forager hut module (FD-22).</summary>
        public const string ForagerHutId = "module.forager_hut";

        /// <summary>
        /// Forager hut: 6 × 4 m, blocks movement, clears trees, 8 logs / 480 worker-minutes (FD-22). It is also the camp's
        /// food-only store: <see cref="FootprintTypeDef.FoodCapacity"/> is the default 30 items, set from
        /// <c>SimTuning.ForagerHutCapacity</c> when the store is created (FD-23b).
        /// </summary>
        public static readonly FootprintTypeDef ForagerHut =
            new FootprintTypeDef(ForagerHutId, 6000, 4000, true, true, recipe: new ConstructionRecipe(8, 480), foodCapacity: 30);

        /// <summary>Id of the market stall (MK-21).</summary>
        public const string MarketStallId = "module.market_stall";

        /// <summary>
        /// Market stall: 3 × 2 m open table that does not block movement (MK-03), 2 planks / 60 worker-minutes (PL-10), and a
        /// food-only store of 30 items (MK-21, MK-22). It has no operation: it stands on a market slot (<c>StallSystem</c>).
        /// </summary>
        public static readonly FootprintTypeDef MarketStall =
            new FootprintTypeDef(MarketStallId, 3000, 2000, false, false,
                recipe: new ConstructionRecipe(new[] { new KeyValuePair<string, int>(Inventory.Items.Plank.Id, 2) }, 60), foodCapacity: 30);

        /// <summary>
        /// The stall's old recipe, 1 log / 60 worker-minutes. Only an unbuilt stall placed before the plank recipe keeps it
        /// (PL-13); see <c>SiteRecipe</c>.
        /// </summary>
        public static readonly ConstructionRecipe MarketStallLogRecipe = new ConstructionRecipe(1, 60);

        /// <summary>Id of the hewing station (HW-01).</summary>
        public const string HewingStationId = "module.hewing_station";
        /// <summary>Id of the sawpit (PL-01).</summary>
        public const string SawpitId = "module.sawpit";

        /// <summary>Hewing station: 6 × 4 m, blocks movement, clears trees, 3 m yard, 6 logs / 480 worker-minutes (HW-01).</summary>
        public static readonly FootprintTypeDef HewingStation =
            new FootprintTypeDef(HewingStationId, 6000, 4000, true, true, recipe: new ConstructionRecipe(6, 480), yardMarginMm: 3000);

        /// <summary>Sawpit: 6 × 4 m, blocks movement, clears trees, 3 m yard, 6 logs / 480 worker-minutes (PL-01).</summary>
        public static readonly FootprintTypeDef Sawpit =
            new FootprintTypeDef(SawpitId, 6000, 4000, true, true, recipe: new ConstructionRecipe(6, 480), yardMarginMm: 3000);

        /// <summary>Id of the storage yard (ST-03).</summary>
        public const string StorageYardId = "module.storage_yard";

        /// <summary>
        /// Storage yard: 8 × 6 m, blocks movement, clears trees, 8 logs / 480 worker-minutes (ST-03). Its General
        /// <c>GoodsStore</c> is created when it is built (<c>StorageYards.OnBuilt</c>).
        /// </summary>
        public static readonly FootprintTypeDef StorageYard =
            new FootprintTypeDef(StorageYardId, 8000, 6000, true, true, recipe: new ConstructionRecipe(8, 480), yardMarginMm: 3000);

        /// <summary>Id of the coppice hut (MAT-10).</summary>
        public const string CoppiceHutId = "module.coppice_hut";

        /// <summary>
        /// Coppice hut: 6 × 4 m, blocks movement, clears trees, 8 logs / 480 worker-minutes (MAT-10). Its rod
        /// <c>GoodsStore</c> (capped per kind, MAT-14) is created when it is built (<c>WorkshopBuildings.OnBuilt</c>).
        /// </summary>
        public static readonly FootprintTypeDef CoppiceHut =
            new FootprintTypeDef(CoppiceHutId, 6000, 4000, true, true, recipe: new ConstructionRecipe(8, 480));

        /// <summary>Id of the thatcher's hut (MAT-39b).</summary>
        public const string ThatcherHutId = "module.thatcher_hut";

        /// <summary>
        /// Thatcher's hut: 6 × 4 m, blocks movement, clears trees, 8 logs / 480 worker-minutes (MAT-39b). Its sheaf
        /// <c>GoodsStore</c> (40 thatch, MAT-38) is created when it is built (<c>WorkshopBuildings.OnBuilt</c>).
        /// </summary>
        public static readonly FootprintTypeDef ThatcherHut =
            new FootprintTypeDef(ThatcherHutId, 6000, 4000, true, true, recipe: new ConstructionRecipe(8, 480));

        /// <summary>Id of the weaver (MAT-20).</summary>
        public const string WeaverId = "module.weaver";
        /// <summary>Id of the wattle yard (MAT-22).</summary>
        public const string WattleYardId = "module.wattle_yard";

        /// <summary>Weaver: 6 × 4 m, blocks movement, clears trees, 3 m yard, 6 logs / 480 worker-minutes (MAT-20).</summary>
        public static readonly FootprintTypeDef Weaver =
            new FootprintTypeDef(WeaverId, 6000, 4000, true, true, recipe: new ConstructionRecipe(6, 480), yardMarginMm: 3000);

        /// <summary>Wattle yard: 6 × 4 m, blocks movement, clears trees, 3 m yard, 6 logs / 480 worker-minutes (MAT-22).</summary>
        public static readonly FootprintTypeDef WattleYard =
            new FootprintTypeDef(WattleYardId, 6000, 4000, true, true, recipe: new ConstructionRecipe(6, 480), yardMarginMm: 3000);

        /// <summary>All module types.</summary>
        public static readonly IReadOnlyList<FootprintTypeDef> All = new[] { WoodcuttingHut, LogStorage, OxenStand, House, Granary, ForagerHut, MarketStall, HewingStation, Sawpit, Weaver, WattleYard, StorageYard, CoppiceHut, ThatcherHut };

        /// <summary>Finds a module type by id, or null if unknown.</summary>
        public static FootprintTypeDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }
    }
}
