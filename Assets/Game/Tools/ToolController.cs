using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Keeps exactly one <see cref="ITool"/> active at a time (ui.md UIB-03, UIB-04, UIB-06). Listens to the build bar,
    /// highlights the active button, and tells the camera rig a tool owns the left and right mouse buttons.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private BuildBarView _bar;
        [SerializeField] private CameraRig _camera;

        private readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        private bool _subscribed;

        /// <summary>The active tool, or null.</summary>
        public ITool ActiveTool { get; private set; }

        /// <summary>True while the pointer is over game UI; tools must ignore clicks then (UIB-05).</summary>
        public bool IsPointerOverUi
        {
            get
            {
                if (_bar != null && _bar.IsPointerOver) return true;
                var mouse = Mouse.current;
                return mouse != null && IsOverAnyPanel(mouse.position.ReadValue());
            }
        }

        /// <summary>True if any visible, pickable UI Toolkit element (bar, tool panel, top bar...) is under the screen position (pixels).</summary>
        public bool IsPointerOverUiAt(Vector2 screenPos)
        {
            return (_bar != null && _bar.IsPointerOver) || IsOverAnyPanel(screenPos);
        }

        private static bool IsOverAnyPanel(Vector2 screenPos)
        {
            var docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                var root = docs[i].rootVisualElement;
                var panel = root?.panel;
                if (panel == null) continue;
                var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
                if (panel.Pick(p) != null) return true;
            }
            return false;
        }

        /// <summary>Sets the build bar (for code that creates the component). Call before enabling.</summary>
        public void SetBar(BuildBarView bar) { _bar = bar; }

        /// <summary>Sets the camera rig told about tool state.</summary>
        public void SetCamera(CameraRig camera) { _camera = camera; SyncCamera(); }

        /// <summary>Makes a tool available under its <see cref="ITool.Id"/>.</summary>
        public void Register(ITool tool) { _tools[tool.Id] = tool; }

        /// <summary>
        /// Build bar click: activates the tool, deactivates it if it is already active, or switches
        /// (cancelling and deactivating the old tool first). Unknown ids do nothing.
        /// </summary>
        public void Toggle(string id)
        {
            if (GameMenuState.IsOpen || !_tools.TryGetValue(id, out var tool)) return;
            if (ActiveTool == tool) { Deselect(); return; }
            Deselect();
            ActiveTool = tool;
            tool.Activate();
            Refresh();
        }

        /// <summary>
        /// Operation panel "Expand modules" (building-tool.md BT-20): activates the draft tool of the operation's type in
        /// the module stage on the existing area. Returns false if no draft tool is registered for that type.
        /// </summary>
        public bool BeginExpandModules(int operationId, string operationTypeId)
        {
            OperationDraftTool found = null;
            foreach (var t in _tools.Values)
                if (t is OperationDraftTool d && d.OperationTypeId == operationTypeId) { found = d; break; }
            if (found == null) return false;
            Deselect();
            found.PrepareExpand(operationId);
            Toggle(found.Id);
            return true;
        }

        /// <summary>
        /// Operation panel "Draw work area" (WC-12): activates the area tool for the given area type with the operation set,
        /// so confirming sends SetWorkArea. Returns false if no area tool is registered for that type.
        /// </summary>
        public bool BeginWorkAreaDraw(int operationId, string workAreaTypeId)
        {
            AreaTool found = null;
            foreach (var t in _tools.Values)
                if (t is AreaTool a && a.TypeId == workAreaTypeId) { found = a; break; }
            if (found == null) return false;
            Deselect();
            Toggle(found.Id);
            found.WorkAreaFor = operationId;
            return true;
        }

        /// <summary>Cancels and deactivates the active tool, if any.</summary>
        public void Deselect()
        {
            var old = ActiveTool;
            if (old == null) return;
            ActiveTool = null;
            old.Cancel();
            old.Deactivate();
            Refresh();
        }

        /// <summary>
        /// Escape (UIB-06): the active tool gets it first. Only if it returns false is the tool deactivated.
        /// Returns true if anything handled the key.
        /// </summary>
        public bool HandleEscape()
        {
            if (ActiveTool == null) return false;
            if (ActiveTool.HandleEscape()) return true;
            Deselect();
            return true;
        }

        /// <summary>
        /// The Esc key (SV-01, SV-02, SV-04): an open menu handles it first (back or close). Otherwise the active tool gets
        /// it (cancel draft, then leave the tool). Only when no tool is active and nothing else (agent or operation panel)
        /// wants it does Esc open the game menu. Returns true if anything handled it.
        /// </summary>
        public bool HandleEscapeKey()
        {
            var menu = _menu != null ? _menu : (_menu = FindFirstObjectByType<GameMenuView>());
            if (menu != null && menu.IsOpen) { menu.HandleEscape(); return true; }
            if (HandleEscape()) return true;
            var agents = FindFirstObjectByType<AgentPicker>();
            if (agents != null && (agents.HasSelection || agents.HasOperation)) return false;
            if (menu == null) return false;
            menu.Open();
            return true;
        }

        private GameMenuView _menu;

        private void OnEnable()
        {
            Subscribe(); SyncCamera();
            // PL-12: the plot overlay follows the active tool; it lives next to the controller so no scene edit is needed.
            if (GetComponent<LordsLegacy.Game.Visuals.PlacementOverlay>() == null)
                gameObject.AddComponent<LordsLegacy.Game.Visuals.PlacementOverlay>().Configure(FindFirstObjectByType<SimHost>(), this);
        }

        private void OnDisable()
        {
            if (_bar != null && _subscribed) _bar.ToolClicked -= Toggle;
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_bar == null || _subscribed) return;
            _bar.ToolClicked += Toggle;
            _subscribed = true;
        }

        private void Update()
        {
            Subscribe();
            if (_camera == null) { _camera = FindFirstObjectByType<CameraRig>(); SyncCamera(); }
            var kb = Keyboard.current;
            // While a confirm dialog is open, Escape belongs to it (ConfirmDialogHost cancels it), not to the tool or menu.
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !ConfirmDialog.OwnsEscape) HandleEscapeKey();
        }

        private void Refresh()
        {
            if (_bar != null) _bar.SetActive(ActiveTool?.Id);
            SyncCamera();
        }

        private void SyncCamera()
        {
            if (_camera != null) _camera.ToolActive = ActiveTool != null;
        }
    }
}
