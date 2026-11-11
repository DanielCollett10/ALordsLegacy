using System;
using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests
{
    public class PopulationTests
    {
        [Test]
        public void World_People_is_never_null_and_empty()
        {
            var w = new SimWorld(new SimTuning());
            Assert.IsNotNull(w.People);
            Assert.AreEqual(0, w.People.CitizenCount);
        }

        [Test]
        public void Citizens_iterate_in_id_order_whatever_the_add_order()
        {
            var p = new Population();
            p.AddFamily(1, "Hale", 0, 0);
            foreach (int id in new[] { 5, 2, 9, 1 }) p.AddCitizen(id, "c" + id, Sex.Male, 0, 1, 0, 0);
            CollectionAssert.AreEqual(new[] { 1, 2, 5, 9 }, p.Citizens.Select(c => c.Id).ToArray());
        }

        [Test]
        public void Families_and_oxen_iterate_in_id_order()
        {
            var p = new Population();
            p.AddFamily(3, "C", 0, 0); p.AddFamily(1, "A", 0, 0);
            p.AddOx(7, "x", 0, 0); p.AddOx(4, "y", 0, 0);
            CollectionAssert.AreEqual(new[] { 1, 3 }, p.Families.Select(f => f.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 4, 7 }, p.Oxen.Select(o => o.Id).ToArray());
        }

        [Test]
        public void Find_returns_record_or_null_when_unknown()
        {
            var p = new Population();
            p.AddFamily(1, "Hale", 2, 3);
            p.AddCitizen(10, "Ann", Sex.Female, -100, 1, 1, 1);
            p.AddOx(1, "Bull", 5, 6);
            Assert.AreEqual("Ann", p.FindCitizen(10).Name);
            Assert.AreEqual("Hale", p.FindFamily(1).Surname);
            Assert.AreEqual(5, p.FindOx(1).X);
            Assert.IsNull(p.FindCitizen(11));
            Assert.IsNull(p.FindFamily(2));
            Assert.IsNull(p.FindOx(2));
        }

        [Test]
        public void Citizen_is_listed_in_family_members_and_family_has_no_workplace()
        {
            var p = new Population();
            var f = p.AddFamily(1, "Hale", 0, 0);
            p.AddCitizen(2, "A", Sex.Male, 0, 1, 0, 0);
            p.AddCitizen(1, "B", Sex.Female, 0, 1, 0, 0);
            CollectionAssert.AreEqual(new[] { 2, 1 }, f.MemberIds.ToArray());
            Assert.IsNull(f.WorkplaceId);
        }

        [Test]
        public void Version_bumps_on_add_and_remove_only()
        {
            var p = new Population();
            int v = p.Version;
            p.AddFamily(1, "H", 0, 0);
            Assert.AreEqual(v + 1, p.Version);
            p.AddCitizen(1, "A", Sex.Male, 0, 1, 0, 0);
            p.AddOx(1, "O", 0, 0);
            Assert.AreEqual(v + 3, p.Version);
            Assert.IsFalse(p.RemoveCitizen(99));
            Assert.AreEqual(v + 3, p.Version);
            Assert.IsTrue(p.RemoveCitizen(1));
            Assert.IsTrue(p.RemoveOx(1));
            Assert.AreEqual(v + 5, p.Version);
        }

        [Test]
        public void Removed_id_cannot_be_reused()
        {
            var p = new Population();
            p.AddFamily(1, "H", 0, 0);
            p.AddCitizen(1, "A", Sex.Male, 0, 1, 0, 0);
            p.RemoveCitizen(1);
            Assert.AreEqual(0, p.FindFamily(1).MemberIds.Count);
            Assert.Throws<ArgumentException>(() => p.AddCitizen(1, "B", Sex.Male, 0, 1, 0, 0));
            p.AddOx(1, "O", 0, 0);
            p.RemoveOx(1);
            Assert.Throws<ArgumentException>(() => p.AddOx(1, "O2", 0, 0));
        }

        [Test]
        public void Duplicate_id_and_unknown_family_throw()
        {
            var p = new Population();
            p.AddFamily(1, "H", 0, 0);
            Assert.Throws<ArgumentException>(() => p.AddFamily(1, "X", 0, 0));
            Assert.Throws<ArgumentException>(() => p.AddCitizen(1, "A", Sex.Male, 0, 9, 0, 0));
        }

        [Test]
        public void Family_with_members_cannot_be_removed()
        {
            var p = new Population();
            p.AddFamily(1, "H", 0, 0);
            p.AddCitizen(1, "A", Sex.Male, 0, 1, 0, 0);
            Assert.IsFalse(p.RemoveFamily(1));
        }

        [Test]
        public void AgeYears_at_default_rate_is_twice_calendar_years()
        {
            var tuning = new SimTuning();
            Assert.AreEqual(2.0, tuning.AgeingRate);
            var c = new Citizen(1, "A", Sex.Male, 0, 1, 0, 0);
            Assert.AreEqual(2.0, c.AgeYears(Citizen.TicksPerYear, tuning.AgeingRate), 1e-9);
            Assert.AreEqual(1.0, c.AgeYears(Citizen.TicksPerYear / 2, tuning.AgeingRate), 1e-9);
        }

        [Test]
        public void AgeYears_counts_from_negative_birth_tick()
        {
            var c = new Citizen(1, "A", Sex.Male, -10 * Citizen.TicksPerYear, 1, 0, 0);
            Assert.AreEqual(20.0, c.AgeYears(0, 2), 1e-9);
        }

        [Test]
        public void AgeYears_is_zero_before_birth_and_at_birth()
        {
            var c = new Citizen(1, "A", Sex.Male, 1000, 1, 0, 0);
            Assert.AreEqual(0.0, c.AgeYears(1000, 2));
            Assert.AreEqual(0.0, c.AgeYears(0, 2));
        }

        [Test]
        public void AgeYears_with_rate_one_is_calendar_years_and_negative_rate_throws()
        {
            var c = new Citizen(1, "A", Sex.Male, 0, 1, 0, 0);
            Assert.AreEqual(1.0, c.AgeYears(Citizen.TicksPerYear, 1), 1e-9);
            Assert.Throws<ArgumentOutOfRangeException>(() => c.AgeYears(10, -1));
        }

        [Test]
        public void Ox_starts_at_home_stand_point()
        {
            var o = new Population().AddOx(1, "Bull", 4, 5);
            Assert.AreEqual(4, o.X);
            Assert.AreEqual(5, o.Z);
            Assert.AreEqual(Activity.Waiting, o.Activity);
        }
    }
}
