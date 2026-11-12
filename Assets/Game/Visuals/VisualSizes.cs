using UnityEngine;

namespace LordsLegacy.Game.Visuals
{
    /// <summary>
    /// Placeholder footprint (x, z) and height in metres for the people and camp visuals, before the definition's
    /// own scale. Renderers stretch each definition's mesh to this box. Final art will replace the mesh and the
    /// numbers can go once models carry their real size.
    /// </summary>
    public static class VisualSizes
    {
        /// <summary>Id of the adult citizen visual.</summary>
        public const string CitizenAdult = "visual.citizen.adult";
        /// <summary>Id of the child citizen visual.</summary>
        public const string CitizenChild = "visual.citizen.child";
        /// <summary>Id of the ox visual.</summary>
        public const string Ox = "visual.ox";
        /// <summary>Id of the tent visual.</summary>
        public const string CampTent = "visual.camp.tent";
        /// <summary>Id of the fire visual.</summary>
        public const string CampFire = "visual.camp.fire";
        /// <summary>Id of the log pile visual.</summary>
        public const string CampLogPile = "visual.camp.logpile";
        /// <summary>Id of the tie post visual.</summary>
        public const string CampTiePost = "visual.camp.tiepost";

        /// <summary>Id of the woodcutting hut module visual.</summary>
        public const string ModuleHut = "visual.module.woodcutting_hut";
        /// <summary>Id of the log storage module visual.</summary>
        public const string ModuleLogStorage = "visual.module.log_storage";
        /// <summary>Id of the oxen stand module visual.</summary>
        public const string ModuleOxenStand = "visual.module.oxen_stand";
        /// <summary>Id of the tree stump visual.</summary>
        public const string TreeStump = "visual.tree.stump";
        /// <summary>Id of the felled trunk visual.</summary>
        public const string TreeFelled = "visual.tree.felled";
        /// <summary>Id of the single log visual.</summary>
        public const string ItemLog = "visual.item.log";

        /// <summary>Box size (x width, y height, z depth) in metres for a visual id; one metre cube if unknown.</summary>
        public static Vector3 Of(string id)
        {
            switch (id)
            {
                case CitizenAdult: return new Vector3(0.5f, 1.7f, 0.5f);
                case CitizenChild: return new Vector3(0.5f, 1.7f, 0.5f); // the definition's 0.7 scale makes it smaller
                case Ox: return new Vector3(0.8f, 1.3f, 1.6f);
                case CampTent: return new Vector3(3f, 2f, 3f);
                case CampFire: return new Vector3(0.8f, 0.4f, 0.8f);
                case CampLogPile: return new Vector3(1.5f, 0.7f, 2.5f);
                case CampTiePost: return new Vector3(0.2f, 1.5f, 0.2f);
                case ModuleHut: return new Vector3(6f, 2.6f, 4f);
                case ModuleLogStorage: return new Vector3(8f, 1.2f, 6f);
                case ModuleOxenStand: return new Vector3(3f, 1f, 3f);
                case TreeStump: return new Vector3(0.6f, 0.5f, 0.6f);
                case TreeFelled: return new Vector3(0.6f, 1f, 0.6f); // y is stretched to the trunk length
                case ItemLog: return new Vector3(0.35f, 1f, 0.35f);   // y is stretched to the log length
                default: return Vector3.one;
            }
        }

        /// <summary>
        /// The matrix that stretches the definition's mesh to <see cref="Of"/> × scale, stands it on
        /// <paramref name="position"/> (its lowest point at that height) and turns it by <paramref name="yawDegrees"/>.
        /// </summary>
        public static Matrix4x4 Place(VisualDefinition def, string id, Vector3 position, float yawDegrees)
        {
            var box = Of(id) * def.Scale;
            var mb = def.Mesh != null ? def.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            var s = new Vector3(box.x / Mathf.Max(0.001f, mb.size.x), box.y / Mathf.Max(0.001f, mb.size.y),
                box.z / Mathf.Max(0.001f, mb.size.z));
            position.y -= mb.min.y * s.y;
            return Matrix4x4.TRS(position, Quaternion.Euler(0f, yawDegrees, 0f), s);
        }
    }
}
