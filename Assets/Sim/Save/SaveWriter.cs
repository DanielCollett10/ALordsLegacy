using System;
using System.Collections.Generic;
using System.Text;

namespace LordsLegacy.Sim.Save
{
    /// <summary>
    /// Writes the save binary format (decision 0034): little-endian primitives, strings and arrays into a growing buffer.
    /// Layout: magic "LLSV", int32 <see cref="CurrentVersion"/>, header, then sections each written as
    /// int32 id, int32 byte length, body. Strings are int32 byte count + UTF-8; arrays are int32 count + items.
    /// </summary>
    public sealed class SaveWriter
    {
        /// <summary>The save format version this code writes and reads.</summary>
        public const int CurrentVersion = 25;

        internal static readonly byte[] Magic = { (byte)'L', (byte)'L', (byte)'S', (byte)'V' };

        private byte[] _buf = new byte[256];
        private int _len;
        private readonly Stack<int> _sectionStarts = new Stack<int>();

        /// <summary>Creates an empty writer (no magic yet; see <see cref="WriteFileStart"/>).</summary>
        public SaveWriter() { }

        /// <summary>Creates a writer that has already written magic, version and <paramref name="header"/>.</summary>
        public SaveWriter(SaveHeader header)
        {
            WriteFileStart(header);
        }

        /// <summary>Number of bytes written so far.</summary>
        public int Length => _len;

        /// <summary>Writes magic, format version and the header.</summary>
        public void WriteFileStart(SaveHeader header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            for (int i = 0; i < Magic.Length; i++) WriteByte(Magic[i]);
            WriteInt32(CurrentVersion);
            WriteString(header.Name);
            WriteInt64(header.CreatedRealTime);
            WriteInt64(header.GameTick);
            WriteString(header.MapId);
            WriteUInt64(header.MapContentHash);
        }

        /// <summary>Starts a section: writes its id and a length placeholder. Must be paired with <see cref="EndSection"/>.</summary>
        public void BeginSection(int id)
        {
            WriteInt32(id);
            _sectionStarts.Push(_len);
            WriteInt32(0);
        }

        /// <summary>Ends the innermost section, patching in its byte length.</summary>
        public void EndSection()
        {
            if (_sectionStarts.Count == 0) throw new InvalidOperationException("No section is open.");
            int lenPos = _sectionStarts.Pop();
            int bodyLen = _len - (lenPos + 4);
            PutInt32(lenPos, bodyLen);
        }

        /// <summary>Returns a copy of the bytes written. All sections must be ended.</summary>
        public byte[] ToArray()
        {
            if (_sectionStarts.Count != 0) throw new InvalidOperationException("A section is still open.");
            var result = new byte[_len];
            Buffer.BlockCopy(_buf, 0, result, 0, _len);
            return result;
        }

        /// <summary>Writes one byte.</summary>
        public void WriteByte(byte v)
        {
            Ensure(1);
            _buf[_len++] = v;
        }

        /// <summary>Writes a bool as one byte (0 or 1).</summary>
        public void WriteBool(bool v) => WriteByte(v ? (byte)1 : (byte)0);

        /// <summary>Writes a little-endian int32.</summary>
        public void WriteInt32(int v)
        {
            Ensure(4);
            PutInt32(_len, v);
            _len += 4;
        }

        /// <summary>Writes a little-endian int64.</summary>
        public void WriteInt64(long v)
        {
            Ensure(8);
            for (int i = 0; i < 8; i++) _buf[_len + i] = (byte)(v >> (8 * i));
            _len += 8;
        }

        /// <summary>Writes a little-endian uint64.</summary>
        public void WriteUInt64(ulong v) => WriteInt64(unchecked((long)v));

        /// <summary>Writes a float32 as its little-endian IEEE bits.</summary>
        public void WriteSingle(float v) => WriteInt32(BitConverter.ToInt32(BitConverter.GetBytes(v), 0));

        /// <summary>Writes a float64 as its little-endian IEEE bits.</summary>
        public void WriteDouble(double v) => WriteInt64(BitConverter.DoubleToInt64Bits(v));

        /// <summary>Writes a string as int32 UTF-8 byte count + bytes. Null is not allowed.</summary>
        public void WriteString(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            byte[] b = Encoding.UTF8.GetBytes(s);
            WriteInt32(b.Length);
            WriteRaw(b);
        }

        /// <summary>Writes an int32 count then the bytes.</summary>
        public void WriteByteArray(byte[] a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            WriteInt32(a.Length);
            WriteRaw(a);
        }

        /// <summary>Writes an int32 count then each int32.</summary>
        public void WriteInt32Array(int[] a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            WriteInt32(a.Length);
            for (int i = 0; i < a.Length; i++) WriteInt32(a[i]);
        }

        /// <summary>Writes an int32 count then each float32.</summary>
        public void WriteSingleArray(float[] a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            WriteInt32(a.Length);
            for (int i = 0; i < a.Length; i++) WriteSingle(a[i]);
        }

        /// <summary>Writes an int32 count then each float64.</summary>
        public void WriteDoubleArray(double[] a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            WriteInt32(a.Length);
            for (int i = 0; i < a.Length; i++) WriteDouble(a[i]);
        }

        private void WriteRaw(byte[] b)
        {
            Ensure(b.Length);
            Buffer.BlockCopy(b, 0, _buf, _len, b.Length);
            _len += b.Length;
        }

        private void PutInt32(int pos, int v)
        {
            _buf[pos] = (byte)v;
            _buf[pos + 1] = (byte)(v >> 8);
            _buf[pos + 2] = (byte)(v >> 16);
            _buf[pos + 3] = (byte)(v >> 24);
        }

        private void Ensure(int extra)
        {
            if (_len + extra <= _buf.Length) return;
            int size = _buf.Length * 2;
            while (size < _len + extra) size *= 2;
            Array.Resize(ref _buf, size);
        }
    }
}
