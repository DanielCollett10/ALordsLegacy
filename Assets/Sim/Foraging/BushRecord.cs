namespace LordsLegacy.Sim.Foraging
{
    /// <summary>One forage bush (FD-20, FD-21): a fixed position, a kind, baskets left and an optional reservation.</summary>
    public sealed class BushRecord
    {
        /// <summary>Stable id, never reused.</summary>
        public int Id { get; }
        /// <summary>World x in metres.</summary>
        public float X { get; }
        /// <summary>World z in metres.</summary>
        public float Z { get; }
        /// <summary>Berries or mushrooms.</summary>
        public BushKind Kind { get; }
        /// <summary>Baskets left to pick, 0 to <c>SimTuning.BushBaskets</c>.</summary>
        public int Baskets { get; internal set; }
        /// <summary>Tick at which the last basket was taken; meaningful only while <see cref="Baskets"/> is 0.</summary>
        public long EmptiedAtTick { get; internal set; }
        /// <summary>Forager (family or citizen id) that reserved this bush, or null.</summary>
        public int? ReservedBy { get; internal set; }

        /// <summary>Creates a bush.</summary>
        public BushRecord(int id, float x, float z, BushKind kind, int baskets)
        {
            Id = id; X = x; Z = z; Kind = kind; Baskets = baskets;
        }
    }
}
