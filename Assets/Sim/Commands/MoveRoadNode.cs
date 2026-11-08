using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Roads;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Moves a road node and re-shapes every segment attached to it (RD-17). Each segment keeps its other control
    /// points; at a node with exactly two segments both leave along one shared direction. Each new curve is checked
    /// like a new road, and any new junction is refused ("would join a road"). The first failure rejects the whole
    /// move. On success the attached segments get new ids (<see cref="CommandResult.Removed"/> = old,
    /// <see cref="CommandResult.Created"/> = new), the node keeps its id, and trees within half the width + clearance of
    /// the new centre lines are removed (<see cref="CommandResult.RemovedTreeIds"/>). Works while paused.
    /// </summary>
    public sealed class MoveRoadNode : ISimCommand
    {
        /// <summary>Id of the node to move.</summary>
        public int NodeId { get; }

        /// <summary>The new node position.</summary>
        public PointMm Position { get; }

        /// <summary>Creates the command.</summary>
        public MoveRoadNode(int nodeId, PointMm position)
        {
            NodeId = nodeId;
            Position = position;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world) => Check(world, out NodeMovePreview _);

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Check(world, out NodeMovePreview plan);
            if (check.IsRejected) return check;

            RoadNetwork net = world.Roads;
            int count = plan.Segments.Count;
            var old = new List<RoadSegment>();
            var cps = new List<PointMm[]>();
            var samples = new List<PointMm[]>();
            var starts = new List<RoadTangent?>();
            var ends = new List<RoadTangent?>();
            foreach (SegmentMovePreview m in plan.Segments)
            {
                old.Add(net.GetSegment(m.OldSegmentId));
                cps.Add(Copy(m.ControlPoints));
                samples.Add(Copy(m.Preview.Samples));
                starts.Add(m.Preview.StartTangent);
                ends.Add(m.Preview.EndTangent);
            }

            List<RoadSegment> created = net.MoveNode(NodeId, Position, old, cps, samples, starts, ends);

            var createdIds = new List<long>();
            var removedIds = new List<long>();
            var trees = new List<long>();
            for (int i = 0; i < count; i++)
            {
                removedIds.Add(old[i].Id);
                createdIds.Add(created[i].Id);
                double clearM = world.RoadTypes.Find(created[i].TypeId).WidthM / 2 + world.Tuning.RoadTreeClearanceM;
                foreach (TreeId t in TreeClearing.RemoveNearPolyline(world.Trees, created[i].Samples, clearM))
                    trees.Add(t.Value);
            }
            createdIds.Sort();
            removedIds.Sort();
            trees.Sort();
            return CommandResult.Accepted(createdIds.ToArray(), removedIds.ToArray(), trees.ToArray());
        }

        private CommandResult Check(SimWorld world, out NodeMovePreview plan)
        {
            plan = RoadValidator.PreviewMove(world, NodeId, Position);
            if (!plan.NodeFound) return CommandResult.Rejected(CommandRejectCode.NotFound, "no road node " + NodeId);
            return plan.IsValid
                ? CommandResult.Accepted()
                : CommandResult.Rejected(DrawRoad.CodeFor(plan.Reason), plan.ReasonText);
        }

        private static PointMm[] Copy(IReadOnlyList<PointMm> list)
        {
            var a = new PointMm[list.Count];
            for (int i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
        }
    }
}
