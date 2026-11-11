using System.IO;
using System.Linq;
using LordsLegacy.EditorTools;
using LordsLegacy.Game;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Start;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;
using UnityEditor;

namespace LordsLegacy.Tests.EditMode
{
    public class StartLayoutConfigTests
    {
        private static StartLayoutConfig Asset()
        {
            var a = AssetDatabase.LoadAssetAtPath<StartLayoutConfig>(StartLayoutGenerator.AssetPath);
            Assert.IsNotNull(a, "Run menu LordsLegacy/Map/Generate Start Layout");
            return a;
        }

        private static MapData Map() => MapDataIO.Read(File.ReadAllBytes(MapBaker.OutputPath));

        [Test]
        public void AssetConvertsAndAppliesToBakedMap()
        {
            StartLayout layout = Asset().ToLayout();
            var world = SimWorld.FromMapData(Map(), new SimTuning());
            Assert.DoesNotThrow(() => StartLayoutApplier.Apply(world, layout));
            Assert.AreEqual(3, world.People.FamilyCount);
            Assert.AreEqual(10, world.People.CitizenCount);
            Assert.AreEqual(2, world.People.OxCount);
            Assert.Greater(world.Roads.Segments.Count(), 0);
            Assert.IsNotNull(world.Camp);
        }

        [Test]
        public void EveryFamilyHasTwoMembersAged14Plus()
        {
            var tuning = new SimTuning();
            var world = SimWorld.FromMapData(Map(), tuning);
            StartLayoutApplier.Apply(world, Asset().ToLayout());
            foreach (Family f in world.People.Families)
            {
                int workers = f.MemberIds.Count(id => world.People.FindCitizen(id).AgeYears(0, tuning.AgeingRate) >= 14.0);
                Assert.GreaterOrEqual(workers, 2, "family " + f.Surname);
            }
        }

        [Test]
        public void CampIsFlatAndNearTrees()
        {
            var world = SimWorld.FromMapData(Map(), new SimTuning());
            var c = Asset().campCentreM;
            Assert.LessOrEqual(world.Terrain.SlopeDegAt(c.x, c.y), StartLayoutGenerator.MaxCampSlopeDeg);
            Assert.GreaterOrEqual(world.Trees.QueryRadius(c.x, c.y, StartLayoutGenerator.TreeRadiusM).Count, StartLayoutGenerator.MinTrees);
        }

        [Test]
        public void GeneratorIsIdempotent()
        {
            string before = File.ReadAllText(StartLayoutGenerator.AssetPath);
            StartLayoutGenerator.Generate(MapBaker.OutputPath, StartLayoutGenerator.AssetPath);
            Assert.AreEqual(before, File.ReadAllText(StartLayoutGenerator.AssetPath));
        }
    }
}
