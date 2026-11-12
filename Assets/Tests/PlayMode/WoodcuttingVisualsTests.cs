using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the log pile, stump and felled trunk drawing (T-280).</summary>
    public class WoodcuttingVisualsTests
    {
        private GameObject _hostGo, _rendGo;
        private VisualRegistry _registry;

        private SimHost MakeHost()
        {
            _hostGo = new GameObject("WoodVisualsHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            host.AutoUpdate = false;
            return host;
        }

        private VisualRegistry MakeRegistry()
        {
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.ItemLog, VisualSizes.TreeStump, VisualSizes.TreeFelled })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }
            return _registry;
        }

        [TearDown]
        public void TearDown()
        {
            if (_rendGo != null) Object.DestroyImmediate(_rendGo);
            if (_hostGo != null) Object.DestroyImmediate(_hostGo);
            if (_registry != null)
            {
                foreach (var d in _registry.Definitions) Object.DestroyImmediate(d);
                Object.DestroyImmediate(_registry);
            }
        }

        [UnityTest]
        public IEnumerator LogModelsDrawnEqualLedgerLogsOnTheGround()
        {
            var host = MakeHost();
            var world = host.World;
            _rendGo = new GameObject("LogPileRenderer");
            var r = _rendGo.AddComponent<LogPileRenderer>();
            r.Configure(host, MakeRegistry());
            yield return null;
            r.Draw();
            Assert.AreEqual(0, r.LogCount);

            world.Inventory.ProduceOnGround(PointMm.FromMetres(50, 50), 1, Items.Log, 7);
            world.Inventory.ProduceOnGround(PointMm.FromMetres(90, 90), 1, Items.Log, 3);
            // A storage holds logs too: those are not on the ground and are not drawn as piles.
            world.Inventory.Produce(world.Inventory.GetOrAddStorage(99), Items.Log, 5);
            r.Draw();
            int onGround = 0;
            foreach (var l in world.Inventory.Locations)
                if (l.Kind == LocationKind.GroundPile) onGround += l.CountOf(Items.Log.Id);
            Assert.AreEqual(10, onGround);
            Assert.AreEqual(onGround, r.LogCount, "One log model per ground log.");
            Assert.AreEqual(15, world.Inventory.TotalOf(Items.Log));
            Assert.IsNull(_rendGo.GetComponentInChildren<Collider>(), "No colliders.");
        }

        [UnityTest]
        public IEnumerator StumpsAndFelledTrunksAreDrawnAndStandingTreesDrop()
        {
            var host = MakeHost();
            var trees = host.World.Trees;
            var ids = new System.Collections.Generic.List<TreeId>();
            foreach (var t in trees.All) ids.Add(t.Id);
            Assert.GreaterOrEqual(ids.Count, 3);

            _rendGo = new GameObject("TreeRenderer");
            var r = _rendGo.AddComponent<TreeRenderer>();
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(_rendGo.transform);
            var reg = MakeRegistry();
            var treeDef = ScriptableObject.CreateInstance<VisualDefinition>();
            treeDef.Configure(TreeRenderer.TreeVisualId, Resources.GetBuiltinResource<Mesh>("Capsule.fbx"), new Material[0], 1f, true);
            reg.Register(treeDef);
            r.Configure(host, reg, cam);
            yield return null;
            r.Draw();
            Assert.AreEqual(0, r.StumpCount);
            Assert.AreEqual(0, r.FelledCount);

            int standingBefore = trees.Count;
            trees.SetState(ids[0], TreeState.Felled);
            trees.SetState(ids[1], TreeState.Stump);
            trees.SetState(ids[2], TreeState.Felled);
            trees.SetState(ids[2], TreeState.Stump);
            r.Draw();
            Assert.AreEqual(2, r.StumpCount);
            Assert.AreEqual(1, r.FelledCount);
            Assert.AreEqual(standingBefore - 3, trees.Count, "Felled and stump trees are not standing.");
            Object.DestroyImmediate(treeDef);
        }
    }
}
