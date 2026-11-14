using System.Collections.Generic;
using LordsLegacy.Sim.Areas;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Land save section (decision 0034). Part 1: trees (as changes against the map's trees) and the road
    /// network. Part 2: areas (shape, flattened polygon, name, per-type name counters, id counter), after roads. The
    /// area spatial grid is rebuilt by re-adding the records.
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
            WriteAreas(world.Areas, w);
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            ReadTrees(world.Trees, r);
            ReadRoads(world.Roads, r);
            ReadAreas(world.Areas, r);
        }

        private static void WriteAreas(AreaRegistry areas, SaveWriter w)
        {
            w.WriteInt32(areas.NextIdValue);
            var counters = areas.NameCountersSorted();
            w.WriteInt32(counters.Count);
            foreach (var c in counters) { w.WriteString(c.Key); w.WriteInt32(c.Value); }
            w.WriteInt32(areas.Count);
            foreach (AreaRecord a in areas.All)
            {
                w.WriteInt32(a.Id);
                w.WriteString(a.TypeId);
                w.WriteString(a.Name);
                WritePoints(w, a.Shape.Vertices);
                for (int i = 0; i < a.Shape.Edges.Count; i++)
                {
                    AreaEdge e = a.Shape.Edges[i];
                    w.WriteInt32((int)e.Kind);
                    WritePoint(w, e.Handle);
                    w.WriteInt32(e.SegmentId);
                    w.WriteInt32((int)e.Side);
                }
                WritePoints(w, a.Polygon);
            }
        }

        private static void ReadAreas(AreaRegistry areas, SaveReader r)
        {
            int nextId = r.ReadInt32();
            try
            {
                int counters = r.ReadCount(8);
                for (int i = 0; i < counters; i++)
                {
                    string type = r.ReadString();
                    areas.RestoreNameCounter(type, r.ReadInt32());
                }
                int count = r.ReadCount(24);
                for (int i = 0; i < count; i++)
                {
                    int id = r.ReadInt32();
                    string type = r.ReadString();
                    string name = r.ReadString();
                    PointMm[] verts = ReadPoints(r);
                    var edges = new AreaEdge[verts.Length];
                    for (int k = 0; k < edges.Length; k++)
                    {
                        int kind = r.ReadInt32();
                        PointMm handle = ReadPoint(r);
                        int seg = r.ReadInt32();
                        int side = r.ReadInt32();
                        if (kind < 0 || kind > (int)AreaEdgeKind.NearArea) throw new SaveFormatException("Bad area edge kind " + kind + ".");
                        if (side < 0 || side > (int)RoadSide.Right) throw new SaveFormatException("Bad road side " + side + ".");
                        edges[k] = MakeEdge((AreaEdgeKind)kind, handle, seg, (RoadSide)side);
                    }
                    PointMm[] poly = ReadPoints(r);
                    if (poly.Length == 0) throw new SaveFormatException("Area " + id + " has no polygon.");
                    areas.Add(new AreaRecord(id, type, name, new AreaShape(verts, edges), poly));
                }
                areas.RestoreNextId(nextId);
            }
            catch (System.InvalidOperationException e) { throw new SaveFormatException(e.Message); }
            catch (System.ArgumentException e) { throw new SaveFormatException(e.Message); }
        }

        private static AreaEdge MakeEdge(AreaEdgeKind kind, PointMm handle, int seg, RoadSide side)
        {
            switch (kind)
            {
                case AreaEdgeKind.Bent: return AreaEdge.Bent(handle);
                case AreaEdgeKind.FollowsRoad: return AreaEdge.FollowsRoad(seg, side);
                case AreaEdgeKind.NearRoad: return AreaEdge.NearRoad(seg, side);
                case AreaEdgeKind.NearArea: return AreaEdge.NearArea(seg);
                default: return AreaEdge.Straight();
            }
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
