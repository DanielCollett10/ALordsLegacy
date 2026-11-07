using System.Collections.Generic;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Roads
{
    /// <summary>Turns control points into a sampled centre line. One implementation per <see cref="CurveKind"/>.</summary>
    public interface ICurveSampler
    {
        /// <summary>The curve kind this sampler implements.</summary>
        CurveKind Kind { get; }

        /// <summary>
        /// Cleans the control points (RD-02) then samples the curve about every sample spacing by arc length.
        /// The first and last samples are the first and last cleaned control points, and every cleaned control point is a sample.
        /// Fewer than two usable points give the cleaned points unchanged.
        /// </summary>
        IReadOnlyList<PointMm> Sample(IReadOnlyList<PointMm> controlPoints);
    }
}
