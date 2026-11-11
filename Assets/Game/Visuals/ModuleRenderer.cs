using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws the modules placed in operations (woodcutter hut, log storage) as plain coloured boxes on their footprints,
    /// so the player sees what was placed. Placeholder art; rebuilt only when <c>FootprintRegistry.Version</c> changes.
    /// Boxes have no colliders, so they never block ground picking.
    /// </summary>
    public sealed class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private SimHost host;

        private readonly List<GameObject> _boxes = new List<GameObject>();
        private int _builtVersion = -1;
        private Material _material;

        /// <summary>Boxes currently drawn. For tests.</summary>
        public int BoxCount => _boxes.Count;

        /// <summary>Sets the SimHost (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost) { host = simHost; _builtVersion = -1; }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds the boxes if the Sim footprints changed.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Footprints.Version == _builtVersion) return;
            _builtVersion = world.Footprints.Version;
            for (int i = 0; i < _boxes.Count; i++) Destroy(_boxes[i]);
            _boxes.Clear();
            foreach (var op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                {
                    var fp = world.Footprints.Get(id);
                    if (fp != null) _boxes.Add(MakeBox(world, fp));
                }
        }

        private GameObject MakeBox(LordsLegacy.Sim.Core.SimWorld world, Footprint fp)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Module " + fp.Type.Id;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            bool hut = fp.Type.Id == ModuleTypes.WoodcuttingHutId;
            float h = hut ? 2.6f : 1.2f;
            float x = (float)fp.Centre.XMetres, z = (float)fp.Centre.ZMetres;
            float y = world.Terrain.HeightAt(x, z);
            go.transform.position = new Vector3(x, y + h * 0.5f, z);
            // Footprint rotation is counter-clockwise (x towards z); a Unity yaw turns z towards x, hence the sign.
            go.transform.rotation = Quaternion.Euler(0f, -(float)fp.RotationDegrees, 0f);
            go.transform.localScale = new Vector3(fp.Type.WidthMm / 1000f, h, fp.Type.DepthMm / 1000f);

            var r = go.GetComponent<Renderer>();
            if (_material == null) _material = r.sharedMaterial;
            var color = hut ? new Color(0.45f, 0.28f, 0.14f) : new Color(0.72f, 0.6f, 0.38f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            return go;
        }
    }
}
