using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.People
{
    /// <summary>
    /// Registry of all families, citizens and oxen (PPL-10..14). Each kind is keyed by an explicit id and
    /// iterates in ascending id order. Ids are never reused, even after removal.
    /// </summary>
    public sealed class Population
    {
        private readonly SortedDictionary<int, Family> _families = new SortedDictionary<int, Family>();
        private readonly SortedDictionary<int, Citizen> _citizens = new SortedDictionary<int, Citizen>();
        private readonly SortedDictionary<int, Ox> _oxen = new SortedDictionary<int, Ox>();
        private readonly HashSet<int> _usedFamilyIds = new HashSet<int>();
        private readonly HashSet<int> _usedCitizenIds = new HashSet<int>();
        private readonly HashSet<int> _usedOxIds = new HashSet<int>();

        /// <summary>Increases by one on every add and every successful remove. Views compare it to know when to redraw.</summary>
        public int Version { get; private set; }

        /// <summary>Families in ascending id order.</summary>
        public IEnumerable<Family> Families => _families.Values;

        /// <summary>Citizens in ascending id order.</summary>
        public IEnumerable<Citizen> Citizens => _citizens.Values;

        /// <summary>Oxen in ascending id order.</summary>
        public IEnumerable<Ox> Oxen => _oxen.Values;

        /// <summary>Number of families.</summary>
        public int FamilyCount => _families.Count;

        /// <summary>Number of citizens.</summary>
        public int CitizenCount => _citizens.Count;

        /// <summary>Number of oxen.</summary>
        public int OxCount => _oxen.Count;

        /// <summary>Adds a family. Throws if the id is or ever was in use.</summary>
        public Family AddFamily(int id, string surname, double homeX, double homeZ)
        {
            Claim(_usedFamilyIds, id, "family");
            var f = new Family(id, surname, homeX, homeZ);
            _families.Add(id, f);
            Version++;
            return f;
        }

        /// <summary>
        /// Adds a citizen and lists them in their family's members. Throws if the id is or ever was in use,
        /// or the family does not exist.
        /// </summary>
        public Citizen AddCitizen(int id, string name, Sex sex, long birthTick, int familyId, double x, double z,
            double facingRad = 0, Activity activity = Activity.MillingAbout)
        {
            if (!_families.TryGetValue(familyId, out var family))
                throw new ArgumentException("Unknown family id " + familyId, nameof(familyId));
            Claim(_usedCitizenIds, id, "citizen");
            var c = new Citizen(id, name, sex, birthTick, familyId, x, z, facingRad, activity);
            _citizens.Add(id, c);
            family.AddMember(id);
            Version++;
            return c;
        }

        /// <summary>Adds an ox standing at its home point. Throws if the id is or ever was in use.</summary>
        public Ox AddOx(int id, string name, double homeX, double homeZ, double facingRad = 0,
            Activity activity = Activity.Waiting)
        {
            Claim(_usedOxIds, id, "ox");
            var o = new Ox(id, name, homeX, homeZ, facingRad, activity);
            _oxen.Add(id, o);
            Version++;
            return o;
        }

        /// <summary>Finds a family, or null.</summary>
        public Family FindFamily(int id) => _families.TryGetValue(id, out var f) ? f : null;

        /// <summary>Finds a citizen, or null.</summary>
        public Citizen FindCitizen(int id) => _citizens.TryGetValue(id, out var c) ? c : null;

        /// <summary>Finds an ox, or null.</summary>
        public Ox FindOx(int id) => _oxen.TryGetValue(id, out var o) ? o : null;

        /// <summary>Removes a family only if it has no members. Returns false if unknown or not empty. The id is not reused.</summary>
        public bool RemoveFamily(int id)
        {
            if (!_families.TryGetValue(id, out var f) || f.MemberIds.Count > 0) return false;
            _families.Remove(id);
            Version++;
            return true;
        }

        /// <summary>Removes a citizen and drops them from their family. Returns false if unknown. The id is not reused.</summary>
        public bool RemoveCitizen(int id)
        {
            if (!_citizens.TryGetValue(id, out var c)) return false;
            _citizens.Remove(id);
            FindFamily(c.FamilyId)?.RemoveMember(id);
            Version++;
            return true;
        }

        /// <summary>Removes an ox. Returns false if unknown. The id is not reused.</summary>
        public bool RemoveOx(int id)
        {
            if (!_oxen.Remove(id)) return false;
            Version++;
            return true;
        }

        private static void Claim(HashSet<int> used, int id, string kind)
        {
            if (!used.Add(id)) throw new ArgumentException("Duplicate or retired " + kind + " id " + id, nameof(id));
        }
    }
}
