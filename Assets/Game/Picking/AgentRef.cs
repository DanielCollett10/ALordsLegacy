namespace LordsLegacy.Game
{
    /// <summary>Identifies a citizen or an ox by id (a selection handle that holds no Sim reference).</summary>
    public readonly struct AgentRef
    {
        /// <summary>Creates a handle.</summary>
        public AgentRef(bool isOx, int id) { IsOx = isOx; Id = id; }

        /// <summary>True for an ox, false for a citizen.</summary>
        public bool IsOx { get; }

        /// <summary>Id in the Sim <c>Population</c>.</summary>
        public int Id { get; }

        /// <summary>Handle for a citizen.</summary>
        public static AgentRef Citizen(int id) => new AgentRef(false, id);

        /// <summary>Handle for an ox.</summary>
        public static AgentRef Ox(int id) => new AgentRef(true, id);
    }
}
