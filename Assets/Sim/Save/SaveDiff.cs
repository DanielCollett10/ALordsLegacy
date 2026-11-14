using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Save
{
    /// <summary>Compares two saves section by section so a mismatch can name the first section that differs.</summary>
    public static class SaveDiff
    {
        /// <summary>
        /// Returns null when the two saves are byte-identical, otherwise a short description such as
        /// "first difference in section 4 at byte 12 of 80". Section 0 means the header or the section layout.
        /// The header (name, times, map) is compared as part of section 0.
        /// </summary>
        public static string FirstDifference(byte[] a, byte[] b)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            if (ArraysEqual(a, b)) return null;
            List<KeyValuePair<int, byte[]>> sa = Split(a, out byte[] headA);
            List<KeyValuePair<int, byte[]>> sb = Split(b, out byte[] headB);
            if (!ArraysEqual(headA, headB)) return Describe(0, headA, headB);
            int n = Math.Min(sa.Count, sb.Count);
            for (int i = 0; i < n; i++)
            {
                if (sa[i].Key != sb[i].Key) return "section order differs at position " + (i + 1) + ": " + sa[i].Key + " vs " + sb[i].Key;
                if (!ArraysEqual(sa[i].Value, sb[i].Value)) return Describe(sa[i].Key, sa[i].Value, sb[i].Value);
            }
            return "section count differs: " + sa.Count + " vs " + sb.Count;
        }

        private static string Describe(int id, byte[] x, byte[] y)
        {
            int i = 0, n = Math.Min(x.Length, y.Length);
            while (i < n && x[i] == y[i]) i++;
            return "first difference in section " + id + " at byte " + i + " (lengths " + x.Length + " vs " + y.Length + ")";
        }

        private static List<KeyValuePair<int, byte[]>> Split(byte[] bytes, out byte[] head)
        {
            SaveReader r = SaveReader.Open(bytes, out _);
            int headerLen = bytes.Length - r.Remaining;
            head = new byte[headerLen];
            Buffer.BlockCopy(bytes, 0, head, 0, headerLen);
            var list = new List<KeyValuePair<int, byte[]>>();
            while (r.NextSection(out int id, out SaveReader body))
                list.Add(new KeyValuePair<int, byte[]>(id, body.ReadRemaining()));
            return list;
        }

        private static bool ArraysEqual(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
