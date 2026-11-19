using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Tuning;

namespace LordsLegacy.Sim.Operations.Workshops
{
    /// <summary>How a recipe input reaches the workshop (decision 0051).</summary>
    public enum CarryClass
    {
        /// <summary>Fetched by ox.</summary>
        Ox,
        /// <summary>Carried by hand.</summary>
        Hand
    }

    /// <summary>One recipe input: item, count per batch and how it is carried.</summary>
    public readonly struct RecipeInput
    {
        /// <summary>The item.</summary>
        public ItemDef Item { get; }
        /// <summary>Items consumed per batch.</summary>
        public int Count { get; }
        /// <summary>How it is fetched.</summary>
        public CarryClass Carry { get; }
        /// <summary>Capacity of this input's own stock; 0 means the def's <see cref="WorkshopDef.InputStockCapacity"/>.</summary>
        public int StockCapacity { get; }

        /// <summary>Creates an input line; count must be positive.</summary>
        public RecipeInput(ItemDef item, int count, CarryClass carry) : this(item, count, carry, 0)
        {
        }

        /// <summary>Creates an input line with its own stock capacity (0 = the def's default); count must be positive.</summary>
        public RecipeInput(ItemDef item, int count, CarryClass carry, int stockCapacity)
        {
            if (stockCapacity < 0) throw new ArgumentOutOfRangeException(nameof(stockCapacity), "Must not be negative.");
            StockCapacity = stockCapacity;
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be positive.");
            Count = count;
            Carry = carry;
        }
    }

    /// <summary>One recipe output: item and count per batch.</summary>
    public readonly struct RecipeOutput
    {
        /// <summary>The item.</summary>
        public ItemDef Item { get; }
        /// <summary>Items made per batch.</summary>
        public int Count { get; }

        /// <summary>Creates an output line; count must be positive.</summary>
        public RecipeOutput(ItemDef item, int count)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be positive.");
            Count = count;
        }
    }

    /// <summary>
    /// A workshop as data (decision 0047, HW-05; recipes decision 0051): what it takes in, what it makes and how fast.
    /// One shared workshop system runs every def. Built from <see cref="SimTuning"/> so every number is tunable; a def
    /// is a snapshot, so rebuild it after changing tuning. The reserve is not part of the def: it stays a per-building,
    /// per-input setting (see <see cref="InputReserve"/>).
    /// </summary>
    public sealed class WorkshopDef
    {
        /// <summary>Stable id, "workshop.hewing" or "workshop.sawing".</summary>
        public string Id { get; }
        /// <summary>Recipe inputs per batch; may be empty.</summary>
        public IReadOnlyList<RecipeInput> Inputs { get; }
        /// <summary>Recipe outputs per batch; at least one.</summary>
        public IReadOnlyList<RecipeOutput> Outputs { get; }
        /// <summary>Batches one family completes per day.</summary>
        public int BatchesPerFamilyDay { get; }
        /// <summary>The single input item (the input stock holds only this); null when the recipe has no or several inputs.</summary>
        public ItemDef Input => Inputs.Count == 1 ? Inputs[0].Item : null;
        /// <summary>The first input item (the one panels and the reserve show); null when the recipe has no input.</summary>
        public ItemDef FirstInput => Inputs.Count > 0 ? Inputs[0].Item : null;
        /// <summary>Capacity of the stock of one input: its own, else <see cref="InputStockCapacity"/>.</summary>
        public int StockCapacityOf(RecipeInput input) => input.StockCapacity > 0 ? input.StockCapacity : InputStockCapacity;
        /// <summary>Output items made per batch, all lines together (room the store needs for one batch).</summary>
        public int OutputsPerBatch { get { int n = 0; foreach (RecipeOutput o in Outputs) n += o.Count; return n; } }
        /// <summary>The first output item.</summary>
        public ItemDef Output => Outputs[0].Item;
        /// <summary>Output items made from one input item (first output's count per batch).</summary>
        public int OutputsPerInput => Outputs[0].Count;
        /// <summary>Input items one family works through per day (single-input recipes: batches times input count).</summary>
        public int InputsPerDayPerFamily => Inputs.Count == 1 ? BatchesPerFamilyDay * Inputs[0].Count : BatchesPerFamilyDay;
        /// <summary>Most input items waiting in the workshop's input stock.</summary>
        public int InputStockCapacity { get; }
        /// <summary>Most output items the workshop's store holds.</summary>
        public int OutputCapacity { get; }
        /// <summary>Input items an ox carries per trip to this workshop.</summary>
        public int OxLoadForInput { get; }
        /// <summary>Youngest age in years that may work here; light work is 8+ (MAT-23), the default is <see cref="OperationStatus.WorkingAgeYears"/>.</summary>
        public double MinWorkAgeYears { get; private set; } = OperationStatus.WorkingAgeYears;
        /// <summary>True when the work task is light work, so citizens under working age may take it (MAT-23).</summary>
        public bool IsLightWork => MinWorkAgeYears < OperationStatus.WorkingAgeYears;
        /// <summary>True when the recipe has exactly one input and one output (all the workshop system runs so far).</summary>
        public bool IsSingleInput => Inputs.Count == 1 && Outputs.Count == 1;

        /// <summary>Creates a single-input def (one input item per batch, fetched by ox); every count must be positive.</summary>
        public WorkshopDef(string id, ItemDef input, ItemDef output, int outputsPerInput, int inputsPerDayPerFamily,
            int inputStockCapacity, int outputCapacity, int oxLoadForInput)
            : this(id, new[] { new RecipeInput(input, 1, CarryClass.Ox) }, new[] { new RecipeOutput(output, outputsPerInput) },
                inputsPerDayPerFamily, inputStockCapacity, outputCapacity, oxLoadForInput)
        {
        }

        /// <summary>Creates a def from a recipe; inputs may be empty, outputs need at least one line, counts must be positive.</summary>
        public WorkshopDef(string id, IList<RecipeInput> inputs, IList<RecipeOutput> outputs, int batchesPerFamilyDay,
            int inputStockCapacity, int outputCapacity, int oxLoadForInput)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outputs == null) throw new ArgumentNullException(nameof(outputs));
            if (outputs.Count == 0) throw new ArgumentException("At least one output is required.", nameof(outputs));
            Id = id;
            Inputs = new List<RecipeInput>(inputs);
            Outputs = new List<RecipeOutput>(outputs);
            BatchesPerFamilyDay = Positive(batchesPerFamilyDay, nameof(batchesPerFamilyDay));
            InputStockCapacity = Positive(inputStockCapacity, nameof(inputStockCapacity));
            OutputCapacity = Positive(outputCapacity, nameof(outputCapacity));
            OxLoadForInput = Positive(oxLoadForInput, nameof(oxLoadForInput));
        }

        /// <summary>
        /// Throws <see cref="NotSupportedException"/> unless the def is a single-input, single-output recipe.
        /// Multi-input and no-input recipes wait for cw-5b/cw-5c.
        /// </summary>
        public static void RequireSingleInput(WorkshopDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (!def.IsSingleInput)
                throw new NotSupportedException("Workshop '" + def.Id + "' has " + def.Inputs.Count + " inputs and "
                    + def.Outputs.Count + " outputs; only single-input, single-output recipes are supported until cw-5b/cw-5c.");
        }

        /// <summary>Sets the youngest working age (light work) and returns this def.</summary>
        public WorkshopDef WithMinWorkAge(double years)
        {
            MinWorkAgeYears = years;
            return this;
        }

        private static int Positive(int v, string name)
        {
            if (v <= 0) throw new ArgumentOutOfRangeException(name, "Must be positive.");
            return v;
        }

        /// <summary>Hewing station: 1 Log to Beams, every number from tuning.</summary>
        public static WorkshopDef Hewing(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.hewing", Items.Log, Items.Beam, t.HewingBeamsPerLog, t.HewingLogsPerDay,
                t.HewingStockCapacity, t.HewingStoreCapacity, t.OxLoadLogs);
        }

        /// <summary>Sawpit: 1 Beam to Planks, every number from tuning.</summary>
        public static WorkshopDef Sawing(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.sawing", Items.Beam, Items.Plank, t.SawingPlanksPerBeam, t.SawingBeamsPerDay,
                t.SawingStockCapacity, t.SawingStoreCapacity, t.OxLoadBeams);
        }

        /// <summary>Weaver (MAT-20): 6 willow rods to 1 basket, 4 baskets per family-day, input 30, store 10. Rods go by hand.</summary>
        public static WorkshopDef Weaving(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.weaving",
                new[] { new RecipeInput(Items.WillowRod, 6, CarryClass.Hand, 30) },
                new[] { new RecipeOutput(Items.Basket, 1) }, 4, 30, 10, Math.Max(1, t.HandCarryRods)).WithMinWorkAge(t.ForagerMinAge);
        }

        /// <summary>Daub pit (HL-01, HL-02): no input, 10 daub per family-day, store 20. Light work from the forager age.</summary>
        public static WorkshopDef Daubing(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.daubing", new RecipeInput[0],
                new[] { new RecipeOutput(Items.Daub, 1) }, 10, 1, 20, HandLoads.DaubPerTrip).WithMinWorkAge(t.ForagerMinAge);
        }

        /// <summary>Wattle yard (MAT-22): 6 hazel + 2 willow rods to 1 panel, 5 panels per family-day, inputs 30 hazel and 10 willow, store 20.</summary>
        public static WorkshopDef Wattling(SimTuning t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            return new WorkshopDef("workshop.wattling",
                new[]
                {
                    new RecipeInput(Items.HazelRod, 6, CarryClass.Hand, 30),
                    new RecipeInput(Items.WillowRod, 2, CarryClass.Hand, 10)
                },
                new[] { new RecipeOutput(Items.WattlePanel, 1) }, 5, 30, 20, Math.Max(1, t.HandCarryRods)).WithMinWorkAge(t.ForagerMinAge);
        }
    }
}
