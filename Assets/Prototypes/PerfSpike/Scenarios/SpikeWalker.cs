using System;
using System.Collections.Generic;
using LordsLegacy.Prototypes.PerfSpike.Agents;
using LordsLegacy.Prototypes.PerfSpike.Nav;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). A position plus the path it is following. Walks <see cref="SpikeCrowd.WalkStepMetres"/>
    /// per tick along cell centres, exactly like <see cref="SpikeCrowd"/>.
    /// </summary>
    public sealed class SpikeWalker
    {
        /// <summary>Position east, metres.</summary>
        public float X;

        /// <summary>Position north, metres.</summary>
        public float Z;

        /// <summary>Current path (cell centres, start to goal). Empty when none.</summary>
        public IReadOnlyList<CellRef> Path = SpikeAgent.NoPath;

        /// <summary>Index of the next cell centre to walk to.</summary>
        public int Next;

        /// <summary>True while there are cells left to walk to.</summary>
        public bool HasPath => Next < Path.Count;

        /// <summary>Gives the walker a new path starting at its current cell.</summary>
        public void SetPath(IReadOnlyList<CellRef> path)
        {
            Path = path;
            Next = 0;
        }

        /// <summary>Walks one tick along the path. Does nothing when there is no path left.</summary>
        public void Walk(float cellSize)
        {
            float remaining = SpikeCrowd.WalkStepMetres;
            while (remaining > 0f && Next < Path.Count)
            {
                CellRef c = Path[Next];
                float tx = (c.X + 0.5f) * cellSize;
                float tz = (c.Z + 0.5f) * cellSize;
                float dx = tx - X;
                float dz = tz - Z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist <= remaining)
                {
                    X = tx;
                    Z = tz;
                    remaining -= dist;
                    Next++;
                }
                else
                {
                    float f = remaining / dist;
                    X += dx * f;
                    Z += dz * f;
                    remaining = 0f;
                }
            }
        }
    }
}
