using System;
using System.Collections.Generic;
using System.Linq;
using LordsLegacy.Sim.Geometry;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class PolygonBooleanTests
    {
        // Axis-aligned rectangle in metres, counter-clockwise, 4 vertices.
        private static PointMm[] Rect(double x0, double z0, double x1, double z1) => new[]
        {
            PointMm.FromMetres(x0, z0), PointMm.FromMetres(x1, z0),
            PointMm.FromMetres(x1, z1), PointMm.FromMetres(x0, z1)
        };

        // Regular polygon approximating a circle, counter-clockwise, in metres.
        private static PointMm[] Circle(double cx, double cz, double r, int n)
        {
            var pts = new PointMm[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n;
                pts[i] = PointMm.FromMetres(cx + r * Math.Cos(a), cz + r * Math.Sin(a));
            }
            return pts;
        }

        private static IReadOnlyList<PointMm[]> Diff(PointMm[] subject, params PointMm[][] clips) =>
            PolygonBoolean.Difference(subject, clips, out _);

        private static IReadOnlyList<PointMm[]> DiffWithHoles(PointMm[] subject, out IReadOnlyList<PointMm[]> holes,
            params PointMm[][] clips) =>
            PolygonBoolean.Difference(subject, clips, out holes);

        private static double TotalArea(IEnumerable<PointMm[]> polys) => polys.Sum(p => Polygons.SignedAreaM2(p));

        private static bool SameVertexSet(PointMm[] a, PointMm[] b) =>
            a.OrderBy(p => p.X).ThenBy(p => p.Z).SequenceEqual(b.OrderBy(p => p.X).ThenBy(p => p.Z));

        private static void AssertAllCcwSimple(IEnumerable<PointMm[]> polys)
        {
            foreach (PointMm[] p in polys)
            {
                Assert.IsTrue(Polygons.IsCounterClockwise(p), "piece must be counter-clockwise");
                Assert.IsTrue(Polygons.IsSimple(p), "piece must be simple");
            }
        }

        [Test]
        public void SquareMinusOverlappingSquare_GivesOneLShapedPiece()
        {
            PointMm[] subject = Rect(0, 0, 20, 20);
            IReadOnlyList<PointMm[]> pieces = DiffWithHoles(subject, out IReadOnlyList<PointMm[]> holes, Rect(10, 10, 30, 30));

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(0, holes.Count);
            Assert.AreEqual(6, pieces[0].Length, "an L has six vertices");
            Assert.AreEqual(300.0, Polygons.SignedAreaM2(pieces[0]), 1e-9);
            // Exact on integer mm: the L's vertices are the subject corners plus the two inner corners, unrounded.
            var expected = new[]
            {
                new PointMm(0, 0), new PointMm(20000, 0), new PointMm(20000, 10000),
                new PointMm(10000, 10000), new PointMm(10000, 20000), new PointMm(0, 20000)
            };
            Assert.IsTrue(SameVertexSet(expected, pieces[0]));
            AssertAllCcwSimple(pieces);
        }

        [Test]
        public void ClipSharesOnlyAnEdge_SubjectUnchanged()
        {
            PointMm[] subject = Rect(0, 0, 10, 10);
            IReadOnlyList<PointMm[]> pieces = Diff(subject, Rect(10, 0, 20, 10));

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(4, pieces[0].Length);
            Assert.IsTrue(SameVertexSet(subject, pieces[0]));
        }

        [Test]
        public void ClipTouchesOnlyAtCorner_SubjectUnchanged()
        {
            PointMm[] subject = Rect(0, 0, 10, 10);
            IReadOnlyList<PointMm[]> pieces = Diff(subject, Rect(10, 10, 20, 20));

            Assert.AreEqual(1, pieces.Count);
            Assert.IsTrue(SameVertexSet(subject, pieces[0]));
        }

        [Test]
        public void ClipCutsSubjectInTwo_GivesTwoPieces()
        {
            IReadOnlyList<PointMm[]> pieces = DiffWithHoles(Rect(0, 0, 30, 10), out IReadOnlyList<PointMm[]> holes,
                Rect(10, -5, 20, 15));

            Assert.AreEqual(2, pieces.Count);
            Assert.AreEqual(0, holes.Count);
            Assert.AreEqual(100.0, Polygons.SignedAreaM2(pieces[0]), 1e-9);
            Assert.AreEqual(100.0, Polygons.SignedAreaM2(pieces[1]), 1e-9);
            AssertAllCcwSimple(pieces);
        }

        [Test]
        public void CurvedClipCrossingAnEdge_TrimsToOneSimplePieceWithCurvedEdge()
        {
            // A 64-point circle of radius 5 m centred on the square's bottom edge: half of it lies inside.
            PointMm[] circle = Circle(10, 0, 5, 64);
            IReadOnlyList<PointMm[]> pieces = Diff(Rect(0, 0, 20, 20), circle);

            Assert.AreEqual(1, pieces.Count);
            Assert.Greater(pieces[0].Length, 4, "the cut edge must follow the curve");
            AssertAllCcwSimple(pieces);
            double expected = 400.0 - Polygons.SignedAreaM2(circle) / 2;
            Assert.AreEqual(expected, Polygons.SignedAreaM2(pieces[0]), 0.2);
        }

        [Test]
        public void ClipFullyInside_GivesOuterPieceAndOneHole()
        {
            IReadOnlyList<PointMm[]> pieces = DiffWithHoles(Rect(0, 0, 30, 30), out IReadOnlyList<PointMm[]> holes,
                Rect(10, 10, 20, 20));

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(900.0, Polygons.SignedAreaM2(pieces[0]), 1e-9);
            Assert.AreEqual(1, holes.Count);
            Assert.AreEqual(100.0, Polygons.SignedAreaM2(holes[0]), 1e-9);
            Assert.IsTrue(Polygons.IsCounterClockwise(holes[0]), "holes are counter-clockwise too");
            Assert.IsTrue(Polygons.IsSimple(holes[0]));
        }

        [Test]
        public void ClipIdenticalToSubject_GivesNoPieces()
        {
            IReadOnlyList<PointMm[]> pieces = Diff(Rect(0, 0, 10, 10), Rect(0, 0, 10, 10));

            Assert.AreEqual(0, pieces.Count, "nothing remains when the clip covers the subject");
        }

        [Test]
        public void EmptySubject_GivesNoPieces()
        {
            IReadOnlyList<PointMm[]> pieces = Diff(new PointMm[0], Rect(0, 0, 10, 10));

            Assert.AreEqual(0, pieces.Count, "an empty subject yields nothing");
        }

        [Test]
        public void SubjectWithTwoPoints_GivesNoPieces()
        {
            var line = new[] { new PointMm(0, 0), new PointMm(10000, 0) };
            IReadOnlyList<PointMm[]> pieces = Diff(line, Rect(0, 0, 10, 10));

            Assert.AreEqual(0, pieces.Count, "a two-point subject has no area, so it yields nothing");
        }

        [Test]
        public void SubjectWithRepeatedAndClosingVertices_SameAsCleanSubject()
        {
            // Same L case as the first test, with a duplicate vertex and a repeated closing vertex.
            PointMm[] messy =
            {
                new PointMm(0, 0), new PointMm(0, 0), new PointMm(20000, 0), new PointMm(20000, 20000),
                new PointMm(20000, 20000), new PointMm(0, 20000), new PointMm(0, 0)
            };
            IReadOnlyList<PointMm[]> pieces = Diff(messy, Rect(10, 10, 30, 30));

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(6, pieces[0].Length, "duplicate vertices must not survive as extra corners");
            Assert.AreEqual(300.0, Polygons.SignedAreaM2(pieces[0]), 1e-9);
        }

        [Test]
        public void ClipWithTwoPoints_SubjectUnchanged()
        {
            PointMm[] subject = Rect(0, 0, 10, 10);
            var line = new[] { new PointMm(5000, 5000), new PointMm(6000, 6000) };
            IReadOnlyList<PointMm[]> pieces = Diff(subject, line);

            Assert.AreEqual(1, pieces.Count);
            Assert.IsTrue(SameVertexSet(subject, pieces[0]), "a two-point clip has no area and is ignored");
        }

        [Test]
        public void ClipWithZeroAreaCollinearPoints_SubjectUnchanged()
        {
            PointMm[] subject = Rect(0, 0, 10, 10);
            var sliver = new[] { new PointMm(2000, 2000), new PointMm(5000, 5000), new PointMm(8000, 8000) };
            IReadOnlyList<PointMm[]> pieces = Diff(subject, sliver);

            Assert.AreEqual(1, pieces.Count);
            Assert.IsTrue(SameVertexSet(subject, pieces[0]), "a zero-area clip is ignored");
        }

        [Test]
        public void SubjectWithExtraCollinearVertex_SameAreaAndCornersAsPlainSquare()
        {
            PointMm[] plain = Rect(0, 0, 20, 20);
            PointMm[] withMid =
            {
                plain[0], PointMm.FromMetres(10, 0), plain[1], plain[2], plain[3]
            };
            PointMm[] clip = Rect(15, 15, 30, 30);

            IReadOnlyList<PointMm[]> a = Diff(plain, clip);
            IReadOnlyList<PointMm[]> b = Diff(withMid, clip);

            Assert.AreEqual(1, b.Count);
            Assert.AreEqual(TotalArea(a), TotalArea(b), 1e-9);
            Assert.AreEqual(a[0].Length, b[0].Length, "a collinear midpoint is not a corner of the result");
        }

        [Test]
        public void LShapeCase_SameResultForEveryStartVertexAndDirection()
        {
            PointMm[] square = Rect(0, 0, 20, 20);
            PointMm[] clip = Rect(10, 10, 30, 30);
            for (int start = 0; start < square.Length; start++)
            {
                for (int reverse = 0; reverse < 2; reverse++)
                {
                    var order = new PointMm[square.Length];
                    for (int i = 0; i < order.Length; i++)
                        order[i] = reverse == 0 ? square[(start + i) % 4] : square[(start - i + 4) % 4];

                    IReadOnlyList<PointMm[]> pieces = Diff(order, clip);

                    string label = "start=" + start + " reverse=" + reverse;
                    Assert.AreEqual(1, pieces.Count, label);
                    Assert.AreEqual(6, pieces[0].Length, label);
                    Assert.AreEqual(300.0, Polygons.SignedAreaM2(pieces[0]), 1e-9, label);
                }
            }
        }

        [Test]
        public void ClockwiseSubject_ReturnsCounterClockwisePieces()
        {
            PointMm[] clockwise = Rect(0, 0, 20, 20).Reverse().ToArray();
            Assert.IsFalse(Polygons.IsCounterClockwise(clockwise), "test input must be clockwise");

            IReadOnlyList<PointMm[]> pieces = Diff(clockwise, Rect(10, 10, 30, 30));

            Assert.AreEqual(1, pieces.Count);
            AssertAllCcwSimple(pieces);
            Assert.AreEqual(300.0, Polygons.SignedAreaM2(pieces[0]), 1e-9);
        }

        [Test]
        public void SelfCrossingHourglassSubject_EveryPieceIsSimpleAndCounterClockwise()
        {
            // Bow-tie: edges (0,0)-(10,10) and (10,0)-(0,10) cross at (5,5). Two triangles of 25 m² each.
            PointMm[] bowtie =
            {
                PointMm.FromMetres(0, 0), PointMm.FromMetres(10, 10),
                PointMm.FromMetres(10, 0), PointMm.FromMetres(0, 10)
            };
            IReadOnlyList<PointMm[]> pieces = Diff(bowtie, Rect(-5, -5, -1, -1));

            AssertAllCcwSimple(pieces);
            Assert.AreEqual(50.0, TotalArea(pieces), 1e-6, "both lobes survive");
        }

        [Test]
        public void MultipleClips_OrderDoesNotChangeResult()
        {
            PointMm[] subject = Rect(0, 0, 30, 10);
            PointMm[] a = Rect(5, -5, 10, 15);
            PointMm[] b = Rect(20, -5, 25, 15);

            IReadOnlyList<PointMm[]> ab = Diff(subject, a, b);
            IReadOnlyList<PointMm[]> ba = Diff(subject, b, a);

            Assert.AreEqual(3, ab.Count, "two cuts leave three strips");
            Assert.AreEqual(3, ba.Count);
            Assert.AreEqual(TotalArea(ab), TotalArea(ba), 1e-9);
            Assert.AreEqual(TotalArea(ab), 300 - 5 * 10 - 5 * 10, 1e-9);
        }

        [Test]
        public void NullSubject_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PolygonBoolean.Difference(null, new List<IReadOnlyList<PointMm>>(), out _));
        }

        [Test]
        public void NullClipEntry_Throws()
        {
            var clips = new List<IReadOnlyList<PointMm>> { null };
            Assert.Throws<ArgumentNullException>(() =>
                PolygonBoolean.Difference(Rect(0, 0, 10, 10), clips, out _));
        }
    }
}
