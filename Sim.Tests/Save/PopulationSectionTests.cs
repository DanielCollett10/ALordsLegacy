using System.Linq;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class PopulationSectionTests
    {
        private static (SimWorld w, MapData map) Make()
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            return (SimWorld.FromMapData(map, new SimTuning { StartHour = 8 }), map);
        }

        private static SimWorld RoundTrip(SimWorld w, MapData map)
        {
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            var back = SimSave.Read(bytes, map);
            Assert.AreEqual(bytes, SimSave.Write(back, "s", 1, map.MapId, map.ContentHash), "second write is identical");
            return back;
        }

        [Test]
        public void EmptyPopulation_RoundTrips()
        {
            var (w, map) = Make();
            SimWorld back = RoundTrip(w, map);
            Assert.AreEqual(0, back.People.FamilyCount);
            Assert.AreEqual(0, back.People.CitizenCount);
            Assert.AreEqual(0, back.People.OxCount);
        }

        [Test]
        public void Family_RoundTripsNameHomeWorkplaceAndMemberOrder()
        {
            var (w, map) = Make();
            Family f = w.People.AddFamily(3, "Ashdown", 12.5, 40.25);
            f.WorkplaceId = 9;
            w.People.AddCitizen(8, "Zed", Sex.Male, 5, 3, 1, 1);
            w.People.AddCitizen(2, "Amy", Sex.Female, 6, 3, 1, 1);
            w.People.AddFamily(4, "Bell", 1, 2);
            Family b = RoundTrip(w, map).People.FindFamily(3);
            Assert.AreEqual("Ashdown", b.Surname);
            Assert.AreEqual(12.5, b.HomeX);
            Assert.AreEqual(40.25, b.HomeZ);
            Assert.AreEqual(9, b.WorkplaceId);
            Assert.AreEqual(new[] { 8, 2 }, b.MemberIds.ToArray());
            Assert.IsNull(RoundTrip(w, map).People.FindFamily(4).WorkplaceId);
        }

        [Test]
        public void Citizen_RoundTripsEveryField()
        {
            var (w, map) = Make();
            w.People.AddFamily(1, "Ash", 5, 6);
            Citizen c = w.People.AddCitizen(7, "Wren", Sex.Female, -12345, 1, 10.123456789, 20.987654321, 1.57, Activity.Working);
            c.PreviousX = 9.5; c.PreviousZ = 19.5; c.CurrentMultiplier = 0.75;
            Citizen b = RoundTrip(w, map).People.FindCitizen(7);
            Assert.AreEqual("Wren", b.Name);
            Assert.AreEqual(Sex.Female, b.Sex);
            Assert.AreEqual(-12345L, b.BirthTick);
            Assert.AreEqual(1, b.FamilyId);
            Assert.AreEqual(10.123456789, b.X);
            Assert.AreEqual(20.987654321, b.Z);
            Assert.AreEqual(1.57, b.FacingRad);
            Assert.AreEqual(Activity.Working, b.Activity);
            Assert.AreEqual(9.5, b.PreviousX);
            Assert.AreEqual(19.5, b.PreviousZ);
            Assert.AreEqual(0.75, b.CurrentMultiplier);
        }

        [Test]
        public void Ox_RoundTripsEveryFieldAndEveryOxState()
        {
            var (w, map) = Make();
            Ox free = w.People.AddOx(1, "Free", 1, 2);
            Ox res = w.People.AddOx(2, "Res", 3, 4, 0.5, Activity.MillingAbout);
            res.State = OxState.Reserved(77);
            Ox led = w.People.AddOx(3, "Led", 5, 6);
            led.State = OxState.Led(42);
            led.X = 7.5; led.Z = 8.5; led.PreviousX = 7; led.PreviousZ = 8; led.CurrentMultiplier = 0.5; led.CarrierId = 11;
            Ox rest = w.People.AddOx(4, "Rest", 9, 10);
            rest.State = OxState.Resting(1234, 5678, 6);
            SimWorld back = RoundTrip(w, map);
            Assert.AreEqual(OxStateKind.Free, back.People.FindOx(1).State.Kind);
            Assert.AreEqual(OxStateKind.Reserved, back.People.FindOx(2).State.Kind);
            Assert.AreEqual(77, back.People.FindOx(2).State.TaskId);
            Assert.AreEqual(0.5, back.People.FindOx(2).FacingRad);
            Assert.AreEqual(Activity.MillingAbout, back.People.FindOx(2).Activity);
            Ox l = back.People.FindOx(3);
            Assert.AreEqual(OxStateKind.Led, l.State.Kind);
            Assert.AreEqual(42, l.State.HandlerId);
            Assert.AreEqual(5.0, l.HomeX);
            Assert.AreEqual(6.0, l.HomeZ);
            Assert.AreEqual(7.5, l.X);
            Assert.AreEqual(8.5, l.Z);
            Assert.AreEqual(7.0, l.PreviousX);
            Assert.AreEqual(8.0, l.PreviousZ);
            Assert.AreEqual(0.5, l.CurrentMultiplier);
            Assert.AreEqual(11, l.CarrierId);
            Ox r = back.People.FindOx(4);
            Assert.AreEqual(OxStateKind.Resting, r.State.Kind);
            Assert.AreEqual(1234L, r.State.SpotXMm);
            Assert.AreEqual(5678L, r.State.SpotZMm);
            Assert.AreEqual(6, r.State.StandId);
            Assert.AreEqual("Rest", r.Name);
        }

        [Test]
        public void IdsAreNeverReusedAfterLoad_IncludingRemovedOnes()
        {
            var (w, map) = Make();
            w.People.AddFamily(1, "A", 0, 0);
            w.People.AddFamily(2, "B", 0, 0);
            w.People.AddCitizen(1, "a", Sex.Male, 0, 1, 0, 0);
            w.People.AddCitizen(2, "b", Sex.Male, 0, 1, 0, 0);
            w.People.AddOx(1, "o1", 0, 0);
            w.People.AddOx(2, "o2", 0, 0);
            w.People.RemoveCitizen(2);
            w.People.RemoveOx(2);
            w.People.RemoveFamily(2);
            SimWorld back = RoundTrip(w, map);
            Population p = back.People;
            Assert.AreEqual(3, p.NextCitizenId);
            Assert.AreEqual(3, p.NextOxId);
            Assert.AreEqual(3, p.NextFamilyId);
            Assert.Throws<System.ArgumentException>(() => p.AddCitizen(2, "x", Sex.Male, 0, 1, 0, 0));
            Assert.Throws<System.ArgumentException>(() => p.AddOx(2, "x", 0, 0));
            Assert.Throws<System.ArgumentException>(() => p.AddFamily(2, "x", 0, 0));
            Citizen c = p.AddCitizen(p.NextCitizenId, "new", Sex.Female, 0, 1, 0, 0);
            Assert.AreEqual(3, c.Id);
            Assert.AreEqual(3, p.AddOx(p.NextOxId, "n", 0, 0).Id);
            Assert.AreEqual(3, p.AddFamily(p.NextFamilyId, "n", 0, 0).Id);
            Assert.AreEqual(new[] { 1, 3 }, p.FindFamily(1).MemberIds.ToArray());
        }

        [Test]
        public void MemberOfMissingCitizen_IsRejected()
        {
            var (w, map) = Make();
            w.People.AddFamily(1, "A", 0, 0);
            w.People.AddCitizen(1, "a", Sex.Male, 0, 1, 0, 0);
            // Corrupt: remove the citizen record but leave the family listing it.
            w.People.RestoreFamily(5, "Bad", 0, 0, null, new[] { 99 });
            var bytes = SimSave.Write(w, "s", 1, map.MapId, map.ContentHash);
            Assert.Throws<SaveFormatException>(() => SimSave.Read(bytes, map));
        }
    }
}
