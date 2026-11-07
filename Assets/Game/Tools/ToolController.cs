using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Keeps exactly one <see cref="ITool"/> active at a time (ui.md UIB-03, UIB-04, UIB-06). Listens to the build bar,
    /// highlights the active button, and tells the camera rig a tool owns the left and right mouse buttons.
    /// </summary>
    public sealed class ToolController : MonoBehaviour
    {
        [SerializeField] private BuildBarView _bar;
        [SerializeField] private CameraRig _camera;

        private readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        private bool _subscribed;

        /// <summary>The active tool, or null.</summary>
        public ITool ActiveTool { get; private set; }

        /// <summary>True while the pointer is over game UI; tools must ignore clicks then (UIB-05).</summary>
        public bool IsPointerOverUi => _bar != null && _bar.IsPointerOver;

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
            if (!_tools.TryGetValue(id, out var tool)) return;
            if (ActiveTool == tool) { Deselect(); return; }
            Deselect();
            ActiveTool = tool;
            tool.Activate();
            Refresh();
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

        private void OnEnable() { Subscribe(); SyncCamera(); }

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
            if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleEscape();
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
