using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Foraging;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    /// <summary>Hazel and willow stools as bush kinds (T-554, decision 0050).</summary>
    public class StoolBushTests
    {
        private const int Day = 1440;
        private static readonly BushKind[] Forage = { BushKind.Berries, BushKind.Mushrooms };

        private static (SimWorld world, MapData map) Make(SimTuning t = null)
        {
            var map = SyntheticMap.CreateRamp();
            return (SimWorld.FromMapData(map, t ?? new SimTuning()), map);
        }

        /// <summary>Ridge terrain (flat low ground either side) with a grid of trees.</summary>
        private static (SimWorld world, MapData map) MakeRidge(SimTuning t = null)
        {
            MapData ridge = SyntheticMap.CreateRidge();
            var trees = new System.Collections.Generic.List<MapTree>();
            int id = 1;
            for (int x = 20; x < 200; x += 30)
                for (int z = 20; z < 200; z += 30)
                    trees.Add(new MapTree(new LordsLegacy.Sim.Forestry.TreeId(id++), x, z, 10));
            var map = new MapData("stool-ridge", ridge.Heights, trees.ToArray());
            return (SimWorld.FromMapData(map, t ?? new SimTuning()), map);
        }

        [Test]
        public void Scatter_Ramp_SteepGround_NoWillow_HazelPlaced()
        {
            var (w, _) = Make();
            Assert.AreEqual(0, w.Bushes.All.Count(b => b.Kind == BushKind.WillowStool), "ramp is steeper than 3 degrees");
            Assert.Greater(w.Bushes.All.Count(b => b.Kind == BushKind.HazelStool), 0);
        }

        [Test]
        public void Scatter_PlacesBothStoolKinds_FullAtRodsPerCut()
        {
            var (w, _) = MakeRidge();
            var hazel = w.Bushes.All.Where(b => b.Kind == BushKind.HazelStool).ToList();
            var willow = w.Bushes.All.Where(b => b.Kind == BushKind.WillowStool).ToList();
            Assert.Greater(hazel.Count, 0);
            Assert.Greater(willow.Count, 0);
            Assert.LessOrEqual(hazel.Count, w.Tuning.HazelStoolCount);
            Assert.LessOrEqual(willow.Count, w.Tuning.WillowStoolCount);
            Assert.IsTrue(hazel.Concat(willow).All(b => b.Baskets == w.Tuning.CoppiceRodsPerCut));
        }

        [Test]
        public void Scatter_HazelIsAtForestEdge_ClearOfTrunks()
        {
            var (w, _) = Make();
            foreach (var b in w.Bushes.All.Where(x => x.Kind == BushKind.HazelStool))
            {
                Assert.Greater(w.Trees.QueryRadius(b.X, b.Z, (float)w.Tuning.BushEdgeM).Count, 0, "edge " + b.Id);
                Assert.AreEqual(0, w.Trees.QueryRadius(b.X, b.Z, (float)w.Tuning.BushTreeClearM).Count, "trunk " + b.Id);
            }
        }

        [Test]
        public void Scatter_WillowIsOnLowGround()
        {
            var (w, _) = MakeRidge();
            Assert.Greater(w.Bushes.All.Count(b => b.Kind == BushKind.WillowStool), 0);
            var low = new LowGround(w.Terrain);
            foreach (var b in w.Bushes.All.Where(x => x.Kind == BushKind.WillowStool))
            {
                Assert.IsTrue(low.Contains(b.X, b.Z), "willow " + b.Id);
                Assert.LessOrEqual(w.Terrain.SlopeDegAt(b.X, b.Z), LowGround.MaxSlopeDeg);
            }
        }

        [Test]
        public void Scatter_SameSeed_SameStools_AndForageUnchangedByStoolCounts()
        {
            var (a, _) = Make();
            var (b, _) = Make();
            var (c, _) = Make(new SimTuning { HazelStoolCount = 0, WillowStoolCount = 0 });
            Assert.AreEqual(a.Bushes.All.Select(x => (x.X, x.Z, x.Kind)), b.Bushes.All.Select(x => (x.X, x.Z, x.Kind)));
            Assert.AreEqual(
                a.Bushes.All.Where(x => !BushKinds.IsStool(x.Kind)).Select(x => (x.X, x.Z, x.Kind)),
                c.Bushes.All.Select(x => (x.X, x.Z, x.Kind)), "forage identical without stools");
            Assert.AreEqual(new SimWorld(new SimTuning()).Random.State, a.Random.State);
        }

        [Test]
        public void LowGround_Grid_QuarterLowestAndFlatOnly()
        {
            // 11x11 grid, height = ix (0..10), so the 25th percentile is about 2.5 m and slope is 45 degrees.
            var h = new float[11 * 11];
            for (int iz = 0; iz < 11; iz++) for (int ix = 0; ix < 11; ix++) h[iz * 11 + ix] = ix;
            var steep = new HeightGrid(0, 0, 1, 11, 11, h);
            Assert.IsFalse(new LowGround(steep).Contains(1, 5), "low but steep");
            Assert.IsTrue(new LowGround(steep, 0.25, 60).Contains(1, 5), "low and under slope limit");
            Assert.IsFalse(new LowGround(steep, 0.25, 60).Contains(8, 5), "high");
        }

        [Test]
        public void LowGround_FlatTerrain_AllLow_EmptyBoundsNone()
        {
            var flat = new HeightGrid(0, 0, 1, 11, 11, new float[121]);
            Assert.IsTrue(new LowGround(flat).Contains(5, 5), "flat map is all low");
            Assert.IsFalse(new LowGround(new FlatHeightField()).Contains(5, 5), "empty bounds: no low ground");
        }

        [Test]
        public void NearestAvailable_KindFilter_IgnoresNearerOtherKinds()
        {
            var r = new BushRegistry();
            var hazel = r.Add(1, 0, BushKind.HazelStool, 10);
            var berry = r.Add(20, 0, BushKind.Berries, 3);
            Assert.AreEqual(berry.Id, r.NearestAvailable(0, 0, Forage).Id);
            Assert.AreEqual(hazel.Id, r.NearestAvailable(0, 0, BushKind.HazelStool).Id);
            Assert.IsNull(r.NearestAvailable(0, 0, BushKind.WillowStool));
            Assert.AreEqual(hazel.Id, r.NearestAvailable(0, 0, BushKind.HazelStool, BushKind.Berries).Id);
        }

        [Test]
        public void Regrow_StoolUsesCoppiceDays_ForageUsesBushDays()
        {
            var t = new SimTuning { CoppiceRegrowDays = 30, CoppiceRodsPerCut = 4, BushRegrowDays = 10, BushBaskets = 3 };
            var r = new BushRegistry();
            var s = r.Add(0, 0, BushKind.WillowStool, 4);
            var f = r.Add(1, 0, BushKind.Berries, 3);
            r.Reserve(s.Id, 1); for (int i = 0; i < 4; i++) r.TakeBasket(s.Id, 1, 0);
            r.Reserve(f.Id, 1); for (int i = 0; i < 3; i++) r.TakeBasket(f.Id, 1, 0);
            Assert.AreEqual(1, r.Regrow(10 * Day, t));
            Assert.AreEqual(0, s.Baskets);
            Assert.AreEqual(3, f.Baskets);
            Assert.AreEqual(1, r.Regrow(30 * Day, t));
            Assert.AreEqual(4, s.Baskets);
        }

        [Test]
        public void Regrow_ThroughSimTicks_HazelStoolRefillsAfterCoppiceDays()
        {
            var (w, _) = Make(new SimTuning { CoppiceRegrowDays = 5 });
            BushRecord s = w.Bushes.All.First(b => b.Kind == BushKind.HazelStool);
            w.Bushes.Reserve(s.Id, 1);
            for (int i = 0; i < w.Tuning.CoppiceRodsPerCut; i++) w.Bushes.TakeBasket(s.Id, 1, w.Tick);
            Assert.AreEqual(0, s.Baskets);
            w.Step(4 * Day);
            Assert.AreEqual(0, s.Baskets);
            w.Step(2 * Day);
            Assert.AreEqual(w.Tuning.CoppiceRodsPerCut, s.Baskets);
        }

        [Test]
        public void RoundTrip_StoolsSurvive_AndNoVersionChange()
        {
            var (w, map) = MakeRidge();
            BushRecord s = w.Bushes.All.First(b => b.Kind == BushKind.WillowStool);
            w.Bushes.Reserve(s.Id, 3);
            w.Bushes.TakeBasket(s.Id, 3, 0);
            byte[] bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash));
            Assert.AreEqual(
                w.Bushes.All.Select(b => (b.Id, b.Kind, b.Baskets, b.ReservedBy)).ToList(),
                back.Bushes.All.Select(b => (b.Id, b.Kind, b.Baskets, b.ReservedBy)).ToList());
            Assert.IsTrue(back.Bushes.All.Any(b => b.Kind == BushKind.HazelStool));
        }
    }
}
