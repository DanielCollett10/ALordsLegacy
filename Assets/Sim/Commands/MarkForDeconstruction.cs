using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Deconstruct a module (CN-50..52, CN-56). An unbuilt site is cancelled at once like <see cref="RemoveModule"/>
    /// (CN-32). A built module gets a deconstruction entry and stops working; marking a hut unassigns its families in
    /// the same Apply. Refused like <see cref="RemoveModule"/> for storage holding reserved logs (CN-34).
    /// </summary>
    public sealed class MarkForDeconstruction : ISimCommand
    {
        /// <summary>Footprint id of the module.</summary>
        public int ModuleId { get; }

        /// <summary>Creates the command.</summary>
        public MarkForDeconstruction(int moduleId) { ModuleId = moduleId; }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            // A built house has no operation: it is marked directly (HM-14). Unbuilt ones are checked by RemoveModule.
            bool builtHouse = world.Homes.Get(ModuleId) != null && world.IsBuilt(ModuleId);
            if (!builtHouse)
            {
                CommandResult r = new RemoveModule(ModuleId).Validate(world);
                if (r.IsRejected) return r;
            }
            if (world.Deconstruction.Get(ModuleId) != null)
                return CommandResult.Rejected(CommandRejectCode.NotAllowed, "already being deconstructed");
            return CommandResult.Accepted();
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            if (!world.IsBuilt(ModuleId)) return new RemoveModule(ModuleId).Apply(world);

            Footprint fp = world.Footprints.Get(ModuleId);
            Operation op = null;
            foreach (Operation o in world.Operations.All)
                foreach (int id in o.ModuleIds)
                    if (id == ModuleId) op = o;
            OperationTypeDef type = op != null ? OperationTypes.Find(op.TypeId) : null;
            if (op != null && type != null && fp != null && fp.Type.Id == type.HutTypeId)
            {
                var families = new List<int>(op.FamilyIds);
                foreach (int fid in families) new UnassignFamily(op.Id, fid).Apply(world);
            }
            HouseRemoval.Evict(world, ModuleId); // no-op for a module that is not a house
            world.Deconstruction.Add(new DeconstructionEntry(ModuleId));
            if (fp != null && DeconstructionWork.TotalMinutes(world, fp) <= 0)
            {
                DeconstructionWork.Finish(world, ModuleId); // 0-minute recipe: removed at once (CN-53)
                return CommandResult.Accepted(removed: new long[] { ModuleId });
            }
            return CommandResult.Accepted();
        }
    }
}
