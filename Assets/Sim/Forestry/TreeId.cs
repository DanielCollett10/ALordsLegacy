using System;

namespace LordsLegacy.Sim.Forestry
{
    /// <summary>Stable identifier of a tree. Comes from the authoring data, never invented by the Sim.</summary>
    public readonly struct TreeId : IEquatable<TreeId>, IComparable<TreeId>
    {
        /// <summary>The raw integer value.</summary>
        public readonly int Value;

        /// <summary>Wraps an integer id.</summary>
        public TreeId(int value) { Value = value; }

        /// <inheritdoc />
        public bool Equals(TreeId other) => Value == other.Value;
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TreeId o && Equals(o);
        /// <inheritdoc />
        public override int GetHashCode() => Value;
        /// <inheritdoc />
        public int CompareTo(TreeId other) => Value.CompareTo(other.Value);
        /// <inheritdoc />
        public override string ToString() => "Tree#" + Value;
        /// <summary>Equality.</summary>
        public static bool operator ==(TreeId a, TreeId b) => a.Value == b.Value;
        /// <summary>Inequality.</summary>
        public static bool operator !=(TreeId a, TreeId b) => a.Value != b.Value;
    }
}
