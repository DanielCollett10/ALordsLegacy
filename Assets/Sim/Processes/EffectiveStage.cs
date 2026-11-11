namespace LordsLegacy.Sim.Processes
{
    /// <summary>A stage as it runs for a given item count: its total minutes, with disabled stages folded in.</summary>
    public readonly struct EffectiveStage
    {
        /// <summary>The id of the enabled stage that runs.</summary>
        public string StageId { get; }
        /// <summary>Total game minutes, including any folded-in time from disabled stages.</summary>
        public int Minutes { get; }
        /// <summary>Whether the work is heavy (from the enabled stage).</summary>
        public bool Heavy { get; }
        /// <summary>Animation key (from the enabled stage).</summary>
        public string AnimationKey { get; }

        /// <summary>Builds an effective stage.</summary>
        public EffectiveStage(string stageId, int minutes, bool heavy, string animationKey)
        {
            StageId = stageId; Minutes = minutes; Heavy = heavy; AnimationKey = animationKey;
        }
    }
}
