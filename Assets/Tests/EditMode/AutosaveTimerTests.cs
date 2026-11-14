using System;
using LordsLegacy.Game;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    /// <summary>EditMode tests for the autosave countdown (saving.md SV-11).</summary>
    public sealed class AutosaveTimerTests
    {
        [Test]
        public void DueOnlyAfterTheInterval()
        {
            var t = new AutosaveTimer(30);
            t.Advance(29.9);
            Assert.IsFalse(t.IsDue);
            t.Advance(0.1);
            Assert.IsTrue(t.IsDue);
        }

        [Test]
        public void RestartClearsTheCount()
        {
            var t = new AutosaveTimer(1);
            t.Advance(5);
            Assert.IsTrue(t.IsDue);
            t.Restart();
            Assert.IsFalse(t.IsDue);
            Assert.AreEqual(0, t.ElapsedSeconds);
        }

        [Test]
        public void NegativeAndNonFiniteStepsAreIgnored()
        {
            var t = new AutosaveTimer(10);
            t.Advance(-5);
            t.Advance(double.NaN);
            t.Advance(double.PositiveInfinity);
            Assert.AreEqual(0, t.ElapsedSeconds);
        }

        [Test]
        public void NonPositiveIntervalIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutosaveTimer(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AutosaveTimer(-1));
        }
    }
}
