using System.Collections.Generic;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Homes;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.People;

namespace LordsLegacy.Sim.Save.Sections
{
    /// <summary>
    /// The Food save section (T-415, decision 0034, id 9, version 9; T-464 adds the FoodHandedOver flag after the camp store id; T-418 adds each family's Sunday trip carrier id; T-417 adds each family's Sunday allotment reservation ids; T-465 adds the last Sunday served/short counts after that flag; T-416 adds each family's carried rations and Hungry flag; T-589 adds each family's allotment food-type count after its reservation ids):the camp food store location id, each family's
    /// larder id and each house's larder id. The food itself is in the Ledger section; this only links to it.
    /// Read after Homes (houses) and Population (families); ids are checked to be FoodStore locations.
    /// </summary>
    public sealed class FoodSection : ISaveSection
    {
        /// <summary>Section id in the save file.</summary>
        public const int SectionId = 9;

        /// <inheritdoc />
        public int Id => SectionId;

        /// <inheritdoc />
        public void Write(SimWorld world, SaveWriter w)
        {
            w.WriteInt32(world.Camp?.FoodStoreLocation?.Id ?? 0);
            w.WriteBool(world.FoodHandedOver); // T-464
            w.WriteInt32(world.LastAllotmentServed); // T-465
            w.WriteInt32(world.LastAllotmentShort);
            w.WriteBool(world.LastHungryTick.HasValue); // T-496
            w.WriteInt64(world.LastHungryTick ?? 0);
            var fams = new List<Family>(world.People.Families);
            w.WriteInt32(fams.Count);
            foreach (Family f in fams) { w.WriteInt32(f.Id); w.WriteInt32(f.LarderLocationId); w.WriteDouble(f.CarriedRations); w.WriteBool(f.IsHungry); w.WriteInt32(f.HungryDays); w.WriteInt32(f.TripCarrierId);
                w.WriteInt32(f.AllotmentReservationIds.Count);
                foreach (int rid in f.AllotmentReservationIds) w.WriteInt32(rid);
                w.WriteInt32(f.AllotmentFoodTypes); // T-589
            }
            w.WriteInt32(world.Homes.Count);
            foreach (HomeRecord h in world.Homes.All) { w.WriteInt32(h.HouseFootprintId); w.WriteInt32(h.LarderLocationId); }
        }

        /// <inheritdoc />
        public void Read(SimWorld world, SaveReader r)
        {
            int camp = r.ReadInt32();
            world.FoodHandedOver = r.ReadBool();
            world.LastAllotmentServed = r.ReadInt32();
            world.LastAllotmentShort = r.ReadInt32();
            bool hasHungry = r.ReadBool();
            long hungryTick = r.ReadInt64();
            world.LastHungryTick = hasHungry ? hungryTick : (long?)null;
            if (camp != 0)
            {
                if (world.Camp == null) throw new SaveFormatException("Food store " + camp + " saved without a camp.");
                world.Camp.FoodStoreLocation = CheckLarder(world, camp, "Camp food store");
            }
            int nf = r.ReadCount(25);
            for (int i = 0; i < nf; i++)
            {
                int id = r.ReadInt32(), loc = r.ReadInt32();
                double carried = r.ReadDouble();
                bool hungry = r.ReadBool();
                int hungryDays = r.ReadInt32();
                if (hungryDays < 0) throw new SaveFormatException("Family " + id + " has bad hungry days.");
                int tripCarrier = r.ReadInt32();
                if (tripCarrier != 0 && world.Inventory.Get(tripCarrier)?.Kind != LocationKind.Carrier)
                    throw new SaveFormatException("Family " + id + " trip carrier " + tripCarrier + " is not a carrier.");
                if (double.IsNaN(carried) || carried < 0) throw new SaveFormatException("Family " + id + " has bad carried rations.");
                Family f = world.People.FindFamily(id);
                if (f == null) throw new SaveFormatException("Larder for missing family " + id + ".");
                f.ClearAllotment();
                int na = r.ReadCount(4);
                for (int k = 0; k < na; k++)
                {
                    int rid = r.ReadInt32();
                    if (world.Inventory.GetReservation(rid) == null) throw new SaveFormatException("Family " + id + " allotment reservation " + rid + " is missing.");
                    f.AddAllotment(rid);
                }
                int foodTypes = r.ReadInt32(); // T-589
                if (foodTypes < 0) throw new SaveFormatException("Family " + id + " has bad allotment food types.");
                f.AllotmentFoodTypes = foodTypes;
                if (loc != 0) CheckLarder(world, loc, "Family " + id + " larder");
                f.LarderLocationId = loc;
                f.CarriedRations = carried;
                f.IsHungry = hungry;
                f.HungryDays = hungryDays;
                f.TripCarrierId = tripCarrier;
            }
            int nh = r.ReadCount(8);
            for (int i = 0; i < nh; i++)
            {
                int id = r.ReadInt32(), loc = r.ReadInt32();
                HomeRecord h = world.Homes.Get(id);
                if (h == null) throw new SaveFormatException("Larder for missing house " + id + ".");
                if (loc != 0) CheckLarder(world, loc, "House " + id + " larder");
                h.LarderLocationId = loc;
            }
        }

        private static LedgerLocation CheckLarder(SimWorld world, int id, string what)
        {
            LedgerLocation l = world.Inventory.Get(id);
            if (l == null || l.Kind != LocationKind.FoodStore) throw new SaveFormatException(what + " " + id + " is not a food store.");
            return l;
        }
    }
}
