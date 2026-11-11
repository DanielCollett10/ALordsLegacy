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

        internal void AddMember(int citizenId) => _memberIds.Add(citizenId);

        internal void RemoveMember(int citizenId) => _memberIds.Remove(citizenId);
    }
}
