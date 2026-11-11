using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Processes;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class ProcessDefTests
    {
        private static int Total(IReadOnlyList<EffectiveStage> stages) => stages.Sum(s => s.Minutes);

        private static StageDef Stage(string id, int minutes, bool perItem = false, bool enabled = true) =>
            new StageDef(id, minutes, perItem, false, enabled, "anim." + id);

        [Test]
        public void Timber_Data_FellDelimbBuckMatchTuning()
        {
            ProcessDef timber = ProcessDefs.Timber;
            Assert.AreEqual("process.timber", timber.Id);
            Assert.AreEqual(3, timber.Stages.Count);

            StageDef fell = timber.Stages[0];
            Assert.AreEqual(90, fell.DurationMinutes);
            Assert.IsFalse(fell.PerItem);
            Assert.IsTrue(fell.Enabled);
            Assert.IsTrue(fell.Heavy);
            Assert.AreEqual("fell", fell.AnimationKey);

            StageDef delimb = timber.Stages[1];
            Assert.AreEqual(30, delimb.DurationMinutes);
            Assert.IsFalse(delimb.Enabled, "Delimb is off by default (TMB-02)");

            StageDef buck = timber.Stages[2];
            Assert.AreEqual(15, buck.DurationMinutes);
            Assert.IsTrue(buck.PerItem, "Buck is per log");
            Assert.IsTrue(buck.Enabled);
            Assert.IsTrue(buck.Heavy);
            Assert.AreEqual("buck", buck.AnimationKey);
        }

        [Test]
        public void DelimbOff_FellThenBuckWith30PlusFifteenPerLog()
        {
            for (int n = 0; n <= 5; n++)
            {
                IReadOnlyList<EffectiveStage> stages = ProcessDefs.Timber.EffectiveStages(n);
                Assert.AreEqual(2, stages.Count, "Delimb off gives two stages (n=" + n + ")");
                Assert.AreEqual(ProcessDefs.FellStageId, stages[0].StageId);
                Assert.AreEqual(90, stages[0].Minutes, "Fell is 90 whatever the log count (n=" + n + ")");
                Assert.AreEqual(ProcessDefs.BuckStageId, stages[1].StageId);
                Assert.AreEqual(30 + 15 * n, stages[1].Minutes, "Buck is 30 + 15 x n (n=" + n + ")");
            }
        }

        [Test]
        public void DelimbOn_GivesThreeStages()
        {
            ProcessDef on = ProcessDefs.Timber.WithStageEnabled(ProcessDefs.DelimbStageId, true);
            IReadOnlyList<EffectiveStage> stages = on.EffectiveStages(4);
            Assert.AreEqual(3, stages.Count);
            Assert.AreEqual(ProcessDefs.FellStageId, stages[0].StageId);
            Assert.AreEqual(90, stages[0].Minutes);
            Assert.AreEqual(ProcessDefs.DelimbStageId, stages[1].StageId);
            Assert.AreEqual(30, stages[1].Minutes, "Delimb on keeps its own 30 minutes, nothing folded in");
            Assert.AreEqual(ProcessDefs.BuckStageId, stages[2].StageId);
            Assert.AreEqual(15 * 4, stages[2].Minutes);
        }

        [Test]
        public void TotalTime_SameWhetherDelimbOnOrOff_ForAnyLogCount()
        {
            ProcessDef on = ProcessDefs.Timber.WithStageEnabled(ProcessDefs.DelimbStageId, true);
            for (int n = 0; n <= 12; n++)
            {
                Assert.AreEqual(Total(ProcessDefs.Timber.EffectiveStages(n)), Total(on.EffectiveStages(n)),
                    "Total minutes equal with and without delimb (n=" + n + ")");
                Assert.AreEqual(90 + 30 + 15 * n, Total(on.EffectiveStages(n)));
            }
        }

        [Test]
        public void Effective_HeavyAndAnimationKeyComeFromEnabledStage()
        {
            IReadOnlyList<EffectiveStage> stages = ProcessDefs.Timber.EffectiveStages(2);
            Assert.IsTrue(stages[0].Heavy);
            Assert.AreEqual("fell", stages[0].AnimationKey);
            Assert.IsTrue(stages[1].Heavy);
            Assert.AreEqual("buck", stages[1].AnimationKey);
        }

        [Test]
        public void Effective_ZeroLogs_BuckIsOnlyTheFoldedDelimbTime()
        {
            IReadOnlyList<EffectiveStage> stages = ProcessDefs.Timber.EffectiveStages(0);
            Assert.AreEqual(30, stages[1].Minutes, "No logs: buck keeps only the 30 folded-in minutes");
        }

        [Test]
        public void Effective_NegativeLogCount_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => ProcessDefs.Timber.EffectiveStages(-1));

        [Test]
        public void Effective_DisabledFirstStage_FoldsIntoNextEnabledStage()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 10, enabled: false), Stage("b", 5) });
            IReadOnlyList<EffectiveStage> stages = p.EffectiveStages(0);
            Assert.AreEqual(1, stages.Count);
            Assert.AreEqual("b", stages[0].StageId);
            Assert.AreEqual(15, stages[0].Minutes);
        }

        [Test]
        public void Effective_TrailingDisabledStage_FoldsIntoLastEnabledStage()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 5), Stage("b", 7, enabled: false) });
            IReadOnlyList<EffectiveStage> stages = p.EffectiveStages(0);
            Assert.AreEqual(1, stages.Count);
            Assert.AreEqual("a", stages[0].StageId);
            Assert.AreEqual(12, stages[0].Minutes, "A trailing disabled stage goes to the last enabled stage before it");
        }

        [Test]
        public void Effective_TwoDisabledStagesInARow_BothFoldIntoNextEnabled()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 1), Stage("b", 2, enabled: false),
                Stage("c", 3, enabled: false), Stage("d", 4) });
            IReadOnlyList<EffectiveStage> stages = p.EffectiveStages(0);
            Assert.AreEqual(2, stages.Count);
            Assert.AreEqual(1, stages[0].Minutes);
            Assert.AreEqual(4 + 2 + 3, stages[1].Minutes, "Both disabled stages fold into d");
        }

        [Test]
        public void Effective_DisabledPerItemStage_ItsPerLogTimeFoldsIntoNextEnabled()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 10, perItem: true, enabled: false), Stage("b", 5) });
            IReadOnlyList<EffectiveStage> stages = p.EffectiveStages(3);
            Assert.AreEqual(5 + 10 * 3, stages[0].Minutes, "Disabled per-item stage contributes 10 x 3 to b");
        }

        [Test]
        public void Effective_AllStagesEnabled_NoFolding()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 1), Stage("b", 2, perItem: true), Stage("c", 3) });
            IReadOnlyList<EffectiveStage> stages = p.EffectiveStages(5);
            Assert.AreEqual(new[] { 1, 10, 3 }, stages.Select(s => s.Minutes).ToArray());
        }

        [Test]
        public void WithStageEnabled_ReturnsCopy_OriginalUnchanged()
        {
            ProcessDef on = ProcessDefs.Timber.WithStageEnabled(ProcessDefs.DelimbStageId, true);
            Assert.IsFalse(ProcessDefs.Timber.FindStage(ProcessDefs.DelimbStageId).Enabled);
            Assert.IsTrue(on.FindStage(ProcessDefs.DelimbStageId).Enabled);
        }

        [Test]
        public void WithStageEnabled_UnknownStage_Throws() =>
            Assert.Throws<ArgumentException>(() => ProcessDefs.Timber.WithStageEnabled("stage.nope", true));

        [Test]
        public void WithStageEnabled_DisablingLastEnabledStage_Throws()
        {
            var p = new ProcessDef("p", new[] { Stage("a", 1) });
            Assert.Throws<ArgumentException>(() => p.WithStageEnabled("a", false));
        }

        [Test]
        public void Def_NoStages_Throws() =>
            Assert.Throws<ArgumentException>(() => new ProcessDef("p", new StageDef[0]));

        [Test]
        public void Def_AllStagesDisabled_Throws() =>
            Assert.Throws<ArgumentException>(() => new ProcessDef("p", new[] { Stage("a", 1, enabled: false) }));

        [Test]
        public void Def_DuplicateStageIds_Throws() =>
            Assert.Throws<ArgumentException>(() => new ProcessDef("p", new[] { Stage("a", 1), Stage("a", 2) }));

        [Test]
        public void Def_NullStage_Throws() =>
            Assert.Throws<ArgumentException>(() => new ProcessDef("p", new StageDef[] { null }));

        [Test]
        public void Def_EmptyId_Throws() =>
            Assert.Throws<ArgumentException>(() => new ProcessDef("", new[] { Stage("a", 1) }));

        [Test]
        public void StageDef_NegativeDuration_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new StageDef("s", -1, false, false, true, "k"));

        [Test]
        public void StageDef_ZeroDuration_Allowed()
        {
            var s = new StageDef("s", 0, false, false, true, "k");
            Assert.AreEqual(0, s.DurationMinutes);
        }

        [Test]
        public void StageDef_EmptyAnimationKey_Throws() =>
            Assert.Throws<ArgumentException>(() => new StageDef("s", 1, false, false, true, ""));

        [Test]
        public void ProcessDefs_Find_KnownIdReturnsTimber_UnknownIdReturnsNull()
        {
            Assert.AreSame(ProcessDefs.Timber, ProcessDefs.Find("process.timber"));
            Assert.IsNull(ProcessDefs.Find("process.nope"));
        }
    }
}
