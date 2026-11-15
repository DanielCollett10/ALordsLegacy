using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// Pure preview of a module ghost inside a draft area that is not in the world yet (building-tool.md BT-12, BT-13,
    /// BT-18). Reuses the rectangle and containment rules of <see cref="ModulePlacement"/>. Changes nothing.
    /// </summary>
    public static class DraftModulePlacement
    {
        /// <summary>
        /// Checks a module rectangle against the draft area polygon, the other draft ghosts, the world's footprints and
        /// road corridors, the per-type maximum and the logs left after the ghosts already drafted.
        /// </summary>
        /// <param name="world">The world (footprints, roads, stock). Not changed.</param>
        /// <param name="opType">The operation being drafted.</param>
        /// <param name="areaPolygon">The draft building area polygon.</param>
        /// <param name="ghosts">Other draft module rectangles (id is ignored); may be null.</param>
        /// <param name="moduleTypeId">Module type to place.</param>
        /// <param name="centre">Rectangle centre.</param>
        /// <param name="rotationDeg">Rectangle rotation in degrees.</param>
        /// <param name="ghostCostSoFar">Logs the other ghosts will cost.</param>
        public static ModulePlacementResult Preview(SimWorld world, OperationTypeDef opType,
            IReadOnlyList<PointMm> areaPolygon, IReadOnlyList<Footprint> ghosts, string moduleTypeId,
            PointMm centre, double rotationDeg, int ghostCostSoFar)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (opType == null) throw new ArgumentNullException(nameof(opType));
            if (areaPolygon == null) throw new ArgumentNullException(nameof(areaPolygon));
            FootprintTypeDef type = ModuleTypes.Find(moduleTypeId);
            if (type == null || !IsUsed(opType, type.Id))
                return Refuse(ModuleRefusal.NotAllowedType, "This operation cannot hold that module");

            int limit = type.Id == opType.HutTypeId ? opType.HutCount
                : type.Id == opType.OxenStandTypeId ? opType.MaxOxenStands : int.MaxValue;
            int have = 0;
            if (ghosts != null)
                foreach (Footprint g in ghosts)
                    if (g != null && g.Type.Id == type.Id) have++;
            if (have >= limit) return Refuse(ModuleRefusal.TooMany, "Only " + limit + " allowed per operation");

            var fp = new Footprint(0, type, centre, rotationDeg);
            if (!ModulePlacement.FitsInside(fp, areaPolygon))
                return Refuse(ModuleRefusal.OutsideArea, "Must sit fully inside the area");

            if (ghosts != null)
                foreach (Footprint g in ghosts)
                    if (g != null && ModulePlacement.Overlaps(fp, g))
                        return Refuse(ModuleRefusal.OverlapsModule, "Overlaps another module");

            foreach (Footprint other in world.Footprints.QueryRect(fp.Bounds))
                if (ModulePlacement.Overlaps(fp, other))
                    return Refuse(ModuleRefusal.OverlapsFootprint, "Overlaps a building");

            foreach (RoadSegment seg in world.Roads.Segments)
            {
                PointMm[] corridor = seg.CorridorPolygon(world.RoadTypes);
                if (corridor.Length >= 3 && PolygonOverlap.Overlaps(fp.Polygon, corridor, ModulePlacement.ToleranceM2))
                    return Refuse(ModuleRefusal.OverlapsRoad, "Overlaps a road");
            }

            int need = type.ConstructionRecipe.Logs;
            if (need > 0)
            {
                int haveLogs = Construction.ConstructionSources.Available(world, Inventory.Items.Log);
                if (ghostCostSoFar + need > haveLogs)
                    return Refuse(ModuleRefusal.NotEnoughMaterials, "Not enough logs (have " + haveLogs + ", need " + (ghostCostSoFar + need) + ")");
            }
            return new ModulePlacementResult(ModuleRefusal.None, string.Empty);
        }

        private static bool IsUsed(OperationTypeDef t, string typeId) =>
            typeId == t.HutTypeId || typeId == t.LogStorageTypeId || (t.OxenStandTypeId != null && typeId == t.OxenStandTypeId);

        private static ModulePlacementResult Refuse(ModuleRefusal r, string text) => new ModulePlacementResult(r, text);
    }
}
