using System;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>Workshop recipe data model (T-557, decision 0051): hewing and sawing stay single-input recipes.</summary>
    public class WorkshopRecipeTests
    {
        [Test]
        public void Hewing_IsOneLogToOneBeam_OxCarried()
        {
            WorkshopDef h = WorkshopDef.Hewing(new SimTuning());
            Assert.AreEqual(1, h.Inputs.Count);
            Assert.AreSame(Items.Log, h.Inputs[0].Item);
            Assert.AreEqual(1, h.Inputs[0].Count);
            Assert.AreEqual(CarryClass.Ox, h.Inputs[0].Carry);
            Assert.AreEqual(1, h.Outputs.Count);
            Assert.AreSame(Items.Beam, h.Outputs[0].Item);
            Assert.AreEqual(1, h.Outputs[0].Count);
            Assert.AreEqual(3, h.BatchesPerFamilyDay);
            Assert.IsTrue(h.IsSingleInput);
        }

        [Test]
        public void Sawing_IsOneBeamToThreePlanks()
        {
            WorkshopDef s = WorkshopDef.Sawing(new SimTuning());
            Assert.AreSame(Items.Beam, s.Inputs[0].Item);
            Assert.AreEqual(1, s.Inputs[0].Count);
            Assert.AreSame(Items.Plank, s.Outputs[0].Item);
            Assert.AreEqual(3, s.Outputs[0].Count);
            Assert.AreEqual(3, s.BatchesPerFamilyDay);
        }

        [Test]
        public void EmptyInputRecipe_IsBuildable_ButRefusedWithClearMessage()
        {
            var d = new WorkshopDef("workshop.test", new RecipeInput[0], new[] { new RecipeOutput(Items.Beam, 1) }, 1, 5, 5, 1);
            Assert.AreEqual(0, d.Inputs.Count);
            Assert.IsNull(d.Input);
            var ex = Assert.Throws<NotSupportedException>(() => WorkshopDef.RequireSingleInput(d));
            StringAssert.Contains("cw-5b", ex.Message);
        }

        [Test]
        public void TwoInputRecipe_IsRefusedByWorkshopStatus()
        {
            var d = new WorkshopDef("workshop.test2",
                new[] { new RecipeInput(Items.Log, 1, CarryClass.Ox), new RecipeInput(Items.Beam, 2, CarryClass.Hand) },
                new[] { new RecipeOutput(Items.Plank, 1) }, 1, 5, 5, 1);
            Assert.Throws<NotSupportedException>(() => WorkshopStatus.Of(null, null, d));
        }

        [Test]
        public void Recipe_RejectsNoOutputsAndZeroCounts()
        {
            Assert.Throws<ArgumentException>(() => new WorkshopDef("x", new RecipeInput[0], new RecipeOutput[0], 1, 1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeInput(Items.Log, 0, CarryClass.Ox));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeOutput(Items.Beam, 0));
        }
    }
}
