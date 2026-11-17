using System.Collections.Generic;
using LordsLegacy.Game.Tools;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Terrain;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// While a placing tool is active (area, plot, market, granary or a module ghost) draws every existing plot outline
    /// (areas, house plots, market, granary yards) with a small dot on each corner, plus the 2 m no-build circle of every
    /// ground pile that holds items (placement.md PL-12, PL-15). A corner the cursor is snapped to is drawn larger and
    /// bright (AR-20). Pure view: it reads the Sim and the active tool and holds no rules. Rebuilds only when something it
    /// draws changes.
    /// </summary>
    public sealed class PlacementOverlay : MonoBehaviour
    {
        /// <summary>Outline colour (all plots look alike here; the area renderer keeps the per-kind colours).</summary>
        public static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.75f);
        /// <summary>Corner dot colour.</summary>
        public static readonly Color CornerColor = new Color(1f, 0.9f, 0.4f, 1f);
        /// <summary>Colour of the corner the cursor is snapped to.</summary>
        public static readonly Color SnappedColor = new Color(0.4f, 1f, 0.5f, 1f);
        /// <summary>Colour of a pile's no-build circle.</summary>
        public static readonly Color PileColor = new Color(0.9f, 0.3f, 0.2f, 0.9f);

        private const float Lift = 0.22f, OutlineWidthM = 0.2f, CornerR = 0.3f, SnappedR = 0.6f;

        private SimHost _host;
        private ToolController _controller;
        private GameObject _object;
        private Mesh _mesh;
        private Material _material;
        private bool _shown;
        private int _areaVersion = -1, _footVersion = -1, _ledgerVersion = -1;
        private PointMm? _builtHighlight;

        /// <summary>True while the overlay is drawn.</summary>
        public bool IsShown => _shown;
        /// <summary>Plot outlines in the current mesh.</summary>
        public int OutlineCount { get; private set; }
        /// <summary>Corner dots in the current mesh.</summary>
        public int CornerCount { get; private set; }
        /// <summary>Pile circles in the current mesh.</summary>
        public int PileCircleCount { get; private set; }
        /// <summary>True while a corner is drawn highlighted as snapped.</summary>
        public bool HasSnappedCorner { get; private set; }

        /// <summary>Sets the host and tool controller (for code that adds the component at run time).</summary>
        public void Configure(SimHost host, ToolController controller)
        {
            _host = host; _controller = controller; _areaVersion = -1;
        }

        /// <summary>True when the tool counts as a placing tool (PL-12).</summary>
        public static bool IsPlacingTool(ITool tool) =>
            tool is AreaTool || tool is PlotTool || tool is MarketTool || tool is StructureTool || tool is OperationDraftTool;

        private void Update()
        {
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            if (_controller == null) _controller = FindFirstObjectByType<ToolController>();
            bool want = _host != null && _host.IsInitialized && _controller != null && IsPlacingTool(_controller.ActiveTool);
            if (!want) { Hide(); return; }
            var snapped = FindSnapped();
            var w = _host.World;
            if (_shown && w.Areas.Version == _areaVersion && w.Footprints.Version == _footVersion
                && w.Inventory.Version == _ledgerVersion && snapped == _builtHighlight) return;
            Build(snapped);
        }

        private static PointMm? FindSnapped()
        {
            foreach (var a in FindObjectsByType<AreaTool>(FindObjectsSortMode.None))
            {
                var s = a.SnappedHover;
                if (s.HasValue) return s;
            }
            return null;
        }

        private void Hide()
        {
            _shown = false; _areaVersion = -1;
            OutlineCount = 0; CornerCount = 0; PileCircleCount = 0; HasSnappedCorner = false;
            if (_object != null) _object.SetActive(false);
        }

        private void Build(PointMm? snapped)
        {
            var w = _host.World;
            _areaVersion = w.Areas.Version; _footVersion = w.Footprints.Version; _ledgerVersion = w.Inventory.Version;
            _builtHighlight = snapped;
            var ground = w.Terrain;
            var b = new DrapeMesh();
            var corners = new List<PointMm>();
            OutlineCount = 0; PileCircleCount = 0;
            foreach (var a in w.Areas.All) { AddOutline(b, ground, a.Polygon); corners.AddRange(a.Shape.Vertices); }
            foreach (var f in w.Footprints.All)
            {
                if (!f.Type.IsStandalone) continue;
                AddOutline(b, ground, f.PlotPolygon);
                corners.AddRange(f.PlotPolygon);
            }
            HasSnappedCorner = false;
            foreach (var c in corners)
            {
                bool hot = snapped.HasValue && snapped.Value == c;
                if (hot) HasSnappedCorner = true;
                AddDot(b, ground, c, hot ? SnappedR : CornerR, hot ? SnappedColor : CornerColor, hot ? Lift + 0.03f : Lift + 0.02f);
            }
            CornerCount = corners.Count;
            foreach (var z in PileZones.Of(w))
            {
                var ring = new List<Vector2>(48);
                float cx = (float)z.Centre.XMetres, cz = (float)z.Centre.ZMetres, r = (float)z.RadiusM;
                for (int i = 0; i < 48; i++)
                {
                    float t = i * Mathf.PI * 2f / 48f;
                    ring.Add(new Vector2(cx + Mathf.Cos(t) * r, cz + Mathf.Sin(t) * r));
                }
                b.Append(TerrainDrape.Ribbon(ground, ring, OutlineWidthM, Lift + 0.01f, PileColor, true, false));
                PileCircleCount++;
            }
            if (_object == null)
            {
                _object = new GameObject("Placement overlay");
                _object.transform.SetParent(transform, false);
                _object.AddComponent<MeshFilter>();
                var mr = _object.AddComponent<MeshRenderer>();
                var shared = GroundOverlay.Material();
                _material = shared != null ? new Material(shared) { name = "PlacementOverlayMaterial" }
                    : new Material(Shader.Find("Sprites/Default")) { name = "PlacementOverlayMaterial" };
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (_mesh != null) Destroy(_mesh);
            _mesh = b.ToMesh("Placement overlay");
            _object.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _object.SetActive(true);
            _shown = true;
        }

        private void AddOutline(DrapeMesh b, IHeightField ground, IReadOnlyList<PointMm> poly)
        {
            if (poly == null || poly.Count < 3) return;
            var pts = new List<Vector2>(poly.Count);
            foreach (var p in poly) pts.Add(new Vector2((float)p.XMetres, (float)p.ZMetres));
            b.Append(TerrainDrape.Ribbon(ground, pts, OutlineWidthM, Lift, OutlineColor, true, false));
            OutlineCount++;
        }

        private static void AddDot(DrapeMesh b, IHeightField ground, PointMm p, float r, Color c, float lift)
        {
            float x = (float)p.XMetres, z = (float)p.ZMetres;
            int i = b.Add(TerrainDrape.Point(ground, x - r, z, lift), c);
            b.Add(TerrainDrape.Point(ground, x, z + r, lift), c);
            b.Add(TerrainDrape.Point(ground, x + r, z, lift), c);
            b.Add(TerrainDrape.Point(ground, x, z - r, lift), c);
            b.Tri(i, i + 1, i + 2);
            b.Tri(i, i + 2, i + 3);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
