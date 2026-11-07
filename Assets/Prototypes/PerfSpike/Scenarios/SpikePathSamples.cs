using System;
using System.Collections.Generic;

namespace LordsLegacy.Prototypes.PerfSpike.Scenarios
{
    /// <summary>
    /// THROWAWAY (T-009). A* query times grouped by straight-line start-to-goal distance band.
    /// Bands (metres): under 100, 100-250, 250-500, 500-1000, 1000 and over.
    /// </summary>
    public sealed class SpikePathSamples
    {
        private static readonly float[] Edges = { 100f, 250f, 500f, 1000f };
        private readonly List<float>[] _ms;

        /// <summary>Creates an empty collector.</summary>
        public SpikePathSamples()
        {
            _ms = new List<float>[Edges.Length + 1];
            for (int i = 0; i < _ms.Length; i++) _ms[i] = new List<float>();
        }

        /// <summary>Number of bands.</summary>
        public int BandCount => _ms.Length;

        /// <summary>Index of the band holding <paramref name="metres"/>.</summary>
        public static int BandOf(float metres)
        {
            int b = 0;
            while (b < Edges.Length && metres >= Edges[b]) b++;
            return b;
        }

        /// <summary>Text label of a band, e.g. "250-500 m".</summary>
        public string BandLabel(int band)
        {
            if (band == 0) return "<100 m";
            if (band == Edges.Length) return ">=" + (int)Edges[Edges.Length - 1] + " m";
            return (int)Edges[band - 1] + "-" + (int)Edges[band] + " m";
        }

        /// <summary>Records one query that spanned <paramref name="metres"/> and took <paramref name="ms"/>.</summary>
        public void Add(float metres, double ms)
        {
            _ms[BandOf(metres)].Add((float)ms);
        }

        /// <summary>Samples in <paramref name="band"/>.</summary>
        public int Count(int band)
        {
            return _ms[band].Count;
        }

        /// <summary>Total samples over all bands.</summary>
        public int TotalCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _ms.Length; i++) n += _ms[i].Count;
                return n;
            }
        }

        /// <summary>Nearest-rank percentile (0..1) in ms for a band; 0 when it has no samples.</summary>
        public double Percentile(int band, double p)
        {
            List<float> list = _ms[band];
            if (list.Count == 0) return 0.0;
            var sorted = list.ToArray();
            Array.Sort(sorted);
            int rank = (int)Math.Ceiling(p * sorted.Length) - 1;
            if (rank < 0) rank = 0;
            if (rank >= sorted.Length) rank = sorted.Length - 1;
            return sorted[rank];
        }

        /// <summary>Drops all samples.</summary>
        public void Clear()
        {
            for (int i = 0; i < _ms.Length; i++) _ms[i].Clear();
        }
    }
}
