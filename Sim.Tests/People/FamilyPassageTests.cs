using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.People
{
    /// <summary>Family.Passage and Family.IsInPassage (T-498, decision 0045, GR-13).</summary>
    public class FamilyPassageTests
    {
        [Test]
        public void NewFamily_PassageIsNone_AndIsNotInPassage()
        {
            var w = new SimWorld(new SimTuning());
            Family f = w.People.AddFamily(1, "Ash", 0, 0);
            Assert.AreEqual(FamilyPassage.None, f.Passage);
            Assert.IsFalse(f.IsInPassage);
        }

        [Test]
        public void RestoredFamily_WithoutPassage_DefaultsToNone()
        {
            var w = new SimWorld(new SimTuning());
            w.People.RestoreFamily(2, "Oak", 0, 0, null, new int[0]);
            Family f = w.People.FindFamily(2);
            Assert.AreEqual(FamilyPassage.None, f.Passage);
            Assert.IsFalse(f.IsInPassage);
        }

        [Test]
        public void Arriving_IsInPassage()
        {
            var w = new SimWorld(new SimTuning());
            Family f = w.People.AddFamily(3, "Elm", 0, 0);
            f.Passage = FamilyPassage.Arriving;
            Assert.IsTrue(f.IsInPassage);
        }

        [Test]
        public void Leaving_IsInPassage_AndNoneIsNot()
        {
            var w = new SimWorld(new SimTuning());
            Family f = w.People.AddFamily(4, "Birch", 0, 0);
            f.Passage = FamilyPassage.Leaving;
            Assert.IsTrue(f.IsInPassage);
            f.Passage = FamilyPassage.None;
            Assert.IsFalse(f.IsInPassage);
        }
    }
}
