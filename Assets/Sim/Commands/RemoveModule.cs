using System.Collections.Generic;
using LordsLegacy.Sim.Construction;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Operations;

namespace LordsLegacy.Sim.Commands
{
    /// <summary>
    /// Removes a placed module (footprint id) from its operation and the footprint registry. Unknown id →
    /// <see cref="CommandRejectCode.NotFound"/>. Trees it cleared do not come back. Removed ids hold the module id.
    /// </summary>
    public sealed class RemoveModule : ISimCommand
    {
        /// <summary>Footprint id of the module.</summary>
        public int ModuleId { get; }

        /// <summary>Creates the command.</summary>
        public RemoveModule(int moduleId)
        {
            ModuleId = moduleId;
        }

        private Operation Owner(SimWorld world)
        {
            foreach (Operation op in world.Operations.All)
                foreach (int id in op.ModuleIds)
                    if (id == ModuleId) return op;
            return null;
        }

        /// <inheritdoc />
        public CommandResult Validate(SimWorld world)
        {
            if (Owner(world) == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no module " + ModuleId);
            LedgerLocation storage = world.Inventory.FindStorage(ModuleId);
            if (storage != null && world.IsBuilt(ModuleId))
                foreach (ConstructionSite s in world.Construction.Sites)
                    foreach (int rid in s.ReservationIds)
                    {
                        Reservation r = world.Inventory.GetReservation(rid);
                        if (r != null && r.LocationId == storage.Id && r.Kind == ReservationKind.Stock)
                            return CommandResult.Rejected(CommandRejectCode.Conflict, "Logs here are reserved for " + SiteName(world, s));
                    }
            return CommandResult.Accepted();
        }

        private static string SiteName(SimWorld world, ConstructionSite s)
        {
            Footprint f = world.Footprints.Get(s.FootprintId);
            return f != null ? f.Type.Id + " " + s.FootprintId : "site " + s.FootprintId;
        }

        private static void ReleaseSite(SimWorld world, ConstructionSite site, Footprint fp)
        {
            Ledger inv = world.Inventory;
            foreach (int rid in site.ReservationIds) inv.Release(rid);
            inv.CancelOwner(site.FootprintId);
            var from = new List<LedgerLocation>();
            LedgerLocation siteLoc = inv.FindSite(site.FootprintId);
            if (siteLoc != null) from.Add(siteLoc);
            bool opIdClash = world.Operations.Get(site.FootprintId) != null;
            if (!opIdClash)
                foreach (LedgerLocation l in inv.Locations)
                    if (l.Kind == LocationKind.GroundPile && l.OwnerOperationId == site.FootprintId) from.Add(l);
            PointMm at = fp != null ? fp.Centre : default(PointMm);
            foreach (LedgerLocation l in from)
            {
                var items = new List<KeyValuePair<string, int>>(l.Counts);
                foreach (var kv in items)
                {
                    LedgerLocation pile = inv.GetOrAddGroundPile(l.Kind == LocationKind.GroundPile ? l.Position : at, null);
                    inv.Transfer(l, pile, Items.Find(kv.Key), kv.Value);
                }
            }
            world.Construction.Remove(site.FootprintId);
        }

        /// <inheritdoc />
        public CommandResult Apply(SimWorld world)
        {
            Operation op = Owner(world);
            if (op == null) return CommandResult.Rejected(CommandRejectCode.NotFound, "no module " + ModuleId);
            CommandResult check = Validate(world);
            if (check.IsRejected) return check;
            ConstructionSite site = world.Construction.Get(ModuleId);
            if (site != null) ReleaseSite(world, site, world.Footprints.Get(ModuleId));
            world.Operations.RemoveModule(op.Id, ModuleId);
            world.Footprints.Remove(ModuleId);
            return CommandResult.Accepted(removed: new long[] { ModuleId });
        }
    }
}
