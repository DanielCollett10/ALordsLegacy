using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Placement
{
    /// <summary>Answer of <see cref="StructureSnap.Snap"/>. Immutable.</summary>
    public readonly struct StructureSnapResult
    {
        /// <summary>Centre to place the structure at (the input centre when not snapped).</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise (the input rotation when not snapped).</summary>
        public double RotationDegrees { get; }
        /// <summary>True when an edge was within reach and the plot was moved flush against it.</summary>
        public bool Snapped { get; }

        internal StructureSnapResult(PointMm centre, double rotationDegrees, bool snapped)
        {
            Centre = centre; RotationDegrees = rotationDegrees; Snapped = snapped;
        }
    }

    /// <summary>
    /// Pure query for placement.md PL-14: turns and slides a fixed structure's plot so one plot edge lies flush against
    /// the nearest plot edge, building-area edge or road side within the snap distance, then, if a second edge at a
    /// right angle is also in reach (a corner), slides along the first edge to meet it. Changes nothing; the tool asks
    /// and shows the result (decisions 0017, 0028). Candidates that would overlap something are skipped.
    /// </summary>
    public static class StructureSnap
    {
        private struct Edge
        {
            public double Ax, Az, Dx, Dz, Len, Nx, Nz; // start, unit direction, length, unit outward normal
        }

        /// <summary>Snaps the structure; returns the input unchanged when nothing is in reach, the type is unknown or the input is invalid.</summary>
        public static StructureSnapResult Snap(SimWorld world, string typeId, PointMm centre, double rotationDegrees, double snapDistanceM)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var unchanged = new StructureSnapResult(centre, rotationDegrees, false);
            FootprintTypeDef type = ModuleTypes.Find(typeId);
            if (type == null || !type.IsStandalone) return unchanged;
            if (double.IsNaN(rotationDegrees) || double.IsInfinity(rotationDegrees)) return unchanged;
            if (double.IsNaN(snapDistanceM) || double.IsInfinity(snapDistanceM) || snapDistanceM <= 0) return unchanged;

            double snapMm = snapDistanceM * PointMm.MmPerMetre;
            double hw = type.WidthMm / 2.0 + type.YardMarginMm, hd = type.DepthMm / 2.0 + type.YardMarginMm;
            double reach = Math.Sqrt(hw * hw + hd * hd) + snapMm;
            var edges = CollectEdges(world, centre, reach);
            if (edges.Count == 0) return unchanged;

            // Phase 1: nearest edge (by move distance) that can be made parallel; try in order, first clear one wins.
            var cands = new List<(double move, double rot, double cx, double cz)>();
            foreach (Edge e in edges)
            {
                double angle = Math.Atan2(e.Dz, e.Dx) * 180.0 / Math.PI;
                double k = Math.Round((angle - rotationDegrees) / 90.0);
                double delta = angle - rotationDegrees - 90.0 * k;
                double rot = Math.Abs(delta) < 1e-9 ? rotationDegrees : rotationDegrees + delta;
                bool dAlongWidth = (((long)k % 2) + 2) % 2 == 0;
                double h = dAlongWidth ? hd : hw, along = dAlongWidth ? hw : hd;
                double s = (centre.X - e.Ax) * e.Nx + (centre.Z - e.Az) * e.Nz;
                if (s <= 0) continue;
                double move = h - s;
                if (Math.Abs(move) > snapMm) continue;
                double t = (centre.X - e.Ax) * e.Dx + (centre.Z - e.Az) * e.Dz;
                if (t + along <= 0 || t - along >= e.Len) continue;
                cands.Add((Math.Abs(move), rot, centre.X + move * e.Nx, centre.Z + move * e.Nz));
            }
            cands.Sort((a, b) => a.move.CompareTo(b.move));
            foreach (var c in cands)
            {
                PointMm p = Round(c.cx, c.cz);
                if (!Clear(world, type, p, c.rot)) continue;
                p = Corner(world, type, edges, p, c.rot, hw, hd, snapMm);
                return new StructureSnapResult(p, c.rot, true);
            }
            return unchanged;
        }

        // Phase 2: with the plot already parallel to its first edge, slide it flush to a perpendicular edge in reach.
        private static PointMm Corner(SimWorld world, FootprintTypeDef type, List<Edge> edges, PointMm p, double rot,
            double hw, double hd, double snapMm)
        {
            double rad = rot * Math.PI / 180.0, ux = Math.Cos(rad), uz = Math.Sin(rad);
            PointMm best = p; double bestMove = double.MaxValue;
            foreach (Edge e in edges)
            {
                double cross = e.Dx * uz - e.Dz * ux, dot = e.Dx * ux + e.Dz * uz;
                bool alongU = Math.Abs(cross) < 1e-3, alongV = Math.Abs(dot) < 1e-3;
                if (!alongU && !alongV) continue;
                double h = alongU ? hd : hw, along = alongU ? hw : hd;
                double s = (p.X - e.Ax) * e.Nx + (p.Z - e.Az) * e.Nz;
                if (s <= 0) continue;
                double move = h - s;
                if (Math.Abs(move) <= 1.0 || Math.Abs(move) > snapMm || Math.Abs(move) >= bestMove) continue; // <=1 mm: already flush
                double t = (p.X - e.Ax) * e.Dx + (p.Z - e.Az) * e.Dz;
                if (t + along <= 0 || t - along >= e.Len) continue;
                PointMm q = Round(p.X + move * e.Nx, p.Z + move * e.Nz);
                if (!Clear(world, type, q, rot)) continue;
                best = q; bestMove = Math.Abs(move);
            }
            return best;
        }

        private static PointMm Round(double x, double z) =>
            new PointMm((long)Math.Round(x, MidpointRounding.AwayFromZero), (long)Math.Round(z, MidpointRounding.AwayFromZero));

        // The same overlap checks as StructurePlacement.Preview, minus the materials check.
        private static bool Clear(SimWorld world, FootprintTypeDef type, PointMm centre, double rot)
        {
            var fp = new Footprint(0, type, centre, rot);
            PointMm[] plot = fp.PlotPolygon;
            double tol = world.Tuning.OverlapToleranceM2;
            BoundsMm b = Polygons.Bounds(plot);
            foreach (Footprint other in world.Footprints.All)
                if (other.Bounds.Intersects(b) && PolygonOverlap.Overlaps(plot, other.Polygon, tol)) return false;
            if (AreaPlacementChecks.FindOverlappingArea(world, plot) != null) return false;
            if (AreaPlacementChecks.CrossesRoad(world, plot)) return false;
            return !PileZones.BlocksPolygon(world, plot);
        }

        private static List<Edge> CollectEdges(SimWorld world, PointMm centre, double reachMm)
        {
            var list = new List<Edge>();
            long r = (long)Math.Ceiling(reachMm);
            var box = new BoundsMm(new PointMm(centre.X - r, centre.Z - r), new PointMm(centre.X + r, centre.Z + r));
            foreach (Footprint f in world.Footprints.All)
                if (f.Type.IsStandalone && Polygons.Bounds(f.PlotPolygon).Intersects(box)) AddRing(list, f.PlotPolygon);
            foreach (AreaRecord a in world.Areas.QueryOverlapCandidates(box))
            {
                var ring = new PointMm[a.Polygon.Count];
                for (int i = 0; i < ring.Length; i++) ring[i] = a.Polygon[i];
                if (a.Bounds.Intersects(box)) AddRing(list, ring);
            }
            foreach (RoadSegment seg in world.Roads.Segments)
            {
                AddSide(list, seg.SideLine(RoadSide.Left, world.RoadTypes), 1, box);
                AddSide(list, seg.SideLine(RoadSide.Right, world.RoadTypes), -1, box);
            }
            return list;
        }

        // A closed ring: the outward normal is on the right of a counter-clockwise ring, the left of a clockwise one.
        private static void AddRing(List<Edge> list, PointMm[] ring)
        {
            if (ring.Length < 3) return;
            double area2 = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                PointMm a = ring[i], c = ring[(i + 1) % ring.Length];
                area2 += (double)a.X * c.Z - (double)c.X * a.Z;
            }
            if (area2 == 0) return;
            double sign = area2 > 0 ? -1 : 1; // multiplies the left normal (-dz, dx)
            for (int i = 0; i < ring.Length; i++) Add(list, ring[i], ring[(i + 1) % ring.Length], sign);
        }

        // sign: +1 puts the outward normal on the left of travel, -1 on the right.
        private static void AddSide(List<Edge> list, PointMm[] line, double sign, BoundsMm box)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                var eb = new BoundsMm(
                    new PointMm(Math.Min(line[i].X, line[i + 1].X), Math.Min(line[i].Z, line[i + 1].Z)),
                    new PointMm(Math.Max(line[i].X, line[i + 1].X), Math.Max(line[i].Z, line[i + 1].Z)));
                if (eb.Intersects(box)) Add(list, line[i], line[i + 1], sign);
            }
        }

        private static void Add(List<Edge> list, PointMm a, PointMm b, double sign)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1) return; // zero-length edge has no direction
            dx /= len; dz /= len;
            list.Add(new Edge { Ax = a.X, Az = a.Z, Dx = dx, Dz = dz, Len = len, Nx = -dz * sign, Nz = dx * sign });
        }
    }
}
