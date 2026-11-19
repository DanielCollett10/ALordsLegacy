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

        /// <summary>The next family id that was never used: one above the highest id ever used (1 if none).</summary>
        public int NextFamilyId => NextAbove(_usedFamilyIds);

        /// <summary>The next citizen id that was never used: one above the highest id ever used (1 if none).</summary>
        public int NextCitizenId => NextAbove(_usedCitizenIds);

        /// <summary>The next ox id that was never used: one above the highest id ever used (1 if none).</summary>
        public int NextOxId => NextAbove(_usedOxIds);

        /// <summary>Every family id ever used, ascending (live and retired). Saved so ids are never reused after load.</summary>
        internal IEnumerable<int> UsedFamilyIds => Sorted(_usedFamilyIds);

        /// <summary>Every citizen id ever used, ascending.</summary>
        internal IEnumerable<int> UsedCitizenIds => Sorted(_usedCitizenIds);

        /// <summary>Every ox id ever used, ascending.</summary>
        internal IEnumerable<int> UsedOxIds => Sorted(_usedOxIds);

        /// <summary>Restore: adds a family with its members list and workplace. Members are checked later by <see cref="CheckLinks"/>.</summary>
        internal void RestoreFamily(int id, string surname, double homeX, double homeZ, int? workplaceId, IEnumerable<int> memberIds,
            FamilyPassage passage = FamilyPassage.None, int level = 1)
        {
            Claim(_usedFamilyIds, id, "family");
            var f = new Family(id, surname, homeX, homeZ) { WorkplaceId = workplaceId, Passage = passage, Level = level };
            foreach (int m in memberIds) f.AddMember(m);
            _families.Add(id, f);
            Version++;
        }

        /// <summary>Restore: adds a citizen without touching the family's member list (the family record carries it).</summary>
        internal Citizen RestoreCitizen(int id, string name, Sex sex, long birthTick, int familyId, double x, double z,
            double facingRad, Activity activity, double previousX, double previousZ, double multiplier)
        {
            Claim(_usedCitizenIds, id, "citizen");
            var c = new Citizen(id, name, sex, birthTick, familyId, x, z, facingRad, activity)
            {
                PreviousX = previousX, PreviousZ = previousZ, CurrentMultiplier = multiplier,
            };
            _citizens.Add(id, c);
            Version++;
            return c;
        }

        /// <summary>Restore: adds an ox with every field.</summary>
        internal Ox RestoreOx(int id, string name, double homeX, double homeZ, double x, double z, double facingRad,
            Activity activity, OxState state, double previousX, double previousZ, double multiplier, int carrierId)
        {
            Claim(_usedOxIds, id, "ox");
            var o = new Ox(id, name, homeX, homeZ, facingRad, activity)
            {
                X = x, Z = z, State = state, PreviousX = previousX, PreviousZ = previousZ,
                CurrentMultiplier = multiplier, CarrierId = carrierId,
            };
            _oxen.Add(id, o);
            Version++;
            return o;
        }

        /// <summary>Restore: marks ids as used (retired ids) without adding records.</summary>
        internal void RestoreUsedIds(IEnumerable<int> families, IEnumerable<int> citizens, IEnumerable<int> oxen)
        {
            foreach (int i in families) _usedFamilyIds.Add(i);
            foreach (int i in citizens) _usedCitizenIds.Add(i);
            foreach (int i in oxen) _usedOxIds.Add(i);
        }

        /// <summary>Restore check: every family member exists and points back at the family. Returns a fault text or null.</summary>
        internal string CheckLinks()
        {
            foreach (Family f in _families.Values)
                foreach (int m in f.MemberIds)
                {
                    if (!_citizens.TryGetValue(m, out var c)) return "Family " + f.Id + " lists missing citizen " + m + ".";
                    if (c.FamilyId != f.Id) return "Citizen " + m + " is listed by family " + f.Id + " but belongs to " + c.FamilyId + ".";
                }
            foreach (Citizen c in _citizens.Values)
            {
                if (!_families.TryGetValue(c.FamilyId, out var f)) return "Citizen " + c.Id + " has missing family " + c.FamilyId + ".";
                if (!HasMember(f, c.Id)) return "Citizen " + c.Id + " is not listed by family " + f.Id + ".";
            }
            return null;
        }

        private static bool HasMember(Family f, int id)
        {
            foreach (int m in f.MemberIds) if (m == id) return true;
            return false;
        }

        private static int NextAbove(HashSet<int> used)
        {
            int max = 0;
            foreach (int i in used) if (i > max) max = i;
            return max + 1;
        }

        private static List<int> Sorted(HashSet<int> used)
        {
            var l = new List<int>(used);
            l.Sort();
            return l;
        }

        private static void Claim(HashSet<int> used, int id, string kind)
        {
            if (!used.Add(id)) throw new ArgumentException("Duplicate or retired " + kind + " id " + id, nameof(id));
        }
    }
}
