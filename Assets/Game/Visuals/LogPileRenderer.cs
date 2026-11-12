using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Inventory;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws one <c>visual.item.log</c> model per log in every ledger location (INV-06, INV-07, ART-A2). Instanced, no
    /// GameObjects or colliders. Fixed stacking rule per kind: ground piles and the camp stockpile stack in rows of
    /// <see cref="LogsPerLayer"/> lying along the pile's yaw; a storage module stacks the same rows along its footprint's
    /// local X, centred in the footprint; logs on an ox carrier lie in a row dragged behind the ox. Piles, storages and
    /// the stockpile are rebuilt only when <c>Ledger.Version</c> changes; carried logs follow the ox every frame.
    /// </summary>
    public sealed class LogPileRenderer : MonoBehaviour
    {
        /// <summary>Logs side by side in one layer of a pile.</summary>
        public const int LogsPerLayer = 4;
        /// <summary>Log diameter in metres.</summary>
        public const float DiameterM = 0.35f;
        /// <summary>Distance from the ox centre to the start of the dragged logs, in metres.</summary>
        public const float DragGapM = 1.0f;

        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly List<Matrix4x4> _logs = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _carried = new List<Matrix4x4>();
        private readonly Dictionary<int, int> _drawnPerLocation = new Dictionary<int, int>();
        private int _builtVersion = -1;
        private Bounds _bounds;
        private Bounds _carriedBounds;

        /// <summary>Log models drawn (piles, storages, stockpile and carried). For tests.</summary>
        public int LogCount => _logs.Count + _carried.Count;

        /// <summary>Log models drawn for one ledger location id (0 if none). For tests.</summary>
        public int DrawnFor(int locationId) => _drawnPerLocation.TryGetValue(locationId, out int n) ? n : 0;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry)
        {
            host = simHost;
            registry = visualRegistry;
            _builtVersion = -1;
        }

        private void Update() { Draw(); }

        /// <summary>Rebuilds if the ledger changed, places carried logs, then draws.</summary>
        public void Draw()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            VisualDefinition def = null;
            if (registry != null) registry.TryGet(VisualSizes.ItemLog, out def);
            if (world.Inventory.Version != _builtVersion) Build(world, def);
            BuildCarried(world, def);
            if (def == null) return;
            InstancedDraw.Draw(def, _logs, _bounds);
            InstancedDraw.Draw(def, _carried, _carriedBounds);
        }

        private void Build(SimWorld world, VisualDefinition def)
        {
            _builtVersion = world.Inventory.Version;
            _logs.Clear();
            _drawnPerLocation.Clear();
            float length = (float)world.Tuning.StandardLogLengthM;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var loc in world.Inventory.Locations)
            {
                int n = loc.CountOf(Items.Log.Id);
                if (n <= 0) continue;
                float cx, cz, yaw;
                if (loc.Kind == LocationKind.GroundPile || loc.Kind == LocationKind.Stockpile)
                {
                    cx = (float)loc.Position.XMetres; cz = (float)loc.Position.ZMetres;
                    yaw = (loc.Id * 37) % 180;
                }
                else if (loc.Kind == LocationKind.Storage)
                {
                    var fp = world.Footprints.Get(loc.ModuleId);
                    if (fp == null) continue; // module removed: nowhere to draw
                    cx = (float)fp.Centre.XMetres; cz = (float)fp.Centre.ZMetres;
                    // Footprint rotation is counter-clockwise (x towards z); local X as a Unity yaw is 90 - rotation.
                    yaw = 90f - (float)fp.RotationDegrees;
                }
                else continue; // carriers are placed behind their ox each frame
                float ground = world.Terrain.HeightAt(cx, cz);
                var across = LyingPiece.Direction(yaw + 90f);
                for (int i = 0; i < n; i++)
                {
                    int layer = i / LogsPerLayer, col = i % LogsPerLayer;
                    float side = (col - (LogsPerLayer - 1) * 0.5f) * DiameterM + (layer % 2 == 1 ? DiameterM * 0.5f : 0f);
                    var c = new Vector3(cx, ground + DiameterM * 0.5f + layer * DiameterM * 0.85f, cz) + across * side;
                    _logs.Add(Matrix(def, c, yaw, length));
                    min = Vector3.Min(min, c); max = Vector3.Max(max, c);
                }
                _drawnPerLocation[loc.Id] = n;
            }
            _bounds = new Bounds();
            if (_logs.Count > 0) _bounds.SetMinMax(min - Vector3.one * (length + 1f), max + Vector3.one * (length + 1f));
        }

        private void BuildCarried(SimWorld world, VisualDefinition def)
        {
            _carried.Clear();
            float length = (float)world.Tuning.StandardLogLengthM;
            float t = world.Paused ? 1f : Mathf.Clamp01((float)host.TickFraction);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var ox in world.People.Oxen)
            {
                if (ox.CarrierId == 0) continue;
                var carrier = world.Inventory.Get(ox.CarrierId);
                int n = carrier == null ? 0 : carrier.CountOf(Items.Log.Id);
                if (carrier != null) _drawnPerLocation[carrier.Id] = n;
                if (n <= 0) continue;
                float x = (float)(ox.PreviousX + (ox.X - ox.PreviousX) * t);
                float z = (float)(ox.PreviousZ + (ox.Z - ox.PreviousZ) * t);
                float yaw = (float)(ox.FacingRad * Mathf.Rad2Deg);
                var fwd = LyingPiece.Direction(yaw);
                var across = LyingPiece.Direction(yaw + 90f);
                for (int i = 0; i < n; i++)
                {
                    float side = (i - (n - 1) * 0.5f) * (DiameterM + 0.05f);
                    float px = x - fwd.x * (DragGapM + length * 0.5f) + across.x * side;
                    float pz = z - fwd.z * (DragGapM + length * 0.5f) + across.z * side;
                    var c = new Vector3(px, world.Terrain.HeightAt(px, pz) + DiameterM * 0.5f, pz);
                    _carried.Add(Matrix(def, c, yaw, length));
                    min = Vector3.Min(min, c); max = Vector3.Max(max, c);
                }
            }
            _carriedBounds = new Bounds();
            if (_carried.Count > 0) _carriedBounds.SetMinMax(min - Vector3.one * (length + 1f), max + Vector3.one * (length + 1f));
        }

        private static Matrix4x4 Matrix(VisualDefinition def, Vector3 c, float yaw, float length) =>
            def != null ? LyingPiece.Place(def, c, yaw, DiameterM, length) : Matrix4x4.TRS(c, Quaternion.identity, Vector3.one);
    }
}
