using System.Collections.Generic;
using LordsLegacy.Sim.Footprints;
using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Draws the modules placed in operations (hut, log storage, oxen stand) with their <c>visual.module.*</c>
    /// definitions, stretched to the footprint, via <c>Graphics.RenderMeshInstanced</c> (no GameObjects, no colliders,
    /// so they never block ground picking). Rebuilt only when <c>FootprintRegistry.Version</c> changes. Placeholder art.
    /// </summary>
    public sealed class ModuleRenderer : MonoBehaviour
    {
        [SerializeField] private SimHost host;
        [SerializeField] private VisualRegistry registry;

        private readonly Dictionary<string, List<Matrix4x4>> _byVisual = new Dictionary<string, List<Matrix4x4>>();
        private int _count;
        private int _builtVersion = -1;
        private int _builtSites = -1;
        private int _builtInventory = -1;
        private Bounds _bounds;

        /// <summary>Modules currently drawn. For tests.</summary>
        public int BoxCount => _count;

        /// <summary>Sets the SimHost and, optionally, the visual registry (for code that adds the component at run time).</summary>
        public void Configure(SimHost simHost, VisualRegistry visualRegistry = null)
        {
            host = simHost;
            registry = visualRegistry;
            _builtVersion = -1;
        }

        /// <summary>The visual id used for a module type id.</summary>
        public static string VisualIdOf(string moduleTypeId)
        {
            switch (moduleTypeId)
            {
                case ModuleTypes.WoodcuttingHutId:
                case ModuleTypes.ForagerHutId:
                case ModuleTypes.CoppiceHutId:
                case ModuleTypes.ThatcherHutId: return VisualSizes.ModuleHut;
                case ModuleTypes.OxenStandId: return VisualSizes.ModuleOxenStand;
                case ModuleTypes.HouseId: return VisualSizes.ModuleHouse;
                case ModuleTypes.MarketStallId: return VisualSizes.ModuleMarketStall;
                case ModuleTypes.HewingStationId: return VisualSizes.ModuleHewingStation;
                case ModuleTypes.SawpitId: return VisualSizes.ModuleSawpit;
                case ModuleTypes.WeaverId: return VisualSizes.ModuleWeaver;
                case ModuleTypes.WattleYardId: return VisualSizes.ModuleWattleYard;
                case ModuleTypes.DaubPitId: return VisualSizes.ModuleDaubPit;
                case ModuleTypes.StorageYardId: return VisualSizes.ModuleStorageYard;
                default: return VisualSizes.ModuleLogStorage;
            }
        }

        /// <summary>Food crates drawn on one stall: 0 when empty, else 1 to 3 by how full the store is (MK-43).</summary>
        public static int CrateCount(int items, int capacity)
        {
            if (items <= 0 || capacity <= 0) return 0;
            return Mathf.Clamp(Mathf.CeilToInt(3f * items / capacity), 1, 3);
        }

        /// <summary>Food crates currently drawn on stalls. For tests.</summary>
        public int CrateInstanceCount => _byVisual.TryGetValue(VisualSizes.ItemFoodCrate, out var l) ? l.Count : 0;

        // Adds up to three crates along the stall's table top, turned with the stall.
        private void AddCrates(LordsLegacy.Sim.Core.SimWorld world, LordsLegacy.Sim.Footprints.Footprint fp, Vector3 p, float yaw)
        {
            var store = world.Inventory.FindFoodStore(fp.Id);
            if (store == null) return;
            int n = CrateCount(store.Total, store.Capacity);
            if (n == 0) return;
            if (!_byVisual.TryGetValue(VisualSizes.ItemFoodCrate, out var list)) { list = new List<Matrix4x4>(); _byVisual[VisualSizes.ItemFoodCrate] = list; }
            float top = VisualSizes.Of(VisualSizes.ModuleMarketStall).y;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < n; i++)
            {
                var pos = p + rot * new Vector3((i - 1) * 0.9f, top, 0f);
                if (registry != null && registry.TryGet(VisualSizes.ItemFoodCrate, out var def))
                    list.Add(VisualSizes.Place(def, VisualSizes.ItemFoodCrate, pos, yaw));
                else list.Add(Matrix4x4.identity);
            }
        }

        /// <summary>Beam stacks currently drawn beside hewing stations. For tests.</summary>
        public int BeamStackCount => _byVisual.TryGetValue(VisualSizes.ItemBeamStack, out var l) ? l.Count : 0;

        /// <summary>Plank stacks currently drawn beside sawpits. For tests.</summary>
        public int PlankStackCount => _byVisual.TryGetValue(VisualSizes.ItemPlankStack, out var l) ? l.Count : 0;

        /// <summary>Stack height as a fraction of a full stack: 0 when empty, else 0.25 to 1 by how full the store is (T-526).</summary>
        public static float StackFill(int items, int capacity)
        {
            if (items <= 0 || capacity <= 0) return 0f;
            return Mathf.Clamp(0.25f + 0.75f * items / capacity, 0.25f, 1f);
        }

        // Adds one stack of the workshop's output (beams or planks) on the ground in front of the building, turned with it.
        private void AddOutputStack(LordsLegacy.Sim.Core.SimWorld world, LordsLegacy.Sim.Footprints.Footprint fp, Vector3 p, float yaw)
        {
            string vid = fp.Type.Id == ModuleTypes.HewingStationId ? VisualSizes.ItemBeamStack : VisualSizes.ItemPlankStack;
            var item = fp.Type.Id == ModuleTypes.HewingStationId ? LordsLegacy.Sim.Inventory.Items.Beam : LordsLegacy.Sim.Inventory.Items.Plank;
            var store = world.Inventory.FindGoodsStore(fp.Id);
            if (store == null) return;
            float fill = StackFill(store.CountOf(item.Id), store.Capacity);
            if (fill <= 0f) return;
            if (!_byVisual.TryGetValue(vid, out var list)) { list = new List<Matrix4x4>(); _byVisual[vid] = list; }
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var local = new Vector3(0f, 0f, -(fp.Type.DepthMm / 2000f + 1f));
            var pos = p + rot * local;
            pos.y = world.Terrain.HeightAt(pos.x, pos.z);
            if (registry != null && registry.TryGet(vid, out var def))
            {
                // Place() stands a full-height box on the ground; squash it to the fill about its centre and lower it
                // by the height lost so it still sits on the ground (placeholder meshes are centred cubes).
                pos.y -= (1f - fill) * VisualSizes.Of(vid).y * def.Scale * 0.5f;
                list.Add(VisualSizes.Place(def, vid, pos, yaw) * Matrix4x4.Scale(new Vector3(1f, fill, 1f)));
            }
            else list.Add(Matrix4x4.identity);
        }

        private void Update() { Refresh(); }

        /// <summary>Rebuilds if the Sim footprints changed, then draws.</summary>
        public void Refresh()
        {
            if (host == null || !host.IsInitialized) return;
            var world = host.World;
            if (world.Footprints.Version != _builtVersion || world.Construction.Count != _builtSites
                || world.Inventory.Version != _builtInventory) Build(world);
            if (registry == null) return;
            foreach (var kv in _byVisual)
                if (registry.TryGet(kv.Key, out var def)) InstancedDraw.Draw(def, kv.Value, _bounds);
        }

        private void Build(LordsLegacy.Sim.Core.SimWorld world)
        {
            _builtVersion = world.Footprints.Version;
            _builtSites = world.Construction.Count;
            _builtInventory = world.Inventory.Version;
            _byVisual.Clear();
            _count = 0;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var ids = new List<int>();
            foreach (var op in world.Operations.All) ids.AddRange(op.ModuleIds);
            foreach (var home in world.Homes.All) ids.Add(home.HouseFootprintId); // houses are not operation modules (HM-20)
            foreach (var f in world.Footprints.All) if (f.Type.IsStandalone) ids.Add(f.Id); // granary: one piece, in no operation (FD-30)
            foreach (var f in world.Footprints.All) if (f.Type.Id == ModuleTypes.MarketStallId) ids.Add(f.Id); // stalls stand in no operation (MK-21)
            {
                foreach (int id in ids)
                {
                    var fp = world.Footprints.Get(id);
                    if (fp == null) continue;
                    if (world.Construction.Get(id) != null) continue; // unbuilt: ConstructionRenderer draws the scaffold
                    string vid = VisualIdOf(fp.Type.Id);
                    var box = VisualSizes.Of(vid);
                    float x = (float)fp.Centre.XMetres, z = (float)fp.Centre.ZMetres;
                    var p = new Vector3(x, world.Terrain.HeightAt(x, z), z);
                    // Footprint rotation is counter-clockwise (x towards z); a Unity yaw turns z towards x, hence the sign.
                    float yaw = -(float)fp.RotationDegrees;
                    Matrix4x4 m;
                    if (registry != null && registry.TryGet(vid, out var def))
                    {
                        var mb = def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                        var s = new Vector3(fp.Type.WidthMm / 1000f / Mathf.Max(0.001f, mb.size.x),
                            box.y * def.Scale / Mathf.Max(0.001f, mb.size.y),
                            fp.Type.DepthMm / 1000f / Mathf.Max(0.001f, mb.size.z));
                        m = Matrix4x4.TRS(new Vector3(p.x, p.y - mb.min.y * s.y, p.z), Quaternion.Euler(0f, yaw, 0f), s);
                    }
                    else m = Matrix4x4.identity;
                    if (!_byVisual.TryGetValue(vid, out var list)) { list = new List<Matrix4x4>(); _byVisual[vid] = list; }
                    list.Add(m);
                    _count++;
                    if (fp.Type.Id == ModuleTypes.MarketStallId) AddCrates(world, fp, p, yaw);
                    if (fp.Type.Id == ModuleTypes.HewingStationId || fp.Type.Id == ModuleTypes.SawpitId) AddOutputStack(world, fp, p, yaw);
                    float r = Mathf.Max(fp.Type.WidthMm, fp.Type.DepthMm) / 1000f;
                    min = Vector3.Min(min, p - new Vector3(r, 1f, r));
                    max = Vector3.Max(max, p + new Vector3(r, box.y + 1f, r));
                }
            }
            _fenceCount = 0;
            AddFences(world, ref min, ref max);
            _bounds = new Bounds();
            if (_count > 0 || _fenceCount > 0) _bounds.SetMinMax(min, max);
        }

        /// <summary>Fence segments and gate posts currently drawn for built house fences (T-593). For tests.</summary>
        public int FenceInstanceCount => _fenceCount;

        private int _fenceCount;

        // Built fences only: a fence under construction does not block yet (HL-12) and shows the scaffold instead.
        private void AddFences(LordsLegacy.Sim.Core.SimWorld world, ref Vector3 min, ref Vector3 max)
        {
            foreach (var home in world.Homes.All)
            {
                if (home.FenceFootprintId == 0 || world.Footprints.Get(home.FenceFootprintId) == null
                    || world.Construction.Get(home.FenceFootprintId) != null) continue;
                var plot = world.Areas.Get(home.PlotAreaId);
                if (plot == null) continue;
                var line = LordsLegacy.Sim.Areas.FenceLine.Of(plot);
                float panel = Mathf.Max(0.5f, (float)world.Tuning.FencePanelLengthM);
                foreach (var run in line.Runs)
                    for (int k = 0; k + 1 < run.Count; k++)
                    {
                        Vector2 a = new Vector2((float)run[k].XMetres, (float)run[k].ZMetres);
                        Vector2 b = new Vector2((float)run[k + 1].XMetres, (float)run[k + 1].ZMetres);
                        int pieces = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / panel - 0.001f));
                        for (int i = 0; i < pieces; i++)
                            AddFencePiece(world, VisualSizes.FenceSegment, Vector2.Lerp(a, b, (i + 0.5f) / pieces),
                                b - a, (b - a).magnitude / pieces, ref min, ref max);
                    }
                if (line.HasFrontage) AddGate(world, plot, line, ref min, ref max);
            }
        }

        // Two posts either side of the access point along the frontage edge: the gap between them is the open gate.
        private void AddGate(LordsLegacy.Sim.Core.SimWorld world, LordsLegacy.Sim.Areas.AreaRecord plot,
            LordsLegacy.Sim.Areas.FenceLine line, ref Vector3 min, ref Vector3 max)
        {
            var outline = plot.ShapeOrderOutline(out int[] starts);
            Vector2 acc = new Vector2((float)line.AccessPoint.XMetres, (float)line.AccessPoint.ZMetres);
            Vector2 dir = Vector2.right;
            float best = float.MaxValue;
            for (int e = 0; e < starts.Length; e++)
            {
                if (plot.Shape.Edges[e].Kind != LordsLegacy.Sim.Areas.AreaEdgeKind.FollowsRoad) continue;
                int end = e + 1 < starts.Length ? starts[e + 1] : outline.Length;
                for (int k = starts[e]; k < end; k++)
                {
                    var p0 = outline[k % outline.Length]; var p1 = outline[(k + 1) % outline.Length];
                    Vector2 a = new Vector2((float)p0.XMetres, (float)p0.ZMetres), b = new Vector2((float)p1.XMetres, (float)p1.ZMetres);
                    Vector2 d = b - a;
                    if (d.sqrMagnitude < 1e-6f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(acc - a, d) / d.sqrMagnitude);
                    float dist = (a + d * t - acc).sqrMagnitude;
                    if (dist < best) { best = dist; dir = d.normalized; }
                }
            }
            float half = Mathf.Max(0.5f, (float)world.Tuning.FencePanelLengthM * 0.5f);
            AddFencePiece(world, VisualSizes.FenceGatePost, acc - dir * half, dir, 0.2f, ref min, ref max);
            AddFencePiece(world, VisualSizes.FenceGatePost, acc + dir * half, dir, 0.2f, ref min, ref max);
        }

        private void AddFencePiece(LordsLegacy.Sim.Core.SimWorld world, string vid, Vector2 at, Vector2 dir, float length,
            ref Vector3 min, ref Vector3 max)
        {
            var box = VisualSizes.Of(vid);
            var p = new Vector3(at.x, world.Terrain.HeightAt(at.x, at.y), at.y);
            float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Matrix4x4 m;
            if (registry != null && registry.TryGet(vid, out var def))
            {
                var mb = def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                // Segments are stretched to their length along the run; posts keep their own width.
                float lx = vid == VisualSizes.FenceSegment ? length : box.x;
                var s = new Vector3(lx / Mathf.Max(0.001f, mb.size.x), box.y * def.Scale / Mathf.Max(0.001f, mb.size.y),
                    box.z / Mathf.Max(0.001f, mb.size.z));
                m = Matrix4x4.TRS(new Vector3(p.x, p.y - mb.min.y * s.y, p.z), Quaternion.Euler(0f, yaw, 0f), s);
            }
            else m = Matrix4x4.identity;
            if (!_byVisual.TryGetValue(vid, out var list)) { list = new List<Matrix4x4>(); _byVisual[vid] = list; }
            list.Add(m);
            _fenceCount++;
            min = Vector3.Min(min, p - new Vector3(length, 1f, length));
            max = Vector3.Max(max, p + new Vector3(length, box.y + 1f, length));
        }
    }
}
