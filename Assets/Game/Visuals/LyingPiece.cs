using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>Builds matrices for a cylinder mesh (long axis = mesh Y) laid on its side, long axis along the yaw direction.</summary>
    public static class LyingPiece
    {
        /// <summary>
        /// Matrix for a cylinder of <paramref name="diameter"/> and <paramref name="length"/> metres whose centre is at
        /// <paramref name="centre"/>, lying along the horizontal direction turned <paramref name="yawDegrees"/> from +Z.
        /// </summary>
        public static Matrix4x4 Place(VisualDefinition def, Vector3 centre, float yawDegrees, float diameter, float length)
        {
            var mb = def != null && def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            var s = new Vector3(diameter * def.Scale / Mathf.Max(0.001f, mb.size.x),
                length / Mathf.Max(0.001f, mb.size.y),
                diameter * def.Scale / Mathf.Max(0.001f, mb.size.z));
            // Euler(90, yaw, 0): mesh +Y (long axis) turns onto the ground plane along the yaw direction.
            return Matrix4x4.TRS(centre, Quaternion.Euler(90f, yawDegrees, 0f), s);
        }

        /// <summary>Horizontal unit direction of the long axis for a yaw.</summary>
        public static Vector3 Direction(float yawDegrees) => Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
    }
}
