using System.Collections.Generic;
using LordsLegacy.EditorTools;
using LordsLegacy.Game.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace LordsLegacy.Tests.EditMode
{
    public class ForestScatterTests
    {
        const string TempAsset = "Assets/Maps/TestMap/TempTreesTest.asset";

        [TearDown]
        public void TearDown() { AssetDatabase.DeleteAsset(TempAsset); }

        static List<TreeEntry> Snapshot(TreeAuthoringData d) { return new List<TreeEntry>(d.trees); }

        [Test]
        public void SameSeedProducesIdenticalAsset()
        {
            var a = Snapshot(ForestScatter.Generate(ForestScatter.RegionPath, TempAsset, 7, ForestScatter.DefaultTreesPerHectare));
            var b = Snapshot(ForestScatter.Generate(ForestScatter.RegionPath, TempAsset, 7, ForestScatter.DefaultTreesPerHectare));
            Assert.That(a.Count, Is.GreaterThan(15000));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeedDiffers()
        {
            var r = ForestScatter.ReadRegion(ForestScatter.RegionPath);
            var a = ForestScatter.Scatter(r, 1, 50f);
            var b = ForestScatter.Scatter(r, 2, 50f);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void TreesAreInsideRegionWithUniqueIdsAndValidTrunks()
        {
            var r = ForestScatter.ReadRegion(ForestScatter.RegionPath);
            var trees = ForestScatter.Scatter(r, 3, ForestScatter.DefaultTreesPerHectare);
            var ids = new HashSet<int>();
            foreach (var t in trees)
            {
                Assert.IsTrue(ids.Add(t.id));
                Assert.That(t.x, Is.InRange(r.minX, r.maxX));
                Assert.That(t.z, Is.InRange(r.minZ, r.maxZ));
                Assert.That(t.trunkLengthM, Is.InRange(12f, 20f));
            }
        }
    }
}
