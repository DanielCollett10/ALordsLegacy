using System.Collections.Generic;

namespace LordsLegacy.Sim.Areas
{
    /// <summary>The area types a world knows (decision 0019). Immutable once built.</summary>
    public sealed class AreaTypeCatalog
    {
        private readonly AreaTypeDef[] _types;

        /// <summary>Creates a catalog from definitions; null gives an empty catalog. Null entries are skipped.</summary>
        public AreaTypeCatalog(IEnumerable<AreaTypeDef> types)
        {
            var list = new List<AreaTypeDef>();
            if (types != null) foreach (var t in types) if (t != null) list.Add(t);
            _types = list.ToArray();
        }

        /// <summary>All area types in definition order.</summary>
        public IReadOnlyList<AreaTypeDef> All => _types;

        /// <summary>Finds a type by id, or null if unknown (or the id is null).</summary>
        public AreaTypeDef Find(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < _types.Length; i++) if (_types[i].Id == id) return _types[i];
            return null;
        }
    }
}
