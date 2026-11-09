using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>Which mesh a <see cref="CursorMarker"/> shows.</summary>
    public enum CursorMarkerShape
    {
        /// <summary>A flat ring lying on the ground.</summary>
        Ring,
        /// <summary>A sphere standing on the ground.</summary>
        Sphere
    }

    /// <summary>
    /// Ground marker that a tool shows at a world point: a ring or a sphere, in a normal or a snapped colour.
    /// Presentation only (decision 0017): it reads the Sim terrain height and never holds game rules.
    /// It sits at the Sim terrain height plus <see cref="HeightOffsetM"/> and has no collider, so it cannot
    /// block picking. Tools create it at run time with <see cref="Create"/>; no scene change is needed.
    /// </summary>
    public sealed class CursorMarker : MonoBehaviour
    {
        /// <summary>Height of the marker above the Sim terrain, in metres.</summary>
        public const float HeightOffsetM = 0.05f;
        /// <summary>Colour when the marker shows a free point.</summary>
        public static readonly Color NormalColor = new Color(1f, 1f, 1f, 0.9f);
        /// <summary>Colour when the marker shows a snap (road node, area vertex or edge). Clearly not white.</summary>
        public static readonly Color SnappedColor = new Color(0.2f, 1f, 0.35f, 1f);

        [SerializeField] private SimHost host;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Material _normalMaterial;
        private Material _snappedMaterial;
        private CursorMarkerShape _shape = CursorMarkerShape.Ring;
        private float _diameterM = 1f;
        private bool _snapped;

        /// <summary>The shape currently shown.</summary>
        public CursorMarkerShape Shape => _shape;

        /// <summary>The diameter currently used, in metres.</summary>
        public float DiameterM => _diameterM;

        /// <summary>True after <see cref="Show"/> was last called with snapped = true.</summary>
        public bool IsSnapped => _snapped;

        /// <summary>True while the marker is shown.</summary>
        public bool IsShown => gameObject.activeSelf;

        /// <summary>The colour the marker is drawn in.</summary>
        public Color CurrentColor => _snapped ? SnappedColor : NormalColor;

        /// <summary>The mesh in use, or null before the first <see cref="SetShape"/>.</summary>
        public Mesh CurrentMesh => _filter != null ? _filter.sharedMesh : null;

        /// <summary>
        /// Creates a marker as a child of <paramref name="parent"/> at run time, hidden until <see cref="Show"/>.
        /// </summary>
        /// <param name="parent">Parent transform (for example the tool's root).</param>
        /// <param name="simHost">The host whose Sim terrain the marker follows.</param>
        /// <returns>The new marker component.</returns>
        public static CursorMarker Create(Transform parent, SimHost simHost)
        {
            var go = new GameObject("CursorMarker");
            go.transform.SetParent(parent, false);
            var marker = go.AddComponent<CursorMarker>();
            marker.Configure(simHost);
            marker.gameObject.SetActive(false);
            return marker;
        }

        /// <summary>Sets the Sim host (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost)
        {
            host = simHost;
        }

        /// <summary>
        /// Chooses the shape and diameter. The mesh is rebuilt only when either changes.
        /// </summary>
        /// <param name="shape">Ring or Sphere.</param>
        /// <param name="diameterM">Diameter in metres, set by the caller.</param>
        public void SetShape(CursorMarkerShape shape, float diameterM)
        {
            EnsureRenderer();
            float d = Mathf.Max(0.01f, diameterM);
            if (_filter.sharedMesh != null && shape == _shape && Mathf.Approximately(d, _diameterM)) return;
            _shape = shape;
            _diameterM = d;
            if (_filter.sharedMesh != null) Destroy(_filter.sharedMesh);
            _filter.sharedMesh = shape == CursorMarkerShape.Ring
                ? CursorMarkerMesh.Ring(d)
                : CursorMarkerMesh.Sphere(d);
        }

        /// <summary>
        /// Shows the marker at Sim (x, z) on the terrain height plus <see cref="HeightOffsetM"/>.
        /// </summary>
        /// <param name="x">World x in metres (Sim x).</param>
        /// <param name="z">World z in metres (Sim z).</param>
        /// <param name="snapped">True to draw in <see cref="SnappedColor"/>, false for <see cref="NormalColor"/>.</param>
        public void Show(float x, float z, bool snapped)
        {
            EnsureRenderer();
            if (_filter.sharedMesh == null) SetShape(_shape, _diameterM);
            float groundY = host != null && host.IsInitialized ? host.World.Terrain.HeightAt(x, z) : 0f;
            transform.position = new Vector3(x, groundY + HeightOffsetM, z);
            _snapped = snapped;
            _renderer.sharedMaterial = snapped ? GetMaterial(ref _snappedMaterial, SnappedColor, "CursorSnapped")
                : GetMaterial(ref _normalMaterial, NormalColor, "CursorNormal");
            gameObject.SetActive(true);
        }

        /// <summary>Hides the marker.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnDestroy()
        {
            if (_filter != null && _filter.sharedMesh != null) Destroy(_filter.sharedMesh);
            if (_normalMaterial != null) Destroy(_normalMaterial);
            if (_snappedMaterial != null) Destroy(_snappedMaterial);
        }

        private void EnsureRenderer()
        {
            if (_filter != null) return;
            _filter = GetComponent<MeshFilter>();
            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private static Material GetMaterial(ref Material cache, Color colour, string name)
        {
            if (cache == null)
            {
                // GroundOverlay is unlit, alpha blended, tints by the material colour and biases depth (decision 0020).
                var shared = GroundOverlay.Material();
                cache = shared != null
                    ? new Material(shared) { name = name, color = colour }
                    : new Material(Shader.Find("Sprites/Default")) { name = name, color = colour };
            }
            return cache;
        }
    }
}
