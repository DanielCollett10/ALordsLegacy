using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Land save section (decision 0034). Part 1: trees (as changes against the map's trees) and the road
    /// network. Areas are added by the next task, after roads, in this same section.
    /// </summary>
    public sealed class LandSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 2;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            WriteTrees(world.Trees, w);
            WriteRoads(world.Roads, w);
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            ReadTrees(world.Trees, r);
            ReadRoads(world.Roads, r);
        }

        private static void WriteTrees(TreeRegistry trees, SaveWriter w)
        {
            List<int> removed = trees.RemovedIdsSorted();
            w.WriteInt32(removed.Count);
            foreach (int id in removed) w.WriteInt32(id);

            List<TreeRecord> changed = trees.ChangedTrees();
            w.WriteInt32(changed.Count);
            foreach (TreeRecord t in changed)
            {
                w.WriteInt32(t.Id.Value);
                w.WriteInt32((int)t.State);
                w.WriteBool(t.ReservedBy.HasValue);
                w.WriteInt32(t.ReservedBy ?? 0);
            }
        }

        private static void ReadTrees(TreeRegistry trees, SaveReader r)
        {
            int removed = r.ReadCount(4);
            for (int i = 0; i < removed; i++) trees.RestoreRemoved(r.ReadInt32());
            int changed = r.ReadCount(10);
            for (int i = 0; i < changed; i++)
            {
                int id = r.ReadInt32();
                int state = r.ReadInt32();
                bool reserved = r.ReadBool();
                int owner = r.ReadInt32();
                if (state < 0 || state > (int)TreeState.Stump) throw new SaveFormatException("Bad tree state " + state + ".");
                try { trees.RestoreTree(id, (TreeState)state, reserved ? owner : (int?)null); }
                catch (System.InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            }
        }

        private static void WriteRoads(RoadNetwork roads, SaveWriter w)
        {
            w.WriteInt32(roads.NextNodeId);
            w.WriteInt32(roads.NextSegmentId);
            w.WriteInt32(roads.NodeCount);
            foreach (RoadNode n in roads.Nodes)
            {
                w.WriteInt32(n.Id);
                WritePoint(w, n.Position);
                w.WriteInt32(n.SegmentIds.Count);
                foreach (int s in n.SegmentIds) w.WriteInt32(s);
            }
            w.WriteInt32(roads.SegmentCount);
            foreach (RoadSegment s in roads.Segments)
            {
                w.WriteInt32(s.Id);
                w.WriteInt32(s.NodeA);
                w.WriteInt32(s.NodeB);
                w.WriteString(s.TypeId);
                w.WriteInt32((int)s.Curve);
                WritePoints(w, s.ControlPoints);
                WritePoints(w, s.Samples);
                WriteTangent(w, s.StartTangent);
                WriteTangent(w, s.EndTangent);
            }
        }

        private static void ReadRoads(RoadNetwork roads, SaveReader r)
        {
            int nextNode = r.ReadInt32();
            int nextSeg = r.ReadInt32();
            try
            {
                roads.RestoreBegin(nextNode, nextSeg);
                int nodes = r.ReadCount(28);
                for (int i = 0; i < nodes; i++)
                {
                    int id = r.ReadInt32();
                    PointMm p = ReadPoint(r);
                    int n = r.ReadCount(4);
                    var segs = new int[n];
                    for (int k = 0; k < n; k++) segs[k] = r.ReadInt32();
                    roads.RestoreNode(id, p, segs);
                }
                int segments = r.ReadCount(30);
                for (int i = 0; i < segments; i++)
                {
                    int id = r.ReadInt32();
                    int a = r.ReadInt32();
                    int b = r.ReadInt32();
                    string type = r.ReadString();
                    int curve = r.ReadInt32();
                    PointMm[] cps = ReadPoints(r);
                    PointMm[] samples = ReadPoints(r);
                    RoadTangent? st = ReadTangent(r);
                    RoadTangent? et = ReadTangent(r);
                    roads.RestoreSegment(id, a, b, type, (CurveKind)curve, cps, samples, st, et);
                }
            }
            catch (System.InvalidOperationException e) { throw new SaveFormatException(e.Message); }
        }

        private static void WritePoint(SaveWriter w, PointMm p) { w.WriteInt64(p.X); w.WriteInt64(p.Z); }

        private static PointMm ReadPoint(SaveReader r)
        {
            long x = r.ReadInt64();
            long z = r.ReadInt64();
            return new PointMm(x, z);
        }

        private static void WritePoints(SaveWriter w, IReadOnlyList<PointMm> pts)
        {
            w.WriteInt32(pts.Count);
            for (int i = 0; i < pts.Count; i++) WritePoint(w, pts[i]);
        }

        private static PointMm[] ReadPoints(SaveReader r)
        {
            int n = r.ReadCount(16);
            var a = new PointMm[n];
            for (int i = 0; i < n; i++) a[i] = ReadPoint(r);
            return a;
        }

        private static void WriteTangent(SaveWriter w, RoadTangent? t)
        {
            w.WriteBool(t.HasValue);
            w.WriteDouble(t.HasValue ? t.Value.X : 0);
            w.WriteDouble(t.HasValue ? t.Value.Z : 0);
        }

        private static RoadTangent? ReadTangent(SaveReader r)
        {
            bool has = r.ReadBool();
            double x = r.ReadDouble();
            double z = r.ReadDouble();
            if (!has) return null;
            return RoadTangent.FromSaved(x, z); // keeps the saved bits; the public constructor would renormalise
        }
    }
}
