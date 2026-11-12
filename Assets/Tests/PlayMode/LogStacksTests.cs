using System.Collections;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>MVP-1-09 (T-293): drawn logs equal the ledger at every location, after every delivery step.</summary>
    public class LogStacksTests
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

        private static void AssertDrawnEqualsLedger(LogPileRenderer r, LordsLegacy.Sim.Core.SimWorld world, string step)
        {
            r.Draw();
            int sum = 0;
            foreach (var l in world.Inventory.Locations)
            {
                int n = l.CountOf(Items.Log.Id);
                Assert.AreEqual(n, r.DrawnFor(l.Id), $"{step}: location {l.Id} ({l.Kind}) drawn vs ledger.");
                sum += n;
            }
            Assert.AreEqual(sum, r.LogCount, $"{step}: total drawn.");
            Assert.AreEqual(world.Inventory.TotalOf(Items.Log), r.LogCount, $"{step}: drawn vs ledger total.");
        }

        [UnityTest]
        public IEnumerator DrawnLogsEqualLedgerAtEveryLocationAfterEachDelivery()
        {
            _hostGo = new GameObject("LogStacksHost");
            var host = _hostGo.AddComponent<SimHost>();
            host.Initialize(new SimTuning(), SyntheticMap.CreateRamp());
            host.AutoUpdate = false;
            var world = host.World;
            var pt = new System.Func<long, long, PointMm>((x, z) => new PointMm(x, z));
            var family = new StartFamily("T", pt(-6000, 0), new[]
            {
                new StartCitizen("A", Sex.Male, -40L * Citizen.TicksPerYear),
            });
            StartLayoutApplier.Apply(world, new StartLayout(pt(100000, 100000), 0, pt(0, 0), pt(4000, 0), pt(-4000, 4000),
                new PointMm[0], new[] { family }, 1));

            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            var d = ScriptableObject.CreateInstance<VisualDefinition>();
            d.Configure(VisualSizes.ItemLog, Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), new Material[0], 1f, true);
            _registry.Register(d);
            _rendGo = new GameObject("LogPileRenderer");
            var r = _rendGo.AddComponent<LogPileRenderer>();
            r.Configure(host, _registry);
            yield return null;

            var inv = world.Inventory;
            var stock = world.Camp.StockpileLocation;
            Assert.AreEqual(20, stock.CountOf(Items.Log.Id), "Camp starts with 20 logs.");
            AssertDrawnEqualsLedger(r, world, "start");
            Assert.AreEqual(20, r.DrawnFor(stock.Id), "20-log camp stockpile is drawn.");

            var fp = world.Footprints.Add(50, ModuleTypes.LogStorage, pt(130000, 100000), 30);
            var storage = inv.GetOrAddStorage(fp.Id, fp.Type);
            var ox = world.People.AddOx(7, "Bess", 120, 100);
            var carrier = world.OxCarrier(ox);

            inv.ProduceOnGround(pt(110000, 90000), 1, Items.Log, 6);
            AssertDrawnEqualsLedger(r, world, "felled 6");

            LedgerLocation ground = null;
            foreach (var l in inv.Locations) if (l.Kind == LocationKind.GroundPile) ground = l;
            Assert.IsNotNull(ground);

            Assert.IsTrue(inv.Transfer(ground, carrier, Items.Log, 4), "load ox");
            AssertDrawnEqualsLedger(r, world, "loaded");
            Assert.AreEqual(4, r.DrawnFor(carrier.Id), "four logs dragged behind the ox.");

            Assert.IsTrue(inv.Transfer(carrier, storage, Items.Log, 4), "unload into storage");
            AssertDrawnEqualsLedger(r, world, "delivered 1");
            Assert.AreEqual(4, r.DrawnFor(storage.Id));

            Assert.IsTrue(inv.Transfer(ground, carrier, Items.Log, 2), "load again");
            Assert.IsTrue(inv.Transfer(carrier, storage, Items.Log, 2), "deliver again");
            AssertDrawnEqualsLedger(r, world, "delivered 2");
            Assert.AreEqual(6, r.DrawnFor(storage.Id));
            Assert.AreEqual(26, r.LogCount);
        }
    }
}
