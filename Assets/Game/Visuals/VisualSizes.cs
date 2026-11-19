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
        /// <summary>Id of the placeholder house visual (HM-20).</summary>
        public const string ModuleHouse = "visual.module.house";
        /// <summary>Id of the construction scaffold visual (stretched to the footprint, height grows with progress).</summary>
        public const string ConstructionScaffold = "visual.construction.scaffold";
        /// <summary>Id of the tree stump visual.</summary>
        public const string TreeStump = "visual.tree.stump";
        /// <summary>Id of the felled trunk visual.</summary>
        public const string TreeFelled = "visual.tree.felled";
        /// <summary>Id of the single log visual.</summary>
        public const string ItemLog = "visual.item.log";
        /// <summary>Id of the market stall visual (a placeholder table, MK-43).</summary>
        public const string ModuleMarketStall = "visual.module.market_stall";
        /// <summary>Id of the food crate visual: food on a stall or carried by a citizen (MK-43).</summary>
        public const string ItemFoodCrate = "visual.item.food_crate";

        /// <summary>Id of the hewing station visual (T-526).</summary>
        public const string ModuleHewingStation = "visual.module.hewing_station";
        /// <summary>Id of the sawpit visual (T-526).</summary>
        public const string ModuleSawpit = "visual.module.sawpit";
        /// <summary>Id of the weaver visual (T-569).</summary>
        public const string ModuleWeaver = "visual.module.weaver";
        /// <summary>Id of the wattle yard visual (T-569).</summary>
        public const string ModuleWattleYard = "visual.module.wattle_yard";
        /// <summary>Id of the daub pit visual (T-593).</summary>
        public const string ModuleDaubPit = "visual.module.daub_pit";
        /// <summary>Id of one wattle fence segment, drawn along a built house fence's line (T-593).</summary>
        public const string FenceSegment = "visual.fence.segment";
        /// <summary>Id of a gate post: two stand either side of the always-open gate at the frontage (T-593).</summary>
        public const string FenceGatePost = "visual.fence.gate_post";
        /// <summary>Id of the storage yard visual (T-549).</summary>
        public const string ModuleStorageYard = "visual.module.storage_yard";
        /// <summary>Id of the beam stack visual: one stack on a hewing station's output store (T-526).</summary>
        public const string ItemBeamStack = "visual.item.beam_stack";
        /// <summary>Id of the plank stack visual: one stack on a sawpit's output store (T-526).</summary>
        public const string ItemPlankStack = "visual.item.plank_stack";

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
                case ModuleHouse: return new Vector3(6f, 4f, 8f);
                case ConstructionScaffold: return new Vector3(1f, 2.6f, 1f); // x, z are the footprint; y is the full height
                case TreeStump: return new Vector3(0.6f, 0.5f, 0.6f);
                case TreeFelled: return new Vector3(0.6f, 1f, 0.6f); // y is stretched to the trunk length
                case ItemLog: return new Vector3(0.35f, 1f, 0.35f);   // y is stretched to the log length
                case ModuleMarketStall: return new Vector3(3f, 0.9f, 2f);
                case ItemFoodCrate: return new Vector3(0.6f, 0.35f, 0.6f);
                case ModuleHewingStation: return new Vector3(6f, 2.2f, 4f);
                case ModuleSawpit: return new Vector3(6f, 1.4f, 4f);
                case ModuleWeaver: return new Vector3(6f, 2f, 4f);
                case ModuleWattleYard: return new Vector3(6f, 1.6f, 4f);
                case ModuleDaubPit: return new Vector3(4f, 0.6f, 4f);
                case FenceSegment: return new Vector3(2f, 1.2f, 0.12f);   // x is stretched to the run length
                case FenceGatePost: return new Vector3(0.2f, 1.5f, 0.2f);
                case ModuleStorageYard: return new Vector3(8f, 0.8f, 6f);
                case ItemBeamStack: return new Vector3(1.2f, 0.5f, 0.6f);   // y is the height of a full stack
                case ItemPlankStack: return new Vector3(1.2f, 0.4f, 0.6f);
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
