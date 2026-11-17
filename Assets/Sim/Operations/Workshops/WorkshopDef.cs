using System;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>
    /// A workshop as data (decision 0047, HW-05): what it takes in, what it makes and how fast. One shared workshop
    /// system runs every def. Built from <see cref="SimTuning"/> so every number is tunable; a def is a snapshot,
    /// so rebuild it after changing tuning.
    /// </summary>
    public sealed class WorkshopDef
    {
        /// <summary>Stable id, "workshop.hewing" or "workshop.sawing".</summary>
        public string Id { get; }
        /// <summary>The item the workshop works on (the input stock holds only this).</summary>
        public ItemDef Input { get; }
        /// <summary>The item the workshop makes.</summary>
        public ItemDef Output { get; }
        /// <summary>Output items made from one input item.</summary>
        public int OutputsPerInput { get; }
        /// <summary>Input items one family works through per day.</summary>
        public int InputsPerDayPerFamily { get; }
        /// <summary>Most input items waiting in the workshop's input stock.</summary>
        public int InputStockCapacity { get; }
        /// <summary>Most output items the workshop's store holds.</summary>
        public int OutputCapacity { get; }
        /// <summary>Input items an ox carries per trip to this workshop.</summary>
        public int OxLoadForInput { get; }

        /// <summary>Creates a def; every count must be positive.</summary>
        public WorkshopDef(string id, ItemDef input, ItemDef output, int outputsPerInput, int inputsPerDayPerFamily,
            int inputStockCapacity, int outputCapacity, int oxLoadForInput)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id is required.", nameof(id));
            Id = id;
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            OutputsPerInput = Positive(outputsPerInput, nameof(outputsPerInput));
            InputsPerDayPerFamily = Positive(inputsPerDayPerFamily, nameof(inputsPerDayPerFamily));
            InputStockCapacity = Positive(inputStockCapacity, nameof(inputStockCapacity));
            OutputCapacity = Positive(outputCapacity, nameof(outputCapacity));
            OxLoadForInput = Positive(oxLoadForInput, nameof(oxLoadForInput));
        }

        private static int Positive(int v, string name)
        {
            if (v <= 0) throw new ArgumentOutOfRangeException(name, "Must be positive.");
            return v;
        }

        /// <summary>Hewing station: Log to Beam, every number from tuning.</summary>
        public static WorkshopDef Hewing(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.hewing", Items.Log, Items.Beam, t.HewingBeamsPerLog, t.HewingLogsPerDay,
                t.HewingStockCapacity, t.HewingStoreCapacity, t.OxLoadLogs);
        }

        /// <summary>Sawpit: Beam to Plank, every number from tuning.</summary>
        public static WorkshopDef Sawing(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.sawing", Items.Beam, Items.Plank, t.SawingPlanksPerBeam, t.SawingBeamsPerDay,
                t.SawingStockCapacity, t.SawingStoreCapacity, t.OxLoadBeams);
        }
    }
}
