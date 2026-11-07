using System.Collections.Generic;

namespace LordsLegacy.Prototypes.PerfSpike.Nav
{
    /// <summary>
    /// THROWAWAY (T-007). A found path: cells from start to goal inclusive, total cost, and how many nodes
    /// A* expanded (a rough work measure for the perf spike).
    /// </summary>
    public sealed class SpikePath
    {
        /// <summary>Creates a path result.</summary>
        public SpikePath(IReadOnlyList<CellRef> cells, float cost, int expanded)
        {
            Cells = cells;
            Cost = cost;
            Expanded = expanded;
        }

        /// <summary>Cells from start to goal, inclusive.</summary>
        public IReadOnlyList<CellRef> Cells { get; }

        /// <summary>Sum over steps of step length (1 or sqrt 2 cells) times the entered cell's cost.</summary>
        public float Cost { get; }

        /// <summary>Nodes taken off the open list while searching.</summary>
        public int Expanded { get; }
    }

    /// <summary>THROWAWAY (T-007). Integer grid cell coordinate.</summary>
    public readonly struct CellRef
    {
        /// <summary>Creates a cell reference.</summary>
        public CellRef(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>Column.</summary>
        public int X { get; }

        /// <summary>Row.</summary>
        public int Z { get; }
    }
}
