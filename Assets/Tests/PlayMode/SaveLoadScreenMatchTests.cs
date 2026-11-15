using System.Collections.Generic;
using LordsLegacy.Game;
using LordsLegacy.Game.Visuals;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEngine;

namespace LordsLegacy.Tests.PlayMode
{
    /// <summary>
    /// SAVE-02 / saving.md SV-22 (T-374): after a save and load the drawn screen matches the Sim, with an ox
    /// mid-haul and a scaffold mid-build. The load is the real path: <c>SimSave.Write</c> then
    /// <c>SimHost.InitializeFromSave</c> on a fresh host with fresh renderers.
    /// </summary>
    public class SaveLoadScreenMatchTests
    {
        private const int MaxTicks = 30000;
        private readonly List<GameObject> _gos = new List<GameObject>();
        private VisualRegistry _registry;
        private MapData _map;

        [TearDown]
        public void TearDown()
        {
            foreach (var g in _gos) if (g != null) Object.DestroyImmediate(g);
            _gos.Clear();
            if (_registry != null)
            {
                foreach (var d in _registry.Definitions) Object.DestroyImmediate(d);
                Object.DestroyImmediate(_registry);
            }
        }

        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static StartCitizen Adult(string n, Sex s) => new StartCitizen(n, s, -4_000_000);

        private sealed class Screen
        {
            public SimHost Host;
            public LogPileRenderer Logs;
            public PeopleRenderer People;
            public ConstructionRenderer Scaffolds;
        }

        private Screen MakeScreen(SimHost host)
        {
            var go = new GameObject("SaveLoadScreen");
            _gos.Add(go);
            var s = new Screen
            {
                Host = host,
                Logs = go.AddComponent<LogPileRenderer>(),
                People = go.AddComponent<PeopleRenderer>(),
                Scaffolds = go.AddComponent<ConstructionRenderer>(),
            };
            s.Logs.Configure(host, _registry);
            s.People.Configure(host, _registry);
            s.Scaffolds.Configure(host, _registry);
            return s;
        }

        private SimHost NewHost(string name)
        {
            var go = new GameObject(name);
            _gos.Add(go);
            var host = go.AddComponent<SimHost>();
            host.AutoUpdate = false;
            return host;
        }

        private static bool MidHaul(SimWorld w)
        {
            foreach (var l in w.Inventory.Locations)
                if (l.Kind == LocationKind.Carrier && l.CountOf(Items.Log.Id) > 0) return true;
            return false;
        }

        private static bool MidBuild(SimWorld w)
        {
            foreach (var site in w.Construction.Sites)
            {
                var p = ConstructionProgress.Of(w, site.FootprintId);
                if (p != null && p.Fraction > 0 && p.Fraction < 1) return true;
            }
            return false;
        }

        private static void RunUntil(SimWorld w, System.Func<SimWorld, bool> cond, string what)
        {
            for (int i = 0; i < MaxTicks && !cond(w); i++) w.Step(1);
            Assert.IsTrue(cond(w), "Never reached " + what + " within " + MaxTicks + " ticks.");
        }

        private static void AssertScreenMatchesSim(Screen s, string step)
        {
            var w = s.Host.World;
            s.Logs.Draw();
            int sum = 0;
            foreach (var l in w.Inventory.Locations)
            {
                int n = l.CountOf(Items.Log.Id);
                Assert.AreEqual(n, s.Logs.DrawnFor(l.Id), $"{step}: drawn logs at location {l.Id} ({l.Kind}).");
                sum += n;
            }
            Assert.AreEqual(sum, s.Logs.LogCount, $"{step}: total drawn logs.");

            s.People.Draw(0f);
            Assert.AreEqual(w.People.CitizenCount, s.People.CitizenInstanceCount, $"{step}: citizens drawn.");
            Assert.AreEqual(w.People.OxCount, s.People.OxInstanceCount, $"{step}: oxen drawn.");

            s.Scaffolds.Refresh();
            int sites = 0;
            foreach (var site in w.Construction.Sites)
            {
                var fp = w.Footprints.Get(site.FootprintId);
                var progress = ConstructionProgress.Of(w, site.FootprintId);
                if (fp == null || progress == null) continue;
                sites++;
                Assert.IsTrue(s.Scaffolds.TryGetSize(site.FootprintId, out var size), $"{step}: scaffold for {site.FootprintId}.");
                float full = VisualSizes.Of(VisualSizes.ConstructionScaffold).y;
                float expected = full * (ConstructionRenderer.MinHeightFraction
                    + (1f - ConstructionRenderer.MinHeightFraction) * (float)progress.Fraction);
                Assert.AreEqual(expected, size.y, 0.001f, $"{step}: scaffold height follows Sim progress.");
            }
            Assert.AreEqual(sites, s.Scaffolds.ScaffoldCount, $"{step}: scaffold count.");
        }

        private void SaveAndLoadThenCompare(Screen before, string step)
        {
            var world = before.Host.World;
            world.Pause();
            AssertScreenMatchesSim(before, step + " before save");
            byte[] bytes = SimSave.Write(world, "t", 1, _map.MapId, _map.ContentHash);

            var host2 = NewHost("LoadedHost");
            host2.InitializeFromSave(bytes, _map);
            var after = MakeScreen(host2);
            var w2 = host2.World;
            AssertScreenMatchesSim(after, step + " after load");

            Assert.AreEqual(world.Tick, w2.Tick, step + ": tick.");
            Assert.AreEqual(world.People.CitizenCount, w2.People.CitizenCount, step + ": citizen count.");
            foreach (var c in world.People.Citizens)
            {
                var c2 = w2.People.FindCitizen(c.Id);
                Assert.IsNotNull(c2, step + ": citizen " + c.Id);
                Assert.AreEqual(c.X, c2.X, 1e-6, step + ": citizen x.");
                Assert.AreEqual(c.Z, c2.Z, 1e-6, step + ": citizen z.");
            }
            foreach (var o in world.People.Oxen)
            {
                var o2 = w2.People.FindOx(o.Id);
                Assert.IsNotNull(o2, step + ": ox " + o.Id);
                Assert.AreEqual(o.X, o2.X, 1e-6, step + ": ox x.");
                Assert.AreEqual(o.Z, o2.Z, 1e-6, step + ": ox z.");
            }
            foreach (var l in world.Inventory.Locations)
                Assert.AreEqual(l.CountOf(Items.Log.Id), after.Logs.DrawnFor(l.Id), step + ": loaded drawn logs at " + l.Id);
            before.Scaffolds.Refresh();
            after.Scaffolds.Refresh();
            foreach (var site in world.Construction.Sites)
            {
                Assert.IsTrue(before.Scaffolds.TryGetSize(site.FootprintId, out var a));
                Assert.IsTrue(after.Scaffolds.TryGetSize(site.FootprintId, out var b), step + ": scaffold survives load.");
                Assert.AreEqual(a.y, b.y, 0.001f, step + ": scaffold progress.");
            }
        }

        [Test]
        public void ScreenMatchesSimAfterLoadMidHaulAndMidBuild()
        {
            _registry = ScriptableObject.CreateInstance<VisualRegistry>();
            foreach (var id in new[] { VisualSizes.ItemLog, VisualSizes.ConstructionScaffold, VisualSizes.CitizenAdult,
                VisualSizes.CitizenChild, VisualSizes.Ox, VisualSizes.ModuleHut })
            {
                var d = ScriptableObject.CreateInstance<VisualDefinition>();
                d.Configure(id, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), new Material[0], 1f, true);
                _registry.Register(d);
            }

            _map = new MapData("flat", new HeightGrid(-50f, -50f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var host = NewHost("SaveLoadHost");
            host.Initialize(new SimTuning { StartHour = 8 }, _map);
            var families = new List<StartFamily>
            {
                new StartFamily("Ashford", M(-9, -5), new[] { Adult("Edmund", Sex.Male), Adult("Mary", Sex.Female) }),
                new StartFamily("Marsh", M(-9, 5), new[] { Adult("John", Sex.Male), Adult("Alice", Sex.Female) }),
                new StartFamily("Thorne", M(9, 5), new[] { Adult("Hugh", Sex.Male), Adult("Joan", Sex.Female) }),
            };
            StartLayoutApplier.Apply(host.World, new StartLayout(M(0, 0), 0, M(0, 0), M(6, 0), M(-6, 0),
                new PointMm[0], families, 2, stock: new[] { new StartStock(Items.Log, 20) }));

            var area = host.Execute(new DesignateArea(
                AreaShape.Straight(new[] { M(38, -12), M(66, -12), M(66, 12), M(38, 12) }), AreaTypes.WoodcuttingId));
            Assert.IsTrue(area.IsAccepted, area.Text);
            int opId = host.World.Operations.FindByArea((int)area.Created[0]).Id;
            var place = host.Execute(new PlaceModule(opId, ModuleTypes.WoodcuttingHutId, M(45, 0), 0));
            Assert.IsTrue(place.IsAccepted, place.Text);

            var screen = MakeScreen(host);
            var world = host.World;

            RunUntil(world, MidHaul, "an ox hauling logs");
            SaveAndLoadThenCompare(screen, "mid-haul");

            world.Resume();
            RunUntil(world, MidBuild, "a scaffold part built");
            SaveAndLoadThenCompare(screen, "mid-build");
        }
    }
}
