using System;
using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Draws a road (RD-07, decision 0013). Re-runs <see cref="RoadValidator"/>; if valid, splits the existing
    /// segments at the planned junctions and adds the new road cut at every junction. Created ids are segment
    /// ids (new segments and split halves); removed ids are the segments that were split.
    /// </summary>
    public sealed class DrawRoad : ISimCommand
    {
        private readonly PointMm[] _controlPoints;

        /// <summary>The clicked control points (a copy).</summary>
        public IReadOnlyList<PointMm> ControlPoints => _controlPoints;

        /// <summary>Road type id.</summary>
        public string TypeId { get; }

        /// <summary>How the control points make the centre line.</summary>
        public CurveKind Curve { get; }

        /// <summary>Creates the command. The control points are copied.</summary>
        public DrawRoad(IReadOnlyList<PointMm> controlPoints, string typeId, CurveKind curve = CurveKind.CatmullRomCentripetal)
        {
            if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
            _controlPoints = new PointMm[controlPoints.Count];
            for (int i = 0; i < _controlPoints.Length; i++) _controlPoints[i] = controlPoints[i];
            TypeId = typeId;
            Curve = curve;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            RoadPreview preview = RoadValidator.Validate(world, _controlPoints, TypeId);
            return preview.IsValid ? CommandResult.Accepted() : CommandResult.Rejected(CodeFor(preview.Reason), preview.ReasonText);
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            RoadPreview preview = RoadValidator.Validate(world, _controlPoints, TypeId);
            if (!preview.IsValid) return CommandResult.Rejected(CodeFor(preview.Reason), preview.ReasonText);

            RoadNetwork net = world.Roads;
            var created = new HashSet<int>();
            var removed = new HashSet<int>();
            var live = new Dictionary<int, List<int>>(); // original segment id -> its current pieces

            var interior = new List<PointMm>();
            PointMm? startNode = null, endNode = null;
            foreach (PlannedJunction j in preview.Junctions)
            {
                PointMm at;
                if (j.Kind == JunctionKind.JoinNode)
                {
                    at = net.GetNode(j.NodeId).Position;
                }
                else
                {
                    int target = PieceNear(net, live, j.SegmentId, j.Point);
                    SplitResult r = net.SplitSegmentAt(target, j.Point);
                    if (r.Split)
                    {
                        List<int> pieces = live[j.SegmentId];
                        pieces.Remove(target);
                        pieces.Add(r.FirstSegmentId);
                        pieces.Add(r.SecondSegmentId);
                        if (!created.Remove(target)) removed.Add(target);
                        created.Add(r.FirstSegmentId);
                        created.Add(r.SecondSegmentId);
                    }
                    at = net.GetNode(r.NodeId).Position;
                }

                if (j.Kind != JunctionKind.X && j.NewArcM <= 0) startNode = at;
                else if (j.Kind != JunctionKind.X && j.NewArcM >= preview.LengthM) endNode = at;
                else interior.Add(at);
            }

            List<PointMm> cps = ControlPointFilter.Clean(_controlPoints, world.Tuning.RoadMinPointSpacingM);
            foreach (RoadPiece piece in RoadCutter.Cut(preview.Samples, cps, startNode, endNode, interior))
                created.Add(net.AddSegment(TypeId, Curve, piece.ControlPoints, piece.Samples).Id);

            return CommandResult.Accepted(Sorted(created), Sorted(removed));
        }

        // The current piece of an original segment that the point lies on (the original itself if never split).
        private static int PieceNear(RoadNetwork net, Dictionary<int, List<int>> live, int originalId, PointMm point)
        {
            if (!live.TryGetValue(originalId, out List<int> pieces))
            {
                pieces = new List<int> { originalId };
                live[originalId] = pieces;
            }
            int best = pieces[0];
            double bestD = double.MaxValue;
            foreach (int id in pieces)
            {
                double d = Polylines.Nearest(net.GetSegment(id).Samples, point).DistanceMm;
                if (d < bestD) { bestD = d; best = id; }
            }
            return best;
        }

        private static long[] Sorted(HashSet<int> ids)
        {
            var list = new List<int>(ids);
            list.Sort();
            var a = new long[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
        }

        private static CommandRejectCode CodeFor(RoadRefusal reason)
        {
            switch (reason)
            {
                case RoadRefusal.UnknownType:
                case RoadRefusal.NotEnoughPoints: return CommandRejectCode.InvalidArgument;
                case RoadRefusal.OverlapsRoad: return CommandRejectCode.Conflict;
                default: return CommandRejectCode.NotAllowed;
            }
        }
    }
}
