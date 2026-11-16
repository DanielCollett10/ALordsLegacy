using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Placement
{
    /// <summary>One no-build circle around a ground pile that holds items (PL-15).</summary>
    public readonly struct PileZone
    {
        /// <summary>The pile's point.</summary>
        public PointMm Centre { get; }
        /// <summary>Radius in metres (<c>SimTuning.PileClearanceM</c>).</summary>
        public double RadiusM { get; }

        /// <summary>Creates a zone.</summary>
        public PileZone(PointMm centre, double radiusM) { Centre = centre; RadiusM = radiusM; }
    }

    /// <summary>
    /// Ground piles with items block building (PL-15, CMP-24, CN-32, CN-54). Pure queries derived from the ledger, so
    /// the circle goes the moment the pile is empty and nothing new is saved.
    /// </summary>
    public static class PileZones
    {
        /// <summary>Player-facing refusal text.</summary>
        public const string Text = "Items on the ground here";

        /// <summary>A circle for every ground pile with at least one item, of any owner, in location id order.</summary>
        public static IReadOnlyList<PileZone> Of(SimWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var list = new List<PileZone>();
            double r = world.Tuning.PileClearanceM;
            foreach (LedgerLocation l in world.Inventory.Locations)
            {
                if (l.Kind != LocationKind.GroundPile) continue;
                if (l.Total <= 0) continue;
                list.Add(new PileZone(l.Position, r));
            }
            return list;
        }

        /// <summary>True if the point lies inside (or on) a circle.</summary>
        public static bool BlocksPoint(SimWorld world, PointMm p)
        {
            foreach (PileZone z in Of(world))
                if (z.Centre.DistanceTo(p) <= z.RadiusM * 1000.0) return true;
            return false;
        }

        /// <summary>True if the polygon (3+ points) touches or contains any circle by more than a grazing contact.</summary>
        public static bool BlocksPolygon(SimWorld world, IReadOnlyList<PointMm> poly)
        {
            if (poly == null || poly.Count < 3) return false;
            foreach (PileZone z in Of(world))
            {
                double rMm = z.RadiusM * 1000.0;
                if (Polygons.Locate(poly, z.Centre) != PointLocation.Outside) return true;
                for (int i = 0; i < poly.Count; i++)
                    if (Segments.DistanceToPoint(poly[i], poly[(i + 1) % poly.Count], z.Centre, out _) < rMm - 1.0) return true;
            }
            return false;
        }
    }
}
