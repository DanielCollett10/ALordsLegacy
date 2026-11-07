using LordsLegacy.Sim.Core;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Deletes one road segment (RD-12, ROAD-11-A). Nodes left with no segments are removed by the road network.
    /// Trees do not come back (BLD-10-D). Works while paused. Removed ids are the deleted segment id.
    /// </summary>
    public sealed class DeleteRoadSegment : ISimCommand
    {
        /// <summary>Id of the segment to delete.</summary>
        public int SegmentId { get; }

        /// <summary>Creates the command.</summary>
        public DeleteRoadSegment(int segmentId)
        {
            SegmentId = segmentId;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            return world.Roads.GetSegment(SegmentId) == null
                ? CommandResult.Rejected(CommandRejectCode.NotFound, "no road segment " + SegmentId)
                : CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            if (!world.Roads.RemoveSegment(SegmentId))
                return CommandResult.Rejected(CommandRejectCode.NotFound, "no road segment " + SegmentId);
            return CommandResult.Accepted(removed: new long[] { SegmentId });
        }
    }
}
