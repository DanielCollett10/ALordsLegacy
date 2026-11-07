using System;

namespace LordsLegacy.Prototypes.PerfSpike.Nav
{
    /// <summary>
    /// THROWAWAY (T-007). Binary min-heap of (priority, node index). Hand-written because
    /// System.Collections.Generic.PriorityQueue is not available in Unity's .NET Standard 2.1 profile.
    /// Allows duplicate entries (lazy deletion in A*).
    /// </summary>
    public sealed class SpikeMinHeap
    {
        private float[] _keys;
        private int[] _items;

        /// <summary>Number of entries.</summary>
        public int Count { get; private set; }

        /// <summary>Creates an empty heap with room for <paramref name="capacity"/> entries (grows as needed).</summary>
        public SpikeMinHeap(int capacity = 256)
        {
            if (capacity < 1) capacity = 1;
            _keys = new float[capacity];
            _items = new int[capacity];
        }

        /// <summary>Removes all entries without releasing storage.</summary>
        public void Clear()
        {
            Count = 0;
        }

        /// <summary>Adds an entry.</summary>
        public void Push(float key, int item)
        {
            if (Count == _keys.Length) Grow();
            int i = Count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_keys[parent] <= key) break;
                _keys[i] = _keys[parent];
                _items[i] = _items[parent];
                i = parent;
            }
            _keys[i] = key;
            _items[i] = item;
        }

        /// <summary>Removes and returns the smallest entry. Throws if empty.</summary>
        public void Pop(out float key, out int item)
        {
            if (Count == 0) throw new InvalidOperationException("heap is empty");
            key = _keys[0];
            item = _items[0];
            int last = --Count;
            if (last == 0) return;

            float k = _keys[last];
            int v = _items[last];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= last) break;
                if (child + 1 < last && _keys[child + 1] < _keys[child]) child++;
                if (_keys[child] >= k) break;
                _keys[i] = _keys[child];
                _items[i] = _items[child];
                i = child;
            }
            _keys[i] = k;
            _items[i] = v;
        }

        private void Grow()
        {
            int size = _keys.Length * 2;
            Array.Resize(ref _keys, size);
            Array.Resize(ref _items, size);
        }
    }
}
