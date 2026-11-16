using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Switches a granary's "Supplies market" flag (MK-10..12, markets.md). Refused with NotFound for an unknown
    /// operation, and NotAllowed when the operation is not a granary (forager huts never supply, MK-12), the granary
    /// is not built ("Hut not built yet", MK-11) or the town has no market ("No market yet"). Many granaries may supply
    /// (MKT-02). Choosing the current value is accepted and changes nothing. A refusal changes nothing.
    /// </summary>
    public sealed class SetGranarySupplies : ISimCommand
    {
        /// <summary>Id of the granary operation (<c>operation.granary</c>).</summary>
        public int GranaryOperationId { get; }
        /// <summary>True to supply the market, false to stop.</summary>
        public bool On { get; }

        /// <summary>Creates the command.</summary>
        public SetGranarySupplies(int granaryOperationId, bool on)
        {
            GranaryOperationId = granaryOperationId;
            On = on;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            Operation op = world.Operations.Get(GranaryOperationId);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no operation " + GranaryOperationId);
            if (op.TypeId != OperationTypes.GranaryId)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Only a granary can supply the market");
            foreach (int id in op.ModuleIds)
            {
                Footprint hut = world.Footprints.Get(id);
                if (hut != null && hut.Type.Id == ModuleTypes.GranaryId && !world.IsBuilt(id))
                    return CommandResult.Rejected(CommandRejectCode.NotAllowed, "Hut not built yet");
            }
            if (world.Markets.Count == 0)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "No market yet");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            world.Operations.SetSuppliesMarket(GranaryOperationId, On);
            return CommandResult.Accepted();
        }
    }
}
