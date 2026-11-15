using LordsLegacy.Game;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Operations;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>Enabled and tooltip decision for cost-gated build bar entries (T-388, BT-02, BT-03).</summary>
    public class BuildEntryStateTests
    {
        [Test]
        public void BelowCost_IsDisabledWithNeedsTooltip()
        {
            var s = BuildEntryState.Of(8, 5);
            Assert.IsFalse(s.Enabled);
            Assert.AreEqual("Needs 8 logs (have 5)", s.Tooltip);
        }

        [Test]
        public void OneBelowCost_IsDisabled()
        {
            var s = BuildEntryState.Of(8, 7);
            Assert.IsFalse(s.Enabled);
            Assert.AreEqual("Needs 8 logs (have 7)", s.Tooltip);
        }

        [Test]
        public void EqualToCost_IsEnabledWithEmptyTooltip()
        {
            var s = BuildEntryState.Of(8, 8);
            Assert.IsTrue(s.Enabled);
            Assert.AreEqual("", s.Tooltip);
        }

        [Test]
        public void AboveCost_IsEnabledWithEmptyTooltip()
        {
            var s = BuildEntryState.Of(8, 40);
            Assert.IsTrue(s.Enabled);
            Assert.AreEqual("", s.Tooltip);
        }

        [Test]
        public void ZeroCost_IsEnabledWithNoLogs()
        {
            var s = BuildEntryState.Of(0, 0);
            Assert.IsTrue(s.Enabled);
        }

        [Test]
        public void WoodcutterEntry_DeclaresMinimumCostSource()
        {
            ToolEntry woodcutter = null;
            foreach (var e in ToolEntry.Defaults())
                if (e.Id == "woodcutter") woodcutter = e;
            Assert.IsNotNull(woodcutter);
            Assert.IsNotNull(woodcutter.CostLogs);
            Assert.AreEqual(BuildCost.MinimumFor(OperationTypes.Woodcutting), woodcutter.CostLogs());
            Assert.AreEqual(8, woodcutter.CostLogs());
        }

        [Test]
        public void EntriesWithoutCost_HaveNoCostSource()
        {
            foreach (var e in ToolEntry.Defaults())
                if (e.Id == "road") Assert.IsNull(e.CostLogs);
        }
    }
}
