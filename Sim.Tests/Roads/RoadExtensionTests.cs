using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Roads
{
    /// <summary>RD-16: a road started or ended on a dead-end node meets the old road flush.</summary>
    public class RoadExtensionTests
    {
        private const string Type = RoadTypes.DirtPathId;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);
        private static SimWorld Flat() => new SimWorld(new SimTuning());

        private static RoadSegment Draw(SimWorld w, params PointMm[] cps)
        {
            CommandResult r = w.Execute(new DrawRoad(cps, Type));
            Assert.That(r.IsAccepted, Is.True, r.Text);
            return w.Roads.GetSegment((int)r.Created[0]);
        }

        // Angle in degrees between the step a0->a1 and the step b0->b1.
        private static double AngleDeg(PointMm a0, PointMm a1, PointMm b0, PointMm b1)
        {
            double ax = a1.X - a0.X, az = a1.Z - a0.Z, bx = b1.X - b0.X, bz = b1.Z - b0.Z;
            double dot = (ax * bx + az * bz) / (Math.Sqrt(ax * ax + az * az) * Math.Sqrt(bx * bx + bz * bz));
            return Math.Acos(Math.Max(-1, Math.Min(1, dot))) * 180 / Math.PI;
        }

        private static bool SameSamples(IReadOnlyList<PointMm> a, IReadOnlyList<PointMm> b) => a.SequenceEqual(b);

        [Test]
        public void Sampler_NoTangents_SamplesIdenticalToPlainCall()
        {
            var s = new CatmullRomSampler(new SimTuning());
            var cps = new[] { M(10, 10), M(40, 30), M(80, 20), M(120, 40) };
            Assert.That(SameSamples(s.Sample(cps, null, null), s.Sample(cps)), Is.True);
        }

        [Test]
        public void Sampler_StartTangent_FirstStepFollowsTangent()
        {
            var s = new CatmullRomSampler(new SimTuning());
            var cps = new[] { M(10, 10), M(60, 20), M(110, 10) };
            var t = new RoadTangent(0, 1);
            IReadOnlyList<PointMm> pts = s.Sample(cps, t, null);
            Assert.That(pts[0], Is.EqualTo(cps[0]));
            Assert.That(pts[pts.Count - 1], Is.EqualTo(cps[2]));
            Assert.That(AngleDeg(pts[0], pts[1], M(0, 0), M(0, 1)), Is.LessThan(5));
        }

        [Test]
        public void Sampler_EndTangent_LastStepFollowsTangent()
        {
            var s = new CatmullRomSampler(new SimTuning());
            var cps = new[] { M(10, 10), M(60, 10), M(110, 10) };
            var t = new RoadTangent(1, 0);
            IReadOnlyList<PointMm> pts = s.Sample(cps, null, t);
            Assert.That(AngleDeg(pts[pts.Count - 2], pts[pts.Count - 1], M(0, 0), M(1, 0)), Is.LessThan(1));
        }

        [Test]
        public void Sampler_TwoPointsBothTangents_StaysBetweenEndsAndKeepsEndpoints()
        {
            var s = new CatmullRomSampler(new SimTuning());
            IReadOnlyList<PointMm> pts = s.Sample(new[] { M(10, 10), M(50, 10) }, new RoadTangent(1, 0), new RoadTangent(1, 0));
            Assert.That(pts.Count, Is.GreaterThan(2));
            Assert.That(pts[0], Is.EqualTo(M(10, 10)));
            Assert.That(pts[pts.Count - 1], Is.EqualTo(M(50, 10)));
        }

        [Test]
        public void Tangent_ZeroDirection_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RoadTangent(0, 0));
        }

        [Test]
        public void Validate_StartOnDeadEndNodeB_FirstStepWithinOneDegreeOfOldLastStep()
        {
            SimWorld w = Flat();
            RoadSegment old = Draw(w, M(10, 10), M(40, 30), M(70, 30));
            IReadOnlyList<PointMm> os = old.Samples;
            RoadPreview p = RoadValidator.Validate(w, new[] { M(70, 30), M(110, 36), M(150, 30) }, Type);
            Assert.That(p.IsValid, Is.True, p.ReasonText);
            Assert.That(p.StartTangent.HasValue, Is.True);
            Assert.That(AngleDeg(os[os.Count - 2], os[os.Count - 1], p.Samples[0], p.Samples[1]), Is.LessThan(1));
        }

        [Test]
        public void Validate_StartOnDeadEndNodeA_LeavesAwayFromOldRoad()
        {
            SimWorld w = Flat();
            RoadSegment old = Draw(w, M(70, 30), M(100, 30), M(130, 34));
            IReadOnlyList<PointMm> os = old.Samples;
            RoadPreview p = RoadValidator.Validate(w, new[] { M(70, 30), M(40, 33), M(10, 30) }, Type);
            Assert.That(p.IsValid, Is.True, p.ReasonText);
            // Old road arrives at its start node going from os[1] to os[0]; the new road continues that way.
            Assert.That(AngleDeg(os[1], os[0], p.Samples[0], p.Samples[1]), Is.LessThan(1));
        }

        [Test]
        public void Validate_EndOnDeadEnd_LastStepContinuesIntoOldRoad()
        {
            SimWorld w = Flat();
            RoadSegment old = Draw(w, M(100, 30), M(130, 30), M(160, 40));
            IReadOnlyList<PointMm> os = old.Samples;
            RoadPreview p = RoadValidator.Validate(w, new[] { M(30, 36), M(60, 30), M(100, 30) }, Type);
            Assert.That(p.IsValid, Is.True, p.ReasonText);
            Assert.That(p.EndTangent.HasValue, Is.True);
            int n = p.Samples.Count;
            Assert.That(AngleDeg(p.Samples[n - 2], p.Samples[n - 1], os[0], os[1]), Is.LessThan(1));
        }

        [Test]
        public void DrawRoad_FromDeadEnd_StoresTangentAndResamplesIdentically_OldSegmentUntouched()
        {
            SimWorld w = Flat();
            RoadSegment old = Draw(w, M(10, 10), M(40, 30), M(70, 30));
            int oldId = old.Id;
            PointMm[] oldSamples = old.Samples.ToArray();
            PointMm[] oldCps = old.ControlPoints.ToArray();

            RoadSegment added = Draw(w, M(70, 30), M(110, 36), M(150, 30));
            Assert.That(added.StartTangent.HasValue, Is.True);
            Assert.That(added.EndTangent.HasValue, Is.False);
            var again = new CatmullRomSampler(w.Tuning).Sample(added.ControlPoints, added.StartTangent, added.EndTangent);
            Assert.That(SameSamples(again, added.Samples), Is.True);

            RoadSegment oldNow = w.Roads.GetSegment(oldId);
            Assert.That(oldNow, Is.Not.Null);
            Assert.That(oldNow.Samples.SequenceEqual(oldSamples), Is.True);
            Assert.That(oldNow.ControlPoints.SequenceEqual(oldCps), Is.True);
            Assert.That(oldNow.StartTangent.HasValue, Is.False);
            Assert.That(w.Roads.GetNode(oldNow.NodeB).SegmentIds.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validate_StraightContinuationFromDeadEnd_IsNotRefusedAsOverlap()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 10), M(60, 10));
            RoadPreview p = RoadValidator.Validate(w, new[] { M(60, 10), M(110, 10) }, Type);
            Assert.That(p.Reason, Is.EqualTo(RoadRefusal.Valid));
            Assert.That(w.Execute(new DrawRoad(new[] { M(60, 10), M(110, 10) }, Type)).IsAccepted, Is.True);
        }

        [Test]
        public void Validate_StraightOldRoad_ContinuationStaysOnTheLine()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 10), M(60, 10));
            RoadPreview p = RoadValidator.Validate(w, new[] { M(60, 10), M(110, 10) }, Type);
            Assert.That(p.Samples.All(s => s.Z == M(0, 10).Z), Is.True);
        }

        [Test]
        public void Validate_TJunction_NoTangentApplied()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 30), M(100, 30));
            var cps = new[] { M(50, 30), M(60, 60), M(50, 90) };
            RoadPreview p = RoadValidator.Validate(w, cps, Type);
            Assert.That(p.Junctions.Count, Is.GreaterThan(0));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.T));
            Assert.That(p.StartTangent.HasValue, Is.False);
            Assert.That(p.EndTangent.HasValue, Is.False);
            Assert.That(SameSamples(p.Samples, new CatmullRomSampler(w.Tuning).Sample(cps)), Is.True);
        }

        [Test]
        public void Validate_XJunction_NoTangentApplied()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 30), M(100, 30));
            var cps = new[] { M(50, 10), M(55, 40), M(50, 70) };
            RoadPreview p = RoadValidator.Validate(w, cps, Type);
            Assert.That(p.Junctions.Any(j => j.Kind == JunctionKind.X), Is.True);
            Assert.That(p.StartTangent.HasValue, Is.False);
            Assert.That(p.EndTangent.HasValue, Is.False);
            Assert.That(SameSamples(p.Samples, new CatmullRomSampler(w.Tuning).Sample(cps)), Is.True);
        }

        [Test]
        public void Validate_StartOnNodeWithTwoSegments_NoTangentApplied()
        {
            SimWorld w = Flat();
            Draw(w, M(10, 30), M(60, 30));
            Draw(w, M(60, 30), M(60, 80));
            Assert.That(w.Roads.FindNearest(M(60, 30), 1000).Kind, Is.EqualTo(RoadHitKind.End));
            var cps = new[] { M(60, 30), M(100, 40), M(140, 30) };
            RoadPreview p = RoadValidator.Validate(w, cps, Type);
            Assert.That(p.Junctions.Count, Is.GreaterThan(0));
            Assert.That(p.Junctions[0].Kind, Is.EqualTo(JunctionKind.JoinNode));
            Assert.That(p.StartTangent.HasValue, Is.False);
            Assert.That(SameSamples(p.Samples, new CatmullRomSampler(w.Tuning).Sample(cps)), Is.True);
        }

        [Test]
        public void Validate_NoRoadsNearby_NoTangentApplied()
        {
            SimWorld w = Flat();
            RoadPreview p = RoadValidator.Validate(w, new[] { M(10, 10), M(50, 20), M(90, 10) }, Type);
            Assert.That(p.StartTangent.HasValue, Is.False);
            Assert.That(p.EndTangent.HasValue, Is.False);
        }
    }
}
