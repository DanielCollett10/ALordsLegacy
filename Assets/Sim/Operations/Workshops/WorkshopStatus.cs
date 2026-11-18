using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>
    /// Status of a workshop operation (T-524, decision 0047), computed from the def and never stored: Paused, MissingHut,
    /// NoWorkers, then Working while a work task runs, else WorkshopStoreFull (no room for one input's outputs), else
    /// WaitingForInput (stock empty), else Working. WaitingForOx comes from <see cref="OperationStatus.Reported"/>.
    /// </summary>
    public static class WorkshopStatus
    {
        /// <summary>The status of <paramref name="op"/>, which must be a workshop operation.</summary>
        public static OperationState Of(SimWorld world, Operation op, WorkshopDef def)
        {
            WorkshopDef.RequireSingleInput(def);
            if (op.Paused) return OperationState.Paused;
            Footprint fp = WorkshopSystem.HutOf(world, op);
            if (fp == null || !world.IsWorking(fp.Id)) return OperationState.MissingHut;
            if (!WorkshopSystem.HasWorker(world, op)) return OperationState.NoWorkers;
            if (WorkshopSystem.OpenWorkCount(world, op) > 0) return OperationState.Working;
            LedgerLocation store = world.Inventory.FindGoodsStore(fp.Id);
            if (store == null || store.FreeSpace < def.OutputsPerInput) return OperationState.WorkshopStoreFull;
            LedgerLocation stock = world.Inventory.FindWorkshopStock(fp.Id);
            if (stock == null || stock.AvailableOf(def.Input.Id) < 1) return OperationState.WaitingForInput;
            return OperationState.Working;
        }

        /// <summary>
        /// True when the stock is empty, no fetch is under way, a source holds a free load of input, and only the reserve
        /// stops a fetch (PL-32). Free load means the source has the load itself, ignoring the reserve.
        /// </summary>
        public static bool BlockedOnlyByReserve(SimWorld world, Operation op, WorkshopDef def)
        {
            Footprint fp = WorkshopSystem.HutOf(world, op);
            if (fp == null) return false;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.Def.Id == WorkshopSystem.FetchTaskId && !t.IsFinished) return false;
            LedgerLocation source = InputSources.CreateDefault().Find(world, def.Input, fp.AccessPoint);
            int load = System.Math.Max(1, def.OxLoadForInput);
            return source != null && source.AvailableOf(def.Input.Id) >= load && !InputReserve.Allows(world, op, def.Input, load);
        }

        /// <summary>
        /// True when the stock is empty, no fetch is under way, and a source holds a free load of input but no ox is
        /// available to carry it (the fetch is never opened without an ox). Used by <see cref="OperationStatus.Reported"/>.
        /// </summary>
        public static bool StuckWithoutOx(SimWorld world, Operation op, WorkshopDef def)
        {
            Footprint fp = WorkshopSystem.HutOf(world, op);
            if (fp == null) return false;
            foreach (TaskRecord t in world.Tasks.ByOwner(op.Id))
                if (t.Def.Id == WorkshopSystem.FetchTaskId && !t.IsFinished) return false;
            LedgerLocation source = InputSources.CreateDefault().Find(world, def.Input, fp.AccessPoint);
            int load = System.Math.Max(1, def.OxLoadForInput);
            return source != null && InputReserve.Allows(world, op, def.Input, load) && source.AvailableOf(def.Input.Id) >= load && new OxPool(world).FindFor(op.Id, fp.AccessPoint) == null;
        }
    }
}
