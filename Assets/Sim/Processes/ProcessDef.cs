using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Processes
{
    /// <summary>
    /// An ordered chain of stages as data (woodcutting.md TMB-01..03). Stages can be switched off; a disabled stage's
    /// time is added to the next enabled stage, so the total time does not depend on which stages are enabled.
    /// Immutable.
    /// </summary>
    public sealed class ProcessDef
    {
        private readonly StageDef[] _stages;

        /// <summary>Stable id, e.g. "process.timber".</summary>
        public string Id { get; }
        /// <summary>All stages in order, enabled or not. Never empty.</summary>
        public IReadOnlyList<StageDef> Stages => _stages;

        /// <summary>
        /// Builds a process. Throws on an empty id, no stages, a null stage, duplicate stage ids, or no enabled stage.
        /// </summary>
        public ProcessDef(string id, IEnumerable<StageDef> stages)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Process id required", nameof(id));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            _stages = new List<StageDef>(stages).ToArray();
            if (_stages.Length == 0) throw new ArgumentException("A process needs at least one stage", nameof(stages));
            bool anyEnabled = false;
            for (int i = 0; i < _stages.Length; i++)
            {
                if (_stages[i] == null) throw new ArgumentException("Null stage", nameof(stages));
                for (int j = 0; j < i; j++)
                    if (_stages[j].Id == _stages[i].Id) throw new ArgumentException("Duplicate stage id " + _stages[i].Id, nameof(stages));
                anyEnabled |= _stages[i].Enabled;
            }
            if (!anyEnabled) throw new ArgumentException("A process needs at least one enabled stage", nameof(stages));
            Id = id;
        }

        /// <summary>The stage with this id, or null if there is none.</summary>
        public StageDef FindStage(string stageId)
        {
            for (int i = 0; i < _stages.Length; i++) if (_stages[i].Id == stageId) return _stages[i];
            return null;
        }

        /// <summary>
        /// A copy of this process with one stage's enable flag set. Throws if the stage is unknown or the change
        /// would leave no enabled stage.
        /// </summary>
        public ProcessDef WithStageEnabled(string stageId, bool enabled)
        {
            if (FindStage(stageId) == null) throw new ArgumentException("Unknown stage " + stageId, nameof(stageId));
            var copy = new StageDef[_stages.Length];
            for (int i = 0; i < _stages.Length; i++)
                copy[i] = _stages[i].Id == stageId ? _stages[i].WithEnabled(enabled) : _stages[i];
            return new ProcessDef(Id, copy);
        }

        /// <summary>
        /// The enabled stages in order, for <paramref name="logCount"/> items. A per-item stage costs
        /// DurationMinutes × logCount; others cost DurationMinutes. The time of each disabled stage (its own
        /// per-item time included) is added to the next enabled stage. Trailing disabled stages have no next enabled
        /// stage, so their time goes to the last enabled stage before them. Summed minutes are the same whatever is
        /// enabled.
        /// </summary>
        /// <param name="logCount">Items processed (logs). Must be >= 0.</param>
        public IReadOnlyList<EffectiveStage> EffectiveStages(int logCount)
        {
            if (logCount < 0) throw new ArgumentOutOfRangeException(nameof(logCount), "Item count must be >= 0");
            var result = new List<EffectiveStage>();
            int pending = 0;
            for (int i = 0; i < _stages.Length; i++)
            {
                StageDef s = _stages[i];
                int minutes = s.PerItem ? s.DurationMinutes * logCount : s.DurationMinutes;
                if (!s.Enabled)
                {
                    pending += minutes;
                    continue;
                }
                result.Add(new EffectiveStage(s.Id, minutes + pending, s.Heavy, s.AnimationKey));
                pending = 0;
            }
            if (pending > 0)
            {
                // Trailing disabled stages: fold them into the last enabled stage.
                EffectiveStage last = result[result.Count - 1];
                result[result.Count - 1] = new EffectiveStage(last.StageId, last.Minutes + pending, last.Heavy, last.AnimationKey);
            }
            return result;
        }
    }
}
