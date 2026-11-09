using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace LordsLegacy.Sim.Geometry
{
    /// <summary>
    /// Polygon boolean operations on integer-millimetre polygons (decision 0021). The work is done by the vendored
    /// Clipper2 library; no Clipper type appears in this public API. Polygons are vertex lists with no repeated
    /// closing vertex. Results are counter-clockwise (GEO-03).
    /// </summary>
    public static class PolygonBoolean
    {
        /// <summary>
        /// Subtracts every clip polygon from the subject polygon.
        /// <para>
        /// Returns the remaining pieces as counter-clockwise outer boundaries. A clip that lies fully inside the
        /// subject leaves a hole: the hole is returned in <paramref name="holes"/> as a counter-clockwise polygon,
        /// and it belongs to the piece that contains it. An island inside a hole is returned as a piece.
        /// </para>
        /// <para>
        /// Degenerate input is handled before Clipper runs: consecutive duplicate vertices (including a repeated
        /// closing vertex) are removed, clockwise input is reversed, and any polygon with fewer than three distinct
        /// vertices is ignored. An empty subject gives no pieces, and an ignored clip changes nothing. A zero-area
        /// clip covers nothing. Clip polygons may overlap each other.
        /// </para>
        /// <para>
        /// Coordinates are exact integers. Where two edges cross at a point that is not on the mm grid, Clipper
        /// rounds that new vertex to the nearest mm. Shared edges and touching corners give no sliver pieces.
        /// </para>
        /// </summary>
        /// <param name="subject">The polygon to cut from.</param>
        /// <param name="clips">Polygons to subtract. Null entries throw <see cref="ArgumentNullException"/>.</param>
        /// <param name="holes">Receives the counter-clockwise hole rings (never null; empty when there are none).</param>
        /// <returns>The counter-clockwise outer pieces (never null; empty when nothing remains).</returns>
        public static IReadOnlyList<PointMm[]> Difference(
            IReadOnlyList<PointMm> subject,
            IEnumerable<IReadOnlyList<PointMm>> clips,
            out IReadOnlyList<PointMm[]> holes)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (clips == null) throw new ArgumentNullException(nameof(clips));

            var pieces = new List<PointMm[]>();
            var holeList = new List<PointMm[]>();
            holes = holeList;

            PointMm[] cleanSubject = Clean(subject);
            if (cleanSubject == null) return pieces;

            var clipper = new Clipper64();
            clipper.PreserveCollinear = false;
            clipper.AddSubject(ToPath(cleanSubject));
            foreach (IReadOnlyList<PointMm> clip in clips)
            {
                if (clip == null) throw new ArgumentNullException(nameof(clips), "A clip polygon is null.");
                PointMm[] cleanClip = Clean(clip);
                if (cleanClip != null) clipper.AddClip(ToPath(cleanClip));
            }

            var tree = new PolyTree64();
            if (!clipper.Execute(ClipType.Difference, FillRule.NonZero, tree))
                throw new InvalidOperationException("Clipper2 could not compute the polygon difference.");

            Collect(tree, pieces, holeList);
            return pieces;
        }

        /// <summary>
        /// Returns the polygon with consecutive duplicate vertices (and a repeated closing vertex) removed and
        /// wound counter-clockwise, or null if fewer than three distinct vertices remain. A zero-area or
        /// self-crossing polygon is returned as is: its net signed area can be zero (a bow-tie), so Clipper decides
        /// what it covers.
        /// </summary>
        private static PointMm[] Clean(IReadOnlyList<PointMm> poly)
        {
            var pts = new List<PointMm>(poly.Count);
            foreach (PointMm p in poly)
                if (pts.Count == 0 || !(pts[pts.Count - 1] == p)) pts.Add(p);
            while (pts.Count > 1 && pts[0] == pts[pts.Count - 1]) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3) return null;
            return Polygons.EnsureCounterClockwise(pts);
        }

        private static Path64 ToPath(PointMm[] poly)
        {
            var path = new Path64();
            foreach (PointMm p in poly) path.Add(new Point64(p.X, p.Z));
            return path;
        }

        /// <summary>Walks the result tree: outer pieces and holes alternate by depth.</summary>
        private static void Collect(PolyPath64 node, List<PointMm[]> pieces, List<PointMm[]> holes)
        {
            for (int i = 0; i < node.Count; i++)
            {
                PolyPath64 child = node.Child(i);
                PointMm[] ring = FromPath(child.Polygon);
                if (ring != null) (child.IsHole ? holes : pieces).Add(ring);
                Collect(child, pieces, holes);
            }
        }

        private static PointMm[] FromPath(Path64 path)
        {
            if (path == null || path.Count < 3) return null;
            var pts = new PointMm[path.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointMm(path[i].X, path[i].Y);
            return Polygons.EnsureCounterClockwise(pts);
        }
    }
}
