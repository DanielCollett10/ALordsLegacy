using System;
using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Forestry;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;

namespace LordsLegacy.Sim.Start
{
    /// <summary>Fills a new world from a <see cref="StartLayout"/> (CMP-01..07, WORLD-08-A).</summary>
    public static class StartLayoutApplier
    {
        /// <summary>
        /// Adds the camp footprint (removing trees under it), draws the trunk road through
        /// <see cref="DrawRoad"/>, adds the camp stockpile (a ledger Stockpile at the stockpile spot, filled with
        /// <see cref="StartLayout.Stock"/>), adds families (home = tent spot), citizens at home and oxen at the stand.
        /// Throws <see cref="InvalidOperationException"/> with the refusal text if the road is refused.
        /// </summary>
        public static void Apply(SimWorld world, StartLayout layout)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (world.Camp != null) throw new InvalidOperationException("A start layout was already applied.");

            int fpId = world.Footprints.Count == 0 ? 1 : world.Footprints.Ids.Max() + 1;
            Footprint camp = world.Footprints.Add(fpId, FootprintTypeDef.Camp, layout.CampCentre, layout.CampRotationDegrees);
            if (camp.Type.ClearsTrees) TreeClearing.RemoveInPolygon(world.Trees, camp.Polygon);

            if (layout.TrunkRoad.Count > 0)
            {
                CommandResult r = world.Execute(new DrawRoad(layout.TrunkRoad, layout.TrunkRoadTypeId));
                if (r.IsRejected) throw new InvalidOperationException("Trunk road refused: " + r.Text);
            }

            PointMm stockpileSpot = ToWorld(layout, layout.Stockpile);
            LedgerLocation stockpile = world.Inventory.AddStockpile(stockpileSpot);
            foreach (StartStock s in layout.Stock)
                if (s.Count > 0) world.Inventory.Produce(stockpile, s.Item, s.Count);

            world.Camp = new CampInfo(fpId, ToWorld(layout, layout.Fire), stockpileSpot,
                ToWorld(layout, layout.OxStand), stockpile);

            int famId = 1, citId = 1;
            foreach (StartFamily f in layout.Families)
            {
                PointMm home = ToWorld(layout, f.TentSpot);
                world.People.AddFamily(famId, f.Surname, home.XMetres, home.ZMetres);
                foreach (StartCitizen c in f.Citizens)
                    world.People.AddCitizen(citId++, c.Name, c.Sex, c.BirthTick, famId, home.XMetres, home.ZMetres);
                famId++;
            }

            PointMm stand = world.Camp.OxStand;
            for (int i = 1; i <= layout.OxCount; i++)
                world.People.AddOx(i, "Ox " + i, stand.XMetres, stand.ZMetres);
        }

        private static PointMm ToWorld(StartLayout l, PointMm rel)
        {
            double rad = l.CampRotationDegrees * Math.PI / 180.0;
            double c = Math.Cos(rad), s = Math.Sin(rad);
            return new PointMm(
                l.CampCentre.X + (long)Math.Round(rel.X * c - rel.Z * s, MidpointRounding.AwayFromZero),
                l.CampCentre.Z + (long)Math.Round(rel.X * s + rel.Z * c, MidpointRounding.AwayFromZero));
        }
    }
}
