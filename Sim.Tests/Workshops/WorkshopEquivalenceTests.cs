using System.Linq;
using System.Text;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.People;
using LordsLegacy.Sim.Tasks;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Workshops
{
    /// <summary>
    /// T-557: a hewing station and a sawpit run ten game days and give exactly the journals and tasks they gave before
    /// the recipe data model. The expected strings were captured on the code before the change (main at T-557 start).
    /// </summary>
    public class WorkshopEquivalenceTests
    {
        private const long AdultBirth = -4_000_000;
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static Operation Workshop(SimWorld w, string type, double x, double z, int family)
        {
            int id = w.Footprints.Add(100 + w.Operations.Count, ModuleTypes.Find(type), M(x, z), 0).Id;
            Operation op = w.Operations.Create(OperationTypes.WorkshopOperationFor(type), 0);
            w.Operations.AddModule(op.Id, id);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(id));
            Assert.IsTrue(w.Execute(new AssignFamily(op.Id, family)).IsAccepted);
            return op;
        }

        // Journals and tasks after ten game days, as one comparable string.
        private static string Digest(int reserve)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8, WorkshopDefaultReserve = 0 });
            for (int fam = 2; fam <= 4; fam++)
            {
                w.People.AddFamily(fam, "F" + fam, 40, 40);
                for (int i = 0; i < 2; i++) w.People.AddCitizen(fam * 10 + i, "C" + fam + i, Sex.Male, AdultBirth, fam, 40, 40);
            }
            for (int i = 0; i < 2; i++) w.People.AddOx(50 + i, "O" + i, 40, 44).State = OxState.Resting(40000, 44000);
            Operation hew = Workshop(w, ModuleTypes.HewingStationId, 60, 60, 3);
            Operation saw = Workshop(w, ModuleTypes.SawpitId, 70, 60, 4);
            LedgerLocation pile = w.Inventory.AddStockpile(M(20, 20));
            w.Inventory.Produce(pile, Items.Log, 12);
            w.Inventory.Produce(pile, Items.Beam, 6);
            if (reserve != 0)
            {
                Assert.IsTrue(w.Execute(new SetWorkshopReserve(hew.Id, Items.Log.Id, reserve)).IsAccepted);
                Assert.IsTrue(w.Execute(new SetWorkshopReserve(saw.Id, Items.Beam.Id, reserve)).IsAccepted);
            }
            w.Step(10 * 24 * 60);
            var sb = new StringBuilder();
            foreach (string id in new[] { Items.Log.Id, Items.Beam.Id, Items.Plank.Id })
                sb.Append(id).Append(" total=").Append(w.Inventory.TotalOf(id)).Append(" consumed=").Append(w.Inventory.ConsumedOf(id)).Append(';');
            var tasks = new StringBuilder();
            foreach (TaskRecord t in w.Tasks.All) tasks.Append(t.Def.Id).Append(':').Append(t.IsFinished).Append('|');
            long hash = 1469598103934665603L;
            foreach (char c in tasks.ToString()) hash = unchecked((hash ^ c) * 1099511628211L);
            sb.Append("tasks=").Append(w.Tasks.All.Count()).Append(" hash=").Append(hash);
            return sb.ToString();
        }

        [Test]
        public void TenDays_DefaultReserve_MatchesPreChangeBaseline()
        {
            Assert.AreEqual("item.log total=1 consumed=11;item.beam total=6 consumed=6;item.plank total=18 consumed=0;tasks=41 hash=-1637335497790793363", Digest(0));
        }

        [Test]
        public void TenDays_ChangedReserve_MatchesPreChangeBaseline()
        {
            Assert.AreEqual("item.log total=3 consumed=9;item.beam total=9 consumed=6;item.plank total=18 consumed=0;tasks=30 hash=3326486814529226683", Digest(3));
        }
    }
}
