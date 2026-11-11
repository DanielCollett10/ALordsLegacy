using System;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    public class RoadsTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static CatmullRomSampler Sampler() => new CatmullRomSampler(new SimTuning());

        [Test]
        public void DirtPath_MatchesTuningDoc()
        {
            RoadTypeDef t = RoadTypes.Find("road.dirt_path");
            Assert.That(t, Is.Not.Null);
            Assert.That(t.WidthM, Is.EqualTo(2.5));
            Assert.That(t.MaxGrade, Is.EqualTo(0.15));
            Assert.That(t.SpeedMultiplier, Is.EqualTo(1.25)); // ROAD-10-A, decision 0023 (no tuning value for this)
            Assert.That(t.VisualId, Is.EqualTo("visual.road.dirt_path"));
            Assert.That(RoadTypes.Find("nope"), Is.Null);
        }

        [Test]
        public void TwoPoints_GiveStraightLine()
        {
            var s = Sampler().Sample(new[] { M(0, 0), M(10, 0) });
            Assert.That(s.Count, Is.EqualTo(11));
            foreach (var p in s) Assert.That(p.Z, Is.EqualTo(0));
            Assert.That(s[0], Is.EqualTo(M(0, 0)));
            Assert.That(s[s.Count - 1], Is.EqualTo(M(10, 0)));
        }

        [Test]
        public void TwoPoints_Diagonal_AllSamplesCollinear()
        {
            var a = M(0, 0); var b = M(30, 40);
            var s = Sampler().Sample(new[] { a, b });
            Assert.That(s.Count, Is.EqualTo(51));
            foreach (var p in s) Assert.That(Math.Abs(PointMm.Cross(a, b, p)), Is.LessThan(100000));
        }

        [Test]
        public void Samples_PassThroughEveryControlPoint_AndEndExactly()
        {
            var cp = new[] { M(0, 0), M(10, 5), M(20, -3), M(35, 8), M(40, 0) };
            var s = Sampler().Sample(cp);
            foreach (var c in cp) Assert.That(s, Does.Contain(c));
            Assert.That(s[0], Is.EqualTo(cp[0]));
            Assert.That(s[s.Count - 1], Is.EqualTo(cp[cp.Length - 1]));
        }

        [Test]
        public void Samples_AreAboutOneMetreApart()
        {
            var s = Sampler().Sample(new[] { M(0, 0), M(10, 5), M(20, -3), M(35, 8), M(40, 0) });
            for (int i = 1; i < s.Count; i++)
            {
                double d = s[i].DistanceTo(s[i - 1]);
                Assert.That(d, Is.InRange(600, 1500), "step " + i);
            }
        }

        [Test]
        public void Sampling_IsDeterministic()
        {
            var cp = new[] { M(0, 0), M(10, 5), M(20, -3), M(35, 8) };
            var a = Sampler().Sample(cp);
            var b = Sampler().Sample(cp);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void ClosePoint_InTheMiddle_IsDropped()
        {
            var r = ControlPointFilter.Clean(new[] { M(0, 0), M(0.5, 0), M(5, 0), M(10, 0) }, 1);
            Assert.That(r, Is.EqualTo(new[] { M(0, 0), M(5, 0), M(10, 0) }));
        }

        [Test]
        public void ClosePoint_ExactlyAtMinSpacing_IsKept()
        {
            var r = ControlPointFilter.Clean(new[] { M(0, 0), M(1, 0), M(5, 0) }, 1);
            Assert.That(r.Count, Is.EqualTo(3));
        }

        [Test]
        public void LastPoint_WithinMinSpacing_ReplacesPrevious()
        {
            var r = ControlPointFilter.Clean(new[] { M(0, 0), M(5, 0), M(10, 0), M(10.4, 0) }, 1);
            Assert.That(r, Is.EqualTo(new[] { M(0, 0), M(5, 0), M(10.4, 0) }));
        }

        [Test]
        public void LastPoint_WithinMinSpacingOfFirstOnly_IsAppendedNotDropped()
        {
            var r = ControlPointFilter.Clean(new[] { M(0, 0), M(0.3, 0) }, 1);
            Assert.That(r, Is.EqualTo(new[] { M(0, 0), M(0.3, 0) }));
        }

        [Test]
        public void LastPoint_EqualToFirst_GivesSinglePoint()
        {
            var r = ControlPointFilter.Clean(new[] { M(0, 0), M(0, 0) }, 1);
            Assert.That(r.Count, Is.EqualTo(1));
        }

        [Test]
        public void EmptyAndSinglePointInput_ReturnWithoutCrashing()
        {
            Assert.That(Sampler().Sample(new PointMm[0]).Count, Is.EqualTo(0));
            Assert.That(Sampler().Sample(new[] { M(1, 1) }).Count, Is.EqualTo(1));
        }

        [Test]
        public void Sample_AppliesCleaningRules_EndsAtLastClickedPoint()
        {
            var s = Sampler().Sample(new[] { M(0, 0), M(0.2, 0), M(10, 0), M(10.5, 0) });
            Assert.That(s[0], Is.EqualTo(M(0, 0)));
            Assert.That(s[s.Count - 1], Is.EqualTo(M(10.5, 0)));
            Assert.That(s, Does.Not.Contain(M(10, 0)));
            Assert.That(s, Does.Not.Contain(M(0.2, 0)));
        }

        [Test]
        public void CollinearControlPoints_StayOnTheLine()
        {
            var s = Sampler().Sample(new[] { M(0, 0), M(5, 0), M(12, 0) });
            foreach (var p in s) Assert.That(Math.Abs(p.Z), Is.LessThan(2));
        }

        [Test]
        public void SpacingComesFromTuning()
        {
            var t = new SimTuning { RoadSampleSpacingM = 2 };
            var s = new CatmullRomSampler(t).Sample(new[] { M(0, 0), M(10, 0) });
            Assert.That(s.Count, Is.EqualTo(6));
        }
    }
}
