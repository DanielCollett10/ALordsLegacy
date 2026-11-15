using System;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Placement
{
    /// <summary>Why a standalone structure cannot be placed (FD-30b).</summary>
    public enum StructureRefusal
    {
        /// <summary>Placeable.</summary>
        None = 0,
        /// <summary>The type id is unknown or is not a standalone structure.</summary>
        UnknownType,
        /// <summary>The rotation is not a finite number.</summary>
        BadRotation,
        /// <summary>The plot overlaps a footprint.</summary>
        OverlapsFootprint,
        /// <summary>The plot overlaps an area.</summary>
        OverlapsArea,
        /// <summary>The plot overlaps a road corridor.</summary>
        OverlapsRoad,
        /// <summary>The settlement cannot pay the recipe's logs.</summary>
        NotEnoughMaterials,
    }

    /// <summary>Answer of <see cref="StructurePlacement.Preview"/>. Immutable.</summary>
    public sealed class StructurePlacementResult
    {
        /// <summary>True if the structure may be placed.</summary>
        public bool Ok => Refusal == StructureRefusal.None;
        /// <summary>The refusal, or None.</summary>
        public StructureRefusal Refusal { get; }
        /// <summary>Player-facing text; empty when Ok.</summary>
        public string Text { get; }
        /// <summary>The building corners (empty for an unknown type or bad rotation).</summary>
        public PointMm[] Building { get; }
        /// <summary>The whole plot, building plus yard (empty for an unknown type or bad rotation).</summary>
        public PointMm[] Plot { get; }

        internal StructurePlacementResult(StructureRefusal refusal, string text, PointMm[] building, PointMm[] plot)
        {
            Refusal = refusal; Text = text ?? string.Empty; Building = building; Plot = plot;
        }
    }

    /// <summary>
    /// Pure placement check for a standalone fixed structure such as the granary (food.md FD-30, FD-30b): the whole
    /// plot (building plus yard) must be clear of every footprint, area and road. Changes nothing.
    /// </summary>
    public static class StructurePlacement
    {
        /// <summary>Checks a structure of the given type at the centre and rotation (degrees, counter-clockwise).</summary>
        public static StructurePlacementResult Preview(SimWorld world, string typeId, PointMm centre, double rotationDegrees)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var none = new PointMm[0];
            FootprintTypeDef type = ModuleTypes.Find(typeId);
            if (type == null || !type.IsStandalone)
                return new StructurePlacementResult(StructureRefusal.UnknownType, "Unknown structure", none, none);
            if (double.IsNaN(rotationDegrees) || double.IsInfinity(rotationDegrees))
                return new StructurePlacementResult(StructureRefusal.BadRotation, "Rotation is not a number", none, none);

            var fp = new Footprint(0, type, centre, rotationDegrees);
            PointMm[] plot = fp.PlotPolygon;
            double tol = world.Tuning.OverlapToleranceM2;
            BoundsMm plotBounds = Polygons.Bounds(plot);
            StructureRefusal r = StructureRefusal.None;
            string text = "";
            foreach (Footprint other in world.Footprints.All)
            {
                if (!other.Bounds.Intersects(plotBounds)) continue;
                if (PolygonOverlap.Overlaps(plot, other.Polygon, tol))
                { r = StructureRefusal.OverlapsFootprint; text = "Overlaps another building"; break; }
            }
            if (r == StructureRefusal.None && AreaPlacementChecks.FindOverlappingArea(world, plot) != null)
            { r = StructureRefusal.OverlapsArea; text = "Overlaps an area"; }
            if (r == StructureRefusal.None && AreaPlacementChecks.CrossesRoad(world, plot))
            { r = StructureRefusal.OverlapsRoad; text = "Overlaps a road"; }
            if (r == StructureRefusal.None)
            {
                int need = type.ConstructionRecipe.Logs, have = Construction.ConstructionSources.Available(world, Items.Log);
                if (have < need)
                { r = StructureRefusal.NotEnoughMaterials; text = "Not enough logs (have " + have + ", need " + need + ")"; }
            }
            return new StructurePlacementResult(r, text, fp.Polygon, plot);
        }
    }
}
