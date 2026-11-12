using System.Collections.Generic;
using LordsLegacy.Sim.Inventory;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws one <c>visual.item.log</c> model per log in every ground pile of the Sim ledger, stacked in rows of
    /// <see cref="LogsPerLayer"/> (INV-06). Instanced, no GameObjects or colliders. Rebuilt only when
    /// <c>Ledger.Version</c> changes. Logs inside storage modules are not drawn (the storage module stands for them).
    /// </summary>
    public sealed class LogPileRenderer : MonoBehaviour
    {
        /// <summary>Logs side by side in one layer of a pile.</summary>
        public const int LogsPerLayer = 4;
        /// <summary>Log diameter in metres.</summary>
        public const float DiameterM = 0.35f;

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly List<Matrix4x4> _logs = new List<Matrix4x4>();
        private int _builtVersion = -1;
        private Bounds _bounds;

        /// <summary>Log models drawn. For tests.</summary>
        public int LogCount => _logs.Count;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry)
        {
            host = simHost;
            registry = visualRegistry;
            _builtVersion = -1;
        }

        private void Update() { Draw(); }

        /// <summary>Rebuilds if the ledger changed, then draws.</summary>
        public void Draw()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Inventory.Version != _builtVersion) Build(world);
            if (registry != null && registry.TryGet(VisualSizes.ItemLog, out var def))
                InstancedDraw.Draw(def, _logs, _bounds);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _builtVersion = world.Inventory.Version;
            _logs.Clear();
            VisualDefinition def = null;
            if (registry != null) registry.TryGet(VisualSizes.ItemLog, out def);
            float length = (float)world.Tuning.StandardLogLengthM;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var pile in world.Inventory.Locations)
            {
                if (pile.Kind != LocationKind.GroundPile) continue;
                int n = pile.CountOf(Items.Log.Id);
                if (n <= 0) continue;
                float px = (float)pile.Position.XMetres, pz = (float)pile.Position.ZMetres;
                float ground = world.Terrain.HeightAt(px, pz);
                float yaw = (pile.Id * 37) % 180;
                var along = LyingPiece.Direction(yaw);
                var across = Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward;
                for (int i = 0; i < n; i++)
                {
                    int layer = i / LogsPerLayer, col = i % LogsPerLayer;
                    float side = (col - (LogsPerLayer - 1) * 0.5f) * DiameterM + (layer % 2 == 1 ? DiameterM * 0.5f : 0f);
                    var c = new Vector3(px, ground + DiameterM * 0.5f + layer * DiameterM * 0.85f, pz) + across * side;
                    if (def != null) _logs.Add(LyingPiece.Place(def, c, yaw, DiameterM, length));
                    else _logs.Add(Matrix4x4.TRS(c, Quaternion.identity, Vector3.one));
                    min = Vector3.Min(min, c); max = Vector3.Max(max, c);
                }
            }
            _bounds = new Bounds();
            if (_logs.Count > 0) _bounds.SetMinMax(min - Vector3.one * (length + 1f), max + Vector3.one * (length + 1f));
        }
    }
}
