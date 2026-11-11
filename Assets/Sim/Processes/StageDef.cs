using System;

namespace LordsLegacy.Sim.Processes
{
    /// <summary>
    /// One stage of a process as data (decision 0005 style): id, base duration, whether the duration repeats per item,
    /// whether the work is heavy (POP-A3), whether the stage is enabled, and the animation key the view plays.
    /// Immutable; use <see cref="WithEnabled"/> to get a copy with another enable flag.
    /// </summary>
    public sealed class StageDef
    {
        /// <summary>Stable id, e.g. "stage.fell".</summary>
        public string Id { get; }
        /// <summary>Game minutes. For a per-item stage this is the time per item; otherwise the time once.</summary>
        public int DurationMinutes { get; }
        /// <summary>True when the duration is multiplied by the item count (e.g. bucking, per log).</summary>
        public bool PerItem { get; }
        /// <summary>True when the work is heavy (POP-A3).</summary>
        public bool Heavy { get; }
        /// <summary>When false the stage is skipped and its time goes to the next enabled stage (TMB-02).</summary>
        public bool Enabled { get; }
        /// <summary>Key the view uses to pick an animation. Never null.</summary>
        public string AnimationKey { get; }

        /// <summary>Builds a stage. Throws on an empty id or animation key, or a negative duration.</summary>
        public StageDef(string id, int durationMinutes, bool perItem, bool heavy, bool enabled, string animationKey)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Stage id required", nameof(id));
            if (string.IsNullOrEmpty(animationKey)) throw new ArgumentException("Animation key required", nameof(animationKey));
            if (durationMinutes < 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration must be >= 0");
            Id = id;
            DurationMinutes = durationMinutes;
            PerItem = perItem;
            Heavy = heavy;
            Enabled = enabled;
            AnimationKey = animationKey;
        }

        /// <summary>A copy of this stage with the enable flag set as given.</summary>
        public StageDef WithEnabled(bool enabled) =>
            new StageDef(Id, DurationMinutes, PerItem, Heavy, enabled, AnimationKey);
    }
}
