using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>PlayMode tests for <see cref="PeopleRenderer"/> and <see cref="CampRenderer"/> (T-262).</summary>
    public class PeopleRendererTests
    {
        private GameObject _hostGo, _rendGo;
        private VisualRegistry _registry;

        private SimHost MakeHost()
        {
            _hostGo = new GameObject("PeopleTestHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            host.AutoUpdate = false;
            return host;
        }

        private VisualRegistry MakeRegistry()
        {
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.CitizenAdult, VisualSizes.CitizenChild, VisualSizes.Ox, VisualSizes.CampTent,
                         VisualSizes.CampFire, VisualSizes.CampLogPile, VisualSizes.CampTiePost })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }
            return _registry;
        }

        [TearDown]
        public void TearDown()
        {
            if (_rendGo != null) Object.Destroy(_rendGo);
            if (_hostGo != null) Object.Destroy(_hostGo);
            if (_registry != null)
            {
                foreach (var d in _registry.Definitions) Object.Destroy(d);
                Object.Destroy(_registry);
            }
        }

        [UnityTest]
        public IEnumerator InstanceCountsEqualPopulation()
        {
            var host = MakeHost();
            var pop = host.World.People;
            pop.AddFamily(1, "Test", 50, 50);
            pop.AddCitizen(1, "Adult", Sex.Male, -40L * Citizen.TicksPerYear, 1, 50, 50);
            pop.AddCitizen(2, "Adult2", Sex.Female, -30L * Citizen.TicksPerYear, 1, 52, 50);
            pop.AddCitizen(3, "Kid", Sex.Female, -2L * Citizen.TicksPerYear, 1, 51, 51);
            pop.AddOx(1, "Ox", 60, 60);
            pop.AddOx(2, "Ox2", 62, 60);

            _rendGo = new GameObject("PeopleRenderer");
            var r = _rendGo.AddComponent<PeopleRenderer>();
            r.Configure(host, MakeRegistry());
            yield return null;
            r.Draw(0.016f);

            Assert.AreEqual(pop.CitizenCount, r.CitizenInstanceCount);
            Assert.AreEqual(pop.OxCount, r.OxInstanceCount);
            Assert.AreEqual(1, r.ChildInstanceCount, "Only the 2-year-old (4 years Sim age) is a child.");

            pop.RemoveCitizen(2);
            r.Draw(0.016f);
            Assert.AreEqual(pop.CitizenCount, r.CitizenInstanceCount, "Count follows the Sim after a removal.");
            Assert.IsNull(_rendGo.GetComponentInChildren<Collider>(), "No colliders.");
            Assert.AreEqual(0, _rendGo.transform.childCount, "No GameObject per agent.");
        }

        [UnityTest]
        public IEnumerator BobOnlyWhileWalking()
        {
            var host = MakeHost();
            var pop = host.World.People;
            pop.AddFamily(1, "Test", 50, 50);
            var walker = pop.AddCitizen(1, "W", Sex.Male, -40L * Citizen.TicksPerYear, 1, 50, 50);
            var still = pop.AddCitizen(2, "S", Sex.Male, -40L * Citizen.TicksPerYear, 1, 60, 60);
            walker.X = 51.74; // moved one tick's worth

            _rendGo = new GameObject("PeopleRenderer");
            var r = _rendGo.AddComponent<PeopleRenderer>();
            r.Configure(host, MakeRegistry(), new ViewSettings());
            yield return null;
            r.Draw(0.25f);
            Assert.Greater(r.CitizenPhase(1), 0, "Walker's gait advances.");
            Assert.AreEqual(0, r.CitizenPhase(2), "Standing citizen does not bob.");
        }

        [UnityTest]
        public IEnumerator CampRebuildsOnlyWhenFootprintVersionChanges()
        {
            var host = MakeHost();
            var world = host.World;
            var pt = new System.Func<long, long, LordsLegacy.Sim.Geometry.PointMm>((x, z) => new LordsLegacy.Sim.Geometry.PointMm(x, z));
            var family = new LordsLegacy.Sim.Start.StartFamily("T", pt(-6000, 0), new[]
            {
                new LordsLegacy.Sim.Start.StartCitizen("A", Sex.Male, -40L * Citizen.TicksPerYear),
            });
            LordsLegacy.Sim.Start.StartLayoutApplier.Apply(world, new LordsLegacy.Sim.Start.StartLayout(
                pt(100000, 100000), 0, pt(0, 0), pt(4000, 0), pt(-4000, 4000), new LordsLegacy.Sim.Geometry.PointMm[0],
                new[] { family }, 1));

            _rendGo = new GameObject("CampRenderer");
            var camp = _rendGo.AddComponent<CampRenderer>();
            camp.Configure(host, MakeRegistry());
            yield return null;
            camp.Draw();
            int builds = camp.BuildCount;
            Assert.AreEqual(1, camp.TentCount);
            Assert.AreEqual(4, camp.PieceCount);
            camp.Draw();
            Assert.AreEqual(builds, camp.BuildCount, "No rebuild while nothing changed.");

            world.Footprints.Add(99, LordsLegacy.Sim.Footprints.FootprintTypeDef.Camp, pt(150000, 150000), 0);
            camp.Draw();
            Assert.AreEqual(builds + 1, camp.BuildCount, "Rebuilt after FootprintRegistry.Version changed.");
        }
    }
}
