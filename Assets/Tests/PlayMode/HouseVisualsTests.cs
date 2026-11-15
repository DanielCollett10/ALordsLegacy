using System.Collections;
using System.Linq;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for house visuals and camp tents (T-409, HM-20).</summary>
    public class HouseVisualsTests
    {
        private GameObject _hostGo, _rendGo;
        private VisualRegistry _registry;

        private static PointMm Pt(long x, long z) => new PointMm(x, z);

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

        private SimHost Setup()
        {
            _hostGo = new GameObject("HouseVisualsHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            host.AutoUpdate = false;
            var family = new StartFamily("T", Pt(-6000, 0), new[]
            {
                new StartCitizen("A", Sex.Male, -40L * Citizen.TicksPerYear),
            });
            StartLayoutApplier.Apply(host.World, new StartLayout(Pt(100000, 100000), 0, Pt(0, 0), Pt(4000, 0),
                Pt(-4000, 4000), new PointMm[0], new[] { family }, 1));
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.CitizenAdult, VisualSizes.CitizenChild, VisualSizes.Ox, VisualSizes.CampTent,
                VisualSizes.CampFire, VisualSizes.CampLogPile, VisualSizes.CampTiePost, VisualSizes.ModuleHouse,
                VisualSizes.ConstructionScaffold })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }
            return host;
        }

        [UnityTest]
        public IEnumerator HouseShowsScaffoldThenModelAndTentGoesWhenFamilyMovesIn()
        {
            var host = Setup();
            var world = host.World;
            _rendGo = new GameObject("HouseVisuals");
            var scaffold = _rendGo.AddComponent<ConstructionRenderer>();
            scaffold.Configure(host, _registry);
            var modules = _rendGo.AddComponent<ModuleRenderer>();
            modules.Configure(host, _registry);
            var camp = _rendGo.AddComponent<CampRenderer>();
            camp.Configure(host, _registry);

            int fpId = world.Footprints.NextId();
            world.Construction.Add(new ConstructionSite(fpId, world.Inventory.GetOrAddSite(fpId).Id));
            world.Footprints.Add(fpId, ModuleTypes.House, Pt(150000, 150000), 0);
            var home = world.Homes.Add(fpId, 1);
            yield return null;

            camp.Draw();
            scaffold.Refresh();
            modules.Refresh();
            Assert.AreEqual(1, scaffold.ScaffoldCount, "Unbuilt house: scaffold.");
            Assert.AreEqual(0, modules.BoxCount, "Unbuilt house: no model.");
            Assert.AreEqual(1, camp.TentCount, "Homeless family keeps its tent.");

            world.Construction.Remove(fpId);
            scaffold.Refresh();
            modules.Refresh();
            Assert.AreEqual(0, scaffold.ScaffoldCount);
            Assert.AreEqual(1, modules.BoxCount, "Built house: placeholder model.");
            Assert.AreEqual(ModuleTypes.HouseId, world.Footprints.Get(fpId).Type.Id);
            Assert.AreEqual(VisualSizes.ModuleHouse, ModuleRenderer.VisualIdOf(ModuleTypes.HouseId));

            Assert.IsTrue(world.Homes.MoveIn(home, world.People.Families.First()));
            camp.Draw();
            Assert.AreEqual(0, camp.TentCount, "Tent disappears once the family moved out.");
        }
    }
}
