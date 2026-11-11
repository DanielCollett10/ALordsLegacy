using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws the camp pieces from Sim state: one tent per family home, the fire, the log pile at the stockpile and
    /// the tie post at the ox stand. Instanced, no GameObjects or colliders. The matrices are rebuilt only when
    /// <c>FootprintRegistry.Version</c>, the camp itself or the family count changes.
    /// </summary>
    public sealed class CampRenderer : MonoBehaviour
    {
        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly List<Matrix4x4> _tents = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _fire = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _logs = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _posts = new List<Matrix4x4>();
        private Bounds _bounds;
        private bool _built;
        private int _builtFootprintVersion;
        private int _builtFamilies;
        private CampInfoRef _builtCamp;

        // Plain wrapper so the reference comparison does not depend on CampInfo equality.
        private sealed class CampInfoRef { public object Camp; }

        /// <summary>How many times the matrices were rebuilt. For tests.</summary>
        public int BuildCount { get; private set; }

        /// <summary>Tent instances drawn.</summary>
        public int TentCount => _tents.Count;

        /// <summary>Instances of all camp pieces drawn.</summary>
        public int PieceCount => _tents.Count + _fire.Count + _logs.Count + _posts.Count;

        /// <summary>Sets the references (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry)
        {
            host = simHost;
            registry = visualRegistry;
            _built = false;
        }

        private void Update() { Draw(); }

        /// <summary>Rebuilds if the Sim camp changed, then draws.</summary>
        public void Draw()
        {
            if (host == null || !host.IsInitialized || registry == null) return;
            var world = host.World;
            if (world.Camp == null)
            {
                _tents.Clear(); _fire.Clear(); _logs.Clear(); _posts.Clear();
                _built = false;
                return;
            }
            if (!_built || world.Footprints.Version != _builtFootprintVersion
                || world.People.FamilyCount != _builtFamilies
                || !ReferenceEquals(_builtCamp?.Camp, world.Camp))
                Build(world);

            InstancedDraw.Draw(registry.Get(VisualSizes.CampTent), _tents, _bounds);
            InstancedDraw.Draw(registry.Get(VisualSizes.CampFire), _fire, _bounds);
            InstancedDraw.Draw(registry.Get(VisualSizes.CampLogPile), _logs, _bounds);
            InstancedDraw.Draw(registry.Get(VisualSizes.CampTiePost), _posts, _bounds);
        }

        private void Build(SimWorld world)
        {
            _built = true;
            BuildCount++;
            _builtFootprintVersion = world.Footprints.Version;
            _builtFamilies = world.People.FamilyCount;
            _builtCamp = new CampInfoRef { Camp = world.Camp };
            _tents.Clear(); _fire.Clear(); _logs.Clear(); _posts.Clear();

            var camp = world.Camp;
            var fp = world.Footprints.Get(camp.FootprintId);
            // Footprint rotation is counter-clockwise (x towards z); a Unity yaw turns z towards x, hence the sign.
            float yaw = fp != null ? -(float)fp.RotationDegrees : 0f;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var f in world.People.Families)
                _tents.Add(Piece(world, registry.Get(VisualSizes.CampTent), VisualSizes.CampTent, f.HomeX, f.HomeZ, yaw, ref min, ref max));
            _fire.Add(Piece(world, registry.Get(VisualSizes.CampFire), VisualSizes.CampFire,
                camp.Fire.XMetres, camp.Fire.ZMetres, yaw, ref min, ref max));
            _logs.Add(Piece(world, registry.Get(VisualSizes.CampLogPile), VisualSizes.CampLogPile,
                camp.Stockpile.XMetres, camp.Stockpile.ZMetres, yaw, ref min, ref max));
            _posts.Add(Piece(world, registry.Get(VisualSizes.CampTiePost), VisualSizes.CampTiePost,
                camp.OxStand.XMetres, camp.OxStand.ZMetres, yaw, ref min, ref max));

            _bounds = new Bounds();
            _bounds.SetMinMax(min - Vector3.one * 4f, max + Vector3.one * 4f);
        }

        private static Matrix4x4 Piece(SimWorld world, VisualDefinition def, string id, double x, double z, float yaw,
            ref Vector3 min, ref Vector3 max)
        {
            var p = new Vector3((float)x, world.Terrain.HeightAt((float)x, (float)z), (float)z);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p + Vector3.up * 4f);
            return VisualSizes.Place(def, id, p, yaw);
        }
    }
}
