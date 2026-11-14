using System;
using System.Text;

namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// Reads the save binary format written by <see cref="SaveWriter"/>. Every read is bounds-checked and throws
    /// <see cref="SaveFormatException"/> on truncation. Use <see cref="ReadHeaderOnly"/> to read just the header, or
    /// <see cref="Open"/> then <see cref="NextSection"/> to walk the sections.
    /// </summary>
    public sealed class SaveReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _pos;

        private SaveReader(byte[] data, int start, int end)
        {
            _data = data; _pos = start; _end = end;
        }

        /// <summary>Creates a reader over raw bytes with no magic check (for tests and section bodies).</summary>
        public SaveReader(byte[] data) : this(data ?? throw new ArgumentNullException(nameof(data)), 0, data.Length) { }

        /// <summary>Bytes left to read.</summary>
        public int Remaining => _end - _pos;

        /// <summary>Reads only the header of a save; the sections are not touched.</summary>
        public static SaveHeader ReadHeaderOnly(byte[] bytes)
        {
            return new SaveReader(bytes).ReadFileStart();
        }

        /// <summary>
        /// Checks magic and version, reads the header and returns a reader positioned at the first section.
        /// Throws <see cref="SaveFormatException"/> for bad magic, an unknown version or truncation.
        /// </summary>
        public static SaveReader Open(byte[] bytes, out SaveHeader header)
        {
            var r = new SaveReader(bytes);
            header = r.ReadFileStart();
            return r;
        }

        private SaveHeader ReadFileStart()
        {
            if (Remaining < 4) throw new SaveFormatException("Save is truncated (no magic).");
            for (int i = 0; i < 4; i++)
                if (_data[_pos + i] != SaveWriter.Magic[i]) throw new SaveFormatException("Not a save file (bad magic).");
            _pos += 4;
            int version = ReadInt32();
            if (version != SaveWriter.CurrentVersion)
                throw new SaveFormatException("Unknown save format version " + version
                    + " (this build reads version " + SaveWriter.CurrentVersion + ").");
            string name = ReadString();
            long created = ReadInt64();
            long tick = ReadInt64();
            string mapId = ReadString();
            ulong hash = ReadUInt64();
            return new SaveHeader(name, created, tick, mapId, hash);
        }

        /// <summary>
        /// Reads the next section's id and returns a reader over just its body, then moves past it, so a section the
        /// caller does not recognise is skipped by ignoring <paramref name="body"/>. Returns false at the end of the data.
        /// </summary>
        public bool NextSection(out int id, out SaveReader body)
        {
            id = 0; body = null;
            if (Remaining == 0) return false;
            id = ReadInt32();
            int len = ReadInt32();
            if (len < 0 || len > Remaining) throw new SaveFormatException("Section " + id + " is truncated or has a bad length.");
            body = new SaveReader(_data, _pos, _pos + len);
            _pos += len;
            return true;
        }

        /// <summary>Reads one byte.</summary>
        public byte ReadByte()
        {
            Need(1);
            return _data[_pos++];
        }

        /// <summary>Reads a bool; any value other than 0 or 1 is corrupt.</summary>
        public bool ReadBool()
        {
            byte b = ReadByte();
            if (b > 1) throw new SaveFormatException("Invalid bool value " + b + ".");
            return b == 1;
        }

        /// <summary>Reads a little-endian int32.</summary>
        public int ReadInt32()
        {
            Need(4);
            int v = _data[_pos] | (_data[_pos + 1] << 8) | (_data[_pos + 2] << 16) | (_data[_pos + 3] << 24);
            _pos += 4;
            return v;
        }

        /// <summary>Reads a little-endian int64.</summary>
        public long ReadInt64()
        {
            Need(8);
            ulong v = 0;
            for (int i = 0; i < 8; i++) v |= (ulong)_data[_pos + i] << (8 * i);
            _pos += 8;
            return unchecked((long)v);
        }

        /// <summary>Reads a little-endian uint64.</summary>
        public ulong ReadUInt64() => unchecked((ulong)ReadInt64());

        /// <summary>Reads a float32.</summary>
        public float ReadSingle() => BitConverter.ToSingle(BitConverter.GetBytes(ReadInt32()), 0);

        /// <summary>Reads a float64.</summary>
        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

        /// <summary>Reads a UTF-8 string written by <see cref="SaveWriter.WriteString"/>.</summary>
        public string ReadString()
        {
            int n = ReadCount(1);
            string s = Encoding.UTF8.GetString(_data, _pos, n);
            _pos += n;
            return s;
        }

        /// <summary>Reads a counted byte array.</summary>
        public byte[] ReadByteArray()
        {
            int n = ReadCount(1);
            var a = new byte[n];
            Buffer.BlockCopy(_data, _pos, a, 0, n);
            _pos += n;
            return a;
        }

        /// <summary>Reads a counted int32 array.</summary>
        public int[] ReadInt32Array()
        {
            int n = ReadCount(4);
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = ReadInt32();
            return a;
        }

        /// <summary>Reads a counted float32 array.</summary>
        public float[] ReadSingleArray()
        {
            int n = ReadCount(4);
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = ReadSingle();
            return a;
        }

        /// <summary>Reads a counted float64 array.</summary>
        public double[] ReadDoubleArray()
        {
            int n = ReadCount(8);
            var a = new double[n];
            for (int i = 0; i < n; i++) a[i] = ReadDouble();
            return a;
        }

        /// <summary>Reads an int32 count and checks that <paramref name="count"/> items of <paramref name="itemSize"/> bytes can fit.</summary>
        public int ReadCount(int itemSize)
        {
            int n = ReadInt32();
            if (n < 0 || (long)n * itemSize > Remaining)
                throw new SaveFormatException("Save is truncated or has a bad length/count (" + n + ").");
            return n;
        }

        private void Need(int n)
        {
            if (Remaining < n) throw new SaveFormatException("Save is truncated.");
        }
    }
}
