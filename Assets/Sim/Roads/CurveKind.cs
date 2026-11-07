namespace LordsLegacy.Sim.Roads
{
    /// <summary>How control points become a centre line (RD-01).</summary>
    public enum CurveKind
    {
        /// <summary>Centripetal Catmull-Rom spline through every control point.</summary>
        CatmullRomCentripetal = 0,
    }
}
