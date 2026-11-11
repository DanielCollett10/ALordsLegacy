using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Debug only (PPL-33), Editor and development builds: with a citizen selected and no tool active, a right click on
    /// the ground sends <see cref="SendCitizenTo"/> through <see cref="SimHost.Execute"/>. A refusal is shown in the
    /// citizen panel. Holds no game rules; in release builds it does nothing.
    /// </summary>
    public sealed class DebugSendController : MonoBehaviour
    {
        [SerializeField] private SimHost _host;
        [SerializeField] private GroundPicker _picker;
        [SerializeField] private AgentPicker _agents;
        [SerializeField] private ToolController _controller;
        [SerializeField] private CitizenPanelView _panel;

        /// <summary>True in the Editor and in development builds.</summary>
        public static bool DebugAvailable => Application.isEditor || Debug.isDebugBuild;

        /// <summary>When true, Update reads the mouse. Tests set false and call <see cref="RightClickAt"/>.</summary>
        public bool ReadMouse { get; set; } = true;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, GroundPicker picker, AgentPicker agents, ToolController controller, CitizenPanelView panel)
        {
            _host = host; _picker = picker; _agents = agents; _controller = controller; _panel = panel;
        }

        /// <summary>
        /// A right click at the screen position. Returns true if a command was sent (accepted or refused).
        /// Does nothing without a selected citizen, while a tool is active, over UI, or off the ground.
        /// </summary>
        public bool RightClickAt(Vector2 screenPos)
        {
            if (!DebugAvailable) return false;
            var host = _host != null ? _host : (_host = FindFirstObjectByType<SimHost>());
            var agents = _agents != null ? _agents : (_agents = FindFirstObjectByType<AgentPicker>());
            var picker = _picker != null ? _picker : (_picker = FindFirstObjectByType<GroundPicker>());
            var controller = _controller != null ? _controller : (_controller = FindFirstObjectByType<ToolController>());
            if (host == null || !host.IsInitialized || agents == null || picker == null || !agents.HasSelection || agents.Selected.IsOx) return false;
            if (controller != null && (controller.ActiveTool != null || controller.IsPointerOverUiAt(screenPos))) return false;
            if (!picker.TryPickScreen(screenPos, out var g) || !g.HasValue) return false;

            var result = host.Execute(new SendCitizenTo(agents.Selected.Id, new PathPoint(g.X, g.Z)));
            var panel = _panel != null ? _panel : (_panel = FindFirstObjectByType<CitizenPanelView>());
            if (panel != null) panel.ShowMessage(result.IsRejected ? result.Text : "");
            return true;
        }

        private void Update()
        {
            if (!ReadMouse || !DebugAvailable) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) RightClickAt(mouse.position.ReadValue());
        }
    }
}
