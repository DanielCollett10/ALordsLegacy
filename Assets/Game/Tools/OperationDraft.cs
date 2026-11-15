using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Game.Tools
{
    /// <summary>Stage of an <see cref="OperationDraft"/> (building-tool.md BT-11).</summary>
    public enum DraftStage
    {
        /// <summary>Drawing the area; nothing locked yet.</summary>
        AreaStage,
        /// <summary>Area locked; ghost modules are being placed.</summary>
        ModuleStage
    }

    /// <summary>What Escape did (building-tool.md BT-17).</summary>
    public enum DraftEscape
    {
        /// <summary>A module in hand was dropped.</summary>
        DroppedModule,
        /// <summary>No module in hand: the caller should cancel the whole draft.</summary>
        Cancel
    }

    /// <summary>
    /// Plain draft model for placing an operation in one go (building-tool.md BT-10..18). Holds the locked area,
    /// ghost modules, the module in hand and the running cost. Every placement rule is delegated to the Sim
    /// (<see cref="DraftModulePlacement"/>, <see cref="BuildCost"/>); nothing reaches the Sim until the caller sends
    /// <see cref="BuildCommand"/>.
    /// </summary>
    public sealed class OperationDraft
    {
        private readonly SimWorld _world;
        private readonly OperationTypeDef _type;
        private readonly List<ModuleSpec> _specs = new List<ModuleSpec>();
        private readonly List<Footprint> _ghosts = new List<Footprint>();
        private AreaShape _shape;
        private IReadOnlyList<PointMm> _polygon;

        /// <summary>Creates a draft for the operation type.</summary>
        public OperationDraft(SimWorld world, OperationTypeDef type)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _type = type ?? throw new ArgumentNullException(nameof(type));
        }

        /// <summary>
        /// Starts a draft that expands an existing operation (BT-20): already in the module stage on the operation's area,
        /// its modules counted as existing. Null when the operation or its area is missing or of another type.
        /// </summary>
        public static OperationDraft ForExpansion(SimWorld world, OperationTypeDef type, int operationId)
        {
            Operation op = world.Operations.Get(operationId);
            var area = op == null ? null : world.Areas.Get(op.AreaId);
            if (op == null || area == null || op.TypeId != type.Id) return null;
            var d = new OperationDraft(world, type) { _expandOperationId = operationId, _polygon = area.Polygon, Stage = DraftStage.ModuleStage };
            foreach (int id in op.ModuleIds)
            {
                Footprint f = world.Footprints.Get(id);
                if (f != null) d._existing.Add(f);
            }
            return d;
        }

        private int? _expandOperationId;
        private readonly List<Footprint> _existing = new List<Footprint>();

        /// <summary>True when this draft expands an existing operation instead of placing a new one.</summary>
        public bool IsExpansion => _expandOperationId.HasValue;

        /// <summary>The operation's modules that already exist (expansion only), drawn solid.</summary>
        public IReadOnlyList<Footprint> Existing => _existing;

        /// <summary>The polygon of the locked area, or null before it is locked.</summary>
        public IReadOnlyList<PointMm> AreaPolygon => _polygon;

        /// <summary>True when the type can still take one more module of this id given the existing ones and the ghosts.</summary>
        public bool CanOffer(string moduleTypeId)
        {
            if (moduleTypeId == _type.HutTypeId)
            {
                int n = CountOf(moduleTypeId);
                foreach (Footprint f in _existing) if (f.Type.Id == moduleTypeId) n++;
                return n < _type.HutCount;
            }
            return true;
        }

        /// <summary>Current stage.</summary>
        public DraftStage Stage { get; private set; }

        /// <summary>Module type id following the cursor, or null.</summary>
        public string ModuleInHand { get; private set; }

        /// <summary>The ghosts in placement order.</summary>
        public IReadOnlyList<Footprint> Ghosts => _ghosts;

        /// <summary>Logs the ghosts cost in total (BT-13).</summary>
        public int RunningCost
        {
            get
            {
                var ids = new List<string>();
                foreach (Footprint g in _ghosts) ids.Add(g.Type.Id);
                return BuildCost.Of(ids);
            }
        }

        /// <summary>Logs available to build with now (CN-11).</summary>
        public int Have => ConstructionSources.Available(_world, Items.Log);

        /// <summary>
        /// Validates the area and locks it, moving to the module stage (BT-11). On refusal the stage is unchanged.
        /// </summary>
        /// <param name="shape">The drawn shape.</param>
        /// <param name="reason">Refusal text, or empty.</param>
        public bool LockArea(AreaShape shape, out string reason)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            reason = string.Empty;
            if (Stage != DraftStage.AreaStage) { reason = "Area is already locked"; return false; }
            AreaPreview p = AreaValidator.Validate(_world, shape, _type.BuildingAreaTypeId);
            if (!p.IsValid) { reason = p.ReasonText; return false; }
            _shape = shape;
            _polygon = p.Polygon;
            Stage = DraftStage.ModuleStage;
            return true;
        }

        /// <summary>Picks a module to follow the cursor. Only in the module stage.</summary>
        public bool PickModule(string moduleTypeId)
        {
            if (Stage != DraftStage.ModuleStage || ModuleTypes.Find(moduleTypeId) == null) return false;
            ModuleInHand = moduleTypeId;
            return true;
        }

        /// <summary>Checks the module in hand at a position without adding it (for the ghost tint).</summary>
        public bool CheckGhost(PointMm centre, double rotationDeg, out string reason)
        {
            if (Stage != DraftStage.ModuleStage) { reason = "Lock the area first"; return false; }
            if (ModuleInHand == null) { reason = "Pick a module first"; return false; }
            IReadOnlyList<Footprint> others = _ghosts;
            if (_existing.Count > 0) { var all = new List<Footprint>(_existing); all.AddRange(_ghosts); others = all; }
            ModulePlacementResult r = DraftModulePlacement.Preview(_world, _type, _polygon, others, ModuleInHand,
                centre, rotationDeg, RunningCost);
            reason = r.Text;
            return r.Ok;
        }

        /// <summary>Places the module in hand as a ghost, or refuses with the Sim's reason (BT-12, BT-13, BT-18).</summary>
        public bool AddGhost(PointMm centre, double rotationDeg, out string reason)
        {
            if (!CheckGhost(centre, rotationDeg, out reason)) return false;
            FootprintTypeDef type = ModuleTypes.Find(ModuleInHand);
            _ghosts.Add(new Footprint(0, type, centre, rotationDeg));
            _specs.Add(new ModuleSpec(type.Id, centre, rotationDeg));
            return true;
        }

        /// <summary>Removes only the topmost ghost under the point; false if none (BT-14).</summary>
        public bool RemoveGhostAt(PointMm point)
        {
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                if (!Polygons.Contains(_ghosts[i].Polygon, point)) continue;
                _ghosts.RemoveAt(i);
                _specs.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>Count of ghosts of the type.</summary>
        public int CountOf(string moduleTypeId)
        {
            int n = 0;
            foreach (Footprint g in _ghosts) if (g.Type.Id == moduleTypeId) n++;
            return n;
        }

        /// <summary>
        /// What is missing before Confirm, e.g. "Place a Hut" (BT-15); empty if nothing mandatory is missing.
        /// </summary>
        public string MissingText
        {
            get
            {
                if (Stage != DraftStage.ModuleStage) return "Finish the area";
                if (IsExpansion) return _ghosts.Count == 0 ? "Place at least one module" : string.Empty;
                if (CountOf(_type.HutTypeId) < _type.HutCount) return "Place a Hut";
                if (CountOf(_type.LogStorageTypeId) < _type.MinLogStorageCount) return "Place a Log storage";
                return string.Empty;
            }
        }

        /// <summary>True when the area is locked, mandatory modules are placed and the total is affordable (BT-15).</summary>
        public bool CanConfirm => MissingText.Length == 0 && BuildCost.CanAfford(_world, RunningCost);

        /// <summary>Escape: drops the module in hand, else signals Cancel (BT-17).</summary>
        public DraftEscape Escape()
        {
            if (ModuleInHand != null) { ModuleInHand = null; return DraftEscape.DroppedModule; }
            return DraftEscape.Cancel;
        }

        /// <summary>Throws away the area, every ghost and the module in hand (BT-17).</summary>
        public void Cancel()
        {
            _ghosts.Clear(); _specs.Clear();
            _shape = null; _polygon = null; ModuleInHand = null;
            Stage = DraftStage.AreaStage;
        }

        /// <summary>The one command for Confirm, modules in placement order (BT-16). Null until the area is locked.</summary>
        public DesignateOperation BuildCommand() =>
            _shape == null ? null : new DesignateOperation(_shape, _type.Id, _specs);

        /// <summary>The one command for Confirm of an expansion (BT-21). Null for a new-operation draft.</summary>
        public PlaceModules BuildExpandCommand() =>
            _expandOperationId.HasValue ? new PlaceModules(_expandOperationId.Value, _specs) : null;
    }
}
