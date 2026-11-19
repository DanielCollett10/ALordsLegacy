using System.Linq;
using LordsLegacy.Sim.Commands;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Footprints;
using LordsLegacy.Sim.Geometry;
using LordsLegacy.Sim.Inventory;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Operations;
using LordsLegacy.Sim.Operations.Workshops;
using LordsLegacy.Sim.Placement;
using LordsLegacy.Sim.Save;
using LordsLegacy.Sim.Terrain;
using LordsLegacy.Sim.Tuning;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Placement
{
    /// <summary>The daub pit's yard is 5 m like the granary, not the workshops' 3 m (T-627, HL-01, owner 19/11/2026).</summary>
    public class DaubPitYardTests
    {
        private static PointMm M(double x, double z) => PointMm.FromMetres(x, z);

        private static SimWorld Make(int logs = 24)
        {
            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            var w = SimWorld.FromMapData(map, new SimTuning { StartHour = 8 });
            if (logs > 0) w.Inventory.Produce(w.Inventory.AddStockpile(M(20, 20)), Items.Log, logs);
            return w;
        }

        private static CommandResult Place(SimWorld w, string typeId, double x, double z) =>
            w.Execute(new PlaceStructure(typeId, M(x, z), 0));

        // Sawpit at (50, 50): building x 47..53, z 48..52. Daub pit building is 4 m wide, so its left edge at
        // 53 + gap needs centre x = 55 + gap. The pit's 5 m yard reaches the sawpit when gap < 5.
        private const double PitCentreAtGap4 = 59;
        private const double PitCentreAtGap5 = 60;

        [Test]
        public void DaubPitType_HasA5mYard_LikeTheGranary()
        {
            Assert.AreEqual(5000, ModuleTypes.DaubPit.YardMarginMm);
            Assert.AreEqual(5000, ModuleTypes.Granary.YardMarginMm);
            Assert.AreSame(ModuleTypes.DaubPit, ModuleTypes.Find(ModuleTypes.DaubPitId));
            var fp = new Footprint(1, ModuleTypes.DaubPit, M(50, 50), 0);
            Assert.AreEqual(14.0 * 14.0, Polygons.SignedAreaM2(fp.PlotPolygon), 0.001, "4 m building + 5 m yard each side");
        }

        [Test]
        public void NewDaubPit_WithinFourMetresOfAStructure_IsRefused()
        {
            SimWorld w = Make();
            Assert.IsTrue(Place(w, ModuleTypes.SawpitId, 50, 50).IsAccepted);
            CommandResult r = Place(w, ModuleTypes.DaubPitId, PitCentreAtGap4, 50);
            Assert.IsTrue(r.IsRejected, "5 m yard reaches the sawpit 4 m away (3 m yard would have allowed it)");
            Assert.AreEqual(StructureRefusal.OverlapsFootprint,
                StructurePlacement.Preview(w, ModuleTypes.DaubPitId, M(PitCentreAtGap4, 50), 0).Refusal);
        }

        [Test]
        public void NewDaubPit_At5mFromAStructure_IsAccepted()
        {
            SimWorld w = Make();
            Assert.IsTrue(Place(w, ModuleTypes.SawpitId, 50, 50).IsAccepted);
            CommandResult r = Place(w, ModuleTypes.DaubPitId, PitCentreAtGap5, 50);
            Assert.IsTrue(r.IsAccepted, "plots only touch at 5 m, which is allowed: " + r.Text);
        }

        [Test]
        public void NewStructure_WithinFiveMetresOfPlacedDaubPit_IsNotBlockedByThePitsYard()
        {
            // Documents the current rule (same as the granary): only the NEW structure's plot is checked against
            // existing buildings, so an existing pit's 5 m yard does not stop a later 3 m workshop 4 m away.
            SimWorld w = Make();
            Assert.IsTrue(Place(w, ModuleTypes.DaubPitId, 50, 50).IsAccepted);
            Assert.IsTrue(Place(w, ModuleTypes.SawpitId, PitCentreAtGap4 + 1, 50).IsAccepted);
        }

        [Test]
        public void NewDaubPit_NextToAGranary_AtFourMetres_IsRefused_AndAt5m_IsAccepted()
        {
            SimWorld w = Make(logs: 40);
            Assert.IsTrue(Place(w, ModuleTypes.GranaryId, 50, 50).IsAccepted);
            // Granary building x 46..54, so the pit building edge at 54 + gap needs centre 56 + gap.
            Assert.IsTrue(Place(w, ModuleTypes.DaubPitId, 60, 50).IsRejected, "gap 4 m: pit yard reaches the granary");
            Assert.IsTrue(Place(w, ModuleTypes.DaubPitId, 61, 50).IsAccepted, "gap 5 m: plots only touch");
        }

        [Test]
        public void DaubPit_PlacedUnder3mRule_SaveLoads_AndNeighbourIsNotRechecked()
        {
            // A current-version save (no version bump, T-627) holds a pit placed under the old 3 m yard. The pit
            // sits 4 m from a sawpit, which the 5 m rule now refuses. Loading must not re-check neighbours.
            SimWorld w = Make();
            Assert.IsTrue(Place(w, ModuleTypes.SawpitId, 50, 50).IsAccepted);
            int pitId = w.Footprints.NextId();
            w.Footprints.Add(pitId, ModuleTypes.DaubPit, M(PitCentreAtGap4, 50), 0);
            Operation pitOp = w.Operations.Create(OperationTypes.DaubingId, 0);
            w.Operations.AddModule(pitOp.Id, pitId);
            WorkshopBuildings.OnBuilt(w, w.Footprints.Get(pitId)); // legacy pit, already built
            Assert.IsFalse(StructurePlacement.Preview(w, ModuleTypes.DaubPitId, M(PitCentreAtGap4, 50), 0).Ok, "the 5 m rule refuses it now");

            var map = new MapData("flat", new HeightGrid(0f, 0f, 2f, 101, 101, new float[101 * 101]), new MapTree[0]);
            byte[] bytes = SimSave.Write(w, "legacy", 1, map.MapId, map.ContentHash);
            SimWorld back = SimSave.Read(bytes, map);

            Assert.AreEqual(2, back.Footprints.Count, "both footprints load");
            Footprint pit = back.Footprints.Get(pitId);
            Assert.IsNotNull(pit);
            Assert.AreEqual(ModuleTypes.DaubPitId, pit.Type.Id);
            Assert.AreEqual(PitCentreAtGap4, pit.Centre.XMetres, 0.001);
            Assert.IsNotNull(back.Operations.FindByModule(pitId), "pit operation loads");
            Assert.AreEqual(bytes, SimSave.Write(back, "legacy", 1, map.MapId, map.ContentHash), "byte-identical re-save");
        }
    }
}
