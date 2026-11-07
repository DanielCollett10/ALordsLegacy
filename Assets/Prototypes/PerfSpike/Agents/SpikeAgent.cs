using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Agents
{
    /// <summary>
    /// THROWAWAY (T-008). One walking agent: a continuous position in metres, the A* path it follows
    /// (cell centres from start to goal) and counters the harness reads. Only <see cref="SpikeCrowd"/> changes it.
    /// </summary>
    public sealed class SpikeAgent
    {
        /// <summary>Path of an agent that has none yet.</summary>
        internal static readonly IReadOnlyList<CellRef> NoPath = new CellRef[0];

        /// <summary>Creates an agent standing at (x,z) metres with no path.</summary>
        public SpikeAgent(int id, float x, float z)
        {
            Id = id;
            X = x;
            Z = z;
            Path = NoPath;
        }

        /// <summary>Index of the agent in its crowd.</summary>
        public int Id { get; }

        /// <summary>Position east, in metres (Sim x).</summary>
        public float X { get; internal set; }

        /// <summary>Position north, in metres (Sim z).</summary>
        public float Z { get; internal set; }

        /// <summary>Cells of the current path, start to goal inclusive. Empty when the agent has none.</summary>
        public IReadOnlyList<CellRef> Path { get; internal set; }

        /// <summary>Index into <see cref="Path"/> of the next cell centre to walk to.</summary>
        public int NextWaypoint { get; internal set; }

        /// <summary>Total distance walked along paths, in metres, since the agent was created.</summary>
        public float Walked { get; internal set; }

        /// <summary>Number of targets this agent has been given and planned a path to.</summary>
        public int Replans { get; internal set; }

        /// <summary>Straight-line distance, in metres, from the agent to its most recent target when it was chosen.</summary>
        public float LastTargetMetres { get; internal set; }

        /// <summary>True when the agent has no path left to walk (it is waiting for a target).</summary>
        public bool NeedsTarget => NextWaypoint >= Path.Count;
    }
}
