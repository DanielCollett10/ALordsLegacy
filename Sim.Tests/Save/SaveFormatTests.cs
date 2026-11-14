using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using LordsLegacy.Sim.Save;

namespace Sim.Tests.Save
{
    [TestFixture]
    public class SaveFormatTests
    {
        [Test]
        public void TestSaveHeader()
        {
            var header = new SaveHeader
            {
                Name = "TestMap",
                CreatedTime = DateTime.UtcNow.ToUnixTimeSeconds(),
                GameTick = 12345,
                MapId = 67890,
                ContentHash = 12345678901234567890
            };

            using (var stream = new MemoryStream())
            {
                var writer = new SaveWriter();
                writer.Write(stream, header, new Dictionary<string, byte[]>());

                stream.Position = 0;
                var reader = new SaveReader();
                var readHeader = reader.ReadHeader(stream);

                Assert.AreEqual(header.Name, readHeader.Name);
                Assert.AreEqual(header.CreatedTime, readHeader.CreatedTime);
                Assert.AreEqual(header.GameTick, readHeader.GameTick);
                Assert.AreEqual(header.MapId, readHeader.MapId);
                Assert.AreEqual(header.ContentHash, readHeader.ContentHash);
            }
        }

        [Test]
        public void TestSaveFormatException()
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0, 4); // Invalid magic
                stream.Write(new byte[] { 0x01 }, 0, 1); // Version 1

                var reader = new SaveReader();
                Assert.Throws<SaveFormatException>(() => reader.ReadHeader(stream));
            }
        }
    }
}