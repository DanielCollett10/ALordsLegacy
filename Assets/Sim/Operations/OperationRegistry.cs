using System;
using System.Collections.Generic;

namespace LordsLegacy.Sim.Operations
{
    /// <summary>
    /// All operations, in ascending id order. One exists per designated operation building area. Every successful
    /// change bumps <see cref="Version"/>.
    /// </summary>
    public sealed class OperationRegistry
    {
        private readonly SortedDictionary<int, Operation> _byId = new SortedDictionary<int, Operation>();
        private int _nextId = 1;

        /// <summary>Increases by one on every successful change.</summary>
        public int Version { get; private set; }

        /// <summary>Number of operations.</summary>
        public int Count => _byId.Count;

        /// <summary>Operations in ascending id order.</summary>
        public IEnumerable<Operation> All => _byId.Values;

        /// <summary>The operation with this id, or null.</summary>
        public Operation Get(int id) => _byId.TryGetValue(id, out var o) ? o : null;

        /// <summary>The operation living in this area, or null.</summary>
        public Operation FindByArea(int areaId)
        {
            foreach (var o in _byId.Values) if (o.AreaId == areaId) return o;
            return null;
        }

        /// <summary>Creates an operation for an area. Throws if the area already has one.</summary>
        public Operation Create(string typeId, int areaId)
        {
            if (typeId == null) throw new ArgumentNullException(nameof(typeId));
            if (FindByArea(areaId) != null) throw new ArgumentException("Area already has an operation: " + areaId, nameof(areaId));
            var o = new Operation(_nextId++, typeId, areaId);
            _byId.Add(o.Id, o);
            Version++;
            return o;
        }

        /// <summary>Removes an operation. Returns false, changing nothing, if unknown.</summary>
        public bool Remove(int id)
        {
            if (!_byId.Remove(id)) return false;
            Version++;
            return true;
        }

        /// <summary>Lists a module (footprint id) as part of the operation. False if unknown op or already listed.</summary>
        public bool AddModule(int operationId, int footprintId) => Add(operationId, o => o.ModuleIdList, footprintId);

        /// <summary>Takes a module out of the operation. False if unknown op or not listed.</summary>
        public bool RemoveModule(int operationId, int footprintId) => Drop(operationId, o => o.ModuleIdList, footprintId);

        /// <summary>Assigns a family. False if unknown op or already assigned.</summary>
        public bool AssignFamily(int operationId, int familyId) => Add(operationId, o => o.FamilyIdList, familyId);

        /// <summary>Unassigns a family. False if unknown op or not assigned.</summary>
        public bool UnassignFamily(int operationId, int familyId) => Drop(operationId, o => o.FamilyIdList, familyId);

        /// <summary>Assigns an ox. False if unknown op or already assigned.</summary>
        public bool AssignOx(int operationId, int oxId) => Add(operationId, o => o.OxIdList, oxId);

        /// <summary>Unassigns an ox. False if unknown op or not assigned.</summary>
        public bool UnassignOx(int operationId, int oxId) => Drop(operationId, o => o.OxIdList, oxId);

        /// <summary>Sets the work area id, or null for the default circle. False if unknown op or unchanged.</summary>
        public bool SetWorkArea(int operationId, int? workAreaId)
        {
            Operation o = Get(operationId);
            if (o == null || o.WorkAreaId == workAreaId) return false;
            o.WorkAreaId = workAreaId;
            Version++;
            return true;
        }

        /// <summary>Pauses or resumes. False if unknown op or unchanged.</summary>
        public bool SetPaused(int operationId, bool paused)
        {
            Operation o = Get(operationId);
            if (o == null || o.Paused == paused) return false;
            o.Paused = paused;
            Version++;
            return true;
        }

        private bool Add(int operationId, Func<Operation, List<int>> pick, int value)
        {
            Operation o = Get(operationId);
            if (o == null) return false;
            List<int> list = pick(o);
            if (list.Contains(value)) return false;
            list.Add(value);
            Version++;
            return true;
        }

        private bool Drop(int operationId, Func<Operation, List<int>> pick, int value)
        {
            Operation o = Get(operationId);
            if (o == null || !pick(o).Remove(value)) return false;
            Version++;
            return true;
        }
    }
}
