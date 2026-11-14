using System.Collections.Generic;
using LordsLegacy.Game;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>
    /// M10 end to end through SimHost: from the start camp, a placed woodcutting hut becomes Built at 32x (CN-22, CN-23).
    /// Uses the default start layout of the MVP-1 scenario (20 logs, two oxen, three families).
    /// </summary>
    public class HutFromStartTests
    {
        /// <summary>Real seconds per Tick call. At 32x the pacer runs about 25 ticks per call.</summary>
        private const float RealSecondsPerTick = 1f;

        /// <summary>Hard stop: 1000 calls is roughly 25000 game minutes at 32x, well past the 4-day felling budget.</summary>
        private const int MaxCalls = 1000;

        private GameObject _go;
        private SimConfig _config;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static StartCitizen Adult(string name, Sex sex) => new StartCitizen(name, sex, -4_000_000);

        // Default start layout from the MVP-1 scenario: camp at the origin, stockpile (6,0), ox stand (-6,0), 20 logs.
        private static StartLayout DefaultLayout()
        {
            var families = new List<StartFamily>
            {
                new StartFamily("Ashford", M(-9, -5), new[] { Adult("Edmund", Sex.Male), Adult("Mary", Sex.Female) }),
                new StartFamily("Marsh", M(-9, 5), new[] { Adult("John", Sex.Male), Adult("Alice", Sex.Female) }),
                new StartFamily("Thorne", M(9, 5), new[] { Adult("Hugh", Sex.Male), Adult("Joan", Sex.Female) }),
            };
            return new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0), new PointMm[0], families, 2,
                stock: new[] { new StartStock(Items.Log, 20) });
        }

        [Test]
        public void HutPlacedFromStartBecomesBuiltAt32x()
        {
            _go = new GameObject("HutFromStartHost");
            var host = _go.AddComponent<SimHost>();
            host.AutoUpdate = false;
            _config = ScriptableObject.CreateInstance<SimConfig>();
            host.Config = _config;

            var map = new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            host.Initialize(_config.BuildTuning(), map);
            StartLayoutApplier.Apply(host.World, DefaultLayout());
            Assert.AreEqual(20, host.World.Inventory.TotalOf(Items.Log.Id), "start stock is 20 logs");

            CommandResult area = host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(38, -12), M(66, -12), M(66, 12), M(38, 12) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(area.IsAccepted, area.Text);
            int opId = host.World.Operations.FindByArea((int)area.Created[0]).Id;

            CommandResult place = host.Execute(new PlaceModule(opId, ModuleTypes.WoodcuttingHutId, M(45, 0), 0));
            Assert.IsTrue(place.IsAccepted, place.Text);
            int fp = (int)place.Created[0];
            Assert.IsFalse(host.World.IsBuilt(fp), "The hut starts as a site, not Built.");

            var speeds = host.Pacer.Speeds;
            int index = -1;
            for (int i = 0; i < speeds.Count; i++) if (speeds[i] == 32) index = i;
            Assert.GreaterOrEqual(index, 0, "32x is a selectable speed");
            host.Pacer.SetSpeed(index);
            Assert.AreEqual(32, host.Pacer.CurrentSpeed);

            int calls = 0;
            while (!host.World.IsBuilt(fp) && calls < MaxCalls)
            {
                host.Tick(RealSecondsPerTick);
                calls++;
            }

            Assert.IsTrue(host.World.IsBuilt(fp), "The hut is Built within " + MaxCalls + " calls at 32x (tick " + host.World.Tick + ").");
            Assert.AreEqual(ModuleTypes.WoodcuttingHutId, host.World.Footprints.Get(fp).Type.Id);
        }
    }
}
