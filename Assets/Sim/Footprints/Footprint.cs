using System;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Footprints
{
    /// <summary>
    /// A placed oriented rectangle. Local frame: width along X, depth along Z; the front edge is the one at local
    /// -Z. Rotation turns the rectangle counter-clockwise (positive X towards positive Z) by the given degrees.
    /// </summary>
    public sealed class Footprint
    {
        /// <summary>Distance in mm the access point sits outside the front edge (MOV-04).</summary>
        public const long AccessOffsetMm = 1000;

        /// <summary>Unique id within the registry.</summary>
        public int Id { get; }
        /// <summary>The type.</summary>
        public FootprintTypeDef Type { get; }
        /// <summary>Centre of the rectangle.</summary>
        public PointMm Centre { get; }
        /// <summary>Rotation in degrees, counter-clockwise.</summary>
        public double RotationDegrees { get; }
        /// <summary>Four corners, counter-clockwise, starting at the front-left (local -X,-Z) corner.</summary>
        public PointMm[] Polygon { get; }
        /// <summary>Midpoint of the front edge, 1 m outside it.</summary>
        public PointMm AccessPoint { get; }
        /// <summary>Bounding box of the polygon.</summary>
        public BoundsMm Bounds { get; }

        /// <summary>Creates a footprint and computes its polygon and access point.</summary>
        public Footprint(int id, FootprintTypeDef type, PointMm centre, double rotationDegrees)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            Id = id; Type = type; Centre = centre; RotationDegrees = rotationDegrees;
            double rad = rotationDegrees * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            double hw = type.WidthMm / 2.0, hd = type.DepthMm / 2.0;
            Polygon = new[]
            {
                Place(-hw, -hd, c, s), Place(hw, -hd, c, s), Place(hw, hd, c, s), Place(-hw, hd, c, s),
            };
            AccessPoint = Place(0, -hd - AccessOffsetMm, c, s);
            Bounds = Polygons.Bounds(Polygon);
        }

        private PointMm Place(double lx, double lz, double c, double s) =>
            new PointMm(Centre.X + (long)Math.Round(lx * c - lz * s, MidpointRounding.AwayFromZero),
                        Centre.Z + (long)Math.Round(lx * s + lz * c, MidpointRounding.AwayFromZero));
    }
}
