using System.Collections;
using System.Collections.Generic;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
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
