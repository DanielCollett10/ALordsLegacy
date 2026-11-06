using LordsLegacy.Sim.Terrain;
using UnityEngine;

namespace LordsLegacy.Game
{
    /// <summary>
    /// Turns a screen position into a ground sample. A ray from the camera is cast against the scene
    /// (the map-1 TerrainCollider is the expected hit); only its x/z is used. Height and slope come from the
    /// Sim's <see cref="IHeightField"/> at that x/z. The <c>TryPick*</c> methods take a screen position or ray
    /// so tests do not need a real mouse.
    /// </summary>
    public sealed class GroundPicker : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private SimHost _host;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private float _maxDistance = 10000f;

        /// <summary>Camera used for screen picks. Null means <see cref="Camera.main"/>.</summary>
        public Camera PickCamera { get => _camera; set => _camera = value; }

        /// <summary>SimHost that owns the terrain. Null means the first SimHost in the scene.</summary>
        public SimHost Host { get => _host; set => _host = value; }

        /// <summary>Reads height and slope at (x,z) from the given Sim terrain. Null terrain gives <see cref="GroundSample.None"/>.</summary>
        public static GroundSample Read(IHeightField terrain, float x, float z)
        {
            if (terrain == null) return GroundSample.None;
            return new GroundSample(x, z, terrain.HeightAt(x, z), terrain.SlopeDegAt(x, z), true);
        }

        /// <summary>Reads the Sim at (x,z) without any ray. Returns None if the Sim is not ready.</summary>
        public GroundSample ReadAt(float x, float z) => Read(SimTerrain(), x, z);

        /// <summary>Casts a ray through a screen position (pixels, origin bottom-left) and reads the hit.</summary>
        public bool TryPickScreen(Vector2 screenPos, out GroundSample sample)
        {
            var cam = ResolveCamera();
            if (cam == null) { sample = GroundSample.None; return false; }
            return TryPickRay(cam.ScreenPointToRay(screenPos), out sample);
        }

        /// <summary>Casts the ray and reads the Sim at the hit point's x/z. False if nothing was hit or the Sim is not ready.</summary>
        public bool TryPickRay(Ray ray, out GroundSample sample)
        {
            sample = GroundSample.None;
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _mask)) return false;
            var terrain = SimTerrain();
            if (terrain == null) return false;
            sample = Read(terrain, hit.point.x, hit.point.z);
            return true;
        }

        private Camera ResolveCamera()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        private IHeightField SimTerrain()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            return _host != null && _host.IsInitialized ? _host.World.Terrain : null;
        }
    }
}
