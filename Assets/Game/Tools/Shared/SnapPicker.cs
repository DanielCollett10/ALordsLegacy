using System.Collections.Generic;

namespace LordsLegacy.Game.Tools.Shared
{
    /// <summary>A snap candidate with a caller-defined kind and its screen distance from the cursor.</summary>
    public readonly struct SnapCandidate
    {
        /// <summary>Caller-defined kind (compared against the kind order).</summary>
        public readonly int Kind;
        /// <summary>Caller-defined id.</summary>
        public readonly int Id;
        /// <summary>Screen distance in pixels.</summary>
        public readonly float Distance;

        /// <summary>Creates a candidate.</summary>
        public SnapCandidate(int kind, int id, float distance)
        {
            Kind = kind; Id = id; Distance = distance;
        }
    }

    /// <summary>Chooses the best snap candidate.</summary>
    public static class SnapPicker
    {
        /// <summary>
        /// Picks the candidate whose kind appears earliest in <paramref name="kindOrder"/>, nearest first within a kind.
        /// Candidates beyond <paramref name="pixelLimit"/> or with a kind not in the order are ignored.
        /// Returns false when snapping is off or nothing qualifies.
        /// </summary>
        public static bool TryPick(IReadOnlyList<SnapCandidate> candidates, IReadOnlyList<int> kindOrder,
            float pixelLimit, bool snappingOn, out SnapCandidate result)
        {
            result = default;
            if (!snappingOn || candidates == null || kindOrder == null) return false;
            int bestRank = int.MaxValue;
            bool found = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.Distance > pixelLimit) continue;
                int rank = -1;
                for (int k = 0; k < kindOrder.Count; k++)
                    if (kindOrder[k] == c.Kind) { rank = k; break; }
                if (rank < 0) continue;
                if (!found || rank < bestRank || (rank == bestRank && c.Distance < result.Distance))
                {
                    found = true; bestRank = rank; result = c;
                }
            }
            return found;
        }
    }
}
