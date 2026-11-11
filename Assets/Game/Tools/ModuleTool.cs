using System.Collections.Generic;
using UnityEngine;

namespace LordsLegacy.Game.Tools
{
    /// <summary>
    /// Minimal stub of the module placement tool (T-278). The area tool hands over to it after a woodcutter area is
    /// designated: it holds the operation id and the queue of module types to place (hut first, then log storage).
    /// The real placing behaviour arrives with the module tool task; this only records the hand-off.
    /// </summary>
    public sealed class ModuleTool : MonoBehaviour, ITool
    {
        /// <summary>Tool id the controller registers the module tool under.</summary>
        public const string ToolId = "module";

        private readonly List<string> _queue = new List<string>();

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

        /// <summary>Sets the operation and module order. Call before the controller activates the tool.</summary>
        public void Begin(int operationId, IEnumerable<string> moduleTypeIds)
        {
            OperationId = operationId;
            _queue.Clear();
            _queue.AddRange(moduleTypeIds);
        }

        /// <inheritdoc />
        public void Activate() { IsActive = true; }

        /// <inheritdoc />
        public void Deactivate() { IsActive = false; }

        /// <inheritdoc />
        public void Cancel() { }

        /// <inheritdoc />
        public bool HandleEscape() { return false; }

        private void OnEnable()
        {
            var c = FindFirstObjectByType<ToolController>();
            if (c != null) c.Register(this);
        }
    }
}
