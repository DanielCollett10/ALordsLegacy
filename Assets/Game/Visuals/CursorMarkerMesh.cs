using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Builds the local-space meshes used by <see cref="CursorMarker"/>. The ring is a flat annulus lying in the
    /// local XZ plane at y = 0. The sphere is a UV sphere whose base touches y = 0 (its centre is at y = radius), so
    /// the marker's transform position is the ground contact point for both shapes.
    /// </summary>
    public static class CursorMarkerMesh
    {
        /// <summary>Radial width of the ring band in metres (the band never gets wider than half the radius).</summary>
        public const float RingWidthM = 0.2f;
        /// <summary>Number of segments around the ring.</summary>
        public const int RingSegments = 48;
        /// <summary>Number of latitude bands of the sphere.</summary>
        public const int SphereStacks = 10;
        /// <summary>Number of longitude slices of the sphere.</summary>
        public const int SphereSlices = 16;

        /// <summary>A flat ring of the given outer diameter in metres.</summary>
        /// <param name="diameterM">Outer diameter in metres; values below 0.01 m are raised to 0.01 m.</param>
        /// <returns>A new mesh with normals pointing up.</returns>
        public static Mesh Ring(float diameterM)
        {
            float outer = Mathf.Max(0.01f, diameterM) * 0.5f;
            float inner = Mathf.Max(outer - RingWidthM, outer * 0.5f);
            var verts = new Vector3[RingSegments * 2];
            var normals = new Vector3[verts.Length];
            var tris = new int[RingSegments * 6];
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * outer, 0f, s * outer);
                verts[i * 2 + 1] = new Vector3(c * inner, 0f, s * inner);
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
            }
            int t = 0;
            for (int i = 0; i < RingSegments; i++)
            {
                int o0 = i * 2, i0 = i * 2 + 1;
                int o1 = ((i + 1) % RingSegments) * 2, i1 = o1 + 1;
                tris[t++] = o0; tris[t++] = o1; tris[t++] = i1;
                tris[t++] = o0; tris[t++] = i1; tris[t++] = i0;
            }
            return Build("CursorMarkerRing", verts, normals, tris);
        }

        /// <summary>A UV sphere of the given diameter in metres, standing on y = 0.</summary>
        /// <param name="diameterM">Diameter in metres; values below 0.01 m are raised to 0.01 m.</param>
        /// <returns>A new mesh.</returns>
        public static Mesh Sphere(float diameterM)
        {
            float r = Mathf.Max(0.01f, diameterM) * 0.5f;
            int vCount = (SphereStacks + 1) * (SphereSlices + 1);
            var verts = new Vector3[vCount];
            var normals = new Vector3[vCount];
            int v = 0;
            for (int i = 0; i <= SphereStacks; i++)
            {
                float phi = Mathf.PI * i / SphereStacks; // 0 at the top pole, PI at the bottom
                float y = Mathf.Cos(phi), ring = Mathf.Sin(phi);
                for (int j = 0; j <= SphereSlices; j++)
                {
                    float theta = Mathf.PI * 2f * j / SphereSlices;
                    var n = new Vector3(ring * Mathf.Cos(theta), y, ring * Mathf.Sin(theta));
                    normals[v] = n;
                    verts[v] = n * r + new Vector3(0f, r, 0f);
                    v++;
                }
            }
            var tris = new int[SphereStacks * SphereSlices * 6];
            int t = 0;
            int row = SphereSlices + 1;
            for (int i = 0; i < SphereStacks; i++)
            {
                for (int j = 0; j < SphereSlices; j++)
                {
                    int a = i * row + j, b = a + row;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = b; tris[t++] = b + 1; tris[t++] = a + 1;
                }
            }
            return Build("CursorMarkerSphere", verts, normals, tris);
        }

        private static Mesh Build(string name, Vector3[] verts, Vector3[] normals, int[] tris)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
