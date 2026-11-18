using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>Everything a workshop panel shows, read from the Sim in one go (T-525, pattern of GranaryReport). Immutable.</summary>
    public sealed class WorkshopReport
    {
        /// <summary>The workshop footprint id.</summary>
        public int FootprintId { get; private set; }
        /// <summary>"Hewing station" or "Sawpit".</summary>
        public string Name { get; private set; }
        /// <summary>True once the workshop is built.</summary>
        public bool IsBuilt { get; private set; }
        /// <summary>The reported operation state (only meaningful when <see cref="IsBuilt"/>).</summary>
        public OperationState State { get; private set; }
        /// <summary>Short status text, e.g. "Waiting for logs".</summary>
        public string StatusText { get; private set; }
        /// <summary>Input items waiting in the input stock.</summary>
        public int InputCount { get; private set; }
        /// <summary>Input stock capacity.</summary>
        public int InputCapacity { get; private set; }
        /// <summary>Output items in the output store.</summary>
        public int OutputCount { get; private set; }
        /// <summary>Output store capacity.</summary>
        public int OutputCapacity { get; private set; }
        /// <summary>Name of the input item, e.g. "log".</summary>
        public string InputName { get; private set; }
        /// <summary>Name of the output item, e.g. "beam".</summary>
        public string OutputName { get; private set; }
        /// <summary>Id of the assigned family, or 0 when none.</summary>
        public int FamilyId { get; private set; }
        /// <summary>Surname of the assigned family, or null when none.</summary>
        public string FamilyName { get; private set; }
        /// <summary>"Input: 3 / 20 logs".</summary>
        public string InputText => "Input: " + InputCount + " / " + InputCapacity + " " + InputName + "s";
        /// <summary>"Output: 6 / 30 beams".</summary>
        public string OutputText => "Output: " + OutputCount + " / " + OutputCapacity + " " + OutputName + "s";

        private WorkshopReport() { }

        private static string ShortName(ItemDef item) => item.Id.Substring(item.Id.LastIndexOf('.') + 1);

        /// <summary>The report for a workshop footprint, or null when the id is not a hewing station or sawpit.</summary>
        public static WorkshopReport Of(SimWorld world, int footprintId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Footprint fp = world.Footprints.Get(footprintId);
            if (fp == null || !WorkshopBuildings.IsWorkshop(fp.Type)) return null;
            WorkshopDef def = OperationTypes.WorkshopOf(OperationTypes.WorkshopOperationFor(fp.Type.Id), world.Tuning);
            var r = new WorkshopReport
            {
                FootprintId = footprintId,
                Name = ModuleTypes.DisplayName(fp.Type.Id),
                IsBuilt = world.IsBuilt(footprintId),
                InputName = ShortName(def.FirstInput),
                OutputName = ShortName(def.Output),
                InputCapacity = def.InputStockCapacity,
                OutputCapacity = def.OutputCapacity,
            };
            LedgerLocation stock = world.Inventory.FindWorkshopStock(footprintId);
            LedgerLocation store = world.Inventory.FindGoodsStore(footprintId);
            if (stock != null) { r.InputCount = stock.CountOf(def.FirstInput.Id); r.InputCapacity = stock.Capacity; }
            if (store != null) { r.OutputCount = store.CountOf(def.Output.Id); r.OutputCapacity = store.Capacity; }
            Operation op = world.Operations.FindByModule(footprintId);
            if (op != null)
            {
                foreach (int fid in op.FamilyIds)
                {
                    Family f = world.People.FindFamily(fid);
                    if (f == null) continue;
                    r.FamilyId = f.Id; r.FamilyName = f.Surname;
                    break;
                }
            }
            if (!r.IsBuilt) r.StatusText = "Under construction";
            else if (op == null) { r.State = OperationState.NoWorkers; r.StatusText = TextOf(r.State, r.InputName); }
            else
            {
                r.State = OperationStatus.Reported(world, op);
                r.StatusText = r.State == OperationState.WaitingForInput && WorkshopStatus.BlockedOnlyByReserve(world, op, def)
                    ? "Waiting: " + InputReserve.InStorage(world, def.FirstInput) + " / " + InputReserve.ReserveOf(world, op, def.FirstInput) + " " + r.InputName + "s in storage"
                    : TextOf(r.State, r.InputName);
            }
            return r;
        }

        /// <summary>Status text for a workshop state; <paramref name="input"/> names what is missing.</summary>
        public static string TextOf(OperationState s, string input)
        {
            switch (s)
            {
                case OperationState.Working: return "Working";
                case OperationState.Paused: return "Paused";
                case OperationState.MissingHut: return "Missing hut";
                case OperationState.NoWorkers: return "No workers";
                case OperationState.WaitingForInput: return "Waiting for " + input + "s";
                case OperationState.WaitingForOx: return "Waiting for ox";
                case OperationState.WorkshopStoreFull: return "Store full";
                default: return s.ToString();
            }
        }

        /// <summary>The confirm-dialog text for placing a hewing station or sawpit, or null for another type.</summary>
        public static string ConfirmText(SimWorld world, string typeId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            FootprintTypeDef type = ModuleTypes.Find(typeId);
            if (!WorkshopBuildings.IsWorkshop(type)) return null;
            WorkshopDef def = OperationTypes.WorkshopOf(OperationTypes.WorkshopOperationFor(typeId), world.Tuning);
            return "Build a " + ModuleTypes.DisplayName(typeId).ToLowerInvariant() + "?\nCosts " + type.ConstructionRecipe.Logs
                + " logs.\nTurns " + def.InputsPerDayPerFamily + " " + ShortName(def.FirstInput) + "s a day into "
                + def.InputsPerDayPerFamily * def.OutputsPerInput + " " + ShortName(def.Output) + "s. One family works it.";
        }
    }
}
