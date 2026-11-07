using System.Collections;
using System.Collections.Generic;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="TreeRenderer"/> against the Game scene.</summary>
    public class TreeRendererTests
    {
        private SimHost _host;
        private VisualRegistry _registry;
        private VisualDefinition _def;
        private Camera _cam;
        private TreeRenderer _renderer;

        private IEnumerator Setup()
        {
            var load = SceneManager.LoadSceneAsync("Game");
            Assert.IsNotNull(load, "Game scene is not in Build Settings.");
            yield return load;
            yield return null;

            _host = Object.FindObjectOfType<SimHost>();
            Assert.IsTrue(_host != null && _host.IsInitialized, "SimHost not initialised.");
            _host.AutoUpdate = false;

            Material material = null;
            Mesh mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
#if UNITY_EDITOR
            var real = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualRegistry>("Assets/Game/Visuals/VisualRegistry.asset");
            Assert.IsTrue(real.TryGet(TreeRenderer.TreeVisualId, out var realDef), "visual.tree.generic missing.");
            material = realDef.Materials[0];
            mesh = realDef.Mesh;
#endif
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            _def = ScriptableObject.CreateInstance<VisualDefinition>();
            _def.Configure(TreeRenderer.TreeVisualId, mesh, new[] { material }, 1f, true);
            _registry.Register(_def);

            var camGo = new GameObject("TestCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 60f;

            var go = new GameObject("TestTreeRenderer");
            _renderer = go.AddComponent<TreeRenderer>();
            _renderer.Configure(_host, _registry, _cam);
        }

        private static Vector3 ForestPoint(SimHost host)
        {
            foreach (var t in host.World.Trees.All)
                return new Vector3(t.X, host.World.Terrain.HeightAt(t.X, t.Z), t.Z);
            Assert.Fail("No trees.");
            return default;
        }

        [TearDown]
        public void TearDown()
        {
            if (_renderer != null) Object.Destroy(_renderer.gameObject);
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_def != null) Object.Destroy(_def);
            if (_registry != null) Object.Destroy(_registry);
        }

        [UnityTest]
        public IEnumerator SwappingMeshLeavesTreeRecordsUnchanged()
        {
            yield return Setup();
            var before = Snapshot();
            Assert.Greater(before.Count, 0);

            _def.Configure(TreeRenderer.TreeVisualId, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),
                _def.Materials, 2f, false);
            _renderer.Invalidate();
            yield return null;
            yield return null;

            var after = Snapshot();
            CollectionAssert.AreEqual(before, after);
        }

        /// <summary>
        /// Builds its own SimHost from SyntheticMap (not the Game scene), removes a tree through the Sim
        /// and checks the drawn instance count drops by one.
        /// </summary>
        [UnityTest]
        public IEnumerator RemovingTreeInSimDropsDrawnInstanceCount()
        {
            var hostGo = new GameObject("SyntheticSimHost");
            var host = hostGo.AddComponent<SimHost>();
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            host.AutoUpdate = false;

            var registry = ScriptableObject.CreateInstance<VisualRegistry>();
            var def = ScriptableObject.CreateInstance<VisualDefinition>();
            def.Configure(TreeRenderer.TreeVisualId, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, false);
            registry.Register(def);

            var camGo = new GameObject("SyntheticCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 2000f;
            cam.transform.position = new Vector3(100f, 300f, 100f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var go = new GameObject("SyntheticTreeRenderer");
            var renderer = go.AddComponent<TreeRenderer>();
            renderer.Configure(host, registry, cam);
            yield return null;

            try
            {
                renderer.Draw();
                Assert.AreEqual(5, renderer.DrawnInstanceCount, "All five synthetic trees should be drawn.");
                var before = new Matrix4x4[renderer.CellCount][];
                for (int i = 0; i < before.Length; i++) before[i] = renderer.GetCellMatrices(i);

                Assert.IsTrue(host.World.Trees.Remove(new TreeId(3)), "Tree 3 should be removed through the Sim.");
                renderer.Draw();
                Assert.AreEqual(4, renderer.DrawnInstanceCount, "Drawn count should drop by one after removal.");

                // Each synthetic tree is in its own 64 m cell: the emptied cell goes, the rest keep their arrays.
                Assert.AreEqual(5, before.Length);
                Assert.AreEqual(4, renderer.CellCount);
                var kept = new[] { before[0], before[1], before[3], before[4] };
                for (int i = 0; i < 4; i++)
                    Assert.AreSame(kept[i], renderer.GetCellMatrices(i), "Untouched cell " + i + " was rebuilt.");
            }
            finally
            {
                Object.Destroy(go);
                Object.Destroy(camGo);
                Object.Destroy(hostGo);
                Object.Destroy(def);
                Object.Destroy(registry);
            }
            yield return null;
        }

        private List<string> Snapshot()
        {
            var list = new List<string>();
            foreach (var t in _host.World.Trees.All)
                list.Add(t.Id.Value + "|" + t.X + "|" + t.Z + "|" + t.TrunkLengthM);
            return list;
        }

        [UnityTest]
        public IEnumerator RaycastDownThroughTreeHitsTerrain()
        {
            yield return Setup();
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Assert.IsNull(_renderer.GetComponentInChildren<Collider>(), "Tree renderer must have no colliders.");

            int checkedCount = 0;
            foreach (var t in _host.World.Trees.All)
            {
                if (checkedCount++ >= 5) break;
                var origin = new Vector3(t.X, 5000f, t.Z);
                Assert.IsTrue(Physics.Raycast(origin, Vector3.down, out var hit, 10000f), "Raycast hit nothing.");
                Assert.IsInstanceOf<TerrainCollider>(hit.collider, "Ray hit " + hit.collider.name + " instead of terrain.");
                Assert.AreEqual(_host.World.Terrain.HeightAt(t.X, t.Z), hit.point.y, 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator CameraOverForestSeesCellsAndAwayFewer()
        {
            yield return Setup();
            var p = ForestPoint(_host);

            _cam.transform.position = p + Vector3.up * 60f;
            _cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            yield return null;
            yield return null;
            int visibleOver = _renderer.VisibleCellCount;
            Assert.GreaterOrEqual(visibleOver, 1);
            Assert.Greater(_renderer.DrawnInstanceCount, 0);

            _cam.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // straight up
            yield return null;
            yield return null;
            Assert.Less(_renderer.VisibleCellCount, visibleOver);
        }
    }
}
