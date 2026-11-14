using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Terrain;

namespace LordsLegacy.Sim.Map
{
    /// <summary>Builds small made-up maps for tests (including Unity-side tests).</summary>
    public static class SyntheticMap
    {
        /// <summary>
        /// A 200x200 m map like <see cref="CreateRamp"/> but with a 5 m high triangular ridge along z centred on x = 60
        /// (12 m either side), flat elsewhere.
        /// </summary>
        public static MapData CreateRidge(string mapId = "synthetic-ridge")
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                {
                    float d = System.Math.Abs(ix * 2f - 60f);
                    h[iz * nodes + ix] = d >= 12f ? 0f : 5f * (1f - d / 12f);
                }
            var grid = new HeightGrid(0f, 0f, 2f, nodes, nodes, h);
            return new MapData(mapId, grid, new MapTree[0]);
        }

        /// <summary>
        /// A 200x200 m map from origin (0,0), 2 m cells (101x101 nodes). Height rises 0.1 m per metre of x
        /// (so HeightAt(x, z) = 0.1 * x). Five trees with ids 1..5 and trunk length 8 + id metres.
        /// </summary>
        public static MapData CreateRamp(string mapId = "synthetic-ramp")
        {
            const int nodes = 101;
            var h = new float[nodes * nodes];
            for (int iz = 0; iz < nodes; iz++)
                for (int ix = 0; ix < nodes; ix++)
                    h[iz * nodes + ix] = 0.1f * (ix * 2f);
            var grid = new HeightGrid(0f, 0f, 2f, nodes, nodes, h);
            var trees = new[]
            {
                new MapTree(new TreeId(1), 20f, 20f, 9),
                new MapTree(new TreeId(2), 50f, 80f, 10),
                new MapTree(new TreeId(3), 100f, 100f, 11),
                new MapTree(new TreeId(4), 150f, 30f, 12),
                new MapTree(new TreeId(5), 180f, 170f, 13),
            };
            return new MapData(mapId, grid, trees);
        }
    }
}
