using System;
using LordsLegacy.Sim.Map;
using LordsLegacy.Sim.Save;
using NUnit.Framework;

namespace LordsLegacy.Sim.Tests.Save
{
    public class SaveFormatTests
    {
        private static SaveHeader Header() => new SaveHeader("Autumn é", 1700000000123L, 4242L, "synthetic-ramp", 0xCBF29CE484222325UL);

        private static byte[] SaveWithSections()
        {
            var w = new SaveWriter(Header());
            w.BeginSection(1);
            w.WriteInt32(7);
            w.EndSection();
            w.BeginSection(99); // unknown to the reader below
            w.WriteString("future stuff");
            w.WriteInt32Array(new[] { 1, 2, 3 });
            w.EndSection();
            w.BeginSection(2);
            w.WriteDouble(2.5);
            w.EndSection();
            return w.ToArray();
        }

        [Test]
        public void Primitives_RoundTrip()
        {
            var w = new SaveWriter();
            w.WriteByte(200); w.WriteBool(true); w.WriteBool(false);
            w.WriteInt32(int.MinValue); w.WriteInt32(-1); w.WriteInt32(123456789);
            w.WriteInt64(long.MinValue); w.WriteUInt64(ulong.MaxValue);
            w.WriteSingle(-1.25f); w.WriteSingle(float.NaN); w.WriteDouble(Math.PI);
            w.WriteString(""); w.WriteString("héllo 世界");
            w.WriteByteArray(new byte[] { 9, 8 }); w.WriteInt32Array(new int[0]);
            w.WriteSingleArray(new[] { 0.5f, -2f }); w.WriteDoubleArray(new[] { 1e300, -0.0 });
            var r = new SaveReader(w.ToArray());
            Assert.That(r.ReadByte(), Is.EqualTo(200));
            Assert.That(r.ReadBool(), Is.True); Assert.That(r.ReadBool(), Is.False);
            Assert.That(r.ReadInt32(), Is.EqualTo(int.MinValue)); Assert.That(r.ReadInt32(), Is.EqualTo(-1));
            Assert.That(r.ReadInt32(), Is.EqualTo(123456789));
            Assert.That(r.ReadInt64(), Is.EqualTo(long.MinValue)); Assert.That(r.ReadUInt64(), Is.EqualTo(ulong.MaxValue));
            Assert.That(r.ReadSingle(), Is.EqualTo(-1.25f)); Assert.That(float.IsNaN(r.ReadSingle()));
            Assert.That(r.ReadDouble(), Is.EqualTo(Math.PI));
            Assert.That(r.ReadString(), Is.EqualTo("")); Assert.That(r.ReadString(), Is.EqualTo("héllo 世界"));
            Assert.That(r.ReadByteArray(), Is.EqualTo(new byte[] { 9, 8 })); Assert.That(r.ReadInt32Array(), Is.Empty);
            Assert.That(r.ReadSingleArray(), Is.EqualTo(new[] { 0.5f, -2f }));
            Assert.That(r.ReadDoubleArray(), Is.EqualTo(new[] { 1e300, -0.0 }));
            Assert.That(r.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void Int32_IsLittleEndian()
        {
            var w = new SaveWriter();
            w.WriteInt32(0x01020304);
            Assert.That(w.ToArray(), Is.EqualTo(new byte[] { 4, 3, 2, 1 }));
        }

        [Test]
        public void Int64_IsLittleEndian()
        {
            var w = new SaveWriter();
            w.WriteInt64(0x0102030405060708L);
            Assert.That(w.ToArray(), Is.EqualTo(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }));
        }

        [Test]
        public void File_StartsWithMagicAndVersion()
        {
            var bytes = new SaveWriter(Header()).ToArray();
            Assert.That(bytes[0], Is.EqualTo((byte)'L')); Assert.That(bytes[1], Is.EqualTo((byte)'L'));
            Assert.That(bytes[2], Is.EqualTo((byte)'S')); Assert.That(bytes[3], Is.EqualTo((byte)'V'));
            Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(SaveWriter.CurrentVersion));
        }

        [Test]
        public void HeaderOnly_ReadsAllFields_WithoutSections()
        {
            var h = SaveReader.ReadHeaderOnly(SaveWithSections());
            Assert.That(h.Name, Is.EqualTo("Autumn é"));
            Assert.That(h.CreatedRealTime, Is.EqualTo(1700000000123L));
            Assert.That(h.GameTick, Is.EqualTo(4242L));
            Assert.That(h.MapId, Is.EqualTo("synthetic-ramp"));
            Assert.That(h.MapContentHash, Is.EqualTo(0xCBF29CE484222325UL));
        }

        [Test]
        public void HeaderOnly_WorksWhenSectionsAreCorruptOrCutOff()
        {
            var full = SaveWithSections();
            var headerEnd = new SaveWriter(Header()).Length;
            var cut = new byte[headerEnd + 3];
            Array.Copy(full, cut, cut.Length);
            Assert.That(SaveReader.ReadHeaderOnly(cut).GameTick, Is.EqualTo(4242L));
        }

        [Test]
        public void Sections_ReadInOrder_UnknownSectionIsSkipped()
        {
            var r = SaveReader.Open(SaveWithSections(), out _);
            var seen = new System.Collections.Generic.List<int>();
            int got1 = 0; double got2 = 0;
            while (r.NextSection(out int id, out var body))
            {
                seen.Add(id);
                if (id == 1) got1 = body.ReadInt32();
                else if (id == 2) got2 = body.ReadDouble();
                // id 99 is unknown: body is ignored.
            }
            Assert.That(seen, Is.EqualTo(new[] { 1, 99, 2 }));
            Assert.That(got1, Is.EqualTo(7));
            Assert.That(got2, Is.EqualTo(2.5));
        }

        [Test]
        public void SectionBody_CannotReadPastItsOwnLength()
        {
            var r = SaveReader.Open(SaveWithSections(), out _);
            r.NextSection(out _, out var body);
            body.ReadInt32();
            Assert.Throws<SaveFormatException>(() => body.ReadInt32());
        }

        [Test]
        public void NextSection_AtEnd_ReturnsFalse()
        {
            var r = SaveReader.Open(new SaveWriter(Header()).ToArray(), out _);
            Assert.That(r.NextSection(out _, out _), Is.False);
        }

        [Test]
        public void EmptySection_RoundTrips()
        {
            var w = new SaveWriter(Header());
            w.BeginSection(5); w.EndSection();
            var r = SaveReader.Open(w.ToArray(), out _);
            Assert.That(r.NextSection(out int id, out var body), Is.True);
            Assert.That(id, Is.EqualTo(5)); Assert.That(body.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void BadMagic_Throws()
        {
            var bytes = SaveWithSections();
            bytes[0] = (byte)'X';
            Assert.That(Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes)).Message, Does.Contain("magic"));
        }

        [Test]
        public void UnknownVersion_Throws()
        {
            var bytes = SaveWithSections();
            bytes[4] = 99;
            Assert.That(Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(bytes)).Message, Does.Contain("version"));
        }

        [Test]
        public void EmptyInput_Throws()
        {
            Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(new byte[0]));
        }

        [Test]
        public void TruncatedMagic_Throws()
        {
            Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(new byte[] { (byte)'L', (byte)'L' }));
        }

        [Test]
        public void HeaderTruncatedAtEveryLength_Throws()
        {
            int headerLen = new SaveWriter(Header()).Length;
            var full = SaveWithSections();
            for (int n = 0; n < headerLen; n++)
            {
                var cut = new byte[n];
                Array.Copy(full, cut, n);
                Assert.Throws<SaveFormatException>(() => SaveReader.ReadHeaderOnly(cut), "length " + n);
            }
        }

        [Test]
        public void SectionTruncated_Throws()
        {
            var full = SaveWithSections();
            var cut = new byte[full.Length - 2];
            Array.Copy(full, cut, cut.Length);
            Assert.Throws<SaveFormatException>(() =>
            {
                var r = SaveReader.Open(cut, out _);
                while (r.NextSection(out _, out _)) { }
            });
        }

        [Test]
        public void SectionHeaderTruncated_Throws()
        {
            var w = new SaveWriter(Header());
            w.WriteInt32(5); // an id with no length
            Assert.Throws<SaveFormatException>(() =>
            {
                var r = SaveReader.Open(w.ToArray(), out _);
                r.NextSection(out _, out _);
            });
        }

        [Test]
        public void NegativeSectionLength_Throws()
        {
            var w = new SaveWriter(Header());
            w.WriteInt32(5); w.WriteInt32(-1);
            Assert.Throws<SaveFormatException>(() =>
            {
                var r = SaveReader.Open(w.ToArray(), out _);
                r.NextSection(out _, out _);
            });
        }

        [Test]
        public void HugeArrayCount_Throws_InsteadOfAllocating()
        {
            var w = new SaveWriter();
            w.WriteInt32(int.MaxValue);
            Assert.Throws<SaveFormatException>(() => new SaveReader(w.ToArray()).ReadInt32Array());
        }

        [Test]
        public void NegativeStringLength_Throws()
        {
            var w = new SaveWriter();
            w.WriteInt32(-4);
            Assert.Throws<SaveFormatException>(() => new SaveReader(w.ToArray()).ReadString());
        }

        [Test]
        public void StringLongerThanData_Throws()
        {
            var w = new SaveWriter();
            w.WriteInt32(10); w.WriteByte(65);
            Assert.Throws<SaveFormatException>(() => new SaveReader(w.ToArray()).ReadString());
        }

        [Test]
        public void InvalidBool_Throws()
        {
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 2 }).ReadBool());
        }

        [Test]
        public void PrimitiveReadPastEnd_Throws()
        {
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[3]).ReadInt32());
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[7]).ReadInt64());
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[0]).ReadByte());
        }

        [Test]
        public void EndSectionWithoutBegin_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new SaveWriter().EndSection());
        }

        [Test]
        public void ToArrayWithOpenSection_Throws()
        {
            var w = new SaveWriter();
            w.BeginSection(1);
            Assert.Throws<InvalidOperationException>(() => w.ToArray());
        }

        [Test]
        public void MapRead_SetsContentHash_Fnv1aOfBytes()
        {
            var bytes = MapDataIO.ToBytes(SyntheticMap.CreateRamp());
            var map = MapDataIO.Read(bytes);
            Assert.That(map.ContentHash, Is.EqualTo(MapDataIO.Fnv1a64(bytes)));
            Assert.That(map.ContentHash, Is.Not.EqualTo(0UL));
            Assert.That(MapDataIO.ComputeContentHash(SyntheticMap.CreateRamp()), Is.EqualTo(map.ContentHash));
        }

        [Test]
        public void MapRead_FromStream_SetsSameHash()
        {
            var bytes = MapDataIO.ToBytes(SyntheticMap.CreateRamp());
            using (var ms = new System.IO.MemoryStream(bytes))
                Assert.That(MapDataIO.Read(ms).ContentHash, Is.EqualTo(MapDataIO.Fnv1a64(bytes)));
        }

        [Test]
        public void MapHash_DiffersForDifferentMaps()
        {
            var a = MapDataIO.Read(MapDataIO.ToBytes(SyntheticMap.CreateRamp()));
            var b = MapDataIO.Read(MapDataIO.ToBytes(SyntheticMap.CreateRidge()));
            Assert.That(a.ContentHash, Is.Not.EqualTo(b.ContentHash));
        }

        [Test]
        public void Fnv1a64_KnownVectors()
        {
            Assert.That(MapDataIO.Fnv1a64(new byte[0]), Is.EqualTo(0xCBF29CE484222325UL));
            Assert.That(MapDataIO.Fnv1a64(new[] { (byte)'a' }), Is.EqualTo(0xAF63DC4C8601EC8CUL));
            Assert.That(MapDataIO.Fnv1a64(System.Text.Encoding.ASCII.GetBytes("foobar")), Is.EqualTo(0x85944171F73967E8UL));
        }

        [Test]
        public void MapNotReadFromBytes_HasZeroHash()
        {
            Assert.That(SyntheticMap.CreateRamp().ContentHash, Is.EqualTo(0UL));
        }
    }
}
