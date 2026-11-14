using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
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
    /// <summary>PlayMode tests for scaffolds and the site log pile (T-354, CN-22, CN-42).</summary>
    public class ConstructionVisualsTests
    {
        private GameObject _hostGo, _rendGo;
        private VisualRegistry _registry;
        private SimHost _host;
        private int _opId;

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private void Setup()
        {
            _hostGo = new GameObject("ConstructionVisualsHost");
            _host = _hostGo.AddComponent<SimHost>();
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            _host.Initialize(new SimTuning { StartHour = 8 }, map);
            _host.AutoUpdate = false;
            var r = _host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(90, 90), M(120, 90), M(120, 110), M(90, 110) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(r.IsAccepted, r.Text);
            _opId = _host.World.Operations.FindByArea((int)r.Created[0]).Id;
            _host.World.Inventory.Produce(_host.World.Inventory.AddStockpile(M(95, 95)), Items.Log, 20);

            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.ItemLog, VisualSizes.ConstructionScaffold, VisualSizes.ModuleHut,
                VisualSizes.ModuleOxenStand, VisualSizes.ModuleLogStorage })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }
            _rendGo = new GameObject("ConstructionVisuals");
        }

        private int Place(string type, double x, double z)
        {
            var r = _host.Execute(new PlaceModule(_opId, type, M(x, z), 0));
            Assert.IsTrue(r.IsAccepted, r.Text);
            return (int)r.Created[0];
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
        public IEnumerator SiteLogModelsEqualLedgerCountAndShrinkWhenConsumed()
        {
            Setup();
            var piles = _rendGo.AddComponent<LogPileRenderer>();
            piles.Configure(_host, _registry);
            int fp = Place(ModuleTypes.WoodcuttingHutId, 105, 100);
            var world = _host.World;
            var site = world.Inventory.FindSite(fp);
            Assert.IsNotNull(site);
            yield return null;
            piles.Draw();
            Assert.AreEqual(0, piles.DrawnFor(site.Id));

            world.Inventory.Produce(site, Items.Log, 5);
            piles.Draw();
            Assert.AreEqual(site.CountOf(Items.Log.Id), piles.DrawnFor(site.Id));
            Assert.AreEqual(5, piles.DrawnFor(site.Id));

            Assert.IsTrue(world.Inventory.Consume(site, Items.Log, 2));
            piles.Draw();
            Assert.AreEqual(3, piles.DrawnFor(site.Id), "The pile shrinks as logs are consumed.");
            Assert.IsNull(_rendGo.GetComponentInChildren<Collider>(), "No colliders.");
        }

        [UnityTest]
        public IEnumerator OxenStandSiteShowsAThreeByThreeScaffoldAndNoModule()
        {
            Setup();
            var scaffold = _rendGo.AddComponent<ConstructionRenderer>();
            scaffold.Configure(_host, _registry);
            var modules = _rendGo.AddComponent<ModuleRenderer>();
            modules.Configure(_host, _registry);
            int fp = Place(ModuleTypes.OxenStandId, 105, 100);
            yield return null;
            scaffold.Refresh();
            modules.Refresh();
            Assert.AreEqual(1, scaffold.ScaffoldCount);
            Assert.IsTrue(scaffold.TryGetSize(fp, out var size));
            Assert.AreEqual(3f, size.x, 0.001f);
            Assert.AreEqual(3f, size.z, 0.001f);
            Assert.AreEqual(0, modules.BoxCount, "The module visual appears only when Built.");

            // Built: the site record goes, the scaffold goes and the module appears.
            _host.World.Construction.Remove(fp);
            scaffold.Refresh();
            modules.Refresh();
            Assert.AreEqual(0, scaffold.ScaffoldCount);
            Assert.AreEqual(1, modules.BoxCount);
        }

        [UnityTest]
        public IEnumerator LogStorageNeverShowsAScaffold()
        {
            Setup();
            var scaffold = _rendGo.AddComponent<ConstructionRenderer>();
            scaffold.Configure(_host, _registry);
            var modules = _rendGo.AddComponent<ModuleRenderer>();
            modules.Configure(_host, _registry);
            int fp = Place(ModuleTypes.LogStorageId, 105, 100);
            yield return null;
            scaffold.Refresh();
            modules.Refresh();
            Assert.AreEqual(0, scaffold.ScaffoldCount);
            Assert.IsFalse(scaffold.TryGetSize(fp, out _));
            Assert.AreEqual(1, modules.BoxCount);
        }
    }
}
