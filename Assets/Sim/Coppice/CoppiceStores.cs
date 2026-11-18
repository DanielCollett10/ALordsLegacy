using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Coppice
{
    /// <summary>
    /// The coppice hut's own rod store (MAT-14, ST-42b): a goods store capped per rod kind. Pure queries. Room for a
    /// kind is its free space minus the rods unfinished coppice tasks have already promised it. The yard never collects
    /// from this store; it counts as storage for the rod reserve (<c>WorkshopBuildings.HoldsGoods</c>).
    /// </summary>
    public static class CoppiceStores
    {
        /// <summary>The rod kinds a stool can give, hazel first (the tie-break for the shortest kind).</summary>
        public static readonly BushKind[] StoolKinds = { BushKind.HazelStool, BushKind.WillowStool };

        /// <summary>The rod a stool kind gives.</summary>
        public static ItemDef RodOf(BushKind kind) => kind == BushKind.WillowStool ? Items.WillowRod : Items.HazelRod;

        /// <summary>The operation's built hut store, or null.</summary>
        public static LedgerLocation HutStore(SimWorld world, Operation op)
        {
            Footprint hut = OperationStatus.Hut(world, op);
            return hut == null ? null : world.Inventory.FindGoodsStore(hut.Id);
        }

        /// <summary>Rods of one kind that unfinished coppice tasks have promised to a store but not yet put in it.</summary>
        public static int Pending(SimWorld world, int storeLocationId, ItemDef rod)
        {
            int n = 0;
            foreach (TaskRecord t in world.Tasks.All)
            {
                if (t.IsFinished || t.Def.Id != CoppiceSystem.CoppiceTaskId || !t.BushId.HasValue) continue;
                if (!world.Bushes.TryGet(t.BushId.Value, out BushRecord b) || RodOf(b.Kind).Id != rod.Id) continue;
                foreach (TaskStep s in t.Def.Steps)
                    if (s.Kind == StepKind.DepositForage && s.LocationId == storeLocationId) n += s.Count;
            }
            return n;
        }

        /// <summary>Rods of this kind the store still has room for after pending deliveries (never negative).</summary>
        public static int Room(SimWorld world, LedgerLocation store, ItemDef rod)
        {
            int r = store.FreeSpaceFor(rod.Id) - Pending(world, store.Id, rod);
            return r < 0 ? 0 : r;
        }
    }
}
