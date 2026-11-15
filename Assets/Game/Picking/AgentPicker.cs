using System;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.People;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game
{
    /// <summary>
    /// The single selection entry point (CMD-09): with no tool active, a left click selects the nearest citizen or ox
    /// within <see cref="WorldRadiusM"/> of the ground point, or within <see cref="ScreenRadiusPx"/> on screen.
    /// Citizens win over oxen. Reads the Sim and holds no game rules; later milestones add modules and areas here.
    /// </summary>
    public sealed class AgentPicker : MonoBehaviour
    {
        /// <summary>Largest distance from the ground point to an agent, in metres.</summary>
        public const float WorldRadiusM = 1.5f;
        /// <summary>Largest screen distance from the cursor to an agent, in pixels.</summary>
        public const float ScreenRadiusPx = 12f;

        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private ToolController _controller;

        private bool _hasSelection;
        private AgentRef _selected;
        private int? _operationId;
        private int? _houseId;

        /// <summary>Raised when the selection changes (select, clear).</summary>
        public event Action SelectionChanged;

        /// <summary>When true, Update reads the mouse. Tests set false and call <see cref="ClickAt"/>.</summary>
        public bool ReadMouse { get; set; } = true;

        /// <summary>True when an agent is selected.</summary>
        public bool HasSelection => _hasSelection;

        /// <summary>The selected agent (only meaningful when <see cref="HasSelection"/>).</summary>
        public AgentRef Selected => _selected;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, GroundPicker picker, ToolController controller)
        {
            _host = host; _picker = picker; _controller = controller;
        }

        /// <summary>Selects an agent. Unknown ids clear the selection.</summary>
        public void Select(AgentRef agent)
        {
            var host = Host();
            bool exists = host != null && host.IsInitialized &&
                (agent.IsOx ? host.World.People.FindOx(agent.Id) != null : host.World.People.FindCitizen(agent.Id) != null);
            if (!exists) { Clear(); return; }
            _hasSelection = true;
            _houseId = null;
            _selected = agent;
            SelectionChanged?.Invoke();
        }

        /// <summary>Clears the agent selection (the operation selection is separate: <see cref="ClearOperation"/>).</summary>
        public void Clear()
        {
            if (!_hasSelection) return;
            _hasSelection = false;
            SelectionChanged?.Invoke();
        }

        /// <summary>True when an operation is selected (through its hut, another module, or its area).</summary>
        public bool HasOperation => _operationId.HasValue;

        /// <summary>Id of the selected operation (only meaningful when <see cref="HasOperation"/>).</summary>
        public int SelectedOperationId => _operationId ?? -1;

        /// <summary>Selects an operation and clears any agent selection. Unknown ids clear the operation selection.</summary>
        public void SelectOperation(int operationId)
        {
            var host = Host();
            bool exists = host != null && host.IsInitialized && host.World.Operations.Get(operationId) != null;
            if (!exists) { ClearOperation(); return; }
            bool changed = _hasSelection || _houseId.HasValue || _operationId != operationId;
            _hasSelection = false;
            _houseId = null;
            _operationId = operationId;
            if (changed) SelectionChanged?.Invoke();
        }

        /// <summary>True when a house (or its plot) is selected (HM-21).</summary>
        public bool HasHouse => _houseId.HasValue;

        /// <summary>Footprint id of the selected house (only meaningful when <see cref="HasHouse"/>).</summary>
        public int SelectedHouseId => _houseId ?? -1;

        /// <summary>Selects a house by footprint id and clears the agent and operation selections. Unknown ids clear the house selection.</summary>
        public void SelectHouse(int houseFootprintId)
        {
            var host = Host();
            bool exists = host != null && host.IsInitialized && host.World.Homes.Get(houseFootprintId) != null
                && host.World.Footprints.Get(houseFootprintId) != null;
            if (!exists) { ClearHouse(); return; }
            bool changed = _hasSelection || _operationId.HasValue || _houseId != houseFootprintId;
            _hasSelection = false;
            _operationId = null;
            _houseId = houseFootprintId;
            if (changed) SelectionChanged?.Invoke();
        }

        /// <summary>Clears the house selection.</summary>
        public void ClearHouse()
        {
            if (!_houseId.HasValue) return;
            _houseId = null;
            SelectionChanged?.Invoke();
        }

        /// <summary>The house a click at the ground point would select: the house footprint, else its plot. False when none.</summary>
        public static bool TryPickHouse(SimWorld world, PointMm ground, out int houseFootprintId)
        {
            houseFootprintId = 0;
            var fp = world.Footprints.FindAt(ground);
            if (fp != null && world.Homes.Get(fp.Id) != null) { houseFootprintId = fp.Id; return true; }
            var plot = world.Areas.FindAt(ground, AreaKind.Building);
            if (plot != null)
            {
                var home = world.Homes.FindByPlot(plot.Id);
                if (home != null) { houseFootprintId = home.HouseFootprintId; return true; }
            }
            return false;
        }

        /// <summary>Clears the operation selection.</summary>
        public void ClearOperation()
        {
            if (!_operationId.HasValue) return;
            _operationId = null;
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// The operation a click at the ground point would select, in order: a module under the point, then the building
        /// area, then a linked work area. Returns false when none applies.
        /// </summary>
        public static bool TryPickOperation(SimWorld world, PointMm ground, out int operationId)
        {
            operationId = 0;
            var fp = world.Footprints.FindAt(ground);
            if (fp != null)
                foreach (var op in world.Operations.All)
                    foreach (int id in op.ModuleIds)
                        if (id == fp.Id) { operationId = op.Id; return true; }
            var building = world.Areas.FindAt(ground, AreaKind.Building);
            if (building != null)
            {
                var op = world.Operations.FindByArea(building.Id);
                if (op != null) { operationId = op.Id; return true; }
            }
            var work = world.Areas.FindAt(ground, AreaKind.Work);
            if (work != null)
                foreach (var op in world.Operations.All)
                    if (op.WorkAreaId == work.Id) { operationId = op.Id; return true; }
            return false;
        }

        /// <summary>
        /// Finds the agent a click at <paramref name="screenPos"/> would select. Nearest citizen first; oxen only
        /// if no citizen qualifies. Distance is whichever of the world or screen test the agent passes better.
        /// </summary>
        public bool TryPick(Vector2 screenPos, out AgentRef agent)
        {
            agent = default;
            var host = Host();
            if (host == null || !host.IsInitialized) return false;
            var pick = Picker();
            if (pick == null) return false;
            bool haveGround = pick.TryPickScreen(screenPos, out var ground) && ground.HasValue;
            var people = host.World.People;

            double best = double.MaxValue;
            foreach (var c in people.Citizens)
            {
                double s = Score(pick, screenPos, haveGround, ground.X, ground.Z, c.X, c.Z);
                if (s < best) { best = s; agent = AgentRef.Citizen(c.Id); }
            }
            if (best <= 1.0) return true;

            best = double.MaxValue;
            foreach (var o in people.Oxen)
            {
                double s = Score(pick, screenPos, haveGround, ground.X, ground.Z, o.X, o.Z);
                if (s < best) { best = s; agent = AgentRef.Ox(o.Id); }
            }
            return best <= 1.0;
        }

        /// <summary>
        /// A left click at the screen position with no tool active: selects the agent hit, or clears the selection
        /// when nothing is hit. Returns false (and does nothing) while a tool is active or the pointer is over UI.
        /// </summary>
        public bool ClickAt(Vector2 screenPos)
        {
            if (GameMenuState.IsOpen) return false;
            if (_controller == null) _controller = FindFirstObjectByType<ToolController>();
            if (_controller != null && (_controller.ActiveTool != null || _controller.IsPointerOverUiAt(screenPos))) return false;
            var host = Host();
            if (TryPick(screenPos, out var agent))
            {
                _operationId = null;
                _houseId = null;
                Select(agent);
            }
            else if (host != null && host.IsInitialized && Picker() != null
                && Picker().TryPickScreen(screenPos, out var hg) && hg.HasValue
                && TryPickHouse(host.World, PointMm.FromMetres(hg.X, hg.Z), out int houseId))
            {
                Clear();
                ClearOperation();
                SelectHouse(houseId);
            }
            else if (host != null && host.IsInitialized && Picker() != null
                && Picker().TryPickScreen(screenPos, out var ground) && ground.HasValue
                && TryPickOperation(host.World, PointMm.FromMetres(ground.X, ground.Z), out int opId))
            {
                Clear();
                SelectOperation(opId);
            }
            else
            {
                Clear();
                ClearOperation();
                ClearHouse();
            }
            return true;
        }

        private void Update()
        {
            var host = Host();
            if (_hasSelection && host != null && host.IsInitialized)
            {
                var p = host.World.People;
                if ((_selected.IsOx ? (object)p.FindOx(_selected.Id) : p.FindCitizen(_selected.Id)) == null) Clear();
            }
            if (_operationId.HasValue && host != null && host.IsInitialized && host.World.Operations.Get(_operationId.Value) == null)
                ClearOperation();
            if (_houseId.HasValue && host != null && host.IsInitialized
                && (host.World.Homes.Get(_houseId.Value) == null || host.World.Footprints.Get(_houseId.Value) == null))
                ClearHouse();
            if (!ReadMouse) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) ClickAt(mouse.position.ReadValue());
        }

        // Below or equal to 1 means "hit"; lower is nearer. Normalised so the world and screen tests are comparable.
        private static double Score(GroundPicker pick, Vector2 screenPos, bool haveGround, float gx, float gz, double ax, double az)
        {
            double score = double.MaxValue;
            if (haveGround)
            {
                double dx = ax - gx, dz = az - gz;
                score = Math.Sqrt(dx * dx + dz * dz) / WorldRadiusM;
            }
            if (pick.TryWorldToScreen((float)ax, (float)az, out var sp))
                score = Math.Min(score, Vector2.Distance(sp, screenPos) / ScreenRadiusPx);
            return score;
        }

        private SimHost Host()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host;
        }

        private GroundPicker Picker()
        {
            if (_picker == null) _picker = FindFirstObjectByType<GroundPicker>();
            return _picker;
        }
    }
}
