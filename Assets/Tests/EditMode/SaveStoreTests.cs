using System.IO;
using LordsLegacy.Game;
using LordsLegacy.Sim.Core;
using LordsLegacy.Sim.Save;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    public sealed class SaveStoreTests
    {
        private string _dir;
        private SaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ll-savestore-" + System.Guid.NewGuid().ToString("N"));
            _store = new SaveStore(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static byte[] Save(string name, long created, int steps = 0)
        {
            var world = new SimWorld(new LordsLegacy.Sim.Tuning.SimTuning());
            world.Step(steps);
            return SimSave.Write(world, name, created, "map", 42UL);
        }

        [Test]
        public void ListIsEmptyWhenFolderMissing()
        {
            Assert.AreEqual(0, _store.List().Count);
        }

        [Test]
        public void ListIsNewestFirst()
        {
            _store.WriteNew("old", Save("old", 100));
            _store.WriteNew("newest", Save("newest", 300));
            _store.WriteNew("mid", Save("mid", 200));
            var list = _store.List();
            Assert.AreEqual(new[] { "newest", "mid", "old" }, new[] { list[0].Header.Name, list[1].Header.Name, list[2].Header.Name });
        }

        [Test]
        public void SameNameGetsUniqueFilesAndNeverOverwrites()
        {
            string a = _store.WriteNew("Camp", Save("Camp", 1));
            string b = _store.WriteNew("Camp", Save("Camp", 2));
            string c = _store.WriteNew("Camp", Save("Camp", 3));
            Assert.AreEqual(3, new[] { a, b, c }.Length);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(b, c);
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(3, _store.List().Count);
            Assert.AreEqual(1, SaveReader.ReadHeaderOnly(File.ReadAllBytes(a)).CreatedRealTime);
        }

        [Test]
        public void NameWithBadFileCharactersStillSaves()
        {
            string path = _store.WriteNew("Sun 14 Mar Year 1 08:40", Save("Sun 14 Mar Year 1 08:40", 5));
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("Sun 14 Mar Year 1 08:40", _store.List()[0].Header.Name);
        }

        [Test]
        public void ManualSaveNamedAutosaveDoesNotTakeTheAutosaveSlot()
        {
            string auto = _store.WriteAutosave(Save("Autosave", 1));
            string manual = _store.WriteNew("Autosave", Save("Autosave", 2));
            Assert.AreNotEqual(auto, manual);
            Assert.AreEqual(1, SaveReader.ReadHeaderOnly(File.ReadAllBytes(auto)).CreatedRealTime);
        }

        [Test]
        public void AutosaveOverwritesItsOwnSlot()
        {
            _store.WriteAutosave(Save("Autosave", 1));
            _store.WriteAutosave(Save("Autosave", 2));
            var list = _store.List();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(2, list[0].Header.CreatedRealTime);
        }

        [Test]
        public void ListReadsHeaderFields()
        {
            _store.WriteNew("h", Save("h", 777, 5));
            var h = _store.List()[0].Header;
            Assert.AreEqual("h", h.Name);
            Assert.AreEqual(777, h.CreatedRealTime);
            Assert.AreEqual(5, h.GameTick);
            Assert.AreEqual("map", h.MapId);
            Assert.AreEqual(42UL, h.MapContentHash);
        }

        [Test]
        public void ListSkipsCorruptFiles()
        {
            _store.WriteNew("good", Save("good", 1));
            File.WriteAllBytes(Path.Combine(_dir, "bad" + SaveStore.Extension), new byte[] { 1, 2, 3 });
            Assert.AreEqual(1, _store.List().Count);
        }
    }
}
