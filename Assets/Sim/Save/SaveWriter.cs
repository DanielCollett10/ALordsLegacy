using System;
using System.Collections.Generic;
using System.IO;

namespace LordsLegacy.Sim.Save
{
    public class SaveWriter
    {
        public void Write(Stream stream, SaveHeader header, Dictionary<string, byte[]> sections)
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write("LLSV");
                writer.Write(1); // Format version
                writer.Write(header.Name);
                writer.Write(header.CreatedTime);
                writer.Write(header.GameTick);
                writer.Write(header.MapId);
                writer.Write(header.ContentHash);

                foreach (var section in sections)
                {
                    writer.Write(section.Key);
                    writer.Write(section.Value.Length);
                    writer.Write(section.Value);
                }
            }
        }
    }
}