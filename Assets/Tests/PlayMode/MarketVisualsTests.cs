using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for the market stall and food crate placeholder visuals (T-478, MK-43).</summary>
    public class MarketVisualsTests
    {
        private GameObject _hostGo, _rendGo;
        private VisualRegistry _registry;

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

        [Test]
        public void CrateCountFollowsFill()
        {
            Assert.AreEqual(0, ModuleRenderer.CrateCount(0, 30));
            Assert.AreEqual(1, ModuleRenderer.CrateCount(1, 30));
            Assert.AreEqual(1, ModuleRenderer.CrateCount(10, 30));
            Assert.AreEqual(2, ModuleRenderer.CrateCount(11, 30));
            Assert.AreEqual(2, ModuleRenderer.CrateCount(20, 30));
            Assert.AreEqual(3, ModuleRenderer.CrateCount(21, 30));
            Assert.AreEqual(3, ModuleRenderer.CrateCount(30, 30));
        }

        [Test]
        public void StallMapsToItsOwnVisualId()
        {
            Assert.AreEqual("visual.module.market_stall", ModuleRenderer.VisualIdOf(ModuleTypes.MarketStallId));
            Assert.AreEqual("visual.item.food_crate", VisualSizes.ItemFoodCrate);
        }

        [UnityTest]
        public IEnumerator BuiltStallDrawsTableAndCratesByStock()
        {
            _hostGo = new GameObject("MarketVisualsHost");
            var host = _hostGo.AddComponent<SimHost>();
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            host.Initialize(new SimTuning { BushCount = 0 }, map);
            host.AutoUpdate = false;
            var w = host.World;
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.ModuleMarketStall, VisualSizes.ItemFoodCrate })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }
            _rendGo = new GameObject("MarketVisuals");
            var modules = _rendGo.AddComponent<ModuleRenderer>();
            modules.Configure(host, _registry);

            var stall = w.Footprints.Add(w.Footprints.NextId(), ModuleTypes.MarketStall, PointMm.FromMetres(100, 100), 0);
            var store = w.Inventory.AddFoodStore(ModuleTypes.MarketStall.FoodCapacity, stall.Id);
            yield return null;

            modules.Refresh();
            Assert.AreEqual(1, modules.BoxCount, "A built stall draws one table.");
            Assert.AreEqual(0, modules.CrateInstanceCount, "An empty stall shows no crates.");

            w.Inventory.Produce(store, Items.Berries, 12);
            modules.Refresh();
            Assert.AreEqual(2, modules.CrateInstanceCount, "12 of 30 shows 2 crates.");

            w.Inventory.Produce(store, Items.Berries, 18);
            modules.Refresh();
            Assert.AreEqual(3, modules.CrateInstanceCount, "A full stall shows the 3-crate maximum.");
        }
    }
}
