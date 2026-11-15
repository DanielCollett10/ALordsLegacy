using System.Collections.Generic;

namespace LordsLegacy.Sim.People
{
    /// <summary>A family (PPL-11): surname, members, one home point and an optional workplace.</summary>
    public sealed class Family
    {
        private readonly List<int> _memberIds = new List<int>();

        /// <summary>Creates a family record. Use <see cref="Population.AddFamily"/>.</summary>
        public Family(int id, string surname, double homeX, double homeZ)
        {
            Id = id;
            Surname = surname ?? string.Empty;
            HomeX = homeX;
            HomeZ = homeZ;
        }

        /// <summary>Unique id; never reused.</summary>
        public int Id { get; }

        /// <summary>Family name.</summary>
        public string Surname { get; }

        /// <summary>Ids of the member citizens, in the order they were added.</summary>
        public IReadOnlyList<int> MemberIds => _memberIds;

        /// <summary>Home point x in metres (the tent spot while homeless).</summary>
        public double HomeX { get; set; }

        /// <summary>Home point z in metres.</summary>
        public double HomeZ { get; set; }

        /// <summary>Id of the workplace, or null for none (always none in M7).</summary>
        public int? WorkplaceId { get; set; }

        /// <summary>Footprint id of the house the family lives in, or null while Homeless (POP-A1, HM-13). Set by <c>HomeRegistry</c>.</summary>
        public int? HomeHouseId { get; internal set; }

        /// <summary>Ledger id of the family's camp larder (FD-10), or 0 for none yet. Saved by the Food section.</summary>
        public int LarderLocationId { get; internal set; }

        /// <summary>Ledger id of the carrier that holds the family's food on the Sunday trip (FD-14), or 0 for none yet. Saved by the Food section.</summary>
        public int TripCarrierId { get; internal set; }

        /// <summary>Rations left over from a part-eaten item, carried to tomorrow (FD-15). Saved by the Food section.</summary>
        public double CarriedRations { get; internal set; }

        /// <summary>True when the family's larder could not cover its last daily need (FD-15). No effect on work (FD-16).</summary>
        public bool IsHungry { get; internal set; }

        /// <summary>Ledger reservation ids of this week's Sunday allotment on the market stock (FD-13). Saved by the Food section.</summary>
        public IReadOnlyList<int> AllotmentReservationIds => _allotment;

        internal void AddAllotment(int reservationId) => _allotment.Add(reservationId);

        internal void ClearAllotment() => _allotment.Clear();

        private readonly List<int> _allotment = new List<int>();

        /// <summary>True while the family has no house (it lives at the camp).</summary>
        public bool IsHomeless => !HomeHouseId.HasValue;

        internal void AddMember(int citizenId) => _memberIds.Add(citizenId);

        internal void RemoveMember(int citizenId) => _memberIds.Remove(citizenId);
    }
}
