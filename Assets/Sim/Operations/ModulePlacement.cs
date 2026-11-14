using System;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>Why a module cannot be placed (WC-03, WC-04, WC-05).</summary>
    public enum ModuleRefusal
    {
        /// <summary>Placeable.</summary>
        None = 0,
        /// <summary>The operation id is unknown.</summary>
        UnknownOperation,
        /// <summary>Not fully inside the operation's building area (WC-03).</summary>
        OutsideArea,
        /// <summary>Overlaps another module of the operation (WC-03).</summary>
        OverlapsModule,
        /// <summary>The module type is not one this operation uses.</summary>
        NotAllowedType,
        /// <summary>The operation already has the most modules of this type (second hut, second oxen stand).</summary>
        TooMany,
        /// <summary>The settlement cannot pay the recipe's logs (CN-10).</summary>
        NotEnoughMaterials,
    }

    /// <summary>Result of <see cref="ModulePlacement.Preview"/>. Immutable.</summary>
    public sealed class ModulePlacementResult
    {
        /// <summary>True if the module may be placed.</summary>
        public bool Ok => Refusal == ModuleRefusal.None;
        /// <summary>The refusal, or <see cref="ModuleRefusal.None"/>.</summary>
        public ModuleRefusal Refusal { get; }
        /// <summary>Text for the UI; empty when Ok.</summary>
        public string Text { get; }

        internal ModulePlacementResult(ModuleRefusal refusal, string text)
        {
            Refusal = refusal; Text = text ?? string.Empty;
        }
    }

    /// <summary>Pure placement check for woodcutting modules (woodcutting.md WC-03..WC-05; decision 0011).</summary>
    public static class ModulePlacement
    {
        /// <summary>Overlap or outside area (m²) that still counts as touching; ~1 mm of an 8 m edge.</summary>
        public const double ToleranceM2 = 0.001;

        /// <summary>
        /// Checks whether a module of the given type fits at the centre and rotation. Changes nothing. The module
        /// must lie fully inside the operation's area (touching the edge is fine) and not overlap another module.
        /// </summary>
        public static ModulePlacementResult Preview(SimWorld world, int operationId, string moduleTypeId,
            PointMm centre, double rotationDeg)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Operation op = world.Operations.Get(operationId);
            if (op == null) return Refuse(ModuleRefusal.UnknownOperation, "No operation " + operationId);
            OperationTypeDef opType = OperationTypes.Find(op.TypeId);
            FootprintTypeDef type = ModuleTypes.Find(moduleTypeId);
            if (opType == null || type == null || !IsUsed(opType, type.Id))
                return Refuse(ModuleRefusal.NotAllowedType, "This operation cannot hold that module");

            int limit = type.Id == opType.HutTypeId ? opType.HutCount : type.Id == opType.OxenStandTypeId ? opType.MaxOxenStands : int.MaxValue;
            int have = 0;
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null && f.Type.Id == type.Id) have++;
            }
            if (have >= limit) return Refuse(ModuleRefusal.TooMany, "Only " + limit + " allowed per operation");

            var fp = new Footprint(0, type, centre, rotationDeg);
            var area = world.Areas.Get(op.AreaId);
            if (area == null) return Refuse(ModuleRefusal.OutsideArea, "The operation's area is gone");
            double fpArea = Polygons.SignedAreaM2(fp.Polygon);
            if (fpArea - PolygonOverlap.AreaM2(fp.Polygon, area.Polygon) > ToleranceM2 || CornerOutside(fp, area.Polygon))
                return Refuse(ModuleRefusal.OutsideArea, "Must sit fully inside the area");

            foreach (int id in op.ModuleIds)
            {
                Footprint other = world.Footprints.Get(id);
                if (other != null && PolygonOverlap.Overlaps(fp.Polygon, other.Polygon, ToleranceM2))
                    return Refuse(ModuleRefusal.OverlapsModule, "Overlaps another module");
            }
            int need = type.ConstructionRecipe.Logs;
            if (need > 0)
            {
                int haveLogs = Construction.ConstructionSources.Available(world, Inventory.Items.Log);
                if (haveLogs < need)
                    return Refuse(ModuleRefusal.NotEnoughMaterials, "Not enough logs (have " + haveLogs + ", need " + need + ")");
            }
            return new ModulePlacementResult(ModuleRefusal.None, string.Empty);
        }

        /// <summary>True if a corner lies outside the polygon by more than <see cref="CornerToleranceMm"/> (catches thin corner pokes).</summary>
        private static bool CornerOutside(Footprint fp, System.Collections.Generic.IReadOnlyList<PointMm> poly)
        {
            foreach (PointMm corner in fp.Polygon)
            {
                if (Polygons.Locate(poly, corner) != PointLocation.Outside) continue;
                double best = double.MaxValue;
                for (int i = 0; i < poly.Count; i++)
                    best = Math.Min(best, Segments.DistanceToPoint(poly[i], poly[(i + 1) % poly.Count], corner, out _));
                if (best > CornerToleranceMm) return true;
            }
            return false;
        }

        /// <summary>A corner this far (mm) outside the area edge is tolerated, to absorb integer rounding of rotated corners.</summary>
        public const double CornerToleranceMm = 2.0;

        private static bool IsUsed(OperationTypeDef t, string typeId) =>
            typeId == t.HutTypeId || typeId == t.LogStorageTypeId || (t.OxenStandTypeId != null && typeId == t.OxenStandTypeId);

        private static ModulePlacementResult Refuse(ModuleRefusal r, string text) => new ModulePlacementResult(r, text);
    }
}
