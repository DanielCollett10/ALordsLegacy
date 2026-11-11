using System.Collections.Generic;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Geometry;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Minimal module placement tool (T-278). The area tool hands over to it after a woodcutter area is designated: it
    /// holds the operation id and the queue of module types to place (hut first, then log storage). It shows a tool
    /// panel naming the next module; a left click on the ground sends <see cref="PlaceModule"/> (the Sim decides if it
    /// fits) and moves to the next module. After the last one, or on Cancel / Esc / right click, it returns to no tool.
    /// The richer behaviour (ghost, rotation) arrives with the module tool task.
    /// </summary>
    public sealed class ModuleTool : MonoBehaviour, ITool
    {
        /// <summary>Tool id the controller registers the module tool under.</summary>
        public const string ToolId = "module";

        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private ToolPanelView _panel;
        [SerializeField] private ToolController _controller;

        private readonly List<string> _queue = new List<string>();
        private string _rejection;
        private bool _subscribed;

        /// <inheritdoc />
        public string Id => ToolId;

        /// <inheritdoc />
        public bool HasDraft => false;

        /// <summary>The operation whose modules are being placed, or 0 when none.</summary>
        public int OperationId { get; private set; }

        /// <summary>Module type ids still to place, in order (first is next).</summary>
        public IReadOnlyList<string> Queue => _queue;

        /// <summary>True while this tool is the active tool.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, GroundPicker picker, ToolPanelView panel, ToolController controller)
        {
            Unsubscribe();
            _host = host; _picker = picker; _panel = panel; _controller = controller;
            if (IsActive) Subscribe();
        }

        /// <summary>Sets the operation and module order. Call before the controller activates the tool.</summary>
        public void Begin(int operationId, IEnumerable<string> moduleTypeIds)
        {
            OperationId = operationId;
            _queue.Clear();
            _queue.AddRange(moduleTypeIds);
            _rejection = null;
        }

        /// <inheritdoc />
        public void Activate()
        {
            IsActive = true;
            Subscribe();
            ShowPanel();
        }

        /// <inheritdoc />
        public void Deactivate()
        {
            IsActive = false;
            Unsubscribe();
            if (_panel != null) _panel.Hide();
        }

        /// <inheritdoc />
        public void Cancel() { }

        /// <inheritdoc />
        public bool HandleEscape() { return false; }

        /// <summary>A left click on the ground tries to place the next module there.</summary>
        public void ClickAt(Vector2 screenPos)
        {
            if (!IsActive || _queue.Count == 0 || _host == null || !_host.IsInitialized || _picker == null) return;
            if (_controller != null && _controller.IsPointerOverUiAt(screenPos)) return;
            if (!_picker.TryPickScreen(screenPos, out var s) || !s.HasValue) return;
            var result = _host.Execute(new PlaceModule(OperationId, _queue[0], PointMm.FromMetres(s.X, s.Z), 0));
            if (!result.IsAccepted)
            {
                _rejection = string.IsNullOrEmpty(result.Text) ? result.Reason.ToString() : result.Text;
                ShowPanel();
                return;
            }
            _rejection = null;
            _queue.RemoveAt(0);
            if (_queue.Count == 0 && _controller != null) _controller.Deselect();
            else ShowPanel();
        }

        private static string NameOf(string typeId)
        {
            switch (typeId)
            {
                case "module.woodcutting_hut": return "woodcutter's hut";
                case "module.log_storage": return "log storage";
                default: return typeId;
            }
        }

        private void ShowPanel()
        {
            if (_panel == null) return;
            if (_queue.Count == 0) { _panel.Hide(); return; }
            string status = "Place " + NameOf(_queue[0]) + (_queue.Count > 1 ? " · next: " + NameOf(_queue[1]) : "");
            if (_rejection != null) status += " · Rejected: " + _rejection;
            _panel.Show(status, "Click inside the area to place it · Cancel, right-click or Esc stops", false);
        }

        private void Subscribe()
        {
            if (_subscribed || _panel == null) return;
            _panel.Cancelled += OnCancelled;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_panel != null) _panel.Cancelled -= OnCancelled;
            _subscribed = false;
        }

        private void OnCancelled()
        {
            if (_controller != null && _controller.ActiveTool == (ITool)this) _controller.Deselect();
        }

        private void OnDestroy() { Unsubscribe(); }

        private void OnEnable()
        {
            var c = _controller != null ? _controller : FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }

        private void Update()
        {
            if (!IsActive) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasReleasedThisFrame) ClickAt(mouse.position.ReadValue());
            if (mouse.rightButton.wasReleasedThisFrame) OnCancelled();
        }
    }
}
