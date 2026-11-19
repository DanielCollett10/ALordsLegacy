using System;
using System.Collections.Generic;
using System.Text;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Homes
{
    /// <summary>One line of the village level count: a level, its name and how many families are at it.</summary>
    public sealed class LevelCountLine
    {
        /// <summary>Creates a line.</summary>
        public LevelCountLine(int level, string name, int families)
        {
            Level = level; Name = name ?? string.Empty; Families = families;
        }

        /// <summary>The household level.</summary>
        public int Level { get; }

        /// <summary>Display name of the level.</summary>
        public string Name { get; }

        /// <summary>Families currently at this level.</summary>
        public int Families { get; }

        /// <summary>"Level 2 Settled household: 3".</summary>
        public string Text => "Level " + Level + " " + Name + ": " + Families;
    }

    /// <summary>
    /// Village counts of households by level, one line per defined level (HL-26). Read only: derived from
    /// <c>Family.Level</c> each time, never stored. Counts every family in the village, including those in passage.
    /// </summary>
    public sealed class HouseholdLevelCounts
    {
        private readonly IReadOnlyList<LevelCountLine> _lines;

        private HouseholdLevelCounts(IReadOnlyList<LevelCountLine> lines) { _lines = lines; }

        /// <summary>One line per defined level, ascending.</summary>
        public IReadOnlyList<LevelCountLine> Lines => _lines;

        /// <summary>Families across all levels.</summary>
        public int Total
        {
            get
            {
                int n = 0;
                foreach (LevelCountLine l in _lines) n += l.Families;
                return n;
            }
        }

        /// <summary>Families at one level, or 0 for an undefined level.</summary>
        public int CountAt(int level)
        {
            foreach (LevelCountLine l in _lines) if (l.Level == level) return l.Families;
            return 0;
        }

        /// <summary>Tooltip text: one "Level N Name: count" line per level, joined with new lines.</summary>
        public string TooltipText
        {
            get
            {
                var sb = new StringBuilder();
                foreach (LevelCountLine l in _lines)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(l.Text);
                }
                return sb.ToString();
            }
        }

        /// <summary>Counts the village's families by level.</summary>
        public static HouseholdLevelCounts Of(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var counts = new Dictionary<int, int>();
            foreach (Family f in world.People.Families)
                counts[f.Level] = counts.TryGetValue(f.Level, out int n) ? n + 1 : 1;
            var lines = new List<LevelCountLine>();
            foreach (HouseholdLevelDef d in HouseholdLevels.All)
                lines.Add(new LevelCountLine(d.Level, d.Name, counts.TryGetValue(d.Level, out int c) ? c : 0));
            return new HouseholdLevelCounts(lines);
        }
    }
}
